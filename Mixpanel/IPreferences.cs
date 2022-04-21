
namespace mixpanel
{
    public interface IPreferences
    {
        void DeleteKey(string key);
        int GetInt(string key);
        int GetInt(string key, int defaultValue);
        string GetString(string key);
        string GetString(string key, string defaultValue);
        bool HasKey(string key);
        void SetInt(string key, int value);
        void SetString(string key, string value);
    }
}