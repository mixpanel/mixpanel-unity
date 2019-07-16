using System.Runtime.InteropServices;

namespace mixpanel
{
    public partial class Mixpanel
    {
#if UNITY_IOS
        [DLLImport("__Internal")]
        private static extern string GetCarrier();
#endif
        
#if UNITY_ANDROID

#endif
    }
}