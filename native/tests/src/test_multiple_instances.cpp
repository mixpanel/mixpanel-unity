#include <mixpanel/mixpanel.hpp>
#include <gtest/gtest.h>
#include <memory>
#include <mixpanel/detail/workarounds.hpp>
#include "test_config.hpp"

TEST(Mixpanel, MultipleInstances)
{
    using namespace mixpanel;

    {
        ASSERT_NO_THROW(Mixpanel("123456789"));
    }

    {
        ASSERT_NO_THROW(Mixpanel("123456789"));
    }

    {
        std::shared_ptr<mixpanel::Mixpanel> instance1, instance2, instance3;
        ASSERT_NO_THROW(instance1 = std::make_shared<Mixpanel>("123456789"));
        ASSERT_THROW(instance2 = std::make_shared<Mixpanel>("123456789"), std::logic_error);
        ASSERT_THROW(instance3 = std::make_shared<Mixpanel>("123456789"), std::logic_error);
    }

    ASSERT_THROW(Mixpanel(""), std::invalid_argument);

    {
        ASSERT_NO_THROW(Mixpanel("123456789"));
    }
}
