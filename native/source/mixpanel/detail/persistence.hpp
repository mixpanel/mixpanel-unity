#ifndef _PERSISTENCE_HPP_
#define _PERSISTENCE_HPP_

#include <atomic>
#include <list>
#include <map>
#include <mutex>
#include <string>
#include <utility>

#include <mixpanel/value.hpp>

class Persistence_TestDropFront_Test;
class Mixpanel_HugeRequest_Test;
class Persistence_Corruption_Test;
class Persistence_MaxQueueSize_Test;
class Engage_set_Test;
class Reachability_NotSending_Test;
class Bugs_TemporaryFailure_Test;
class Bugs_TemporaryFailure2_Test;
void test_drain_queues();
void testsuite_wait_for_delivery(const std::string &queue_name, long for_seconds);


namespace mixpanel
{
    namespace detail
    {
        class Persistence
        {
            public:
                static void set_storage_directory(const std::string& storage_directory);
                static void set_maximum_queue_size(std::size_t maximum_size);

                static Value read(const std::string name);
                static void write(const std::string& name, const Value& o);
            private:
                friend void ::testsuite_wait_for_delivery(const std::string &queue_name, long for_seconds);

                friend class ::Persistence_TestDropFront_Test;
                friend class ::Mixpanel_HugeRequest_Test;
                friend class ::Persistence_Corruption_Test;
                friend class ::Engage_set_Test;
                friend class ::Reachability_NotSending_Test;
                friend class ::Persistence_MaxQueueSize_Test;
                friend class ::Bugs_TemporaryFailure_Test;
                friend class ::Bugs_TemporaryFailure2_Test;
                friend void ::test_drain_queues();

                friend class Worker;
                static bool enqueue(const std::string& name, const Value& o);

                // return a pair of the read values and the total size of the queue
                static std::pair<Value, std::size_t> dequeue(const std::string& name, unsigned int max_items=50);
                static void drop_front(const std::string& name, size_t count);

                #ifdef WIN32
                    static std::wstring get_full_name(const std::string& name);
                #else
                    static std::string get_full_name(const std::string& name);
                #endif
                static std::size_t get_queue_size(const std::string& name);

                static std::recursive_mutex mutex;
                static std::string storage_directory;
                static std::atomic<std::size_t> maximum_queue_size;

                // write data in memory_queues to disk and clear memory_queues
                static void persist_memory_queues();

                static std::recursive_mutex memory_queues_mutex;
                typedef std::map<std::string, std::list<Value>> Memory_queues;
                static Memory_queues memory_queues;
        };
    } // namespace detail
} // namespace mixpanel

#endif /* _PERSISTENCE_HPP_ */
