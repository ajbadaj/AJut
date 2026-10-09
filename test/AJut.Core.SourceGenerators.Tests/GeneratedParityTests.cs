namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The generated serializers have to write and read exactly what the reflection path does, since a consumer can opt a type
    /// in or out at any time and has data on disk and on the wire from both. Each test compiles one fixture twice, once with the
    /// generator and once without, fills both instances the same way, and compares the json they write and what they read back.
    /// </summary>
    [TestClass]
    public class GeneratedParityTests
    {
        private const string kTypeIdKey = "__type";

        private const string kFixture = @"
using System;
using System.Collections.Generic;
using AJut.Text.AJson;
namespace ParityNs
{
    public enum eMood { Calm, Busy, Loud }

    public class Inner
    {
        public string Tag { get; set; }
        public int Weight { get; set; }
    }

    [OptimizeAJson]
    public class Everything
    {
        public int Count { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public double Ratio { get; set; }
        public eMood Mood { get; set; }
        public int? MaybeSet { get; set; }
        public int? MaybeNull { get; set; }
        public eMood? MaybeMood { get; set; }
        public DateTime When { get; set; }
        public Guid Id { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, int> Scores { get; set; }
        public Inner Child { get; set; }
        [JsonPropertyAlias(""renamed"")] public string Aliased { get; set; }
        [JsonOmitIfDefault] public int OmittedWhenZero { get; set; }
        [JsonOmitIfDefault(5)] public int OmittedWhenFive { get; set; }
        [JsonOmitIfDefault(5)] public int KeptWhenNotFive { get; set; }
        [JsonPropertyAlias(""both"")] [JsonOmitIfDefault(3)] public int AliasedAndOmitted { get; set; }
        [JsonPropertyAlias(""bothKept"")] [JsonOmitIfDefault(3)] public int AliasedAndKept { get; set; }
        [JsonRuntimeTypeEval] public object BoxedNumber { get; set; }
        [JsonRuntimeTypeEval] public object BoxedText { get; set; }
        [JsonIgnore] public string Ignored { get; set; }
        public int InitOnly { get; init; }
    }

    [OptimizeAJson]
    [JsonPropertyAsSelf(""Value"")]
    public class Elevated { public int Value { get; set; } }

    [OptimizeAJson]
    public class ElevatedHolder { public Elevated Lifted { get; set; } }
}";

        // Types built through a constructor route, and types with init-only properties, which the generated reader sets through
        //  UnsafeAccessors and the reflection path through PropertyInfo.SetValue
        private const string kRouteFixture = @"
using AJut.Text.AJson;
namespace ParityRouteNs
{
    public enum eMode { Calm, Busy, Loud }

    [OptimizeAJson]
    public class Routed
    {
        [AJsonConstructor]
        public Routed (string name, int size, eMode mode, int declared = 7)
        {
            Name = name;
            Size = size;
            Mode = mode;
            Declared = declared;
        }

        public string Name { get; }
        [JsonPropertyAlias(""sz"")] public int Size { get; }
        [JsonOmitIfDefault(eMode.Busy)] public eMode Mode { get; }
        [JsonOmitIfDefault] public int Declared { get; }
        public string Label { get; init; } = ""unset"";
        public double Ratio { get; set; }
    }

    [OptimizeAJson]
    public record PositionalPair (int Left, string Right)
    {
        public string Note { get; init; } = ""unset"";
    }

    [OptimizeAJson]
    public struct InitStruct
    {
        public int X { get; init; }
        public string Label { get; init; }
    }

    [OptimizeAJson]
    public class RouteHolder
    {
        public Routed First { get; set; }
        public PositionalPair Second { get; set; }
        public InitStruct Third { get; set; }
    }
}";

        [TestMethod]
        public void Generated_WritesAndReadsTheSameAsReflection ()
        {
            Assembly generated = FixtureAssemblies.LoadWithGeneratedSerializers(kFixture, "Parity_Generated_Everything");
            Assembly reflection = FixtureAssemblies.LoadWithoutGenerator(kFixture, "Parity_Reflection_Everything");

            object fromGenerated = _BuildEverything(generated);
            object fromReflection = _BuildEverything(reflection);
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(fromGenerated.GetType(), out _), "the generated fixture registered its serializer");
            Assert.IsFalse(AJsonGeneratedDispatch.TryGet(fromReflection.GetType(), out _), "the reflection fixture has no generated serializer");

            AssertSameJsonAndReadBack(fromGenerated, fromReflection);

            static object _BuildEverything (Assembly _fixture)
            {
                Type everythingType = _fixture.GetType("ParityNs.Everything", throwOnError: true)!;
                Type moodType = _fixture.GetType("ParityNs.eMood", throwOnError: true)!;
                object inner = Activator.CreateInstance(_fixture.GetType("ParityNs.Inner", throwOnError: true)!)!;
                SetProperty(inner, "Tag", "child");
                SetProperty(inner, "Weight", 12);

                object everything = Activator.CreateInstance(everythingType)!;
                SetProperty(everything, "Count", 3);
                SetProperty(everything, "Name", "everything");
                SetProperty(everything, "Active", true);
                SetProperty(everything, "Ratio", 0.125);
                SetProperty(everything, "Mood", Enum.ToObject(moodType, 1));
                SetProperty(everything, "MaybeSet", 8);
                SetProperty(everything, "MaybeNull", null);
                SetProperty(everything, "MaybeMood", Enum.ToObject(moodType, 2));
                SetProperty(everything, "When", new DateTime(2026, 10, 5, 4, 30, 15, DateTimeKind.Utc));
                SetProperty(everything, "Id", new Guid("6f1c2d3e-4b5a-4c6d-8e7f-9a0b1c2d3e4f"));
                SetProperty(everything, "Tags", new List<string> { "a", "b" });
                SetProperty(everything, "Scores", new Dictionary<string, int> { { "x", 1 }, { "y", 2 } });
                SetProperty(everything, "Child", inner);
                SetProperty(everything, "Aliased", "under another key");
                SetProperty(everything, "OmittedWhenZero", 0);
                SetProperty(everything, "OmittedWhenFive", 5);
                SetProperty(everything, "KeptWhenNotFive", 6);
                SetProperty(everything, "AliasedAndOmitted", 3);
                SetProperty(everything, "AliasedAndKept", 4);
                SetProperty(everything, "BoxedNumber", 42);
                SetProperty(everything, "BoxedText", "boxed");
                SetProperty(everything, "Ignored", "never written");
                SetProperty(everything, "InitOnly", 99);
                return everything;
            }
        }

        [TestMethod]
        public void Generated_PropertyAsSelf_WritesAndReadsTheSameAsReflection ()
        {
            Assembly generated = FixtureAssemblies.LoadWithGeneratedSerializers(kFixture, "Parity_Generated_AsSelf");
            Assembly reflection = FixtureAssemblies.LoadWithoutGenerator(kFixture, "Parity_Reflection_AsSelf");

            AssertSameJsonAndReadBack(_BuildHolder(generated), _BuildHolder(reflection));

            static object _BuildHolder (Assembly _fixture)
            {
                object elevated = Activator.CreateInstance(_fixture.GetType("ParityNs.Elevated", throwOnError: true)!)!;
                SetProperty(elevated, "Value", 17);
                object holder = Activator.CreateInstance(_fixture.GetType("ParityNs.ElevatedHolder", throwOnError: true)!)!;
                SetProperty(holder, "Lifted", elevated);
                return holder;
            }
        }

        [TestMethod]
        public void Generated_ConstructorRouteAndInitOnly_WritesAndReadsTheSameAsReflection ()
        {
            Assembly generated = FixtureAssemblies.LoadWithGeneratedSerializers(kRouteFixture, "Parity_Generated_Route");
            Assembly reflection = FixtureAssemblies.LoadWithoutGenerator(kRouteFixture, "Parity_Reflection_Route");

            object fromGenerated = _BuildHolder(generated);
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(fromGenerated.GetType(), out _), "the generated fixture registered its serializer");
            object readBack = AssertSameJsonAndReadBack(fromGenerated, _BuildHolder(reflection));

            object first = readBack.GetType().GetProperty("First")!.GetValue(readBack)!;
            Assert.AreEqual(0, first.GetType().GetProperty("Declared")!.GetValue(first), "zero comes back as zero, not the declared default of 7");

            static object _BuildHolder (Assembly _fixture)
            {
                // Mode sits at its omit value and Declared at zero, so the writer leaves both out and they read back through the
                //  missing-key rule: Mode as its omit value, and Declared, whose [JsonOmitIfDefault] is bare, as the type's default
                //  rather than the parameter's declared default of 7.
                Type modeType = _fixture.GetType("ParityRouteNs.eMode", throwOnError: true)!;
                object routed = Activator.CreateInstance(_fixture.GetType("ParityRouteNs.Routed", throwOnError: true)!, "routed", 12, Enum.ToObject(modeType, 1), 0)!;
                SetProperty(routed, "Label", "labeled");
                SetProperty(routed, "Ratio", 0.75);

                object pair = Activator.CreateInstance(_fixture.GetType("ParityRouteNs.PositionalPair", throwOnError: true)!, 4, "four")!;
                SetProperty(pair, "Note", "noted");

                object initStruct = Activator.CreateInstance(_fixture.GetType("ParityRouteNs.InitStruct", throwOnError: true)!)!;
                SetProperty(initStruct, "X", 9);
                SetProperty(initStruct, "Label", "nine");

                object holder = Activator.CreateInstance(_fixture.GetType("ParityRouteNs.RouteHolder", throwOnError: true)!)!;
                SetProperty(holder, "First", routed);
                SetProperty(holder, "Second", pair);
                SetProperty(holder, "Third", initStruct);
                return holder;
            }
        }

        [TestMethod]
        public void Generated_ConstructorRouteAndInitOnly_MissingKeysReadTheSameAsReflection ()
        {
            // Keys left out of hand-written json: constructor parameters take their missing-key values, and init-only and settable
            //  properties stay as construction left them
            Assembly generated = FixtureAssemblies.LoadWithGeneratedSerializers(kRouteFixture, "Parity_Generated_RouteMissing");
            Assembly reflection = FixtureAssemblies.LoadWithoutGenerator(kRouteFixture, "Parity_Reflection_RouteMissing");
            const string kSparse = "{ \"First\": { \"Name\": \"sparse\" }, \"Second\": { \"Right\": \"right only\" }, \"Third\": { \"Label\": \"label only\" } }";

            Type generatedType = generated.GetType("ParityRouteNs.RouteHolder", throwOnError: true)!;
            Type reflectionType = reflection.GetType("ParityRouteNs.RouteHolder", throwOnError: true)!;
            Json forGenerated = JsonHelper.ParseText(kSparse);
            Json forReflection = JsonHelper.ParseText(kSparse);
            object generatedRead = JsonHelper.BuildObjectForJson(generatedType, forGenerated);
            object reflectionRead = JsonHelper.BuildObjectForJson(reflectionType, forReflection);
            Assert.IsFalse(forGenerated.HasErrors, forGenerated.GetErrorReport());
            Assert.IsFalse(forReflection.HasErrors, forReflection.GetErrorReport());

            AssertSameValue(reflectionRead, generatedRead, "$");
        }

        // ===========================[ Helpers ]===========================

        /// <summary>
        /// Writes both, checks the json matches key for key, then reads that same json back with each and checks the results match
        /// </summary>
        /// <returns>What the generated reader read back, which the check has shown matches the reflection path's</returns>
        private static object AssertSameJsonAndReadBack (object fromGenerated, object fromReflection)
        {
            Json generatedJson = JsonHelper.BuildJsonForObject(fromGenerated);
            Json reflectionJson = JsonHelper.BuildJsonForObject(fromReflection);
            Assert.IsFalse(generatedJson.HasErrors, generatedJson.GetErrorReport());
            Assert.IsFalse(reflectionJson.HasErrors, reflectionJson.GetErrorReport());

            string context = $"\nGenerated:  {generatedJson}\nReflection: {reflectionJson}";
            AssertSameJson(reflectionJson.Data, generatedJson.Data, "$", context);

            // Both read the reflection path's json, which is what is already on disk and on the wire
            string text = reflectionJson.ToString();
            Json forGenerated = JsonHelper.ParseText(text);
            Json forReflection = JsonHelper.ParseText(text);
            object generatedReadBack = JsonHelper.BuildObjectForJson(fromGenerated.GetType(), forGenerated);
            object reflectionReadBack = JsonHelper.BuildObjectForJson(fromReflection.GetType(), forReflection);
            Assert.IsFalse(forGenerated.HasErrors, forGenerated.GetErrorReport());
            Assert.IsFalse(forReflection.HasErrors, forReflection.GetErrorReport());

            AssertSameValue(reflectionReadBack, generatedReadBack, "$");
            return generatedReadBack;
        }

        /// <summary>
        /// Key order is not compared, since the two paths walk properties independently. A type id is compared for presence only,
        /// since the two fixtures are different assemblies and their type names differ by assembly.
        /// </summary>
        private static void AssertSameJson (JsonValue expected, JsonValue actual, string path, string context)
        {
            Assert.IsNotNull(actual, $"{path}: missing from the generated json{context}");
            Assert.AreEqual(expected.IsDocument, actual.IsDocument, $"{path}: document on one side only{context}");
            Assert.AreEqual(expected.IsArray, actual.IsArray, $"{path}: array on one side only{context}");

            if (expected.IsDocument)
            {
                JsonDocument expectedDoc = (JsonDocument)expected;
                JsonDocument actualDoc = (JsonDocument)actual;
                CollectionAssert.AreEquivalent(expectedDoc.AllKeys().ToList(), actualDoc.AllKeys().ToList(), $"{path}: different keys{context}");
                foreach (string key in expectedDoc.AllKeys())
                {
                    if (key != kTypeIdKey)
                    {
                        AssertSameJson(expectedDoc.ValueFor(key), actualDoc.ValueFor(key), $"{path}.{key}", context);
                    }
                }
            }
            else if (expected.IsArray)
            {
                JsonArray expectedArray = (JsonArray)expected;
                JsonArray actualArray = (JsonArray)actual;
                Assert.AreEqual(expectedArray.Count, actualArray.Count, $"{path}: different lengths{context}");
                for (int index = 0; index < expectedArray.Count; ++index)
                {
                    AssertSameJson(expectedArray[index], actualArray[index], $"{path}[{index}]", context);
                }
            }
            else
            {
                Assert.AreEqual(expected.StringValue, actual.StringValue, $"{path}: different values{context}");
                Assert.AreEqual(expected.IsQuoted, actual.IsQuoted, $"{path}: quoted on one side only{context}");
            }
        }

        /// <summary>
        /// Compares two read-back objects whose types live in different fixture assemblies: enums by value, lists and dictionaries
        /// item by item, and anything else by its public properties
        /// </summary>
        private static void AssertSameValue (object? expected, object? actual, string path)
        {
            if (expected == null || actual == null)
            {
                Assert.AreEqual(expected == null, actual == null, $"{path}: null on one side only (reflection: {expected ?? "null"}, generated: {actual ?? "null"})");
                return;
            }

            Type expectedType = expected.GetType();
            if (expectedType.IsEnum)
            {
                Assert.IsTrue(actual.GetType().IsEnum, $"{path}: enum on one side only");
                Assert.AreEqual(Convert.ToInt64(expected), Convert.ToInt64(actual), $"{path}: different enum values");
                return;
            }

            if (expectedType.IsPrimitive || expected is string || expected is decimal || expected is DateTime || expected is Guid)
            {
                Assert.AreEqual(expected, actual, $"{path}: different values");
                return;
            }

            if (expected is IDictionary expectedDictionary)
            {
                IDictionary actualDictionary = (IDictionary)actual;
                Assert.AreEqual(expectedDictionary.Count, actualDictionary.Count, $"{path}: different counts");
                foreach (DictionaryEntry entry in expectedDictionary)
                {
                    Assert.IsTrue(actualDictionary.Contains(entry.Key), $"{path}: generated is missing key {entry.Key}");
                    AssertSameValue(entry.Value, actualDictionary[entry.Key], $"{path}[{entry.Key}]");
                }
                return;
            }

            if (expected is IList expectedList)
            {
                IList actualList = (IList)actual;
                Assert.AreEqual(expectedList.Count, actualList.Count, $"{path}: different counts");
                for (int index = 0; index < expectedList.Count; ++index)
                {
                    AssertSameValue(expectedList[index], actualList[index], $"{path}[{index}]");
                }
                return;
            }

            foreach (PropertyInfo property in expectedType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                PropertyInfo? match = actual.GetType().GetProperty(property.Name);
                Assert.IsNotNull(match, $"{path}: generated type has no property {property.Name}");
                AssertSameValue(property.GetValue(expected), match!.GetValue(actual), $"{path}.{property.Name}");
            }
        }

        private static void SetProperty (object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);
    }
}
