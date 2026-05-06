namespace AJut.Bench.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    // STJ source-generator context for the source-gen variant of the System.Text.Json benchmarks.
    // The reflection-mode STJ benchmarks use JsonSerializer.Serialize/Deserialize directly and do
    // not go through this context.
    [JsonSerializable(typeof(TinyMessage))]
    [JsonSerializable(typeof(DockZoneLayout))]
    [JsonSerializable(typeof(List<DockZoneLayout>))]
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    internal partial class BenchSerializerContext : JsonSerializerContext
    {
    }
}
