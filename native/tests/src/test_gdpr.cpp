#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>
#include <mixpanel/detail/persistence.hpp>
#include <thread>
#include <fstream>
#include <stdio.h>

using namespace mixpanel;
using namespace mixpanel::detail;

class GDPR : public ::testing::Test {
protected:
    virtual void SetUp() {

    }
    virtual void TearDown() {
        mixpanel::Value state = mixpanel::detail::Persistence::read("state");
        state["opted_out"] = false;
        mixpanel::detail::Persistence::write("state", state);
    }
};


TEST_F(GDPR, optOutFlagAfterInitWithOptOut)
{
    Mixpanel mp("123456789", false, true);
    ASSERT_TRUE(mp.has_opted_out());
}

TEST_F(GDPR, optOutFlagByDefault)
{
    Mixpanel mp("123456789");
    ASSERT_FALSE(mp.has_opted_out());
}

TEST_F(GDPR, noTrackCallDuringOrAfterInitWithOptOut)
{
    Mixpanel mp("123456789", false, true);
    auto queue = Persistence::dequeue("track");
    ASSERT_EQ(queue.first.size(), 0);
}

TEST_F(GDPR, optOutFlagAfterInitWithOptIn)
{
    Mixpanel mp("123456789", false, false);
    ASSERT_FALSE(mp.has_opted_out());
}

TEST_F(GDPR, outOutTracking)
{
    Mixpanel mp("123456789");
    ASSERT_FALSE(mp.has_opted_out());

    mp.opt_out_tracking();
    ASSERT_TRUE(mp.has_opted_out());
}

TEST_F(GDPR, optInTracking)
{
    Mixpanel mp("123456789", false, true);
    ASSERT_TRUE(mp.has_opted_out());
    mp.opt_in_tracking("aDistinctId", mixpanel::Value());
    ASSERT_FALSE(mp.has_opted_out());
}

TEST_F(GDPR, optInTrackingEvent)
{
    Mixpanel mp("123456789", false, false);
    mp.opt_in_tracking("aDistinctId", mixpanel::Value());

    auto queue = Persistence::dequeue("track");
    ASSERT_EQ(queue.first[0]["event"].asString(), "$opt_in");
}

TEST_F(GDPR, optInTrackingForDistinctId)
{
    Mixpanel mp("123456789", false, false);
    mp.opt_in_tracking("aDistinctId", mixpanel::Value());

    auto queue = Persistence::dequeue("track");
    ASSERT_EQ(queue.first[0]["properties"]["distinct_id"].asString(), "aDistinctId");
}

TEST_F(GDPR, optInTrackingForDistinctIdAndProperties)
{
    Mixpanel mp("123456789", false, false);
    Value obj;
    obj["zee"] = "bar";
    mp.opt_in_tracking("aDistinctId", obj);

    auto queue = Persistence::dequeue("track");
    ASSERT_EQ(queue.first[0]["properties"]["zee"].asString(), "bar");
}

TEST_F(GDPR, outOutTrackingWillNoLongerTrack)
{
    Mixpanel mp("123456789");
    mp.opt_out_tracking();
    ASSERT_TRUE(mp.has_opted_out());

    mp.track("test");
    auto queue = Persistence::dequeue("track");
    ASSERT_EQ(queue.first.size(), 0);
}

TEST_F(GDPR, outOutTrackingWillNoLongerEngage)
{
    Mixpanel mp("123456789");
    mp.opt_out_tracking();
    ASSERT_TRUE(mp.has_opted_out());

    mp.people.set_first_name("Zee");
    auto queue = Persistence::dequeue("engage");
    ASSERT_EQ(queue.first.size(), 0);
}

TEST_F(GDPR, outOutTrackingWillSkipIdentify)
{
    Mixpanel mp("123456789");
    mp.opt_out_tracking();
    ASSERT_TRUE(mp.has_opted_out());

    mp.identify("newDistinctId");
    Value state = mixpanel::detail::Persistence::read("state");

    ASSERT_NE(state["distinct_id"], "newDistinctId");
}

TEST_F(GDPR, outOutTrackingWillSkipAlias)
{
    Mixpanel mp("123456789");
    mp.opt_out_tracking();
    ASSERT_TRUE(mp.has_opted_out());

    mp.alias("testAlias");
    Value state = mixpanel::detail::Persistence::read("state");

    ASSERT_NE(state["alias"], "testAlias");
}

TEST_F(GDPR, outOutTrackingRegisterSuperProperties)
{
    Mixpanel mp("123456789");
    mp.opt_out_tracking();
    ASSERT_TRUE(mp.has_opted_out());

    Value obj;
    obj["testkey"] = "bar";
    mp.register_properties(obj);

    Value superProperties = mixpanel::detail::Persistence::read("super_properties");
    ASSERT_NE(superProperties["testkey"], "bar");
}

TEST_F(GDPR, outOutTrackingRegisterSuperPropertiesOnce)
{
    Mixpanel mp("123456789");
    mp.opt_out_tracking();
    ASSERT_TRUE(mp.has_opted_out());

    Value obj;
    obj["testkey"] = "bar";
    mp.register_once_properties(obj);

    Value superProperties = mixpanel::detail::Persistence::read("super_properties");
    ASSERT_NE(superProperties["testkey"], "bar");
}

TEST_F(GDPR, outOutTrackingWillSkipTimeEvent)
{
    Mixpanel mp("123456789");
    mp.set_flush_interval(1);

    mp.clear_timed_events();
    mp.opt_out_tracking();

    ASSERT_FALSE(mp.start_timed_event("timed_event"));
}

TEST_F(GDPR, outOutTrackingWillClearTrackQueue)
{
    Mixpanel mp("123456789");
    mp.set_flush_interval(100);

    for(int i = 0; i != 5; ++i)
        mp.track("event");

    auto size = mixpanel::detail::Persistence::get_queue_size("track");
    ASSERT_EQ(mixpanel::detail::Persistence::get_queue_size("track"), size);

    mp.opt_out_tracking();
    ASSERT_EQ(mixpanel::detail::Persistence::get_queue_size("track"), 0);
}

TEST_F(GDPR, outOutTrackingWillClearEngageQueue)
{
    using namespace mixpanel;
    Mixpanel mp("123456789");

    for(int i = 0; i != 5; ++i)
        mp.people.set("$name", "Karl Heinz");

    ASSERT_EQ(mixpanel::detail::Persistence::dequeue("engage").first.size(), 5);
    mp.opt_out_tracking();
    ASSERT_EQ(mixpanel::detail::Persistence::dequeue("engage").first.size(), 0);
}
