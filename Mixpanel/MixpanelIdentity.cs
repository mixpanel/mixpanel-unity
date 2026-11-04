using System;
using System.Collections.Generic;

namespace mixpanel
{
    /// <summary>
    /// Names who events and profile updates belong to: a user, their company on a hierarchical groups
    /// project, and the device.
    /// </summary>
    /// <description>
    /// <p>%Mixpanel owns the device id, so <c>Mixpanel.GetIdentity()</c> reports a complete identity and
    /// <c>Mixpanel.Identify(MixpanelIdentity)</c> takes only the user id and company id.</p>
    /// <p>On a project that uses hierarchical groups, %Mixpanel builds the user's distinct id from
    /// <c>$company_id</c> and <c>$user_id</c>, so the company is part of the user's identity: the same user id
    /// in two companies is two users. Other projects ignore the company id.</p>
    /// <p>Every value may be null. Instances are immutable and compare by value.</p>
    /// </description>
    /// <code>
    ///        Mixpanel.Identify(new MixpanelIdentity("user_123", "acme"));<br/>
    /// </code>
    public sealed class MixpanelIdentity : IEquatable<MixpanelIdentity>
    {
        /// <summary>
        /// The user's id. On a hierarchical groups project it only needs to be unique within the company.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// The id of the user's top-level company, on a hierarchical groups project.
        /// </summary>
        public string CompanyId { get; }

        /// <summary>
        /// The device id %Mixpanel assigned. Events that carry it are attributed to the device until the
        /// user is known, then follow the user.
        /// </summary>
        public string DeviceId { get; }

        /// <summary>
        /// Creates an identity.
        /// </summary>
        /// <param name="userId">the user's id</param>
        /// <param name="companyId">the id of the user's company, on a hierarchical groups project</param>
        /// <param name="deviceId">the device id. Ignored by <c>Mixpanel.Identify(MixpanelIdentity)</c>,
        /// since %Mixpanel owns the device id.</param>
        public MixpanelIdentity(string userId, string companyId = null, string deviceId = null)
        {
            UserId = userId;
            CompanyId = companyId;
            DeviceId = deviceId;
        }

        public bool Equals(MixpanelIdentity other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            return UserId == other.UserId && CompanyId == other.CompanyId && DeviceId == other.DeviceId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MixpanelIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = UserId != null ? UserId.GetHashCode() : 0;
                result = 31 * result + (CompanyId != null ? CompanyId.GetHashCode() : 0);
                result = 31 * result + (DeviceId != null ? DeviceId.GetHashCode() : 0);
                return result;
            }
        }

        public static bool operator ==(MixpanelIdentity left, MixpanelIdentity right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.Equals(right);
        }

        public static bool operator !=(MixpanelIdentity left, MixpanelIdentity right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Formats as <c>MixpanelIdentity(userId=u, companyId=c, deviceId=d)</c>, omitting null values.
        /// </summary>
        public override string ToString()
        {
            List<string> parts = new List<string>(3);
            if (UserId != null) parts.Add("userId=" + UserId);
            if (CompanyId != null) parts.Add("companyId=" + CompanyId);
            if (DeviceId != null) parts.Add("deviceId=" + DeviceId);
            return "MixpanelIdentity(" + string.Join(", ", parts) + ")";
        }
    }
}
