namespace AJut.Bench.Benchmarks
{
    using AJut.Bench.Models;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using SystemTextJson = System.Text.Json.JsonSerializer;
    using NewtonsoftJson = Newtonsoft.Json.JsonConvert;
    using AJsonV1 = AJut.Text.AJson.Legacy.JsonHelper;
    using AJsonV2 = AJut.Text.AJson.JsonHelper;

    // Wire-message-shaped object, ~10 properties, primitives only. Models the Call Familiar
    // high-volume case.
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class TinyBench
    {
        private TinyMessage m_model;
        private TinyMessageReflection m_modelReflection;
        private string m_json;

        [GlobalSetup]
        public void Setup ()
        {
            m_model = PayloadFactory.BuildTiny();
            m_modelReflection = PayloadFactory.BuildTinyReflection();
            // Canonical JSON for deserialize benches uses STJ's strict spec-compliant output so
            //  all readers see the same input. Anything looser would unfairly tilt toward the
            //  lenient parsers.
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
        public string Serialize_AJsonV1 () => AJsonV1.BuildJsonForObject(m_model).ToString();

        [BenchmarkCategory("Serialize"), Benchmark(Description = "AJson V2 (reflection)")]
        public string Serialize_AJsonV2_Reflection () => AJsonV2.BuildJsonForObject(m_modelReflection).ToString();

        [BenchmarkCategory("Serialize"), Benchmark(Description = "AJson V2 (source-gen)")]
        public string Serialize_AJsonV2_SourceGen () => AJsonV2.BuildJsonForObject(m_model).ToString();

        // ===========[ Deserialize ]===================================
        [BenchmarkCategory("Deserialize"), Benchmark(Baseline = true, Description = "STJ Reflection")]
        public TinyMessage Deserialize_StjReflection () => SystemTextJson.Deserialize<TinyMessage>(m_json);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "STJ SourceGen")]
        public TinyMessage Deserialize_StjSourceGen () => SystemTextJson.Deserialize(m_json, BenchSerializerContext.Default.TinyMessage);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "Newtonsoft")]
        public TinyMessage Deserialize_Newtonsoft () => NewtonsoftJson.DeserializeObject<TinyMessage>(m_json);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "AJson V1")]
        public TinyMessage Deserialize_AJsonV1 () => AJsonV1.BuildObjectForJson<TinyMessage>(AJsonV1.ParseText(m_json));

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "AJson V2 (reflection)")]
        public TinyMessageReflection Deserialize_AJsonV2_Reflection () => AJsonV2.BuildObjectForJson<TinyMessageReflection>(AJsonV2.ParseText(m_json));

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "AJson V2 (source-gen)")]
        public TinyMessage Deserialize_AJsonV2_SourceGen () => AJsonV2.BuildObjectForJson<TinyMessage>(AJsonV2.ParseText(m_json));
    }
}
