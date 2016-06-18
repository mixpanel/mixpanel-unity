#include <chrono>
#include <cmath>
#include <ctime>
#include <iomanip>
#include <iostream>
#include <string>
#include <vector>

#include <mixpanel/mixpanel.hpp>

#include "./persistence.hpp"
#include "./worker.hpp"
#include "platform_helpers.hpp"

#include "./workarounds.hpp"

#if defined (_MSC_VER) && defined(DEBUG)
#    define WIN32_LEAN_AND_MEAN
#    define VC_EXTRALEAN
#    include <Windows.h> // for output debug string
#endif

namespace mixpanel
{
    static const std::string sdk_version = "v1.0.1";

    // helper function to get the time since epoch as double / float etc.
    template <typename T>
    static T time_since_epoch()
    {
        typedef std::chrono::system_clock Clock;
        return std::chrono::duration<T>(Clock::now().time_since_epoch()).count();
    }

    using namespace detail;

    static std::chrono::steady_clock::time_point app_start = std::chrono::steady_clock::now();

    std::shared_ptr<detail::Worker> Mixpanel::worker;

    Mixpanel::Mixpanel(
        const std::string& token,
        const std::string& distinct_id,
        const std::string& storage_directory,
        const bool enable_log_queue
    )
        :people(this)
        ,token(token)
        ,automatic_properties(collect_automatic_properties())
        ,automatic_people_properties(collect_automatic_people_properties())
        ,enable_log_queue(enable_log_queue)
        ,network_reachability(NetworkReachability::ReachableViaLocalAreaNetwork)
#if defined(DEBUG)
        ,min_log_level(LogEntry::LL_TRACE)
#else
        ,min_log_level(LogEntry::LL_WARNING)
#endif
    {
        if (worker)
        {
            throw std::logic_error("Only one Mixpanel instance at a time is supported.");
        }

        if (token.size() < 8)
        {
            throw std::invalid_argument("You must provide a valid Mixpanel token.");
        }

        Persistence::set_storage_directory(storage_directory);
        super_properties = Persistence::read("super_properties");
        automatic_people_properties = collect_automatic_people_properties();
        timed_events = Persistence::read("timed_events");
        state = Persistence::read("state");

        // if no distinct_id given by user and we have none stored
        if (distinct_id.empty() && (!state["distinct_id"].isString() || state["distinct_id"].asString().empty()))
        {
            state["distinct_id"] = PlatformHelpers::get_uuid();
        }
        else if (!distinct_id.empty())
        {
            state["distinct_id"] = distinct_id;
        }

        assert(state["distinct_id"].isString());
        assert(!state["distinct_id"].asString().empty());

        log(LogEntry::LL_DEBUG, "distinct_id is : " + state["distinct_id"].asString());
        log(LogEntry::LL_DEBUG, "storage directory is : " + storage_directory);

        Persistence::write("state", state);
        worker = std::make_shared<Worker>(this);
    }


    Mixpanel::Mixpanel(
        const std::string& token,
        const bool enable_log_queue
    )
        :Mixpanel(
            token,
            "", // the other constructor will take care of that
            PlatformHelpers::get_storage_directory(token),
            enable_log_queue
    ) {}


    Mixpanel::~Mixpanel()
    {
        log(LogEntry::LL_DEBUG, "*** destroying Mixpanel instance");
        worker = nullptr;
    }

    void Mixpanel::log(LogEntry::Level level, const std::string& message)
    {
        assert(level == LogEntry::LL_TRACE      ||
               level == LogEntry::LL_DEBUG      ||
               level == LogEntry::LL_INFO       ||
               level == LogEntry::LL_WARNING    ||
               level == LogEntry::LL_ERROR);

        if (level < min_log_level) return;

        // we're locking here, so that output to std::clog is also synchronized
        std::unique_lock<std::mutex> lock(log_queue_mutex);
        if (enable_log_queue)
        {
            if (log_entries.size() > 100)
            {
                std::clog << "Warning: log queue overflow. " << std::endl;
            }
            else
            {
                log_entries.push({level, message});
            }
        }
        else
        {
            static const char *level_names[] = {
                "TRACE", "DEBUG", "INFO", "WARNING", "ERROR", "NONE"
            };
            const char *level_name = level_names[level];
            std::clog << "Mixpanel[" << level_name << "]: " << message << std::endl;
            #if defined (_MSC_VER) && defined(DEBUG)
                OutputDebugStringA(("Mixpanel[" + std::string(level_name) + "]: " + message).c_str());
            #endif
        }
    }

