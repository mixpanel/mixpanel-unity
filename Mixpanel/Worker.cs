using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Web;

namespace mixpanel
{

    internal static class Util
    {
        internal static double CurrentTimeInSeconds()
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double currentEpochTime = (DateTime.UtcNow - epochStart).TotalSeconds;
            return currentEpochTime;
        }

        internal static double CurrentTimeInMilliseconds()
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long currentEpochTime = (long)(DateTime.UtcNow - epochStart).TotalMilliseconds;
            return currentEpochTime;
        }

        internal static string CurrentDateTime()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        }

        internal static string GetRadio()
        {
            switch(Application.internetReachability)
            {
                case NetworkReachability.NotReachable :
                    return "none";
                case NetworkReachability.ReachableViaCarrierDataNetwork :
                    return "carrier";
                case NetworkReachability.ReachableViaLocalAreaNetwork :
                    return "wifi";
            }
            return "none";
        }
    }
}
