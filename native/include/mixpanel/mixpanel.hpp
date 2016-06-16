/*
    *** do not modify the line below, it is updated by the build scripts ***
    Mixpanel C++ SDK version v1.0.1
*/

#ifndef _MIXPANEL_HPP_
#define _MIXPANEL_HPP_

#include <string>
#include <queue>
#include <mutex>
#include <ctime>
#include <atomic>
#include <stdexcept>
#include <memory>
#include "./value.hpp"
#include "../../tests/gtest/include/gtest/gtest_prod.h"

#if defined(_MSC_VER)
#    pragma warning( disable : 4290 )
#endif

class MixpanelNetwork_RetryAfter_Test;
class MixpanelNetwork_BackOffTime_Test;
class MixpanelNetwork_FailureRecovery_Test;

namespace mixpanel
{
    namespace detail
    {
        class Worker;
    }

    /*!
        This is the entry point into the SDK. Create an instance of this class somewhere and start using the tracking functions.

        Only one instance of this class should be created in your application.

        Internally it creates a background worker that takes care of the sending so the calls do not block the caller.

        When the instance is destroyed, sending out the last 50 events is attempted one more time (if there are any) and the worker
        is terminated. Only after that the destructor returns.

        Creating a global (or static) instance is untested, so be careful. If you need global access consider using a unique_ptr or
        shared_ptr and reset it via std::atexit()
    */
    class Mixpanel
    {
        public:
            /// construct a Mixpanel instance where most parameters are determined automatically
            Mixpanel(
                /// the token you get from the mixpanel dashboard
                const std::string& token,

                /// don't print to std::clog, but queue the log entries for retrieval via get_next_log_entry().
                /// note that the queue will hold at most 100 entries. So make sure to get_next_log_entry() frequently enough.
                const bool enable_log_queue = false
            );

            /// construct a Mixpanel instance with custom parameters. This is useful, if you want to modify the defaults
            Mixpanel(
                const std::string& token,              ///< the token you get from the mixpanel dashboard
                const std::string& distinct_id,        ///< if empty, we're going to get the device id on Android, iOS and OSX and a random UUID on Windows
                const std::string& storage_directory,  ///< a writable directory to persist the data to
                const bool enable_log_queue = false    ///< if true, don't print to std::clog, but queue the log entries for retrieval via get_next_log_entry()
            );

            virtual ~Mixpanel();

            /// sets the distinct_id
            void identify(const std::string& unique_id) throw(std::invalid_argument);

            /// creates a $create_alias event and sets distinct_id to alias
            void alias(const std::string& alias) throw(std::invalid_argument);

            /// registers a super property
            void register_(const std::string& key, const Value& value);
            /// $MP_DOCS_NEEDED
            void register_properties(const Value& properties);

            /// registers a super property, but only if it has not already registered
            bool register_once(const std::string& key, const Value& value);
            /// $MP_DOCS_NEEDED
            bool register_once_properties(const Value& properties);

            /// unregisters a super property
            bool unregister(const std::string& key);
            bool unregister_properties(const Value& properties);

            /// get a list of all registered super properties.
            Value get_super_properties() const;

            /// clear all (non-internal) super properties
            void clear_super_properties();

            /// start a timed event. The effect is, that a timestamp will be stored for the event.
            /// every time you track() an event with *event_name*, a property called $duration will be
            /// automatically attached. The start times are persisted, so you can track durations
            /// over the lifetime of your app. This version overwrites the start time.
            /// thus it is useful, if you want to track events from session start (or some other point in
            /// time). Use start_timed_event_once() if you don't want to overwrite the start time.
            /// returns true, if the event was not registered before, false if it was reset.
            bool start_timed_event(const std::string& event_name) throw(std::invalid_argument);

            /// like start_timed_event, but does nothing, if the event is already registered as a timed event.
            /// Use this, if you want to know the duration from the point in time the event was first registered.
            /// returns true on first registration, false if the event_name was already registered.
            bool start_timed_event_once(const std::string& event_name) throw(std::invalid_argument);

            /// declares, that event named *event_name* is no longer a timed event and that $duration will
            /// no longer be attached. Will throw std::invalid_argument if event_name is empty. Will do nothing,
            /// if event_name is not registered as a timed event.
            /// return true, if the event was found, false otherwise.
            bool clear_timed_event(const std::string& event_name) throw(std::invalid_argument);

            /// clear all timed events
            void clear_timed_events();

            /// clear super properties, timed events and send queues. Note, that all arguments set via the constructor will still be intact.
            /// So this instance will still be valid (in the OOP sense of the word).
            /// If you want to completely start from scratch, you may want to create a new instance.
            void reset();

            /// track a named *event* with optional *properties*.
            void track(const std::string event, const Value& properties=Value());

            /// access to profile related tracking functionality. Use Mixpanel::people to get access to the member functions.
            class People
            {
                public:
                    /// Takes a JSON object containing names and values of profile properties. If the profile does not exist, it creates it with these properties. If it does exist, it sets the properties to these values, overwriting existing values.
                    void set(const std::string& property,  const Value& to);
                    /// Set a collection of properties on the identified user all at once.
                    void set_properties(const Value& properties) throw(std::invalid_argument);

