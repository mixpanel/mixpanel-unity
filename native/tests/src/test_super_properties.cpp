#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>
#include <mixpanel/detail/persistence.hpp>

TEST(Mixpanel, SuperProperties)
{
    {
        mixpanel::Mixpanel mp("123456789");
        mp.set_flush_interval(1);
        mp.set_minimum_log_level(mixpanel::Mixpanel::LogEntry::LL_TRACE);

        mp.register_("test_key", "test value");
        ASSERT_TRUE(mp.unregister("test_key"));

        ASSERT_TRUE(mp.register_once("test_key", "foo"));
        ASSERT_FALSE(mp.register_once("test_key", "foo"));
    }

    {
        mixpanel::Mixpanel mp("123456789");
        ASSERT_TRUE(mp.unregister("test_key"));
    }
}
