using System.Text.Json.Serialization;

namespace PluginTools.Entry.JsonContext;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ConfigFileEntry))]
[JsonSerializable(typeof(PackConfig))]
public partial class JsonContextGenerate : JsonSerializerContext
{
}