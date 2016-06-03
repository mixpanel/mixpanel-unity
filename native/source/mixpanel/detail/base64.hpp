/// see .cpp for license

#ifndef _BASE64_HPP_
#define _BASE64_HPP_

#include <string>

namespace mixpanel
{
    namespace detail
    {
        std::string base64_encode(const std::string& s);
    }
}

#endif /* _BASE64_HPP_ */
