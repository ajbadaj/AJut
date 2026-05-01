namespace AJut.Bench.Benchmarks
{
    using AJut.Bench.Models;
    using AJut.Text.AJson.Legacy;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using SystemTextJson = System.Text.Json.JsonSerializer;
    using NewtonsoftJson = Newtonsoft.Json.JsonConvert;

    // Layout-serialization-shaped object, nested 3-4 deep, mixed primitives + arrays + a
    // dictionary. Models the AJut docking layout case.
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class MediumBench
    {
        private DockZoneLayout m_model;
        private string m_json;

        [GlobalSetup]
        public void Setup ()
        {
            m_model = PayloadFactory.BuildMedium();
            m_json = SystemTextJson.Serialize(m_model);
        }

        // ===========[ Serialize ]===================================
        [BenchmarkCategory("Serialize"), Benchmark(Baseline = true, Description = "STJ Reflection")]
        public string Serialize_StjReflection () => SystemTextJson.Serialize(m_model);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "STJ SourceGen")]
        public string Serialize_StjSourceGen () => SystemTextJson.Serialize(m_model, BenchSerializerContext.Default.DockZoneLayout);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "Newtonsoft")]
        public string Serialize_Newtonsoft () => NewtonsoftJson.SerializeObject(m_model);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "AJson V1")]
        public string Serialize_AJsonV1 () => JsonHelper.BuildJsonForObject(m_model).ToString();

        // ===========[ Deserialize ]===================================
        [BenchmarkCategory("Deserialize"), Benchmark(Baseline = true, Description = "STJ Reflection")]
        public DockZoneLayout Deserialize_StjReflection () => SystemTextJson.Deserialize<DockZoneLayout>(m_json);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "STJ SourceGen")]
        public DockZoneLayout Deserialize_StjSourceGen () => SystemTextJson.Deserialize(m_json, BenchSerializerContext.Default.DockZoneLayout);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "Newtonsoft")]
        public DockZoneLayout Deserialize_Newtonsoft () => NewtonsoftJson.DeserializeObject<DockZoneLayout>(m_json);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "AJson V1")]
        public DockZoneLayout Deserialize_AJsonV1 () => JsonHelper.BuildObjectForJson<DockZoneLayout>(JsonHelper.ParseText(m_json));
    }
}
