namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// [JsonIncludeReadOnlyInWrite]: a get-only member it marks is written, the members of its value it names are written on top
    /// of the value's defaults (through dotted names, collections and dictionaries, on the value's runtime type), and the text
    /// still reads back. Each case runs on the reflection path and, where it applies, through a generated serializer, whose text
    /// has to match.
    /// </summary>
    [TestClass]
    public class JsonIncludeReadOnlyInWriteTests
    {
        private readonly List<string> m_loggedOutput = new List<string>();

        // ===========================[ Test Models ]===========================
        /// <summary>
        /// Stands in for a type the app doesn't own, so it can't carry the attribute: Right and Area are worked out from the rest
        /// </summary>
        public struct Box
        {
            public double X { get; set; }
            public double Width { get; set; }
            public double Right => this.X + this.Width;
            public double Area => this.Width * this.Width;
        }

        public abstract class ManifestBase
        {
            public string Name { get; set; }
            public abstract string ResourceType { get; }
        }

        public class SampleManifest : ManifestBase
        {
            [JsonIncludeReadOnlyInWrite]
            public override string ResourceType => "sample";
        }

        public class UnmarkedManifest : ManifestBase
        {
            public override string ResourceType => "unmarked";
        }

        /// <summary>
        /// The reflection twin of <see cref="SampleManifestGen"/>, the same shape, so key order matches
        /// </summary>
        public class SampleManifestFlat
        {
            public string Name { get; set; }

            [JsonIncludeReadOnlyInWrite]
            public string ResourceType => "sample";
        }

        [OptimizeAJson]
        public class SampleManifestGen
        {
            public string Name { get; set; }

            [JsonIncludeReadOnlyInWrite]
            public string ResourceType => "sample";
        }

        public class KindBase
        {
            public int Count { get; set; }

            [JsonIncludeReadOnlyInWrite]
            public virtual string Kind => "base";
        }

        public class KindDerived : KindBase
        {
            public override string Kind => "derived";
        }

        public class FieldAndOmit
        {
            private int m_computed;

            public string Name { get; set; }

            [JsonIncludeReadOnlyInWrite]
            public readonly int Version = 3;

            [JsonIncludeReadOnlyInWrite]
            [JsonOmitIfDefault]
            public int Computed => m_computed;

            [JsonIncludeReadOnlyInWrite]
            [JsonPropertyAlias("label")]
            public string Label => $"{this.Name}!";

            public void SetComputed (int value) => m_computed = value;
        }

        public class BoundsHolder
        {
            [JsonIncludeReadOnlyInWrite("Right", "Width")]
            public Box Bounds { get; set; }
        }

        [OptimizeAJson]
        public class BoundsHolderGen
        {
            [JsonIncludeReadOnlyInWrite("Right", "Width")]
            public Box Bounds { get; set; }
        }

        public class Frame
        {
            public Box Inner { get; set; }
            public double Scale => 2.0;
        }

        [OptimizeAJson]
        public class FrameGen
        {
            public Box Inner { get; set; }
            public double Scale => 2.0;
        }

        public class FrameHolder
        {
            [JsonIncludeReadOnlyInWrite("Scale", "Inner.Right")]
            public Frame Frame { get; set; }
        }

        /// <summary>
        /// The names reach a value with a generated serializer, which wrote its members before the names were added
        /// </summary>
        public class GeneratedFrameHolder
        {
            [JsonIncludeReadOnlyInWrite("Scale", "Inner.Right")]
            public FrameGen Frame { get; set; }
        }

        public class CollectionsHolder
        {
            [JsonIncludeReadOnlyInWrite("Right")]
            public List<Box> Boxes { get; set; }

            [JsonIncludeReadOnlyInWrite("Right")]
            public Box[] BoxArray { get; set; }

            [JsonIncludeReadOnlyInWrite("Right")]
            public Dictionary<string, Box> BoxesByName { get; set; }

            [JsonIncludeReadOnlyInWrite("Right")]
            public Dictionary<Box, Box> BoxesByBox { get; set; }

            [JsonIncludeReadOnlyInWrite("Inner.Right")]
            public List<Frame> Frames { get; set; }

            [JsonIncludeReadOnlyInWrite("Right")]
            public List<List<Box>> Nested { get; set; }
        }

        [OptimizeAJson]
        public class CollectionsHolderGen
        {
            [JsonIncludeReadOnlyInWrite("Right")]
            public List<Box> Boxes { get; set; }

            [JsonIncludeReadOnlyInWrite("Right")]
            public Box[] BoxArray { get; set; }

            [JsonIncludeReadOnlyInWrite("Right")]
            public Dictionary<string, Box> BoxesByName { get; set; }

            [JsonIncludeReadOnlyInWrite("Right")]
            public Dictionary<Box, Box> BoxesByBox { get; set; }

            [JsonIncludeReadOnlyInWrite("Inner.Right")]
            public List<Frame> Frames { get; set; }

            [JsonIncludeReadOnlyInWrite("Right")]
            public List<List<Box>> Nested { get; set; }
        }

        public class Shape
        {
            public string Name { get; set; }
        }

        public class Circle : Shape
        {
            public double Diameter { get; set; }
            public double Radius => this.Diameter / 2.0;
        }

        public class ShapeHolder
        {
            [JsonIncludeReadOnlyInWrite(eAllowedProperties.DerivedTypes, "Radius")]
            public Shape Shape { get; set; }
        }

        [OptimizeAJson]
        public class ShapeHolderGen
        {
            [JsonIncludeReadOnlyInWrite(eAllowedProperties.DerivedTypes, "Radius")]
            public Shape Shape { get; set; }
        }

        public class SettableNameHolder
        {
            [JsonIncludeReadOnlyInWrite("X")]
            public Box Bounds { get; set; }
        }

        public class UnmatchedNameHolder
        {
            public string Name { get; set; }

            [JsonIncludeReadOnlyInWrite("Rigth", "Right")]
            public Box Bounds { get; set; }
        }

        public class SingleValueHolder
        {
            [JsonIncludeReadOnlyInWrite("Length")]
            public string Text { get; set; }
        }

        // ===========================[ The member itself ]===========================
        [TestMethod]
        public void Bare_GetOnlyOverride_IsWritten_AndAnUnmarkedOneIsNot ()
        {
            JsonDocument written = Write(new SampleManifest { Name = "first" });
            Assert.AreEqual("sample", written.ValueFor("ResourceType")?.StringValue);
            Assert.AreEqual("first", written.ValueFor("Name")?.StringValue);

            Assert.IsNull(Write(new UnmarkedManifest { Name = "first" }).ValueFor("ResourceType"), "the 1.6 default still holds without the attribute");

            SampleManifest readBack = ReadBack<SampleManifest>(new SampleManifest { Name = "first" });
            Assert.AreEqual("first", readBack.Name);
        }

        [TestMethod]
        public void Bare_Generated_MatchesReflection ()
        {
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(typeof(SampleManifestGen), out _), "the generator has to have run for this to test it");

            AssertSameText(new SampleManifestFlat { Name = "first" }, new SampleManifestGen { Name = "first" });
            SampleManifestGen readBack = ReadBack<SampleManifestGen>(new SampleManifestGen { Name = "first" });
            Assert.AreEqual("first", readBack.Name);
        }

        [TestMethod]
        public void Bare_OnABaseProperty_ReachesItsOverride ()
        {
            JsonDocument written = Write(new KindDerived { Count = 4 });
            Assert.AreEqual("derived", written.ValueFor("Kind")?.StringValue);
            Assert.AreEqual(4, ReadBack<KindDerived>(new KindDerived { Count = 4 }).Count);
        }

        [TestMethod]
        public void Bare_ReadonlyField_Alias_AndOmitIfDefault_AreHonored ()
        {
            FieldAndOmit source = new FieldAndOmit { Name = "n" };
            JsonDocument written = Write(source);
            Assert.AreEqual("3", written.ValueFor("Version")?.StringValue);
            Assert.AreEqual("n!", written.ValueFor("label")?.StringValue);
            Assert.IsNull(written.ValueFor("Label"), "written under its alias");
            Assert.IsNull(written.ValueFor("Computed"), "left out at its default, as [JsonOmitIfDefault] says");

            source.SetComputed(9);
            Assert.AreEqual("9", Write(source).ValueFor("Computed")?.StringValue);
            Assert.AreEqual("n", ReadBack<FieldAndOmit>(source).Name);
        }

        // ===========================[ Named members of the value ]===========================
        [TestMethod]
        public void Named_AddsToTheValuesDefaults_OnATypeNotOwned ()
        {
            BoundsHolder source = new BoundsHolder { Bounds = new Box { X = 1, Width = 3 } };
            JsonDocument bounds = (JsonDocument)Write(source).ValueFor("Bounds");

            Assert.AreEqual("1", bounds.ValueFor("X")?.StringValue, "what it writes by default is still written");
            Assert.AreEqual("3", bounds.ValueFor("Width")?.StringValue, "a settable name adds nothing, and is not written twice");
            Assert.AreEqual("4", bounds.ValueFor("Right")?.StringValue);
            Assert.IsNull(bounds.ValueFor("Area"), "only the names given are added");
            Assert.AreEqual(1, bounds.AllKeys().Count(k => k == "Width"));

            BoundsHolder readBack = ReadBack<BoundsHolder>(source);
            Assert.AreEqual(1, readBack.Bounds.X);
            Assert.AreEqual(3, readBack.Bounds.Width);
        }

        [TestMethod]
        public void Named_Generated_MatchesReflection ()
        {
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(typeof(BoundsHolderGen), out _));

            Box box = new Box { X = 1, Width = 3 };
            AssertSameText(new BoundsHolder { Bounds = box }, new BoundsHolderGen { Bounds = box });
            Assert.AreEqual(3, ReadBack<BoundsHolderGen>(new BoundsHolderGen { Bounds = box }).Bounds.Width);
        }

        [TestMethod]
        public void Named_DottedName_ReachesFurtherDown ()
        {
            FrameHolder source = new FrameHolder { Frame = new Frame { Inner = new Box { X = 2, Width = 5 } } };
            JsonDocument frame = (JsonDocument)Write(source).ValueFor("Frame");

            Assert.AreEqual("2", frame.ValueFor("Scale")?.StringValue);
            JsonDocument inner = (JsonDocument)frame.ValueFor("Inner");
            Assert.AreEqual("7", inner.ValueFor("Right")?.StringValue);
            Assert.IsNull(inner.ValueFor("Area"));

            Assert.AreEqual(5, ReadBack<FrameHolder>(source).Frame.Inner.Width);
        }

        [TestMethod]
        public void Named_ReachesAValueWithAGeneratedSerializer_KeepingItsKeyOrder ()
        {
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(typeof(FrameGen), out _));

            Box box = new Box { X = 2, Width = 5 };
            AssertSameText(new FrameHolder { Frame = new Frame { Inner = box } }, new GeneratedFrameHolder { Frame = new FrameGen { Inner = box } });

            JsonDocument frame = (JsonDocument)Write(new GeneratedFrameHolder { Frame = new FrameGen { Inner = box } }).ValueFor("Frame");
            CollectionAssert.AreEqual(new[] { "Inner", "Scale" }, frame.AllKeys().ToArray(), "a member written again for its names keeps its place");
            Assert.AreEqual(5, ReadBack<GeneratedFrameHolder>(new GeneratedFrameHolder { Frame = new FrameGen { Inner = box } }).Frame.Inner.Width);
        }

        // ===========================[ Collections ]===========================
        [TestMethod]
        public void Collections_PassTheNamesToEachItem ()
        {
            CollectionsHolder source = MakeCollections<CollectionsHolder>();
            JsonDocument written = Write(source);

            AssertEachHasRight((JsonArray)written.ValueFor("Boxes"), "4", "10");
            AssertEachHasRight((JsonArray)written.ValueFor("BoxArray"), "4", "10");

            JsonDocument byName = (JsonDocument)written.ValueFor("BoxesByName");
            Assert.AreEqual("4", ((JsonDocument)byName.ValueFor("a")).ValueFor("Right")?.StringValue);
            Assert.AreEqual("10", ((JsonDocument)byName.ValueFor("b")).ValueFor("Right")?.StringValue);

            // A key that isn't one value makes the dictionary an array of Key and Value pairs, and only the values get the names
            JsonDocument pair = (JsonDocument)((JsonArray)written.ValueFor("BoxesByBox"))[0];
            Assert.IsNull(((JsonDocument)pair.ValueFor("Key")).ValueFor("Right"));
            Assert.AreEqual("4", ((JsonDocument)pair.ValueFor("Value")).ValueFor("Right")?.StringValue);

            JsonDocument firstFrame = (JsonDocument)((JsonArray)written.ValueFor("Frames"))[0];
            Assert.AreEqual("7", ((JsonDocument)firstFrame.ValueFor("Inner")).ValueFor("Right")?.StringValue);
            Assert.IsNull(firstFrame.ValueFor("Scale"), "only Inner.Right is named");

            AssertEachHasRight((JsonArray)((JsonArray)written.ValueFor("Nested"))[0], "4", "10");

            CollectionsHolder readBack = ReadBack<CollectionsHolder>(source);
            Assert.AreEqual(2, readBack.Boxes.Count);
            Assert.AreEqual(6, readBack.BoxArray[1].Width);
            Assert.AreEqual(6, readBack.BoxesByName["b"].Width);
            Assert.AreEqual(3, readBack.BoxesByBox.Single().Value.Width);
            Assert.AreEqual(5, readBack.Frames[0].Inner.Width);
            Assert.AreEqual(6, readBack.Nested[0][1].Width);
        }

        [TestMethod]
        public void Collections_Generated_MatchesReflection ()
        {
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(typeof(CollectionsHolderGen), out _));

            AssertSameText(MakeCollections<CollectionsHolder>(), MakeCollections<CollectionsHolderGen>());
            Assert.AreEqual(6, ReadBack<CollectionsHolderGen>(MakeCollections<CollectionsHolderGen>()).BoxesByName["b"].Width);
        }

        // ===========================[ Runtime type ]===========================
        [TestMethod]
        public void Names_AreLookedUpOnTheRuntimeType ()
        {
            ShapeHolder source = new ShapeHolder { Shape = new Circle { Name = "c", Diameter = 6 } };
            JsonDocument shape = (JsonDocument)Write(source).ValueFor("Shape");
            Assert.AreEqual("3", shape.ValueFor("Radius")?.StringValue);
            Assert.AreEqual("6", shape.ValueFor("Diameter")?.StringValue);

            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(typeof(ShapeHolderGen), out _));
            AssertSameText(source, new ShapeHolderGen { Shape = new Circle { Name = "c", Diameter = 6 } });
        }

        // ===========================[ Mismatches ]===========================
        [TestMethod]
        public void Unmatched_Name_IsLoggedOnce_AndTheRestIsWritten ()
        {
            List<string> logged = this.CaptureLogDuring(() =>
            {
                for (int pass = 0; pass < 2; ++pass)
                {
                    UnmatchedNameHolder source = new UnmatchedNameHolder { Name = "n", Bounds = new Box { X = 1, Width = 1 } };
                    Json json = JsonHelper.BuildJsonForObject(source);
                    Assert.IsFalse(json.HasErrors, json.GetErrorReport());

                    JsonDocument written = (JsonDocument)json.Data;
                    Assert.AreEqual("n", written.ValueFor("Name")?.StringValue);
                    Assert.AreEqual("2", ((JsonDocument)written.ValueFor("Bounds")).ValueFor("Right")?.StringValue);
                }
            });

            string[] errors = logged.Where(line => line.Contains("Rigth")).ToArray();
            Assert.AreEqual(1, errors.Length, string.Join("\n", logged));
            StringAssert.Contains(errors[0], nameof(Box));
        }

        [TestMethod]
        public void DerivedTypes_NameMissingFromTheBaseValue_IsNotLogged_OnEitherPath ()
        {
            // The log is kept once per type and name for the whole process, so both paths share it: if either one still logs,
            //  its line shows up here
            List<string> logged = this.CaptureLogDuring(() =>
            {
                JsonDocument fromReflection = (JsonDocument)Write(new ShapeHolder { Shape = new Shape { Name = "plain" } }).ValueFor("Shape");
                Assert.AreEqual("plain", fromReflection.ValueFor("Name")?.StringValue);
                Assert.IsNull(fromReflection.ValueFor("Radius"));

                Assert.IsTrue(AJsonGeneratedDispatch.TryGet(typeof(ShapeHolderGen), out _));
                JsonDocument fromGenerated = (JsonDocument)Write(new ShapeHolderGen { Shape = new Shape { Name = "plain" } }).ValueFor("Shape");
                Assert.AreEqual("plain", fromGenerated.ValueFor("Name")?.StringValue);
                Assert.IsNull(fromGenerated.ValueFor("Radius"));
            });

            Assert.IsFalse(logged.Any(line => line.Contains("JsonIncludeReadOnlyInWrite")), string.Join("\n", logged));
        }

        [TestMethod]
        public void SingleValue_WithNames_IsLoggedOnce ()
        {
            List<string> logged = this.CaptureLogDuring(() =>
            {
                for (int pass = 0; pass < 2; ++pass)
                {
                    Assert.AreEqual("abc", Write(new SingleValueHolder { Text = "abc" }).ValueFor("Text")?.StringValue);
                }
            });

            string[] errors = logged.Where(line => line.Contains("JsonIncludeReadOnlyInWrite") && line.Contains("Length")).ToArray();
            Assert.AreEqual(1, errors.Length, string.Join("\n", logged));
        }

        [TestMethod]
        public void SettableName_IsNotLogged_AndChangesNothing ()
        {
            Box box = new Box { X = 1, Width = 3 };
            List<string> logged = this.CaptureLogDuring(() =>
            {
                JsonDocument bounds = (JsonDocument)Write(new SettableNameHolder { Bounds = box }).ValueFor("Bounds");
                CollectionAssert.AreEquivalent(new[] { "X", "Width" }, bounds.AllKeys().ToArray());
            });

            Assert.IsFalse(logged.Any(line => line.Contains("JsonIncludeReadOnlyInWrite")), string.Join("\n", logged));
        }

        // ===========================[ Helpers ]===========================
        private static JsonDocument Write (object source)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            return (JsonDocument)json.Data;
        }

        private static T ReadBack<T> (T source)
        {
            Json written = JsonHelper.BuildJsonForObject(source);
            Json parsed = JsonHelper.ParseText(written.ToString());
            T read = JsonHelper.BuildObjectForJson<T>(parsed);
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            return read;
        }

        private static void AssertSameText (object fromReflection, object fromGenerated)
        {
            Assert.AreEqual(JsonHelper.BuildJsonForObject(fromReflection).ToString(), JsonHelper.BuildJsonForObject(fromGenerated).ToString());
        }

        private static void AssertEachHasRight (JsonArray boxes, params string[] rights)
        {
            Assert.AreEqual(rights.Length, boxes.Count);
            for (int index = 0; index < rights.Length; ++index)
            {
                Assert.AreEqual(rights[index], ((JsonDocument)boxes[index]).ValueFor("Right")?.StringValue, $"item {index}");
            }
        }

        /// <summary>
        /// The same collections in either holder, which share member names
        /// </summary>
        private static T MakeCollections<T> () where T : new()
        {
            Box first = new Box { X = 1, Width = 3 };
            Box second = new Box { X = 4, Width = 6 };
            T holder = new T();
            Type type = typeof(T);
            type.GetProperty("Boxes").SetValue(holder, new List<Box> { first, second });
            type.GetProperty("BoxArray").SetValue(holder, new[] { first, second });
            type.GetProperty("BoxesByName").SetValue(holder, new Dictionary<string, Box> { { "a", first }, { "b", second } });
            type.GetProperty("BoxesByBox").SetValue(holder, new Dictionary<Box, Box> { { second, first } });
            type.GetProperty("Frames").SetValue(holder, new List<Frame> { new Frame { Inner = new Box { X = 2, Width = 5 } } });
            type.GetProperty("Nested").SetValue(holder, new List<List<Box>> { new List<Box> { first, second } });
            return holder;
        }

        private List<string> CaptureLogDuring (Action action)
        {
            m_loggedOutput.Clear();
            Logger.ResetToDefaults();
            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;
            Logger.SetSingleOverrideLogTarget(this.CaptureLog);
            try
            {
                action();
            }
            finally
            {
                Logger.ResetToDefaults();
            }

            return m_loggedOutput.ToList();
        }

        private void CaptureLog (string line)
        {
            m_loggedOutput.Add(line);
        }
    }
}
