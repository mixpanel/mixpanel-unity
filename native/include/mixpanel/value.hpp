#ifndef _MIXPANEL_VALUE_HPP_
#define _MIXPANEL_VALUE_HPP_

#include <string>
#include <vector>
#include <map>
#include "./json/json.h"

namespace mixpanel
{
    /// a json var like object. It can store null, bools, numbers, string, arrays and objects.
    typedef detail::Json::Value Value;
}

#endif /* _MIXPANEL_VALUE_HPP_ */
