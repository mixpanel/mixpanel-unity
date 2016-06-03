#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>

TEST(Mixpanel, TimedEvents)
{
    mixpanel::Mixpanel mp("123456789");
    mp.set_flush_interval(1);

    mp.clear_timed_events();

    ASSERT_TRUE(mp.start_timed_event("timed_event"));
    ASSERT_FALSE(mp.start_timed_event("timed_event"));
    ASSERT_TRUE(mp.clear_timed_event("timed_event"));
    ASSERT_FALSE(mp.clear_timed_event("timed_event"));
    ASSERT_TRUE(mp.start_timed_event_once("timed_event_once"));
    ASSERT_FALSE(mp.start_timed_event_once("timed_event_once"));
    ASSERT_TRUE(mp.clear_timed_event("timed_event_once"));
    ASSERT_FALSE(mp.clear_timed_event("timed_event_once"));
}
