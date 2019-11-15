using System;

namespace mixpanel
{
    /// <summary>
    /// Core class for interacting with %Mixpanel Analytics.
    /// </summary>
    /// <description>
    /// <p>Open unity project settings and set the properties in the unity inspector (token, debug token, etc.)</p>
    /// <p>Once you have the mixpanel settings setup, you can track events by using <c>Mixpanel.Track(string eventName)</c>.
    /// You can also update %People Analytics records with Mixpanel.people. </p>
    /// </description>
    /// <code>
    ///        //Track an event in Mixpanel Engagement<br/>
    ///        Mixpanel.Track("Hello World");<br/>
    ///        Mixpanel.Identify("CURRENT USER DISTINCT ID");<br/>
    ///        Mixpanel.People.Set("Plan", "Premium");<br/>
    /// </code>
    public static partial class Mixpanel
    {
        internal const string MixpanelUnityVersion = "2.1.0";

        /// <summary>
        /// Creates a distinct_id alias.
        /// </summary>
        /// <param name="alias">the new distinct_id that should represent original</param>
        public static void Alias(string alias)
        {
            if (alias == MixpanelStorage.DistinctId) return;
            Value properties = ObjectPool.Get();
            properties["alias"] = alias;
            Track("$create_alias", properties);
            Flush();
        }

        /// <summary>
        /// Clears all current event timers.
        /// </summary>
        public static void ClearTimedEvents()
        {
            MixpanelStorage.ResetTimedEvents();
        }

        /// <summary>
        /// Clears the event timer for a single event.
        /// </summary>
        /// <param name="eventName">the name of event to clear event timer</param>
        public static void ClearTimedEvent(string eventName)
        {
            Value properties = MixpanelStorage.TimedEvents;
            properties.Remove(eventName);
            MixpanelStorage.TimedEvents = properties;
        }

        /// <summary>
        /// Sets the distinct ID of the current user.
        /// </summary>
        /// <param name="uniqueId">a string uniquely identifying this user. Events sent to %Mixpanel
        /// using the same distinct_id will be considered associated with the same visitor/customer for
        /// retention and funnel reporting, so be sure that the given value is globally unique for each
        /// individual user you intend to track.
        /// </param>
        public static void Identify(string uniqueId)
        {
            if (MixpanelStorage.DistinctId == uniqueId) return;
            string oldDistinctId = MixpanelStorage.DistinctId;
            MixpanelStorage.DistinctId = uniqueId;
            Track("$identify", "$anon_distinct_id", oldDistinctId);
        }

        [Obsolete("Please use 'DistinctId' instead!")]
        public static string DistinctID {
            get => MixpanelStorage.DistinctId;
        }

        public static string DistinctId {
            get => MixpanelStorage.DistinctId;
        }

        /// <summary>
        /// Opt out tracking.
        /// </summary>
        public static void OptOutTracking()
        {
            People.ClearCharges();
            Flush();
            People.DeleteUser();
            Flush();
            Reset();
            MixpanelStorage.IsTracking = false;
        }

        /// <summary>
        /// Opt in tracking.
        /// </summary>
        public static void OptInTracking()
        {
            MixpanelStorage.IsTracking = true;
            Controller.DoTrack("$opt_in", ObjectPool.Get());
        }

        /// <summary>
        /// Opt in tracking.
        /// </summary>
        /// <param name="distinctId">the distinct id for events. Behind the scenes,
        /// <code>Identify</code> will be called by using this distinct id.</param>
        public static void OptInTracking(string distinctId)
        {
            Identify(distinctId);
            OptInTracking();
        }

        /// <summary>
        /// Registers super properties, overwriting ones that have already been set.
        /// </summary>
        /// <param name="key">name of the property to register</param>
        /// <param name="value">value of the property to register</param>
        public static void Register(string key, Value value)
        {
            Value properties = MixpanelStorage.SuperProperties;
            properties[key] = value;
            MixpanelStorage.SuperProperties = properties;
        }

        /// <summary>
        /// Registers super properties without overwriting ones that have already been set.
        /// </summary>
        /// <param name="key">name of the property to register</param>
        /// <param name="value">value of the property to register</param>
        public static void RegisterOnce(string key, Value value)
        {
            Value properties = MixpanelStorage.OnceProperties;
            properties[key] = value;
            MixpanelStorage.OnceProperties = properties;
        }

        /// <summary>
        /// Clears all super properties, once properties, timed events and push registrations from persistent MixpanelStorage.
        /// </summary>
        public static void Reset()
        {
            MixpanelStorage.ResetSuperProperties();
            MixpanelStorage.ResetOnceProperties();
            MixpanelStorage.ResetTimedEvents();
            MixpanelStorage.SavePushDeviceToken("");
            Flush();
            MixpanelStorage.DistinctId = "";
        }

