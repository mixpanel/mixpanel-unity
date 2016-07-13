#include <algorithm>
#include <fstream>
#include <assert.h>
#include <string>
#include <utility>
#include "./persistence.hpp"
#include "./platform_helpers.hpp"

namespace mixpanel
{
    namespace detail
    {
        std::recursive_mutex Persistence::mutex;
        std::string Persistence::storage_directory = ".";
        std::atomic<std::size_t> Persistence::maximum_queue_size(5 * 1024 * 1024);

        std::recursive_mutex Persistence::memory_queues_mutex;
        Persistence::Memory_queues Persistence::memory_queues;

        void Persistence::set_storage_directory(const std::string& storage_directory)
        {
            Persistence::storage_directory = storage_directory;
        }

#ifdef WIN32
        std::wstring Persistence::get_full_name(const std::string& name)
        {
            return PlatformHelpers::utf8_to_wstring(storage_directory + "/mp_" + name + ".json");
        }
#else
        std::string Persistence::get_full_name(const std::string& name)
        {
            return storage_directory + "/mp_" + name + ".json";
        }
#endif

        Value Persistence::read(const std::string name)
        {
            auto full_name = get_full_name(name);

            std::lock_guard<decltype(mutex)> lock(mutex);
            Value o;
            std::ifstream ifs(full_name.c_str(), std::ios::binary);
            Json::Reader reader;
            reader.parse(ifs, o, false);

            return o;
        }

        std::size_t Persistence::get_queue_size(const std::string& name)
        {
            std::size_t memory_queue_size = 0;
            {
                std::lock_guard<decltype(mutex)> lock(memory_queues_mutex);
                auto memory_queue = memory_queues.find(name);
                if (memory_queue != memory_queues.end())
                    memory_queue_size = memory_queue->second.size() * 128; // 128 is a rough estimate
            }

            // we don't worry about locking here, if occasionally an event slips through, we don't care to much
            std::ifstream in(get_full_name(name).c_str(), std::ifstream::ate | std::ifstream::binary);
            if (in.good()) // file exists
            {
                // return file size minus some bytes plus memory queue size
                return std::max<int>(0, std::size_t(in.tellg())-5) + memory_queue_size;
            }
            else
            {
                // file does not exist, only return approximated memory queue size
                return 0 + memory_queue_size;
            }
        }

        void Persistence::write(const std::string& name, const Value& o)
        {
            assert(!o.isNull());
            auto full_name = get_full_name(name);

            Json::FastWriter writer;
            auto data = writer.write(o);

            {
                std::lock_guard<decltype(mutex)> lock(mutex);
                std::ofstream ofs(full_name.c_str(), std::ios::binary);
                ofs << data;
            }
        }

        bool Persistence::enqueue(const std::string& name, const Value& o)
        {
            assert(!o.isNull());
            if (get_queue_size(name) > maximum_queue_size)
            {
                return false;
            }

            // we don't write here to not block the caller (main-thread / app)
            // instead we're writing out the data in dequeue.
            std::lock_guard<decltype(mutex)> lock(memory_queues_mutex);
            memory_queues[name].push_back(o);

            return true;
        }

        void Persistence::persist_memory_queues()
        {
            Memory_queues memory_queues;

            { // swap queues, so that we block as short as possible (essentially double buffering)
                std::lock_guard<decltype(mutex)> lock(memory_queues_mutex);
                std::swap(Persistence::memory_queues, memory_queues);
                assert(Persistence::memory_queues.empty());
            }

            for(auto& memory_queue : memory_queues)
            {
                if (!memory_queue.second.empty())
                {
                    auto queue = read(memory_queue.first);
                    assert(queue.isNull() || queue.isArray());

                    for(const auto& o : memory_queue.second)
                        queue.append(o);

                    write(memory_queue.first, queue);
                }
            }
        }

        std::pair<Value, std::size_t> Persistence::dequeue(const std::string& name, unsigned int max_items)
        {
            persist_memory_queues();

            auto queue = read(name);
            assert(queue.isNull() || queue.isArray());

            Value ret;
            for (auto i = 0; i != std::min(max_items, queue.size()); ++i)
            {
                assert(queue[i].isObject());
                ret.append(queue[i]);
            }
            assert(ret.isNull() || ret.isArray());
            return std::make_pair(ret, queue.size());
        }

        void Persistence::drop_front(const std::string& name, size_t count)
        {
            persist_memory_queues();

            auto queue = read(name);
            assert(queue.isNull() || queue.isArray());

            Value v;
            while(!queue.empty() && count-- && queue.removeIndex(0, &v))
            {
                assert(!v.isNull());
            }
            write(name, queue);
        }

        void Persistence::set_maximum_queue_size(std::size_t maximum_size)
        {
            Persistence::maximum_queue_size = maximum_size;
        }
    } // namespace detail
} // namespace mixpanel
