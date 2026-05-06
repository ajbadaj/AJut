namespace AJut.Bench
{
    using System;
    using System.IO;
    using AJut.Bench.Benchmarks;
    using AJut.Bench.Models;
    using BenchmarkDotNet.Running;
    using SystemTextJson = System.Text.Json.JsonSerializer;

    // Entry point for the AJut benchmark project.
    //
    // Run all benchmarks (Release config required by BDN):
    //   dotnet run -c Release
    //
    // Run a single bench class:
    //   dotnet run -c Release -- --filter *TinyBench*
    //
    // Regenerate the committed sample payload .json files (writes to ./Payloads/):
    //   dotnet run -c Release -- --gen-payloads
    public static class Program
    {
        public static void Main (string[] args)
        {
            if (args.Length > 0 && args[0] == "--gen-payloads")
            {
                GenerateSamplePayloads();
                return;
            }

            BenchmarkSwitcher.FromTypes(new[]
            {
                typeof(TinyBench),
                typeof(MediumBench),
                typeof(LargeBench),
            }).Run(args);
        }

        // Dump deterministic sample payloads to disk so the JSON we're benching against can be
        //  inspected, diffed, and committed alongside the project. Each model serializes via
        //  STJ (the canonical strict-JSON path) for the file content.
        private static void GenerateSamplePayloads ()
        {
            string outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Payloads");
            outputDir = Path.GetFullPath(outputDir);
            Directory.CreateDirectory(outputDir);

            var indented = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };

            File.WriteAllText(
                Path.Combine(outputDir, "tiny.json"),
                SystemTextJson.Serialize(PayloadFactory.BuildTiny(), indented)
            );
            File.WriteAllText(
                Path.Combine(outputDir, "medium.json"),
                SystemTextJson.Serialize(PayloadFactory.BuildMedium(), indented)
            );
            File.WriteAllText(
                Path.Combine(outputDir, "large.json"),
                SystemTextJson.Serialize(PayloadFactory.BuildLarge(), indented)
            );

            Console.WriteLine("Wrote sample payloads to: " + outputDir);
        }
    }
}