                    /// Works just like set, except it will not overwrite existing property values. This is useful for properties like "First login date".
                    void set_once(const std::string& property,  const Value& to);
                    /// Like set(const Value& properties), but will not set properties that already exist on a record.
                    void set_once_properties(const Value& properties) throw(std::invalid_argument);
                    /// MP-REMOVE
                    void unset(const std::string& property);
                    /// Takes a string property name, and permanently removes the property and their values from a profile.
                    void unset_properties(const Value& properties) throw(std::invalid_argument);

                    /// Takes a JSON object containing keys and numerical values. When processed, the property values are added to the existing values of the properties on the profile.
                    /// If the property is not present on the profile, the value will be added to 0. It is possible to decrement by calling "$add" with negative values. This is useful for maintaining the values of properties like "Number of Logins" or "Files Uploaded".
                    void increment(const std::string& property,  const Value& by) throw(std::invalid_argument);
                    /// Change the existing values of multiple People Analytics properties at once.
                    void increment_properties(const Value& properties) throw(std::invalid_argument);
                    /// Appends a value to a list-valued property.
                    void append(const std::string& list_name,  const Value& value);
                    /// Takes a JSON object containing keys and values, and appends each to a list associated with the corresponding property name. $appending to a property that doesn't exist will result in assigning a list with one element to that property.
                    void append_properties(const Value& properties) throw(std::invalid_argument);

                    /// Takes a JSON object containing keys and list values. The list values in the request are merged with the existing list on the user profile, ignoring duplicate list values.
                    /// throws std::invalid argument if values is not an array.
                    void union_(const std::string& list_name,  const Value& values) throw(std::invalid_argument);
                    /// Adds values to a list-valued property only if they are not already present in the list.
                    void union_properties(const Value& properties) throw(std::invalid_argument);

                    /// track a transaction of *amount* and optional *properties*
                    void track_charge(double amount, const Value& properties=Value());

                    /// clear all charges related to the current profile
                    void clear_charges();

                    /// adds *token* to $ios_devices on iOS or $android_devices on Android. Does nothing on other platforms.
                    void set_push_id(const std::string& token);

                    /// convenience methods for special properties
                    void set_first_name(const std::string& to);
                    void set_last_name(const std::string& to);
                    void set_name(const std::string& to);
                    void set_email(const std::string& to);
                    void set_phone(const std::string& to);
                private:
                    friend class Mixpanel;
                    People(class Mixpanel *mixpanel);
                    class Mixpanel *mixpanel;
            };

            /// accessor for profile related tracking functionality
            People people;

            struct LogEntry
            {
                enum Level
                {
                    LL_TRACE  = 0,
                    LL_DEBUG  = 1,
                    LL_INFO   = 2,
                    LL_WARNING= 3,
                    LL_ERROR  = 4,
                    LL_NONE   = 5
                };

                Level level;
                std::string message;
            };

            /// sets the minimum log level you're interested in (inclusive). The default is LL_WARNING. To completely silence logs pass LL_NONE
            void set_minimum_log_level(LogEntry::Level level);

            /// if enable_log_queue is true, this receives the next log entry from the queue and returns true. returns false if there are no more items.
            bool get_next_log_entry(LogEntry& entry);

            typedef unsigned Seconds;
            typedef unsigned Days;

            /// returns now() in UTC as iso formatted string
            static std::string utc_now();

            enum class NetworkReachability
            {
                NotReachable,                   ///< Network is not reachable.
                ReachableViaCarrierDataNetwork, ///< Network is reachable via carrier data network.
                ReachableViaLocalAreaNetwork    ///< Network is reachable via WiFi or cable.
            };

            /// call this when the reachability of the device changes (if you happen to have that information). Used to restrict data sending
            void on_reachability_changed(NetworkReachability network_reachability);

            /// sets the maximum size of the outgoing queues (track, engage) in bytes. The default is 5 MB.
            /// if this size is exceeded no new data will be appended until the size is below this threshold.
            void set_maximum_queue_size(std::size_t maximum_size);

            /// set the interval at which the contents of the queue are tried to be flushed. The default is 60 seconds.
            /// Setting a flush interval of 0 will turn off the flush timer.
            void set_flush_interval(unsigned seconds);

            /// attempt to flush the queue now. This call is non-blocking.
            void flush_queue();
        private:
            friend class People;
            friend class mixpanel::detail::Worker;
            FRIEND_TEST(::MixpanelNetwork, RetryAfter);
            FRIEND_TEST(::MixpanelNetwork, BackOffTime);
            FRIEND_TEST(::MixpanelNetwork, FailureRecovery);

            enum Op
            {
                op_set = 0,
                op_set_once,
                op_add,
                op_append,
                op_union,
                op_unset,
                op_delete,
            };

            /// clears out the send queues.
            void clear_send_queues();

            void engage(Op op, const Value& value);
            void track_charge(double amount, const Value& properties);

            /// iso-format a time in UTC
            static std::string utc_iso_format(time_t time);
            static std::time_t utc_now_timestamp();

            std::string get_distinct_id() const;

            std::string token;
            Value state;
            Value super_properties;
            Value automatic_properties;
            Value automatic_people_properties;
            Value timed_events;

            static Value collect_automatic_properties();
            static Value collect_automatic_people_properties();

            void log(LogEntry::Level level, const std::string& message);
            bool enable_log_queue;
            std::atomic<NetworkReachability> network_reachability;

            LogEntry::Level min_log_level;
            std::queue<LogEntry> log_entries;
            std::mutex log_queue_mutex;

            static std::shared_ptr<detail::Worker> worker;
    };
}

#endif /* _MIXPANEL_HPP_ */
