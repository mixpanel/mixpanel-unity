#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>
#include <mixpanel/detail/persistence.hpp>
#include <mixpanel/detail/workarounds.hpp>
#include <thread>

#include "test_config.hpp"

void testsuite_wait_for_delivery(const std::string &queue_name, long for_seconds);

TEST(Mixpanel, HugeRequest)
{
    using namespace mixpanel;
    using namespace mixpanel::detail;

    mixpanel::Mixpanel mp(mp_token);
    mp.set_flush_interval(1);
    mp.set_minimum_log_level(Mixpanel::LogEntry::LL_TRACE);

    Value properties;
    for(int i=0; i!=20;++i)
    {
        properties[std::to_string(i)]=std::to_string(i*i);
    }

    for(int i=0; i!=55; ++i)
       mp.track("foo", properties);

    testsuite_wait_for_delivery("track", 60);
}
