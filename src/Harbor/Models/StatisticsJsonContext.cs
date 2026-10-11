using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harbor.Models;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(TaskStatistics))]
public partial class StatisticsJsonContext : JsonSerializerContext;
