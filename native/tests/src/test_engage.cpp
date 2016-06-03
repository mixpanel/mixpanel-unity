#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>
#include <mixpanel/detail/persistence.hpp>
#include <mixpanel/detail/workarounds.hpp>

#include <thread>
#include <dependencies/nano/include/nanowww/nanowww.h>

#include "test_config.hpp"

namespace mixpanel
{
    namespace detail
    {
        extern volatile bool delivery_failure_flag;
    }
}

void testsuite_wait_for_delivery(const std::string &queue_name, long for_seconds)
{
    for_seconds = for_seconds ? for_seconds : 5;

    auto start = std::chrono::steady_clock::now();
    while(
        (mixpanel::detail::Persistence::read(queue_name).size() ||
        mixpanel::detail::Persistence::get_queue_size(queue_name) > 0) &&
        (std::chrono::steady_clock::now() - start) < std::chrono::seconds(for_seconds)
    )
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
        std::clog << "testsuite_wait_for_delivery: " << mixpanel::detail::Persistence::read(queue_name).size() << std::endl;

        if(mixpanel::detail::delivery_failure_flag)
            throw std::runtime_error("delivery failed.");
    }

    if ((std::chrono::steady_clock::now() - start) >= std::chrono::seconds(for_seconds))
        throw std::runtime_error("delivery timed out");
}

//// disabled: too slow and unreliable, because the data is not immediately visible via the API
//TEST(Engage, set)
//{
//    using namespace mixpanel;
//    using namespace mixpanel::detail;
//
//    mixpanel::Mixpanel mp(mp_token);
//    mp.set_flush_interval(1);
//    mp.set_minimum_log_level(Mixpanel::LogEntry::LL_TRACE);
//    mp.people.set("$name", "Tina Tester");
//
//    mp.people.set_once("$name", "Karl Heinz");
//    mp.people.set_once("$name", "YOU SHOULD NEVER SEE THIS IN THE BACKEND!");
//
//    // unset
//    ASSERT_THROW(mp.people.unset(std::string("$name")), std::invalid_argument);
//    Value items_to_unset;
//    items_to_unset.append("$name");
//    mp.people.unset(items_to_unset);
//
//    { // append
//        mixpanel::Value items(mixpanel::detail::Json::arrayValue);
//        mp.people.set("items", items);
//        items.append("The Red Sword");
//        mp.people.append("items", "The Red Sword");
//        items.append("The Green Sword");
//        mp.people.append("items", "The Green Sword");
//    }
//
//    { // union
//        mixpanel::Value checkpoints(mixpanel::detail::Json::arrayValue);
//        mp.people.set("checkpoints", checkpoints);
//        checkpoints.append("checkpoint1");
//        mp.people.union_("checkpoints", checkpoints);
//        mp.people.union_("checkpoints", checkpoints);
//        checkpoints.append("checkpoint2");
//        mp.people.union_("checkpoints", checkpoints);
//    }
//
//    { // union with non array value
//        ASSERT_THROW(mp.people.union_("foobar", "xyz"), std::invalid_argument);
//        ASSERT_THROW(mp.people.union_("foobar", 123), std::invalid_argument);
//        ASSERT_THROW(mp.people.union_("foobar", 42.0), std::invalid_argument);
//        Value value;
//        value["foo"] = 12345;
//        ASSERT_THROW(mp.people.union_("foobar", value), std::invalid_argument);
//    }
//
//    { // increment
//        mp.people.set("coins", 0);
//        mp.people.increment("coins", 100);
//        mp.people.increment("coins", 50);
//        mp.people.increment("coins", 8);
//    }
//
//    { // transactions
//        mp.people.clear_charges();
//        mp.people.track_charge(10.0);
//        mp.people.track_charge(5.0);
//        mp.people.track_charge(5.0);
//    }
//    testsuite_wait_for_delivery("engage", 0);
//}
