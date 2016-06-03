#ifndef _MIXPANEL_WORKER_HPP_
#define _MIXPANEL_WORKER_HPP_

#include <memory>
#include <string>
#include <thread>
#include <mutex>
#include <atomic>
#include <condition_variable>
#include <mixpanel/value.hpp>

namespace mixpanel
{
    class Mixpanel;

    namespace detail
    {
        class Worker
        {
            public:
                Worker(Mixpanel* mixpanel);
                ~Worker();

                void enqueue(const std::string& name, const Value& o);
                void notify();

                void set_flush_interval(unsigned seconds);
                void flush_queue();
                void clear_send_queues();
        private:
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

                Mixpanel* mixpanel;
                std::atomic<bool> thread_should_exit;
                std::atomic<bool> new_data;
                std::atomic<bool> should_flush_queue;
                std::atomic<unsigned> flush_interval;
                std::thread send_thread;

                std::mutex mutex;
                std::condition_variable condition;
        };
    }
}

#endif
