using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PluginTools.Entry.JsonContext
{
	[JsonSourceGenerationOptions(
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
	[JsonSerializable(typeof(ConfigFileEntry))]
	[JsonSerializable(typeof(PackConfig))]
	public partial class JsonContextGenerate : JsonSerializerContext
	{
	}
}
