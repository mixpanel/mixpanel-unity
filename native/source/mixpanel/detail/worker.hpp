#ifndef _MIXPANEL_WORKER_HPP_
#define _MIXPANEL_WORKER_HPP_

#include <atomic>
#include <condition_variable>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <utility>
#include <mixpanel/value.hpp>
#include "../../../tests/gtest/include/gtest/gtest_prod.h"
#include "../../dependencies/nano/include/nanowww/nanowww.h"

class MixpanelNetwork_RetryAfter_Test;
class MixpanelNetwork_BackOffTime_Test;
class MixpanelNetwork_FailureRecovery_Test;

namespace mixpanel
{
    class Mixpanel;

    namespace detail
    {
        class Worker
        {
            public:
                explicit Worker(Mixpanel* mixpanel);
                ~Worker();

                void enqueue(const std::string& name, const Value& o);
                void notify();

                void set_flush_interval(unsigned seconds);
                void flush_queue();
                void clear_send_queues();
            private:
                FRIEND_TEST(::MixpanelNetwork, RetryAfter);
                FRIEND_TEST(::MixpanelNetwork, BackOffTime);
                FRIEND_TEST(::MixpanelNetwork, FailureRecovery);

                void main();

                struct Result
                {
                    bool status;
                    std::string error;
                };

                std::pair<Result, Result> send_batches();
                Result send_track_batch();
                Result send_engage_batch();
                Result send_batch(const std::string& name, bool verbose);

                int parse_www_retry_after(const nanowww::Response& response);
                static int calculate_back_off_time(int failure_count);

                Mixpanel* mixpanel;
                std::atomic<bool> thread_should_exit;
                std::atomic<bool> new_data;
                std::atomic<bool> should_flush_queue;
                std::atomic<int> failure_count;
                std::atomic<unsigned> flush_interval;
                std::atomic<time_t> network_requests_allowed_time;
                std::thread send_thread;

                std::mutex mutex;
                std::condition_variable condition;
        };
    } // namespace detail
} // namespace mixpanel

#endif
