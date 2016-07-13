#include <gtest/gtest.h>
#include <fstream>
#include <mixpanel/detail/platform_helpers.hpp>
#include <mixpanel/detail/persistence.hpp>
#include <mixpanel/mixpanel.hpp>
#include "./test_config.hpp"

void test_drain_queues();
void testsuite_wait_for_delivery(const std::string &queue_name, long for_seconds);

namespace mixpanel
{
    namespace detail
    {
        extern volatile bool delivery_failure_flag;
    }
}

#if !defined(_MSC_VER) // MSVC does not support including a giant files like we do here via #include "temporary_failure.inc" :(
TEST(Bugs, TemporaryFailure)
{
    void test_drain_queues();

    { // overwrite the queue with the test data
        ASSERT_EQ(mixpanel::detail::Persistence::dequeue("track", 100).second, 0);

        const std::string event_queue_data =
            #include "temporary_failure.inc"
        ;

        auto storage_directory = mixpanel::detail::PlatformHelpers::get_storage_directory(mp_token);
        mixpanel::detail::Persistence::set_storage_directory(storage_directory);
        auto path_to_event_json = mixpanel::detail::Persistence::get_full_name("track");

        {
            std::ofstream ofs(path_to_event_json.c_str(), std::ios::binary);
            ofs << event_queue_data;
        }

        // make sure, there are exactly 50 entries in the queue now
        ASSERT_EQ(mixpanel::detail::Persistence::dequeue("track", 100).second, 50);
    }


    mixpanel::detail::delivery_failure_flag = false;
    mixpanel::Mixpanel mp(mp_token);
    mp.set_flush_interval(1);
    mp.set_minimum_log_level(mixpanel::Mixpanel::LogEntry::LL_TRACE);

    ASSERT_FALSE(mixpanel::detail::delivery_failure_flag);
    ASSERT_ANY_THROW(testsuite_wait_for_delivery("track", 30));

    ASSERT_TRUE(mixpanel::detail::delivery_failure_flag); // delivery failed
    ASSERT_EQ(mixpanel::detail::Persistence::get_queue_size("track"), 0); // but queue is empty, because we dropped it
}
#endif /* _MSC_VER */
