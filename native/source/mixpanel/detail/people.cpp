#include <string>

#include <mixpanel/mixpanel.hpp>
#include "platform_helpers.hpp"

namespace mixpanel
{
    using namespace detail;

    Mixpanel::People::People(class Mixpanel *mixpanel) : mixpanel(mixpanel) {}

    void Mixpanel::People::set(const std::string& property,  const Value& to)
    {
        Value args;
        args[property] = to;
        set_properties(args);
    }

    void Mixpanel::People::set_properties(const Value& properties) throw(std::invalid_argument)
    {
        if (!properties.isObject()) throw std::invalid_argument("properties must be an object");
        mixpanel->engage(Mixpanel::op_set, properties);
    }

    void Mixpanel::People::set_once(const std::string& property,  const Value& to)
    {
        Value args;
        args[property] = to;
        set_once_properties(args);
    }

    void Mixpanel::People::set_once_properties(const Value& properties) throw(std::invalid_argument)
    {
        if (!properties.isObject()) throw std::invalid_argument("properties must be an object");
        mixpanel->engage(Mixpanel::op_set_once, properties);
    }

    void Mixpanel::People::unset(const std::string& property)
    {
        Value properties(detail::Json::arrayValue);
        properties.append(property);
        unset_properties(properties);
    }

    void Mixpanel::People::unset_properties(const Value& properties) throw(std::invalid_argument)
    {
        if (!properties.isArray()) throw std::invalid_argument("properties must be a list");
        mixpanel->engage(Mixpanel::op_unset, properties);
    }


    void Mixpanel::People::increment(const std::string& property,  const Value& by) throw(std::invalid_argument)
    {
        if (!by.isNumeric()) throw std::invalid_argument("by must be numeric");
        Value args;
        args[property] = by;
        increment_properties(args);
    }

    void Mixpanel::People::increment_properties(const Value& properties) throw(std::invalid_argument)
    {
        if (!properties.isObject()) throw std::invalid_argument("properties must be an object");
        mixpanel->engage(Mixpanel::op_add, properties);
    }

    void Mixpanel::People::append(const std::string& list_name,  const Value& value)
    {
        Value args;
        args[list_name] = value;
        append_properties(args);
    }

    void Mixpanel::People::append_properties(const Value& properties) throw(std::invalid_argument)
    {
        if (!properties.isObject()) throw std::invalid_argument("properties must be an object");
        mixpanel->engage(Mixpanel::op_append, properties);
    }

    void Mixpanel::People::union_(const std::string& list_name,  const Value& values) throw(std::invalid_argument)
    {
        if (!values.isArray()) throw std::invalid_argument("values argument to union_ by must be an array");
        Value args;
        args[list_name] = values;
        union_properties(args);
    }

    void Mixpanel::People::union_properties(const Value& properties) throw(std::invalid_argument)
    {
        if (!properties.isObject()) throw std::invalid_argument("properties must be an object");
        mixpanel->engage(Mixpanel::op_union, properties);
    }

    void Mixpanel::People::track_charge(double amount, const Value& properties)
    {
        mixpanel->track_charge(amount, properties);
    }

    void Mixpanel::People::clear_charges()
    {
        this->set("$transactions", Value(Json::arrayValue));
    }

    void Mixpanel::People::set_first_name(const std::string& to)
    {
        this->set("$first_name", to);
    }

    void Mixpanel::People::set_last_name(const std::string& to)
    {
        this->set("$last_name", to);
    }

    void Mixpanel::People::set_name(const std::string& to)
    {
        this->set("$name", to);
    }

    void Mixpanel::People::set_email(const std::string& to)
    {
        this->set("$email", to);
    }

    void Mixpanel::People::set_phone(const std::string& to)
    {
        this->set("$phone", to);
    }

    void Mixpanel::People::set_push_id(const std::string &token)
    {
        if (PlatformHelpers::is_android())
        {
            Value array;
            array.append(token);
            union_("$android_devices", array);
        }
        else if (PlatformHelpers::is_ios())
        {
            Value array;
            array.append(token);
            union_("$ios_devices", array);
        }
        else
        {
            mixpanel->log(Mixpanel::LogEntry::LL_INFO, "set_push_id() only works on iOS and Android.");
        }
    }

} // namespace mixpanel
