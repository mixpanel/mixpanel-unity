using System;
using UnityEngine;

namespace mixpanel
{
    public static partial class Mixpanel
    {
        /// <summary>
        /// Logs general Mixpanel debug information when Config.ShowDebug is true.
        /// </summary>
        /// <param name="s"></param>
        public static void Log(string s)
        {
            if (Config.ShowDebug)
            {
                Debug.Log("[Mixpanel] " + s);
            }
        }

        /// <summary>
        /// Logs an error when Config.ShowDebug is true.
        /// </summary>
        /// <param name="errorMessage"></param>
        public static void LogError(string errorMessage)
        {
            if (Config.ShowDebug)
            {
                Debug.LogError("[Mixpanel] " + errorMessage);
            }
        }

        /// <summary>
        /// Logs an error message when Config.ShowDebug is true. Includes a verbose warning for breadcrumbs.
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <param name="verbose"></param>
        /// <param name="e"></param>
        public static void LogError(string errorMessage, string verbose, Exception e = null)
        {
            if (Config.ShowDebug)
            {
                Debug.LogWarning("[Mixpanel] " + verbose);
                Debug.LogError("[Mixpanel] " + errorMessage);
            }
        }
    }
}