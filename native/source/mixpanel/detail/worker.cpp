#include <algorithm>
#include <cmath>
#include <map>
#include <string>
#include <utility>

#include <mixpanel/mixpanel.hpp>
#include <mixpanel/value.hpp>

#include "./worker.hpp"
#include "./base64.hpp"
#include "./persistence.hpp"
#include "./workarounds.hpp"

namespace mixpanel
{
    namespace detail
    {
        // used in the test-suite
        volatile bool delivery_failure_flag = false;

        #ifdef WIN32
        // We're using algorithm instead
        #undef min
        #undef max
        struct _WSInit_
        {
            _WSInit_()
            {
                int iResult;

                // Initialize Winsock
                WSADATA wsaData;
                iResult = WSAStartup(MAKEWORD(2, 2), &wsaData);
                if (iResult != 0) {
                    std::clog << "WSAStartup failed: " << iResult << std::endl;
                }
            }
        } _wsinit_;
        #endif

        #ifdef HAVE_MBEDTLS
        static const std::string api_host = "https://api.mixpanel.com/";
        #else
        static const std::string api_host = "";
        #endif

        static std::string encode(const Value& v);

        static const bool verbose = true;

        Worker::Worker(Mixpanel* mixpanel)
        : mixpanel(mixpanel)
        , thread_should_exit(false)
        , new_data(false)
        , should_flush_queue(false)
        #if defined(DEBUG)
        , flush_interval(1)
        #else
        , flush_interval(60)
        #endif
        {
            delivery_failure_flag = false;
            network_requests_allowed_time = time(0);
            failure_count = 0;

            assert(mixpanel);
            mixpanel->log(Mixpanel::LogEntry::LL_INFO, "starting mixpanel worker");
            send_thread = std::thread([this](){
                main();
            });

            // notify thread to do one iteration
            {
                std::lock_guard<std::mutex> lock(mutex);
                new_data = true;
            }
            condition.notify_one();
        }

        Worker::~Worker()
        {
            mixpanel->log(Mixpanel::LogEntry::LL_INFO, "shutting down mixpanel worker");

            {
                std::lock_guard<std::mutex> lock(mutex);
                thread_should_exit = true;
            }

            condition.notify_one();

            if (send_thread.joinable())
            {
                send_thread.join();
            }
        }

        std::pair<Worker::Result, Worker::Result> Worker::send_batches()
        {
            return std::make_pair(
                send_track_batch(),
                send_engage_batch()
            );
        }

        Worker::Result Worker::send_track_batch()
        {
            return send_batch("track", verbose);
        }

        Worker::Result Worker::send_engage_batch()
        {
            return send_batch("engage", verbose);
        }

        Worker::Result Worker::send_batch(const std::string& name, bool verbose)
        {
            auto objs = Persistence::dequeue(name, 50);
            if (objs.first.empty())
            {
                return {true, ""};
            }

            std::map<std::string, std::string> post;

            post["data"] = encode(objs.first);

            std::string url = api_host + name + "/";
            if (verbose)
            {
                url += "?verbose=1";
            }

            if (name == "track")
            {
                url += verbose ? "&" : "?";
                url += "ip=1";
            }

            mixpanel->log(Mixpanel::LogEntry::LL_TRACE, "URL: " + url);
            mixpanel->log(Mixpanel::LogEntry::LL_TRACE, "data: " + objs.first.toStyledString());

            nanowww::Client client;
            nanowww::Request request("POST", url, post);
            nanowww::Response response;
            if (client.send_request(request, &response))
            {
                {
                    std::lock_guard<std::mutex> lock(mutex);
                    // Prevent requests until the back off time has passed
                    network_requests_allowed_time = parse_www_retry_after(response);
                }

                Json::Reader reader;
                Value parsed_response;
                if (reader.parse(response.content(), parsed_response, false))
                {
                    bool success = (!verbose && parsed_response.asBool()) || (verbose && parsed_response["status"].asBool());
                    if (success)
                    {
                        // delivery succeeded
                        mixpanel->log(Mixpanel::LogEntry::LL_DEBUG, "delivered " + std::to_string(objs.first.size()) + " objects in " + std::to_string(post["data"].size()) + " bytes. " + std::to_string(objs.second) + " in queue (including the sent entries).");
                    }
                    else
                    {
                        mixpanel->log(Mixpanel::LogEntry::LL_WARNING, "API rejected some items");
                    }

                    // Note: the whole batch is dropped if delivery fails - this is the same behavious of the iOS SDK (https://github.com/mixpanel/mixpanel-iphone/blob/d0f7323617641f54796a62c28e0733461c31aecb/Mixpanel/Mixpanel.m#L697)
                    Persistence::drop_front(name, objs.first.size());

                    if (verbose)
                        return {parsed_response["status"].asBool(), parsed_response["error"].asString()};
                    else
                        return {parsed_response.asBool(), parsed_response.asBool()?"":"error, enable verbose responses for debugging."};
                }
                else
                {
                    return {false, "failed to parse: " + response.content()};
                }
            }

            return {false, client.errstr()};
        }