        /// <summary>
        /// Clears all items from the Track and Engage request queues, anything not already sent to the Mixpanel
        /// API will no longer be sent
        /// </summary>
        public static void Clear()
        {
            Controller.DoClear();
        }

        /// <summary>
        /// Start timing of an event. Calling Mixpanel.StartTimedEvent(string eventName) will not send an event,
        /// but when you eventually call Mixpanel.Track(string eventName), your tracked event will be sent with a "$duration" property,
        /// representing the number of seconds between your calls.
        /// </summary>
        /// <param name="eventName">the name of the event to track with timing</param>
        public static void StartTimedEvent(string eventName)
        {
            Value properties = MixpanelStorage.TimedEvents;
            properties[eventName] = Util.CurrentTime();
            MixpanelStorage.TimedEvents = properties;
        }

        /// <summary>
        /// Begin timing of an event, but only if the event has not already been registered as a timed event.
        /// Useful if you want to know the duration from the point in time the event was first registered.
        /// </summary>
        /// <param name="eventName">the name of the event to track with timing</param>
        public static void StartTimedEventOnce(string eventName)
        {
            if (!MixpanelStorage.TimedEvents.ContainsKey(eventName))
            {
                Value properties = MixpanelStorage.TimedEvents;
                properties[eventName] = Util.CurrentTime();
                MixpanelStorage.TimedEvents = properties;
            }
        }

        /// <summary>
        /// Tracks an event.
        /// </summary>
        /// <param name="eventName">the name of the event to send</param>
        public static void Track(string eventName) => Controller.DoTrack(eventName, null);

        /// <summary>
        /// Tracks an event with properties of key=value.
        /// </summary>
        /// <param name="eventName">the name of the event to send</param>
        /// <param name="key">A Key value for the data</param>
        /// <param name="value">The value to use for the key</param>
        public static void Track(string eventName, string key, Value value)
        {
            Value properties = ObjectPool.Get();
            properties[key] = value;
            Controller.DoTrack(eventName, properties);
        }

        /// <summary>
        /// Tracks an event with properties.
        /// </summary>
        /// <param name="eventName">the name of the event to send</param>
        /// <param name="properties">A Value containing the key value pairs of the properties
        /// to include in this event. Pass null if no extra properties exist.
        /// </param>
        public static void Track(string eventName, Value properties) => Controller.DoTrack(eventName, properties);

        /// <summary>
        /// Removes a single superProperty.
        /// </summary>
        /// <param name="key">name of the property to unregister</param>
        public static void Unregister(string key)
        {
            Value properties = MixpanelStorage.SuperProperties;
            properties.Remove(key);
            MixpanelStorage.SuperProperties = properties;
        }

        /// <summary>
        /// Flushes the queued data to Mixpanel
        /// </summary>
        public static void Flush()
        {
            Controller.DoFlush();
        }

        /// <summary>
        /// Core interface for using %Mixpanel %People Analytics features. You can get an instance by calling Mixpanel.people
        /// </summary>
        public static class People
        {
            /// <summary>
            /// Append values to list properties.
            /// </summary>
            /// <param name="properties">mapping of list property names to values to append</param>
            public static void Append(Value properties)
            {
                Controller.DoEngage(new Value {{"$append", properties}});
            }

            /// <summary>
            /// Appends a value to a list-valued property.
            /// </summary>
            /// <param name="property">the %People Analytics property that should have it's value appended to</param>
            /// <param name="values">the new value that will appear at the end of the property's list</param>
            public static void Append(string property, Value values)
            {
                Append(new Value {{property, values}});
            }

            /// <summary>
            /// Permanently clear the whole transaction history for the identified people profile.
            /// </summary>
            public static void ClearCharges()
            {
                Unset("$transactions");
            }

            /// <summary>
            /// Permanently delete the identified people profile
            /// </summary>
            public static void DeleteUser()
            {
                Controller.DoEngage(new Value {{"$delete", ""}});
            }

            /// <summary>
            /// Change the existing values of multiple %People Analytics properties at once.
            /// </summary>
            /// <param name="properties"> A map of String properties names to Long amounts. Each property associated with a name in the map </param>
            public static void Increment(Value properties)
            {
                Controller.DoEngage(new Value {{"$add", properties}});
            }

            /// <summary>
            /// Convenience method for incrementing a single numeric property by the specified amount.
            /// </summary>
            /// <param name="property">property name</param>
            /// <param name="by">amount to increment by</param>
            public static void Increment(string property, Value by)
            {
                Increment(new Value {{property, by}});
            }

