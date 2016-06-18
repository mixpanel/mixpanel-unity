#ifndef MIXPANEL_PLATFORM_HELPERS_HPP
#define MIXPANEL_PLATFORM_HELPERS_HPP

#include <string>
#include <mixpanel/value.hpp>

namespace mixpanel
{
    namespace detail
    {
        struct PlatformHelpers
        {
            //! Might be random
            static std::string get_uuid();

            //! get a path to a directory where we can write to. note: token might be ignored
            static std::string get_storage_directory(const std::string& token);

            static std::string get_os_name();
            static std::string get_device_model();

            // only needed on windows
            static std::wstring utf8_to_wstring(const std::string& str);
            static std::string wstring_to_utf8(const std::wstring& str);

            static bool is_ios();
            static bool is_osx();
            static bool is_android();
            static bool is_windows();
            static bool is_desktop();
            static bool is_mobile();

            static Value collect_automatic_properties();
            static Value collect_automatic_people_properties();
        };
    } // namespace detail
} // namespace mixpanel

#endif //MIXPANEL_PLATFORM_HELPERS_HPP
