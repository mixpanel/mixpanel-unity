#include <gtest/gtest.h>
#include <mixpanel/mixpanel.hpp>
#include <mixpanel/detail/worker.hpp>
#include "../../source/dependencies/nano/include/nanowww/nanowww.h"
#include "test_config.hpp"

//
// Note: We've placed these tests in to the mixpanel namespace so we can access
//       private members and functions as a friend class. This makes use of
//       the FRIEND_TEST macro in worker.hpp
//
namespace mixpanel
{
    namespace detail
    {
        // Ensure "Retry-After" HTTP header is respected
        TEST(MixpanelNetwork, RetryAfter)
        {
            auto mp = new mixpanel::Mixpanel(mp_token);
            auto worker = new mixpanel::detail::Worker(mp);

            nanowww::Response retry_after_response;
            retry_after_response.push_header("Retry-After", "51");

            auto retry_after_time = worker->parse_www_retry_after(retry_after_response);
            auto back_off_duration = retry_after_time - time(0);
            ASSERT_EQ(back_off_duration, 51);
        }

        TEST(MixpanelNetwork, BackOffTime)
        {
            auto mp = new mixpanel::Mixpanel(mp_token);
            auto worker = new mixpanel::detail::Worker(mp);

            nanowww::Response failure_response;
            failure_response.set_status(503);

            // We need 2 consecutive failures to enable exponential back off
            worker->parse_www_retry_after(failure_response);
            auto retry_after_time = worker->parse_www_retry_after(failure_response);

            auto back_off_duration = retry_after_time - time(0);
            // Should back off randomly between 120 - 150s
            ASSERT_GT(back_off_duration, 115);
            ASSERT_LE(back_off_duration, 150);

            // Test a third failure
            retry_after_time = worker->parse_www_retry_after(failure_response);
            back_off_duration = retry_after_time - time(0);

            // Should back off randomly between 240 - 270s
            ASSERT_GT(back_off_duration, 235);
            ASSERT_LE(back_off_duration, 270);
        }

        TEST(MixpanelNetwork, FailureRecovery)
        {
            auto mp = new mixpanel::Mixpanel(mp_token);
            auto worker = new mixpanel::detail::Worker(mp);

            nanowww::Response failure_response;
            failure_response.set_status(503);

            // We need 2 consecutive failures to enable exponential back off
            worker->parse_www_retry_after(failure_response);
            worker->parse_www_retry_after(failure_response);

            // Followed by 1 success to reset the back off
            nanowww::Response success_response;
            success_response.set_status(200);

            auto retry_after_time = worker->parse_www_retry_after(success_response);
            auto back_off_duration = retry_after_time - time(0);
            // Back off time should be reset
            ASSERT_EQ(back_off_duration, 0);
        }
    }
}
