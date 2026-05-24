namespace LeiTing.Storage
{
    public interface IGameStorage
    {
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);
        float GetFloat(string key, float defaultValue = 0f);
        void SetFloat(string key, float value);
        string GetString(string key, string defaultValue = "");
        void SetString(string key, string value);
        bool HasKey(string key);
        void DeleteKey(string key);
        void DeleteAll();
        void Save();
    }
}
