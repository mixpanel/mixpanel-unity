#ifndef MIXPANEL_WORKAROUNDS_HPP
#define MIXPANEL_WORKAROUNDS_HPP

#if defined(__ANDROID__)

#include <sstream>
#include <string>

namespace std
{
    template <typename T>
    std::string to_string(const T& t)
    {
        std::stringstream ss;
        ss << t;
        return ss.str();
    }
} // namespace std

#endif

#endif //MIXPANEL_WORKAROUNDS_HPP