            /// <summary>
            /// Set a collection of properties on the identified user all at once.
            /// </summary>
            /// <param name="properties">a JSONObject containing the collection of properties you wish to apply
            /// to the identified user. Each key in the JSONObject will be associated with a property name, and the value
            /// of that key will be assigned to the property.
            /// </param>
            public static void Set(Value properties)
            {
                properties.Merge(Controller.GetEngageDefaultProperties());
                Controller.DoEngage(new Value {{"$set", properties}});
            }

            /// <summary>
            /// Sets a single property with the given name and value for this user.
            /// </summary>
            /// <param name="property">property name</param>
            /// <param name="to">property value</param>
            public static void Set(string property, Value to)
            {
                Set(new Value {{property, to}});
            }

            /// <summary>
            /// Like Mixpanel.Set(Value properties), but will not set properties that already exist on a record.
            /// </summary>
            /// <param name="properties">a JSONObject containing the collection of properties you wish to apply to the identified user. Each key in the JSONObject will be associated with a property name, and the value of that key will be assigned to the property.</param>
            public static void SetOnce(Value properties)
            {
                Controller.DoEngage(new Value {{"$set_once", properties}});
            }

            /// <summary>
            /// Like Mixpanel.Set(string property, Value to), but will not set properties that already exist on a record.
            /// </summary>
            /// <param name="property">property name</param>
            /// <param name="to">property value</param>
            public static void SetOnce(string property, Value to)
            {
                SetOnce(new Value {{property, to}});
            }

            /// <summary>
            /// Track a revenue transaction for the identified people profile.
            /// </summary>
            /// <param name="amount">amount of revenue received</param>
            public static void TrackCharge(double amount)
            {
                TrackCharge(new Value {{"$amount", amount}});
            }

            /// <summary>
            /// Track a revenue transaction for the identified people profile.
            /// </summary>
            /// <param name="properties">a JSONObject containing the collection of properties you wish to apply</param>
            public static void TrackCharge(Value properties)
            {
                properties["$time"] = Util.CurrentDateTime();
                Controller.DoEngage(new Value {{"$append", new Value {{"$transactions", properties}}}});
            }

            /// <summary>
            /// Adds values to a list-valued property only if they are not already present in the list.
            /// If the property does not currently exist, it will be created with the given list as it's value.
            /// If the property exists and is not list-valued, the union will be ignored.
            /// </summary>
            /// <param name="properties">mapping of list property names to lists to union</param>
            public static void Union(Value properties)
            {
                Controller.DoEngage(new Value {{"$union", properties}});
            }

            /// <summary>
            /// Adds values to a list-valued property only if they are not already present in the list.
            /// If the property does not currently exist, it will be created with the given list as it's value.
            /// If the property exists and is not list-valued, the union will be ignored.            /// </summary>
            /// <param name="property">name of the list-valued property to set or modify</param>
            /// <param name="values">an array of values to add to the property value if not already present</param>
            public static void Union(string property, Value values)
            {
                if (!values.IsArray)
                    throw new ArgumentException("Union with values property must be an array", nameof(values));
                Union(new Value {{property, values}});
            }

            /// <summary>
            /// Takes a string property name, and permanently removes the property and their values from a profile.
            /// </summary>
            /// <param name="property">property</param>
            public static void Unset(string property)
            {
                Controller.DoEngage(new Value {{"$unset", new string[]{property}}});
            }

            /// <summary>
            /// Sets the email for this user.
            /// </summary>
            public static string Email
            {
                set => Set(new Value {{"$email", value}});
            }

            /// <summary>
            /// Sets the first name for this user.
            /// </summary>
            public static string FirstName
            {
                set => Set(new Value {{"$first_name", value}});
            }

            /// <summary>
            /// Sets the last name for this user.
            /// </summary>
            public static string LastName
            {
                set => Set(new Value {{"$last_name", value}});
            }

            /// <summary>
            /// Sets the name for this user.
            /// </summary>
            public static string Name
            {
                set => Set(new Value {{"$name", value}});
            }

            /// <summary>
            /// Register the given device to receive push notifications.
            /// </summary>
            public static byte[] PushDeviceToken
            {
                set
                {
                    string token = BitConverter.ToString(value).ToLower().Replace("-", "");
                    MixpanelStorage.SavePushDeviceToken(token);
                    #if UNITY_IOS
                        Union("$ios_devices", new string[] {token});
                    #elif UNITY_ANDROID
                        Union("$android_devices", new string[] {token});
                    #endif
                }
            }
        }
    }
}