    void Mixpanel::set_minimum_log_level(LogEntry::Level level)
    {
        this->min_log_level = level;
    }

    bool Mixpanel::get_next_log_entry(LogEntry& entry)
    {
        std::unique_lock<std::mutex> lock(log_queue_mutex);
        if (!log_entries.empty())
        {
            entry = log_entries.front();
            log_entries.pop();
            return true;
        }
        return false;
    }


    static void merge(Value& a, const Value& b, bool allow_overwrite=true)
    {
        for (const auto& k : b.getMemberNames())
        {
            if (allow_overwrite || a[k].isNull())
            {
                a[k] = b[k];
            }
        }
    }

    std::string Mixpanel::get_distinct_id() const
    {
        assert(state["distinct_id"].isString() && !state["distinct_id"].asString().empty());
        return state["distinct_id"].asString();
    }

    void Mixpanel::track(const std::string event, const Value& properties)
    {
        Value data;
        data["event"] = event;

        auto event_start_time = timed_events.get(event, 0);
        if (event_start_time != 0)
        {
            data["properties"]["$duration"] = time_since_epoch<double>() - event_start_time.asDouble();
        }

        merge(data["properties"], properties, true);

        data["properties"]["token"] = token;
        data["properties"]["distinct_id"] = get_distinct_id();
        data["properties"]["time"] = (Json::Int64) utc_now_timestamp();

        merge(data["properties"], super_properties, false);
        merge(data["properties"], automatic_properties, false);
        data["properties"]["$wifi"] = (network_reachability == NetworkReachability::ReachableViaLocalAreaNetwork);

        worker->enqueue("track", data);
    }

    void Mixpanel::track_charge(double amount, const Value& properties)
    {
        Value data;
        data["$transactions"]["$amount"] = amount;
        data["$transactions"]["$time"] = utc_now();
        merge(data["$transactions"], properties, false);
        engage(op_append, data);
    }

    void Mixpanel::engage(Op op, const Value& values)
    {
        if (op_set > op || op > op_delete)
        {
            log(LogEntry::LL_ERROR, "error: invalid engage op: " + std::to_string(op));
            return;
        }
        static std::vector<std::string> op_names = {"$set", "$set_once", "$add", "$append", "$union", "$unset", "$delete"};
        auto op_name = op_names.at(op);

        Value data;
        data["$token"] = token;
        data["$distinct_id"] = get_distinct_id();

        data["$time"] = static_cast<Value::Int64>(Mixpanel::utc_now_timestamp());
        data[op_name] = values;

        if (op == op_set || op == op_set_once)
        {
            merge(data[op_name], automatic_people_properties, false);
            merge(data[op_name], super_properties, false);
        }

        worker->enqueue("engage", data);
    }

    void Mixpanel::identify(const std::string& unique_id) throw(std::invalid_argument)
    {
        if (unique_id.empty()) throw std::invalid_argument("unique_id cannot be empty");

        if (unique_id != get_distinct_id())
        {
            state["distinct_id"] = unique_id;
            Persistence::write("state", state);
        }
        else
        {
            log(LogEntry::LL_WARNING, "WARNING: unique_id matches current distinct_id.");
        }
    }

    void Mixpanel::alias(const std::string& alias) throw(std::invalid_argument)
    {
        if (alias.empty()) throw std::invalid_argument("alias cannot be empty");

        if (alias != get_distinct_id())
        {
            Value data;
            data["alias"] = alias;
            track("$create_alias", data);
            identify(alias);
        }
        else
        {
            log(LogEntry::LL_WARNING, "alias matches current distinct_id - skipping api call.");
            identify(alias);
        }
    }

    void Mixpanel::register_(const std::string& key, const Value& value)
    {
        assert(!value.isNull());
        assert(!value.isObject());
        assert(!value.isArray());
        super_properties[key] = value;
        Persistence::write("super_properties", super_properties);
    }

    void Mixpanel::register_properties(const Value& properties)
    {
        assert(properties.isObject());

        for (const auto& name : properties.getMemberNames())
        {
            if (name.size() > 0)
            {
                super_properties[name] = properties[name];
            }
        }

        Persistence::write("super_properties", super_properties);
    }

