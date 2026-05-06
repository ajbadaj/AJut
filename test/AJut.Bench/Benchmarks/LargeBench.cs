namespace AJut.Bench.Benchmarks
{
    using System.Collections.Generic;
    using AJut.Bench.Models;
    using AJut.Text.AJson.Legacy;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;
    using SystemTextJson = System.Text.Json.JsonSerializer;
    using NewtonsoftJson = Newtonsoft.Json.JsonConvert;

    // List of ~1000 medium-shaped layouts. Models the worst case AJson sees today.
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class LargeBench
    {
        private List<DockZoneLayout> m_model;
        private string m_json;

        [GlobalSetup]
        public void Setup ()
        {
            m_model = PayloadFactory.BuildLarge();
            m_json = SystemTextJson.Serialize(m_model);
        }

        // ===========[ Serialize ]===================================
        [BenchmarkCategory("Serialize"), Benchmark(Baseline = true, Description = "STJ Reflection")]
        public string Serialize_StjReflection () => SystemTextJson.Serialize(m_model);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "STJ SourceGen")]
        public string Serialize_StjSourceGen () => SystemTextJson.Serialize(m_model, BenchSerializerContext.Default.ListDockZoneLayout);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "Newtonsoft")]
        public string Serialize_Newtonsoft () => NewtonsoftJson.SerializeObject(m_model);

        [BenchmarkCategory("Serialize"), Benchmark(Description = "AJson V1")]
        public string Serialize_AJsonV1 () => JsonHelper.BuildJsonForObject(m_model).ToString();

        // ===========[ Deserialize ]===================================
        [BenchmarkCategory("Deserialize"), Benchmark(Baseline = true, Description = "STJ Reflection")]
        public List<DockZoneLayout> Deserialize_StjReflection () => SystemTextJson.Deserialize<List<DockZoneLayout>>(m_json);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "STJ SourceGen")]
        public List<DockZoneLayout> Deserialize_StjSourceGen () => SystemTextJson.Deserialize(m_json, BenchSerializerContext.Default.ListDockZoneLayout);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "Newtonsoft")]
        public List<DockZoneLayout> Deserialize_Newtonsoft () => NewtonsoftJson.DeserializeObject<List<DockZoneLayout>>(m_json);

        [BenchmarkCategory("Deserialize"), Benchmark(Description = "AJson V1")]
        public List<DockZoneLayout> Deserialize_AJsonV1 () => JsonHelper.BuildObjectForJson<List<DockZoneLayout>>(JsonHelper.ParseText(m_json));
    }
}
