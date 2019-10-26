using UnityEngine;

namespace mixpanel.platforms
{
	public class Android
	{
        public static string GetBrand() {
            AndroidJavaClass osBuildClass = new AndroidJavaClass("android.os.Build");
            string brand = osBuildClass.GetStatic<string> ("BRAND");
            return brand != null ? brand : "UNKNOWN";
        }

        public static string GetManufacturer() {
            AndroidJavaClass osBuildClass = new AndroidJavaClass("android.os.Build");
            string manufacturer = null;
            if (osBuildClass != null) {
                manufacturer = osBuildClass.GetStatic<string> ("MANUFACTURER");
            }
            return manufacturer != null ? manufacturer : "UNKNOWN";
        }

        public static int GetVersionCode() {
            AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var ca = up.GetStatic<AndroidJavaObject>("currentActivity");
            int versionCode = -1;
            if (ca != null) {
                AndroidJavaObject packageManager = ca.Call<AndroidJavaObject>("getPackageManager");
                var pInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 0);
                versionCode = pInfo.Get<int>("versionCode");
            }
            return versionCode;
        }
    }
}