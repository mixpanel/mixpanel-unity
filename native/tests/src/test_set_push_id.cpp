#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>
#include <mixpanel/detail/persistence.hpp>
#include <mixpanel/detail/workarounds.hpp>
#include <thread>

#include "test_config.hpp"

void testsuite_wait_for_delivery(const std::string &queue_name, long for_seconds);

namespace mixpanel
{
    namespace detail
    {
        extern volatile bool delivery_failure_flag;
    }
}

/*
 * This tests if the push-id is delivered to the
 * backend without errors.
 * */
TEST(Mixpanel, SetPushId)
{
    using namespace mixpanel;
    using namespace mixpanel::detail;

    delivery_failure_flag = false;
    mixpanel::Mixpanel mp(mp_token);
    mp.set_flush_interval(1);
    mp.set_minimum_log_level(Mixpanel::LogEntry::LL_TRACE);

    mp.people.set_push_id("this-is-a-fake-test-push-id");

    testsuite_wait_for_delivery("engage", 0);
    ASSERT_FALSE(delivery_failure_flag);
}
