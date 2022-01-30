using UnityEngine;

namespace mixpanel
{
    public class PlayerPreferences : IPreferences
    {
        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }
        
        public float GetFloat(string key)
        {
            return PlayerPrefs.GetFloat(key);
        }

        public int GetInt(string key)
        {
            return PlayerPrefs.GetInt(key);
        }

        public int GetInt(string key, int defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }

        public string GetString(string key)
        {
            return PlayerPrefs.GetString(key);
        }

        public string GetString(string key, string defaultValue)
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }
    }
}