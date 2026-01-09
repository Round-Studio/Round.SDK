using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Round.SDK.Global;

namespace Round.SDK.Entity;

public class ConfigEntity<T> where T : new()
{
    public T Data { get; set; }
    public string Path { get; private set; }
    private JsonTypeInfo<T>? TypeInfo;

    public ConfigEntity(string configFile, JsonTypeInfo<T>? typeInfo = default)
    {
        Path = configFile;
        TypeInfo = typeInfo;
        Load();
    }

    public void Load()
    {
        if (!File.Exists(Path))
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            Save();
            return;
        }

        var json = File.ReadAllText(Path);
        if (string.IsNullOrEmpty(json))
        {
            Save();
        }
        else
        {
            try
            {
                Data = TypeInfo != null
                    ? JsonSerializer.Deserialize<T>(json, TypeInfo)
                    : JsonSerializer.Deserialize<T>(json, JsonSerializerOption.Options);
            }
            catch
            {
                Save();
            }
        }
    }

    public void Save()
    {
        Console.WriteLine($"触发保存配置项：{Path}");
        if (Data == null)
        {
            Data = new T(); // 现在这里可以正常工作了
        }

        string jsresult = TypeInfo != null
            ? JsonSerializer.Serialize(Data, TypeInfo)
            : JsonSerializer.Serialize(Data, JsonSerializerOption.Options);
        File.WriteAllText(Path, jsresult);
    }
}