        static std::string encode(const Value& v)
        {
            Json::FastWriter writer;
            return base64_encode(writer.write(v));
        }

        void Worker::enqueue(const std::string& name, const Value& o)
        {
            mixpanel->log(Mixpanel::LogEntry::LL_TRACE, "enqueueing " + o.toStyledString() + " into " + name);
            if (!Persistence::enqueue(name, o))
            {
                mixpanel->log(Mixpanel::LogEntry::LL_WARNING, "event not queued into " + name + ": queue full.");
            }

            if (flush_interval == 0) // only notify worker, if in immediate send mode
            {
                notify();
            }
        }

        int Worker::parse_www_retry_after(const nanowww::Response& response)
        {
            // Check for an HTTP Retry-After header
            auto retry_after_header = response.get_header("Retry-After");
            auto retry_after = 0;
            if (!retry_after_header.empty()) {
                retry_after = atoi(retry_after_header.c_str());
            }

            // Check for a 5XX response code
            bool failed = (500 <= response.status() && response.status() <= 599);
            if (failed) {
                mixpanel->log(Mixpanel::LogEntry::LL_ERROR, "/track HTTP Call Failed - Status Code (" + std::to_string(response.status()) + "): " + response.content());
                failure_count++;
            } else {
                failure_count = 0;
            }

            // Calculate exponential back off
            if (failure_count > 1) {
                retry_after = std::max(retry_after, calculate_back_off_time(failure_count));
            }

            auto now = time(0);
            auto allowed_after_time = now + retry_after;

            if (mixpanel->min_log_level >= Mixpanel::LogEntry::LL_TRACE) {
                mixpanel->log(Mixpanel::LogEntry::LL_TRACE, "/track HTTP Response Headers: \n" + response.headers()->as_string());
                mixpanel->log(Mixpanel::LogEntry::LL_TRACE, "/track HTTP Response Body: \n" + response.content());
                mixpanel->log(Mixpanel::LogEntry::LL_TRACE, "Network requests allowed after time " + std::to_string(allowed_after_time) +
                              ". Current time " + std::to_string(now) + ". Delta: " + std::to_string(allowed_after_time - now));
            }

            return allowed_after_time;
        }

        int Worker::calculate_back_off_time(int failure_count)
        {
            int back_off_time = pow(2.0, failure_count - 1) * 60.0 + rand() % 30;
            return std::min(std::max(60, back_off_time), 600);
        }

        void Worker::notify()
        {
            {
                std::lock_guard<std::mutex> lock(mutex);
                new_data = true;
            }
            condition.notify_one();
        }


        void Worker::set_flush_interval(unsigned seconds)
        {
            {
                std::lock_guard<std::mutex> lock(mutex);
                flush_interval = seconds;
            }
            condition.notify_one();
        }

        void Worker::flush_queue()
        {
            {
                std::lock_guard<std::mutex> lock(mutex);
                should_flush_queue = true;
            }
            condition.notify_one();
        }

        void Worker::clear_send_queues()
        {
            Persistence::drop_front("track", Persistence::get_queue_size("track"));
            Persistence::drop_front("engage", Persistence::get_queue_size("engage"));
        }

        void Worker::main()
        {
            /*
             * if flush_interval == 0, the main loop sleeps for ten seconds or until new data in enqueued.
             * if flush_interval > 0, the main loop sleeps for flush_interval, but is not woken up when new
             *     data arrives. It only tries to send after flush_interval have passed AND new data is in the queue.
             * But if flush_interval is equal to zero, no sending will be attempted (as in the iOS SDK)
             * */
            while (!thread_should_exit)
            {
                { // wait for ten seconds or for new data
                    std::unique_lock<std::mutex> lock(mutex);
                    auto last_flush_interval = flush_interval.load();
                    condition.wait_for(lock, std::chrono::seconds(flush_interval.load() == 0 ? 10 : flush_interval.load()), [this, last_flush_interval]
                    {
                        return thread_should_exit || ((flush_interval.load() == 0 || should_flush_queue) && new_data) || (last_flush_interval != flush_interval);
                    });
                    new_data = false;
                    should_flush_queue = false;
                }

                auto block_time_left = network_requests_allowed_time.load() - time(0);
                auto network_blocked = (mixpanel->network_reachability == Mixpanel::NetworkReachability::NotReachable || block_time_left > 0);
                if (flush_interval > 0 && !network_blocked)
                {
                    // here thread_should_exit might be true, but we try to send anyways
                    auto results = send_batches();

                    // Note: the level is INFO here, because a request might fail when offline.
                    if (!results.first.status) mixpanel->log(Mixpanel::LogEntry::LL_INFO, "error while sending tracking calls: " + results.first.error);
                    if (!results.second.status) mixpanel->log(Mixpanel::LogEntry::LL_INFO, "error while sending engage calls: " + results.second.error);

                    delivery_failure_flag = delivery_failure_flag || !results.first.status || !results.second.status;
                }
            }
        }
    } // namespace detail
} // namespace mixpanel
