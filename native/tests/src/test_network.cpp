#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>
#include <mixpanel/detail/worker.hpp>
#include "../../source/dependencies/nano/include/nanowww/nanowww.h"
#include "test_config.hpp"

using namespace mixpanel;

//
// Ensure "Retry-After" HTTP header is respected
//
TEST(MixpanelNetwork, RetryAfter)
{
    Mixpanel mp(mp_token);
    auto worker = Mixpanel::worker;

    nanowww::Response retry_after_response;
    retry_after_response.push_header("Retry-After", "51");

    auto retry_after_time = worker->parse_www_retry_after(retry_after_response);
    auto back_off_duration = retry_after_time - time(0);
    ASSERT_EQ(back_off_duration, 51);
    ASSERT_EQ(worker->failure_count, 0);
}

//
// Ensure successive failures result in an exponential back
//      off time.
//
TEST(MixpanelNetwork, BackOffTime)
{
    Mixpanel mp(mp_token);
    auto worker = Mixpanel::worker;

    nanowww::Response failure_response;
    failure_response.set_status(503);

    // We need 2 consecutive failures to enable exponential back off
    worker->parse_www_retry_after(failure_response);
    ASSERT_EQ(worker->failure_count, 1);
    auto retry_after_time = worker->parse_www_retry_after(failure_response);
    ASSERT_EQ(worker->failure_count, 2);

    auto back_off_duration = retry_after_time - time(0);
    // Should back off randomly between 120 - 150s
    ASSERT_GT(back_off_duration, 115);
    ASSERT_LE(back_off_duration, 150);

    // Test a third failure
    retry_after_time = worker->parse_www_retry_after(failure_response);
    ASSERT_EQ(worker->failure_count, 3);
    back_off_duration = retry_after_time - time(0);

    // Should back off randomly between 240 - 270s
    ASSERT_GT(back_off_duration, 235);
    ASSERT_LE(back_off_duration, 270);
}

//
// A single success after a number of failures should reset the back off time.
//
TEST(MixpanelNetwork, FailureRecovery)
{
    Mixpanel mp(mp_token);
    auto worker = Mixpanel::worker;

    nanowww::Response failure_response;
    failure_response.set_status(503);

    // We need 2 consecutive failures to enable exponential back off
    worker->parse_www_retry_after(failure_response);
    ASSERT_EQ(worker->failure_count, 1);
    worker->parse_www_retry_after(failure_response);
    ASSERT_EQ(worker->failure_count, 2);

    // Followed by 1 success to reset the back off
    nanowww::Response success_response;
    success_response.set_status(200);

    auto retry_after_time = worker->parse_www_retry_after(success_response);
    ASSERT_EQ(worker->failure_count, 0);

    auto back_off_duration = retry_after_time - time(0);
    // Back off time should be reset
    ASSERT_EQ(back_off_duration, 0);
}
