namespace mixpanel
{
    public static partial class Mixpanel
    {
        /// <summary>
        /// A group profile to update, from <c>Mixpanel.GetGroup(groupKey, groupId)</c> or <c>Mixpanel.GetCompany()</c>.
        /// </summary>
        /// <description>
        /// Updates are sent to <c>/groups</c>. On a hierarchical groups project each one carries the current
        /// <c>$company_id</c>, and is dropped with an error log until a company is identified.
        /// </description>
        /// <code>
        ///        Mixpanel.GetGroup("team_id", "finance").Set("size", 12);<br/>
        /// </code>
        public sealed class Group
        {
            private readonly string _groupKey;
            private readonly Value _groupId;
            // Set on a group with nothing to update, whose every update is dropped with this reason
            private readonly string _unavailableReason;

            internal Group(string groupKey, Value groupId)
            {
                _groupKey = groupKey;
                _groupId = groupId;
            }

            private Group(string unavailableReason)
            {
                _unavailableReason = unavailableReason;
            }

            internal static Group Unavailable(string reason)
            {
                return new Group(reason);
            }

            /// <summary>
            /// Set a collection of properties on the group all at once.
            /// </summary>
            /// <param name="properties">the properties to set</param>
            public void Set(Value properties)
            {
                Record("$set", properties);
            }

            /// <summary>
            /// Sets a single property with the given name and value for this group.
            /// </summary>
            /// <param name="property">property name</param>
            /// <param name="to">property value</param>
            public void Set(string property, Value to)
            {
                Set(new Value {{property, to}});
            }

            /// <summary>
            /// Like Set(Value properties), but will not set properties that already exist on the group.
            /// </summary>
            /// <param name="properties">the properties to set</param>
            public void SetOnce(Value properties)
            {
                Record("$set_once", properties);
            }

            /// <summary>
            /// Like Set(string property, Value to), but will not set a property that already exists on the group.
            /// </summary>
            /// <param name="property">property name</param>
            /// <param name="to">property value</param>
            public void SetOnce(string property, Value to)
            {
                SetOnce(new Value {{property, to}});
            }

            /// <summary>
            /// Adds values to a list-valued property only if they are not already present in the list.
            /// </summary>
            /// <param name="property">name of the list-valued property to set or modify</param>
            /// <param name="values">an array of values to add to the property value if not already present</param>
            public void Union(string property, Value values)
            {
                Record("$union", new Value {{property, values}});
            }

            /// <summary>
            /// Removes a value from a list-valued property.
            /// </summary>
            /// <param name="property">name of the list-valued property</param>
            /// <param name="value">the value to remove from the list</param>
            public void Remove(string property, Value value)
            {
                Record("$remove", new Value {{property, value}});
            }

            /// <summary>
            /// Permanently removes a property and its value from the group.
            /// </summary>
            /// <param name="property">property name</param>
            public void Unset(string property)
            {
                Record("$unset", new string[] {property});
            }

            /// <summary>
            /// Permanently deletes the group profile.
            /// </summary>
            public void DeleteGroup()
            {
                Record("$delete", "");
            }

            private void Record(string actionType, Value properties)
            {
                if (!IsInitialized()) return;
                if (_unavailableReason != null)
                {
                    LogError(_unavailableReason + " The update was dropped.");
                    return;
                }
                string companyId = Config.HierarchicalGroups ? MixpanelStorage.CompanyId : null;
                Controller.DoGroup(_groupKey, _groupId, companyId, actionType, properties);
            }
        }
    }
}
