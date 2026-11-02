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
        internal const string MixpanelUnityVersion = "3.6.0";

        /// <summary>
        /// Creates an Mixpanel instance. Use only if you have enabled "Manual Initialization" from your Project Settings.
        /// Do not forget to call Disable() when you want to dispose your object.
        /// </summary>
        public static void Init()
        {
            Controller.Initialize();
        }

        /// <summary>
        /// Checks whether Mixpanel is initialized or not. If it is not, every API will be no-op.
        /// </summary>
        public static bool IsInitialized()
        {
            bool initialized = Controller.IsInitialized();
            if (!initialized) {
                Mixpanel.Log("Mixpanel is not initialized");
            }
            return initialized;
        }

        /// <summary>
        /// By default, Mixpanel uses PlayerPreferences for data persistence. However you can call this method to
        /// set the data persistence of your choice as long as it follows IPeferences
        /// </summary>
        /// <param name="preferences">the new distinct_id that should represent original</param>
        public static void SetPreferencesSource(IPreferences preferences)
        {
            MixpanelStorage.SetPreferencesSource(preferences);
        }

        /// <summary>
        /// Creates a distinct_id alias.
        /// </summary>
        /// <description>
        /// A hierarchical groups project identifies a user by their company and user id, which an alias of one
        /// distinct id to another can't express, so this is dropped with an error log there. Use
        /// <c>Identify(MixpanelIdentity)</c> instead.
        /// </description>
        /// <param name="alias">the new distinct_id that should represent original</param>
        public static void Alias(string alias)
        {
            if (!IsInitialized()) return;
            if (Config.HierarchicalGroups)
            {
                LogError("Alias is not supported for hierarchical groups projects, which identify a user by " +
                    "company and user id. Use Identify(new MixpanelIdentity(userId, companyId)). The alias was dropped.");
                return;
            }
            if (alias == MixpanelStorage.DistinctId) return;
            Value properties = new Value();
            properties["alias"] = alias;
            Track("$create_alias", properties);
            Flush();
        }

        /// <summary>
        /// Clears all current event timers.
        /// </summary>
        public static void ClearTimedEvents()
        {
            if (!IsInitialized()) return;
            MixpanelStorage.ResetTimedEvents();
        }

        /// <summary>
        /// Clears the event timer for a single event.
        /// </summary>
        /// <param name="eventName">the name of event to clear event timer</param>
        public static void ClearTimedEvent(string eventName)
        {
            if (!IsInitialized()) return;
            Value properties = MixpanelStorage.TimedEvents;
            properties.Remove(eventName);
            MixpanelStorage.TimedEvents = properties;
        }

        /// <summary>
        /// Sets the distinct ID of the current user.
        /// </summary>
        /// <description>
        /// Not supported on a hierarchical groups project, which identifies a user by company and user id, so it
        /// is logged and ignored there. Use <c>Identify(MixpanelIdentity)</c> instead.
        /// </description>
        /// <param name="uniqueId">a string uniquely identifying this user. Events sent to %Mixpanel
        /// using the same distinct_id will be considered associated with the same visitor/customer for
        /// retention and funnel reporting, so be sure that the given value is globally unique for each
        /// individual user you intend to track.
        /// </param>
        public static void Identify(string uniqueId)
        {
            if (!IsInitialized()) return;
            if (Config.HierarchicalGroups)
            {
                LogError("Identify(string) is not supported for hierarchical groups projects, which identify a " +
                    "user by company and user id. Use Identify(new MixpanelIdentity(userId, companyId)). " +
                    "Nothing was identified.");
                return;
            }
            IdentifyDistinctId(uniqueId);
        }

        private static void IdentifyDistinctId(string uniqueId)
        {
            if (MixpanelStorage.DistinctId == uniqueId) return;
            string oldDistinctId = MixpanelStorage.DistinctId;
            // Remember the anonymous id and the user for GetIdentity(); events are unchanged
            if (MixpanelStorage.DeviceId == null) MixpanelStorage.DeviceId = oldDistinctId;
            MixpanelStorage.UserId = uniqueId;
            MixpanelStorage.DistinctId = uniqueId;
            Track("$identify", "$anon_distinct_id", oldDistinctId);
        }

        /// <summary>
        /// Associates all future events and %People updates with the given identity.
        /// </summary>
        /// <description>
        /// <p>On a project that uses hierarchical groups, enabled with <c>HierarchicalGroups</c> in the %Mixpanel
        /// project settings, identify with both a user and a company. %Mixpanel builds the user's distinct id
        /// from <c>$company_id</c> and <c>$user_id</c>, so the company is part of the user's identity: the same
        /// user id in two companies is two users. Every later event carries <c>$company_id</c> beside
        /// <c>$user_id</c> and <c>$device_id</c>, and %People updates go to that company's <c>$user_id</c> group
        /// profile. Both ids persist across restarts until the next identify or <c>Reset()</c>.</p>
        /// <p>Calling this with the current user and a different company is an identity change and tracks
        /// <c>$identify</c>, as switching users does. An identity without both a user id and a company id is
        /// logged and ignored.</p>
        /// <p>On any other project %Mixpanel ignores <c>$company_id</c>, so an identity that names a company is
        /// logged and ignored rather than attributed without it. An identity with a null or empty user id is
        /// also logged and ignored.</p>
        /// <p>The SDK owns the device id, so a <c>DeviceId</c> on the identity is ignored, with a warning when
        /// it differs from the current one.</p>
        /// </description>
        /// <code>
        ///        Mixpanel.Identify(new MixpanelIdentity("user_123", "acme"));<br/>
        /// </code>
        /// <param name="identity">the user, and on a hierarchical groups project their company</param>
        public static void Identify(MixpanelIdentity identity)
        {
            if (!IsInitialized()) return;
            if (!MixpanelStorage.IsTracking) return;
            if (identity == null)
            {
                LogError("Can't identify with a null MixpanelIdentity.");
                return;
            }
            string userId = identity.UserId;
            string companyId = identity.CompanyId;
            if (Config.HierarchicalGroups)
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(companyId))
                {
                    LogError("Can't identify " + identity + ": hierarchical groups projects identify a user by " +
                        "company and user id, so both are required. Nothing was identified.");
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(userId))
                {
                    LogError("Can't identify " + identity + " without a user id. Nothing was identified.");
                    return;
                }
                if (companyId != null)
                {
                    LogError("Can't identify " + identity + ": a company requires HierarchicalGroups in the " +
                        "Mixpanel project settings. Mixpanel ignores $company_id on other projects, so nothing " +
                        "was identified.");
                    return;
                }
            }
            string deviceId = identity.DeviceId;
            string currentDeviceId = GetIdentity().DeviceId;
            if (deviceId != null && deviceId != currentDeviceId)
            {
                Log("Ignoring deviceId " + deviceId + " on " + identity + ". The SDK owns the device id (" +
                    currentDeviceId + ") so earlier anonymous events follow the user.");
            }
            if (!Config.HierarchicalGroups)
            {
                IdentifyDistinctId(userId);
                return;
            }
            string oldDistinctId = Controller.EventsDistinctId();
            // A user in another company is another user, so a new company is an identity change
            bool userChanged = userId != MixpanelStorage.UserId;
            bool companyChanged = companyId != MixpanelStorage.CompanyId;
            if (!userChanged && !companyChanged) return;
            MixpanelStorage.EnsureDeviceId();
            MixpanelStorage.CompanyId = companyId;
            MixpanelStorage.UserId = userId;
            MixpanelStorage.DistinctId = userId;
            Track("$identify", "$anon_distinct_id", oldDistinctId);
        }

        /// <summary>
        /// Returns who events are currently attributed to: the identified user, their company on a hierarchical
        /// groups project, and the device id.
        /// </summary>
        /// <description>
        /// Before <c>Identify</c> the user id is null and only the device id is set. The returned identity is a
        /// snapshot and does not change after a later identify or <c>Reset()</c>.
        /// </description>
        /// <returns>the current identity</returns>
        public static MixpanelIdentity GetIdentity()
        {
            string userId = MixpanelStorage.UserId;
            if (Config.HierarchicalGroups)
            {
                return new MixpanelIdentity(userId, MixpanelStorage.CompanyId, MixpanelStorage.EnsureDeviceId());
            }
            // Before the first identify the anonymous distinct id is the device id
            string deviceId = MixpanelStorage.DeviceId ?? (userId == null ? MixpanelStorage.DistinctId : null);
            return new MixpanelIdentity(userId, null, deviceId);
        }

        [Obsolete("Please use 'GetIdentity()' instead!")]
        public static string DistinctID {
            get => DistinctId;
        }

        /// <summary>
        /// The distinct id %Mixpanel resolves the current user to.
        /// </summary>
        /// <description>
        /// On a hierarchical groups project %Mixpanel builds the distinct id from the company and the user, so
        /// once both are known this is <c>$company:{companyId}|$user:{userId}</c>, and before that it is
        /// <c>$device:</c> followed by the device id. Events still send the user id as their distinct_id.
        /// </description>
        [Obsolete("Please use 'GetIdentity()' instead! Its user, company and device ids are what every Mixpanel SDK accepts directly.")]
        public static string DistinctId {
            get
            {
                if (!Config.HierarchicalGroups) return MixpanelStorage.DistinctId;
                string userId = MixpanelStorage.UserId;
                string companyId = MixpanelStorage.CompanyId;
                if (userId != null && companyId != null) return "$company:" + companyId + "|$user:" + userId;
                return "$device:" + MixpanelStorage.EnsureDeviceId();
            }
        }

        /// <summary>
        /// Opt out tracking.
        /// </summary>
        public static void OptOutTracking()
        {
            if (!IsInitialized()) return;
            if (!Config.HierarchicalGroups)
            {
                People.DeleteUser();
            }
            else if (MixpanelStorage.UserId != null && MixpanelStorage.CompanyId != null)
            {
                // Deletes the user's profile, never the company's or other group profiles
                People.DeleteUser();
            }
            Flush();
            Reset();
            MixpanelStorage.IsTracking = false;
        }

        /// <summary>
        /// Opt in tracking.
        /// </summary>
        public static void OptInTracking()
        {
            if (!IsInitialized()) return;
            MixpanelStorage.IsTracking = true;
            Controller.DoTrack("$opt_in", null);
        }

        /// <summary>
        /// Opt in tracking.
        /// </summary>
        /// <description>
        /// Not supported on a hierarchical groups project, where it is logged and ignored and the user stays
        /// opted out. Use <c>OptInTrackingWithIdentity(MixpanelIdentity)</c> instead.
        /// </description>
        /// <param name="distinctId">the distinct id for events. Behind the scenes,
        /// <code>Identify</code> will be called by using this distinct id.</param>
        public static void OptInTracking(string distinctId)
        {
            if (!IsInitialized()) return;
            if (Config.HierarchicalGroups)
            {
                LogError("OptInTracking(string) is not supported for hierarchical groups projects, which identify " +
                    "a user by company and user id. Use OptInTrackingWithIdentity(new MixpanelIdentity(userId, " +
                    "companyId)). The user is still opted out.");
                return;
            }
            Identify(distinctId);
            OptInTracking();
        }

        /// <summary>
        /// Opt in tracking and identify the user.
        /// </summary>
        /// <description>
        /// Opting out clears the user's identity, so on a hierarchical groups project use this to opt back in
        /// with the user's company: the <c>$opt_in</c> event and every event after it carry the user and
        /// company, which <c>OptInTracking(string)</c> can't express.
        /// </description>
        /// <param name="identity">the user, and on a hierarchical groups project their company. When not null,
        /// <c>Identify(MixpanelIdentity)</c> is called with it.</param>
        /// <param name="properties">optional properties to add to the <c>$opt_in</c> event</param>
        public static void OptInTrackingWithIdentity(MixpanelIdentity identity, Value properties = null)
        {
            if (!IsInitialized()) return;
            MixpanelStorage.IsTracking = true;
            if (identity != null) Identify(identity);
            Controller.DoTrack("$opt_in", properties);
        }

        /// <summary>
        /// Registers super properties, overwriting ones that have already been set.
        /// </summary>
        /// <param name="key">name of the property to register</param>
        /// <param name="value">value of the property to register</param>
        public static void Register(string key, Value value)
        {
            if (!IsInitialized()) return;
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
            if (!IsInitialized()) return;
            Value properties = MixpanelStorage.OnceProperties;
            if (!properties.ContainsKey(key)) {
                properties[key] = value;
                MixpanelStorage.OnceProperties = properties;
            }
        }

        /// <summary>
        /// Clears all super properties, once properties, timed events from persistent MixpanelStorage.
        /// </summary>
        public static void Reset()
        {
            if (!IsInitialized()) return;
            MixpanelStorage.DeleteAllTrackingData(MixpanelStorage.FlushType.EVENTS);
            MixpanelStorage.DeleteAllTrackingData(MixpanelStorage.FlushType.PEOPLE);
            MixpanelStorage.ResetSuperProperties();
            MixpanelStorage.ResetOnceProperties();
            MixpanelStorage.ResetTimedEvents();
            MixpanelStorage.DeleteAllTrackingData(MixpanelStorage.FlushType.GROUPS);
            Flush();
            MixpanelStorage.ResetIdentity();
            // A hierarchical groups project starts again under a new device id
            MixpanelStorage.DistinctId = Config.HierarchicalGroups ? "$device:" + MixpanelStorage.EnsureDeviceId() : "";
        }

        /// <summary>
        /// Clears all items from the Track and Engage request queues, anything not already sent to the Mixpanel
        /// API will no longer be sent
        /// </summary>
        public static void Clear()
        {
            if (!IsInitialized()) return;
            Controller.DoClear();
        }

        /// <summary>
        /// Clears all super properties
        /// </summary>
        public static void ClearSuperProperties()
        {
            MixpanelStorage.ResetSuperProperties();
        }

        /// <summary>
        /// Start timing of an event. Calling Mixpanel.StartTimedEvent(string eventName) will not send an event,
        /// but when you eventually call Mixpanel.Track(string eventName), your tracked event will be sent with a "$duration" property,
        /// representing the number of seconds between your calls.
        /// </summary>
        /// <param name="eventName">the name of the event to track with timing</param>
        public static void StartTimedEvent(string eventName)
        {
            if (!IsInitialized()) return;
            Value properties = MixpanelStorage.TimedEvents;
            properties[eventName] = Util.CurrentTimeInSeconds();
            MixpanelStorage.TimedEvents = properties;
        }

        /// <summary>
        /// Begin timing of an event, but only if the event has not already been registered as a timed event.
        /// Useful if you want to know the duration from the point in time the event was first registered.
        /// </summary>
        /// <param name="eventName">the name of the event to track with timing</param>
        public static void StartTimedEventOnce(string eventName)
        {
            if (!IsInitialized()) return;
            if (!MixpanelStorage.TimedEvents.ContainsKey(eventName))
            {
                Value properties = MixpanelStorage.TimedEvents;
                properties[eventName] = Util.CurrentTimeInSeconds();
                MixpanelStorage.TimedEvents = properties;
            }
        }

        /// <summary>
        /// Tracks an event.
        /// </summary>
        /// <param name="eventName">the name of the event to send</param>
        public static void Track(string eventName)
        {
            if (!IsInitialized()) return;
            Controller.DoTrack(eventName, null);
        }

        /// <summary>
        /// Tracks an event with properties of key=value.
        /// </summary>
        /// <param name="eventName">the name of the event to send</param>
        /// <param name="key">A Key value for the data</param>
        /// <param name="value">The value to use for the key</param>
        public static void Track(string eventName, string key, Value value)
        {
            if (!IsInitialized()) return;
            Value properties = new Value();
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
        public static void Track(string eventName, Value properties) {
            if (!IsInitialized()) return;
            Controller.DoTrack(eventName, properties);
        }

        /// <summary>
        /// Removes a single superProperty.
        /// </summary>
        /// <param name="key">name of the property to unregister</param>
        public static void Unregister(string key)
        {
            if (!IsInitialized()) return;
            Value properties = MixpanelStorage.SuperProperties;
            properties.Remove(key);
            MixpanelStorage.SuperProperties = properties;
        }

        /// <summary>
        /// Set the group this user belongs to.
        /// </summary>
        /// <description>
        /// <p>The group is registered as a super property and set on the user's profile.</p>
        /// <p>On a hierarchical groups project a user belongs to one group per group key, so the group is stored
        /// as a single value rather than a list, and an array <c>groupId</c> is dropped with an error log.</p>
        /// </description>
        /// <param name="groupKey">the property name associated with this group type (must already have been set up)</param>
        /// <param name="groupId">the group the user belongs to. Outside hierarchical groups, an array sets
        /// every group in it.</param>
        public static void SetGroup(string groupKey, Value groupId)
        {
            if (!IsInitialized()) return;
            if (!MixpanelStorage.IsTracking) return;
            if (groupKey == null || groupId == null)
            {
                LogError("groupKey and groupId must be non-null");
                return;
            }
            if (Config.HierarchicalGroups)
            {
                // Hierarchical groups read a list as the name of one group, not as membership in each
                if (groupId.IsArray)
                {
                    LogSingleGroupOnly("SetGroup with a list", groupKey);
                    return;
                }
                Register(groupKey, groupId);
                People.Set(groupKey, groupId);
                return;
            }
            Value groupIds = groupId.IsArray ? groupId : ArrayOf(groupId);
            Register(groupKey, groupIds);
            People.Set(groupKey, groupIds);
        }

        /// <summary>
        /// Add a group to this user's membership for a particular group key.
        /// </summary>
        /// <description>
        /// A hierarchical groups project allows one group per group key, so this is dropped with an error log
        /// there. Use <c>SetGroup(groupKey, groupId)</c> instead.
        /// </description>
        /// <param name="groupKey">the property name associated with this group type (must already have been set up)</param>
        /// <param name="groupId">the new group the user belongs to</param>
        public static void AddGroup(string groupKey, Value groupId)
        {
            if (!IsInitialized()) return;
            if (!MixpanelStorage.IsTracking) return;
            if (Config.HierarchicalGroups)
            {
                LogSingleGroupOnly("AddGroup", groupKey);
                return;
            }
            if (groupKey == null || groupId == null)
            {
                LogError("groupKey and groupId must be non-null");
                return;
            }
            Value properties = MixpanelStorage.SuperProperties;
            Value existing;
            if (properties.TryGetValue(groupKey, out existing) && existing.IsArray)
            {
                if (!ContainsGroup(existing, groupId)) existing.Add(groupId);
            }
            else if (existing != null && !existing.IsNull)
            {
                properties[groupKey] = ArrayOf(existing, groupId);
            }
            else
            {
                properties[groupKey] = ArrayOf(groupId);
            }
            MixpanelStorage.SuperProperties = properties;
            // This is a best effort--if the people property is not already a list, this call does nothing.
            People.Union(groupKey, ArrayOf(groupId));
        }

        /// <summary>
        /// Remove a group from this user's membership for a particular group key.
        /// </summary>
        /// <description>
        /// On a hierarchical groups project this clears the group key when the user belongs to <c>groupId</c>,
        /// and does nothing otherwise.
        /// </description>
        /// <param name="groupKey">the property name associated with this group type (must already have been set up)</param>
        /// <param name="groupId">the group to remove</param>
        public static void RemoveGroup(string groupKey, Value groupId)
        {
            if (!IsInitialized()) return;
            if (!MixpanelStorage.IsTracking) return;
            if (groupKey == null || groupId == null)
            {
                LogError("groupKey and groupId must be non-null");
                return;
            }
            Value properties = MixpanelStorage.SuperProperties;
            Value existing;
            properties.TryGetValue(groupKey, out existing);
            if (Config.HierarchicalGroups)
            {
                if (existing == null || !SameGroup(SingleGroup(existing), groupId)) return;
                properties.Remove(groupKey);
                MixpanelStorage.SuperProperties = properties;
                People.Unset(groupKey);
                return;
            }
            if (existing == null || !existing.IsArray || existing.Count <= 1)
            {
                properties.Remove(groupKey);
                MixpanelStorage.SuperProperties = properties;
                // This is a best effort--we can't guarantee people and super properties match
                People.Unset(groupKey);
                return;
            }
            Value remaining = Value.Array;
            foreach (Value group in existing.Values)
            {
                if (!SameGroup(group, groupId)) remaining.Add(group);
            }
            properties[groupKey] = remaining;
            MixpanelStorage.SuperProperties = properties;
            // This is a best effort--if the people property is not a list, this call does nothing.
            People.Remove(groupKey, groupId);
        }

        /// <summary>
        /// Returns a group profile to update.
        /// </summary>
        /// <description>
        /// On a hierarchical groups project every update carries the current company, and is dropped with an
        /// error log until a company is identified.
        /// </description>
        /// <param name="groupKey">the property name associated with this group type (must already have been set up)</param>
        /// <param name="groupId">the group's id</param>
        /// <returns>the group, never null</returns>
        public static Group GetGroup(string groupKey, Value groupId)
        {
            if (groupKey == null || groupId == null)
            {
                return Group.Unavailable("GetGroup() needs a non-null groupKey and groupId.");
            }
            return new Group(groupKey, groupId);
        }

        /// <summary>
        /// Returns the identified user's company profile on a hierarchical groups project, as a group to set
        /// properties on. This is <c>GetGroup("$company_id", companyId)</c> with the company from the last
        /// <c>Identify(MixpanelIdentity)</c>.
        /// </summary>
        /// <description>
        /// <p>Only for projects that use hierarchical groups. On any other project, or before a company is
        /// identified, this logs an error and returns null.</p>
        /// <p>The company is fixed when this is called, so a group kept across <c>Reset()</c> or a new identify
        /// still updates the earlier company.</p>
        /// </description>
        /// <code>
        ///        Mixpanel.Identify(new MixpanelIdentity("user_123", "acme"));<br/>
        ///        Mixpanel.GetCompany()?.Set("tier", "enterprise");<br/>
        /// </code>
        /// <returns>the identified company's group, or null when there is no company</returns>
        public static Group GetCompany()
        {
            if (!Config.HierarchicalGroups)
            {
                LogError("GetCompany() is only for hierarchical groups projects, enabled with " +
                    "HierarchicalGroups in the Mixpanel project settings.");
                return null;
            }
            string companyId = MixpanelStorage.CompanyId;
            if (companyId == null)
            {
                LogError("GetCompany() needs a company. Call Identify(new MixpanelIdentity(userId, " +
                    "companyId)) first.");
                return null;
            }
            return GetGroup(CompanyIdGroupKey, companyId);
        }

        private const string CompanyIdGroupKey = "$company_id";

        private static void LogSingleGroupOnly(string operation, string groupKey)
        {
            LogError(operation + " is not supported for hierarchical groups projects, which allow one " + groupKey +
                " per user. Use SetGroup(groupKey, groupId). The update was dropped.");
        }

        private static Value ArrayOf(params Value[] values)
        {
            Value array = Value.Array;
            foreach (Value value in values) array.Add(value);
            return array;
        }

        // A group key's value as one group. A one-item list, stored by an earlier version or before
        // hierarchical groups was enabled, counts as that item.
        private static Value SingleGroup(Value value)
        {
            return value.IsArray && value.Count == 1 ? value[0] : value;
        }

        private static bool ContainsGroup(Value groups, Value groupId)
        {
            foreach (Value group in groups.Values)
            {
                if (SameGroup(group, groupId)) return true;
            }
            return false;
        }

        // Compares as JSON, so the string "1" and the number 1 are different groups
        private static bool SameGroup(Value a, Value b)
        {
            return ArrayOf(a).ToString() == ArrayOf(b).ToString();
        }

        /// <summary>
        /// Flushes the queued data to Mixpanel
        /// </summary>
        /// <param name="onFlushComplete">callback to be called when the flush is complete. Returns true if overall success</param>
        public static void Flush(Action<bool> onFlushComplete = null)
        {
            if (!IsInitialized()) return;
            Controller.GetInstance().DoFlush(onFlushComplete);
        }

        /// <summary>
        /// Sets the project token to be used. This setting will override what it is set in the Unity Project Settings.
        /// </summary>
        public static void SetToken(string token)
        {
            if (!IsInitialized()) return;
            MixpanelSettings.Instance.DebugToken = token;
            MixpanelSettings.Instance.RuntimeToken = token;
        }

        /// <summary>
        /// Disables Mixpanel Component. Useful if you have "Manual Initialization" enabled under your Project Settings.
        /// </summary>
        public static void Disable()
        {
            if (!IsInitialized()) return;
            Controller.Disable();
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
                if (!IsInitialized()) return;
                RecordUpdate("$append", properties);
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
            /// <description>
            /// Not supported on a hierarchical groups project, which can't TrackCharge, so it is logged and ignored there.
            /// </description>
            public static void ClearCharges()
            {
                // TrackCharge has no hierarchical groups equivalent, so there are no charges to clear
                if (Config.HierarchicalGroups)
                {
                    LogError("ClearCharges is not supported for hierarchical groups projects, which can't " +
                        "TrackCharge. The update was dropped.");
                    return;
                }
                Unset("$transactions");
            }

            /// <summary>
            /// Permanently delete the identified people profile
            /// </summary>
            public static void DeleteUser()
            {
                if (!IsInitialized()) return;
                RecordUpdate("$delete", "");
            }

            /// <summary>
            /// Change the existing values of multiple %People Analytics properties at once.
            /// </summary>
            /// <param name="properties"> A map of String properties names to Long amounts. Each property associated with a name in the map </param>
            public static void Increment(Value properties)
            {
                if (!IsInitialized()) return;
                RecordUpdate("$add", properties);
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
                if (!IsInitialized()) return;
                properties.Merge(Controller.GetEngageDefaultProperties());
                RecordUpdate("$set", properties);
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
                if (!IsInitialized()) return;
                RecordUpdate("$set_once", properties);
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
            /// <param name="properties">a JSONObject containing the collection of properties you wish to apply</param>
            public static void TrackCharge(double amount, Value properties)
            {
                properties["$amount"] = amount;
                TrackCharge(properties);
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
                if (!IsInitialized()) return;
                properties["$time"] = Util.CurrentDateTime();
                RecordUpdate("$append", new Value {{"$transactions", properties}});
            }

            /// <summary>
            /// Adds values to a list-valued property only if they are not already present in the list.
            /// If the property does not currently exist, it will be created with the given list as it's value.
            /// If the property exists and is not list-valued, the union will be ignored.
            /// </summary>
            /// <param name="properties">mapping of list property names to lists to union</param>
            public static void Union(Value properties)
            {
                if (!IsInitialized()) return;
                RecordUpdate("$union", properties);
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
            /// Removes a value from a list-valued property. If the property does not exist or is not
            /// list-valued, the remove will be ignored.
            /// </summary>
            /// <param name="property">name of the list-valued property</param>
            /// <param name="value">the value to remove from the list</param>
            public static void Remove(string property, Value value)
            {
                if (!IsInitialized()) return;
                RecordUpdate("$remove", new Value {{property, value}});
            }

            /// <summary>
            /// Takes a string property name, and permanently removes the property and their values from a profile.
            /// </summary>
            /// <param name="property">property</param>
            public static void Unset(string property)
            {
                if (!IsInitialized()) return;
                RecordUpdate("$unset", new string[]{property});
            }

            // /groups has no $add, $append or $merge
            private static readonly string[] GroupOperations =
                {"$set", "$set_once", "$unset", "$union", "$remove", "$delete"};

            // Sends a profile update to /engage, or on a hierarchical groups project to /groups as the
            // $user_id group profile within the company
            private static void RecordUpdate(string actionType, Value properties)
            {
                if (!Config.HierarchicalGroups)
                {
                    Controller.DoEngage(new Value {{actionType, properties}});
                    return;
                }
                if (!MixpanelStorage.IsTracking) return;
                if (Array.IndexOf(GroupOperations, actionType) < 0)
                {
                    LogError(actionType + " is not supported for hierarchical groups projects, whose user " +
                        "profiles are group profiles keyed by $user_id. Supported operations are [" +
                        string.Join(", ", GroupOperations) + "], so increment, append, merge and trackCharge have " +
                        "no hierarchical groups equivalent. The update was dropped.");
                    return;
                }
                string userId = MixpanelStorage.UserId;
                string companyId = MixpanelStorage.CompanyId;
                if (userId == null || companyId == null)
                {
                    LogError("User profile updates for hierarchical groups projects need a user and a company. " +
                        "Call Identify(new MixpanelIdentity(userId, companyId)) first. The " + actionType +
                        " update was dropped.");
                    return;
                }
                Controller.DoGroup(UserIdGroupKey, userId, companyId, actionType, properties);
            }

            private const string UserIdGroupKey = "$user_id";

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

        }
    }
}
