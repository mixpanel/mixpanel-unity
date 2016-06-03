#include <gtest/gtest.h>
#include <mixpanel/detail/base64.hpp>

TEST(Mixpanel, Base64)
{
    ASSERT_EQ(mixpanel::detail::base64_encode(""), "");
    ASSERT_EQ(mixpanel::detail::base64_encode("a"), "YQ==");
    ASSERT_EQ(mixpanel::detail::base64_encode("aa"), "YWE=");
    ASSERT_EQ(mixpanel::detail::base64_encode("aaa"), "YWFh");
    ASSERT_EQ(mixpanel::detail::base64_encode("aaaa"), "YWFhYQ==");
}
