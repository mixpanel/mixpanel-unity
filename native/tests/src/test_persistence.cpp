#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>
#include <mixpanel/detail/persistence.hpp>
#include <thread>
#include <fstream>

TEST(Persistence, TestDropFront)
{
    using namespace mixpanel::detail;

    Persistence::enqueue("test", 10);

    for(int i=0; i!=10; ++i)
        Persistence::drop_front("test", 10000);

    auto queue = Persistence::dequeue("test");

    ASSERT_EQ(queue.first.size(), 0);

    mixpanel::Value obj;
    obj["key"] = "value";
    for(int i=0; i!=10; ++i)
    {
        Persistence::enqueue("test", obj);
    }

    ASSERT_EQ(Persistence::dequeue("test").first.size(), 10);

    Persistence::drop_front("test", 5);
    ASSERT_EQ(Persistence::dequeue("test").first.size(), 5);

    Persistence::drop_front("test", 4);
    ASSERT_EQ(Persistence::dequeue("test").first.size(), 1);

    Persistence::drop_front("test", 1);
    ASSERT_EQ(Persistence::dequeue("test").first.size(), 0);

    Persistence::drop_front("test", 1);
    ASSERT_EQ(Persistence::dequeue("test").first.size(), 0);

    Persistence::drop_front("test", 100);
    ASSERT_EQ(Persistence::dequeue("test").first.size(), 0);

    ASSERT_TRUE(Persistence::dequeue("test").first.isNull());
    ASSERT_EQ(Persistence::dequeue("test").first, mixpanel::Value());

}

TEST(Persistence, TestWrite)
{
    using namespace mixpanel::detail;
    mixpanel::Value obj;
    obj["foo"]="bar";
    obj["baz"]=1234.5678;
    obj["nested"]=22;

    Persistence::write("test2", obj);
    ASSERT_EQ(Persistence::read("test2"), obj);
}

TEST(Persistence, Corruption)
{
    using namespace mixpanel::detail;
    auto file_name = Persistence::get_full_name("test3");

    {
        std::ofstream ofs(file_name.c_str(), std::ios::binary);
        std::string garbage = "{\"i_look_like_json\":\"20,";
        garbage.push_back(rand());
        garbage.push_back(rand());
        garbage.push_back(rand());
        garbage.push_back(rand());
        garbage.push_back(rand());
        ofs.write(garbage.data(), garbage.size());
    }

    ASSERT_NO_THROW(Persistence::read("test3"));
}


TEST(Persistence, MaxQueueSize)
{
    using namespace mixpanel;
    using namespace mixpanel::detail;

    mixpanel::Mixpanel mp("012345789");
    mp.set_flush_interval(1);
    mp.set_minimum_log_level(Mixpanel::LogEntry::LL_TRACE);

    // drain
    while(Persistence::get_queue_size("track") > 3)
        std::this_thread::sleep_for(std::chrono::milliseconds(50));

    mp.on_reachability_changed(Mixpanel::NetworkReachability::NotReachable);

    mp.set_maximum_queue_size(10);

    for(int i=0; i!=5;++i)
       mp.track("event");

    auto size = Persistence::get_queue_size("track");

    mp.track("event");

    ASSERT_EQ(Persistence::get_queue_size("track"), size);

    mp.set_maximum_queue_size(1000000000);

    mp.track("event");

    ASSERT_GT(Persistence::get_queue_size("track"), size);

    mp.on_reachability_changed(Mixpanel::NetworkReachability::ReachableViaLocalAreaNetwork);

    // drain
    while(Persistence::get_queue_size("track") > 3)
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
}
