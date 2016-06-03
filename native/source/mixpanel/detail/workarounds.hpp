#ifndef MIXPANEL_WORKAROUNDS_HPP
#define MIXPANEL_WORKAROUNDS_HPP

#if defined(__ANDROID__)
namespace std
{
    template <typename T>
    std::string to_string(const T& t)
    {
        std::stringstream ss;
        ss << t;
        return ss.str();
    }
}
#endif

#endif //MIXPANEL_WORKAROUNDS_HPP
