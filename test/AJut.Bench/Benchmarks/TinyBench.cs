namespace AJut.Bench.Benchmarks
{
    using AJut.Bench.Models;
    using AJut.Text.AJson.Legacy;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using SystemTextJson = System.Text.Json.JsonSerializer;
    using NewtonsoftJson = Newtonsoft.Json.JsonConvert;

    // Wire-message-shaped object, ~10 properties, primitives only. Models the Call Familiar
    // high-volume case.
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class TinyBench
    {
        private TinyMessage m_model;
        private string m_json;

        [GlobalSetup]
        public void Setup ()
        {
            m_model = PayloadFactory.BuildTiny();
            // Canonical JSON for deserialize benches uses STJ's strict spec-compliant output so
            //  all four readers see the same input. Anything looser would unfairly tilt toward
            //  the lenient parsers.
            m_json = SystemTextJson.Serialize(m_model);
        }

        // ===========[ Serialize ]===================================
        [BenchmarkCategory("Serialize"), Benchmark(Baseline = true, Description = "STJ Reflection")]
        public string Serialize_StjReflection () => SystemTextJson.Serialize(m_model);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "STJ SourceGen")]
        public string Serialize_StjSourceGen () => SystemTextJson.Serialize(m_model, BenchSerializerContext.Default.TinyMessage);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "Newtonsoft")]
        public string Serialize_Newtonsoft () => NewtonsoftJson.SerializeObject(m_model);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "AJson V1")]
        public string Serialize_AJsonV1 () => JsonHelper.BuildJsonForObject(m_model).ToString();

        // ===========[ Deserialize ]===================================
        [BenchmarkCategory("Deserialize"), Benchmark(Baseline = true, Description = "STJ Reflection")]
        public TinyMessage Deserialize_StjReflection () => SystemTextJson.Deserialize<TinyMessage>(m_json);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "STJ SourceGen")]
        public TinyMessage Deserialize_StjSourceGen () => SystemTextJson.Deserialize(m_json, BenchSerializerContext.Default.TinyMessage);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "Newtonsoft")]
        public TinyMessage Deserialize_Newtonsoft () => NewtonsoftJson.DeserializeObject<TinyMessage>(m_json);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "AJson V1")]
        public TinyMessage Deserialize_AJsonV1 () => JsonHelper.BuildObjectForJson<TinyMessage>(JsonHelper.ParseText(m_json));
    }
}
