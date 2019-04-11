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
    ///        Mixpanel.people.Set("Plan", "Premium");<br/>
    /// </code>
    public static partial class Mixpanel
    {
        /// <summary>
        /// Creates a distinct_id alias.
        /// </summary>
        /// <param name="alias">the new distinct_id that should represent original</param>
        public static void Alias(string alias)
        {
            if (alias == DistinctID) return;
            track("$create_alias", new Value(){{ "distinct_id", DistinctID }, { "alias", alias }});
        }

        /// <summary>
        /// Clears all current event timers.
        /// </summary>
        public static void ClearTimedEvents()
        {
            TimedEvents = new Value();
        }

        /// <summary>
        /// Clears the event timer for a single event.
        /// </summary>
        /// <param name="eventName">the name of event to clear event timer</param>
        public static void ClearTimedEvent(string eventName)
        {
            var events = TimedEvents;
            events.Remove(eventName);
            TimedEvents = events;
        }

        /// <summary>
        /// Sets the distinct ID of the current user.
        /// </summary>
        /// <param name="uniqueId">a string uniquely identifying this user. Events sent to %Mixpanel
        /// using the same disinct_id will be considered associated with the same visitor/customer for
        /// retention and funnel reporting, so be sure that the given value is globally unique for each
        /// individual user you intend to track.
        /// </param>
        public static void Identify(string uniqueId)
        {
            DistinctID = uniqueId;
        }

        /// <summary>
        /// Opt out tracking.
        /// </summary>
        public static void OptOutTracking()
        {
            IsTracking = false;
        }

        /// <summary>
        /// Opt in tracking.
        /// </summary>
        public static void OptInTracking()
        {
            IsTracking = true;
        }

        /// <summary>
        /// Opt in tracking.
        /// </summary>
        /// <param name="distinct_id">the distinct id for events. Behind the scenes,
        /// <code>Identify</code> will be called by using this distinct id.</param>
        public static void OptInTracking(string distinct_id)
        {
            IsTracking = true;
            DistinctID = distinct_id;
        }

        /// <summary>
        /// Registers super properties, overwriting ones that have already been set.
        /// </summary>
        /// <param name="key">name of the property to register</param>
        /// <param name="value">value of the property to register</param>
        public static void Register(string key, object value) {
            var props = SuperProperties;
            props[key] = value;
            SuperProperties = props;
        }

        /// <summary>
        /// Registers super properties without overwriting ones that have already been set.
        /// </summary>
        /// <param name="key">name of the property to register</param>
        /// <param name="value">value of the property to register</param>
        public static void RegisterOnce(string key, object value) {
            if (!props.ContainsKey(key))
                OnceProperties.Add(key, value);
        }

        /// <summary>
        /// Clears all distinct_ids, superProperties, and push registrations from persistent storage.
        /// Will not clear referrer information.
        /// </summary>
        public static void Reset()
        {
            DistinctID = GetID();
            SuperProperties = new Value();
            OnceProperties = new Value();
            // push registrations
        }

        /// <summary>
        /// Start timing of an event. Calling Mixpanel.StartTimedEvent(string eventName) will not send an event,
        /// but when you eventually call Mixpanel.Track(string eventName), your tracked event will be sent with a "$duration" property,
        /// representing the number of seconds between your calls.
        /// </summary>
        /// <param name="eventName">the name of the event to track with timing</param>
        public static void StartTimedEvent(string eventName)
        {
            var events = TimedEvents;
            events[eventName] = CurrentTime();
            TimedEvents = events;
        }

        /// <summary>
        /// Begin timing of an event, but only if the event has not already been registered as a timed event.
        /// Useful if you want to know the duration from the point in time the event was first registered.
        /// </summary>
        /// <param name="eventName">the name of the event to track with timing</param>
        public static void StartTimedEventOnce(string eventName)
        {
            var events = TimedEvents;
            if (!events.ContainsKey(eventName))
            {
                events[eventName] = CurrentTime();
                TimedEvents = events;
            }
        }

        /// <summary>
        /// Tracks an event.
        /// </summary>
        /// <param name="eventName">the name of the event to send</param>
        public static void Track(string eventName)
        {
            track(eventName, new Value());
        }

        /// <summary>
        /// Tracks an event with properties of key=value.
        /// </summary>
        /// <param name="eventName">the name of the event to send</param>
        /// <param name="key">A Key value for the data</param>
        /// <param name="value">The value to use for the key</param>
        public static void Track(string eventName, string key, object value)
        {
            var data = new Value();
            data[key] = value;
            track(eventName, data);
        }

        /// <summary>
        /// Tracks an event with properties.
        /// </summary>
        /// <param name="eventName">the name of the event to send</param>
        /// <param name="properties">A JSONObject containing the key value pairs of the properties
        /// to include in this event. Pass null if no extra properties exist.
        /// </param>
        public static void Track(string eventName, Value properties)
        {
            track(eventName, properties);
        }

        /// <summary>
        /// Removes a single superProperty.
        /// </summary>
        /// <param name="key">name of the property to unregister</param>
        public static void Unregister(string key) {
            var props = SuperProperties;
            props.Remove(key);
            SuperProperties = props;
        }
    }
}