    bool Mixpanel::register_once(const std::string& key, const Value& value)
    {
        if (super_properties[key].isNull())
        {
            register_(key, value);
            return true;
        }
        return false;
    }

    bool Mixpanel::register_once_properties(const Value& properties)
    {
        assert(properties.isObject());

        bool foundNullKeys = false;
        for (const auto& name : properties.getMemberNames())
        {
            if (name.size() > 0)
            {
                foundNullKeys = register_once(name, properties[name]);
            }
        }

        return foundNullKeys;
    }

    bool Mixpanel::unregister(const std::string& key)
    {
        if (!super_properties.removeMember(key).isNull())
        {
            Persistence::write("super_properties", super_properties);
            return true;
        }
        return false;
    }

    bool Mixpanel::unregister_properties(const Value& properties)
    {
        assert(properties.isArray());

        bool foundNullKeys = false;
        for(auto i = 0; i <= properties.size(); ++i)
        {
            auto name = properties[i].asString();
            if (name.size() > 0)
            {
                foundNullKeys = unregister(name);
            }
        }

        return foundNullKeys;
    }

    Value Mixpanel::get_super_properties() const
    {
        return super_properties;
    }

    void Mixpanel::clear_super_properties()
    {
        for (const auto& name : super_properties.getMemberNames())
        {
            if (name.size() > 0 && name[0] != '$')
            {
                super_properties.removeMember(name);
            }
        }
        Persistence::write("super_properties", super_properties);
    }

    std::string Mixpanel::utc_iso_format(time_t time)
    {
        char buf[32];
        std::strftime(buf, sizeof(buf), "%FT%T", std::gmtime(&time));
        std::string ret = buf;
        return ret;
    }

    std::string Mixpanel::utc_now()
    {
        return utc_iso_format(utc_now_timestamp());
    }

    std::time_t Mixpanel::utc_now_timestamp()
    {
        return std::time(nullptr);
    }

    ////////////////// reachability
    void Mixpanel::on_reachability_changed(NetworkReachability network_reachability)
    {
        this->network_reachability = network_reachability;
        worker->notify();
    }

    void Mixpanel::set_maximum_queue_size(std::size_t maximum_size)
    {
        detail::Persistence::set_maximum_queue_size(maximum_size);
    }

    void Mixpanel::set_flush_interval(unsigned seconds)
    {
        worker->set_flush_interval(seconds);
    }

    void Mixpanel::flush_queue()
    {
        worker->flush_queue();
    }

    void Mixpanel::clear_send_queues()
    {
        worker->clear_send_queues();
    }

    void Mixpanel::reset()
    {
        clear_super_properties();
        clear_send_queues();
        clear_timed_events();
    }

    bool Mixpanel::start_timed_event(const std::string& event_name) throw(std::invalid_argument)
    {
        if (event_name.empty()) throw std::invalid_argument("timed event must have a value.");

        bool result = timed_events.get(event_name, 0) == 0;

        timed_events[event_name] = time_since_epoch<double>();
        Persistence::write("timed_events", timed_events);

        return result;
    }

    bool Mixpanel::start_timed_event_once(const std::string& event_name) throw(std::invalid_argument)
    {
        if (!timed_events.isObject() || timed_events.get(event_name, 0) == 0)
            return start_timed_event(event_name);
        return false;
    }

    bool Mixpanel::clear_timed_event(const std::string& event_name) throw(std::invalid_argument)
    {
        if (event_name.empty()) throw std::invalid_argument("timed event ca not be empty");

        Value value;
        if (timed_events.removeMember(event_name, &value))
        {
            Persistence::write("timed_events", timed_events);
            return true;
        }
        return false;
    }

    void Mixpanel::clear_timed_events()
    {
        timed_events = Value(detail::Json::objectValue);
        Persistence::write("timed_events", timed_events);
    }

    Value Mixpanel::collect_automatic_properties()
    {
        Value ret = PlatformHelpers::collect_automatic_properties();
        ret["$lib_version"] = sdk_version;
        ret["mp_lib"] = "unity";
        return ret;
    }


    Value Mixpanel::collect_automatic_people_properties()
    {
        Value ret = PlatformHelpers::collect_automatic_people_properties();
        ret["$unity_lib_version"] = sdk_version;
        return ret;
    }

} // namespace mixpanel
