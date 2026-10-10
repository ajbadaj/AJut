namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Which members AJson writes and reads: public fields as well as properties, get-only properties only where something
    /// reads them back, get-only collections filled in place, and indexers never. Each case runs on the reflection path and,
    /// where it applies, through a generated serializer.
    /// </summary>
    [TestClass]
    public class JsonMemberWalkTests
    {
        // ===========================[ Test Models ]===========================
        public class VectorHolder
        {
            public Vector2 Offset { get; set; }
            public Vector3 Position { get; set; }
            public Vector4 Color { get; set; }
            public Quaternion Rotation { get; set; }
            public Plane Ground { get; set; }
            public Matrix3x2 Transform2D { get; set; }
            public Matrix4x4 Transform { get; set; }
        }

        [OptimizeAJson]
        public class VectorHolderGen
        {
            public Vector2 Offset { get; set; }
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
        }

        public class FieldHolder
        {
            public string Name;
            public int Count;
            public Vector2 Offset;
            public readonly int Fixed = 7;
        }

        [OptimizeAJson]
        public class FieldHolderGen
        {
            public string Name;
            public int Count;
            public Vector2 Offset;
        }

        public struct FieldStruct
        {
            public int Width;
            public int Height;
        }

        /// <summary>
        /// Shaped like the WinUI3 Rect, Point and Size: each value has a public field and a property in front of it
        /// </summary>
        public struct BackedByPublicFields
        {
            public float _x;
            public float _width;

            public double X
            {
                get => _x;
                set => _x = (float)value;
            }

            public double Width
            {
                get => _width;
                set => _width = (float)value;
            }
        }

        [OptimizeAJson]
        public struct BackedByPublicFieldsGen
        {
            public float _x;

            public double X
            {
                get => _x;
                set => _x = (float)value;
            }
        }

        public class AttributedFields
        {
            [JsonPropertyAlias("label")]
            public string Name;

            [JsonIgnore]
            public int Scratch;
        }

        [OptimizeAJson]
        public class AttributedFieldsGen
        {
            [JsonPropertyAlias("label")]
            public string Name;

            [JsonIgnore]
            public int Scratch;
        }

        public class HiddenFieldHolder
        {
            public int Kept;
            public int Dropped;
        }

        /// <summary>
        /// Shaped like a Rect: four values that can be set, and edges worked out from them
        /// </summary>
        public struct RectLike
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double Right => this.X + this.Width;
            public double Bottom => this.Y + this.Height;
            public bool IsEmpty => this.Width <= 0 || this.Height <= 0;
        }

        public class Labeled
        {
            public string Name { get; set; }
            public string DisplayName => this.Name + "!";
        }

        [OptimizeAJson]
        public class LabeledGen
        {
            public string Name { get; set; }
            public string DisplayName => this.Name + "!";
        }

        public class Money
        {
            [AJsonConstructor]
            public Money (decimal amount)
            {
                this.Amount = amount;
            }

            public decimal Amount { get; }
            public string Currency { get; set; }
        }

        [OptimizeAJson]
        public class MoneyGen
        {
            [AJsonConstructor]
            public MoneyGen (decimal amount)
            {
                this.Amount = amount;
            }

            public decimal Amount { get; }
            public string Currency { get; set; }
        }

        public class Immutable
        {
            public Immutable (string name, int size)
            {
                this.Name = name;
                this.Size = size;
            }

            public string Name { get; }
            public int Size { get; }
        }

        public class Tagged
        {
            public string Name { get; set; }
            public List<string> Tags { get; } = new List<string>();
            public Dictionary<string, int> Counts { get; } = new Dictionary<string, int>();
        }

        public class Seeded
        {
            public List<string> Tags { get; } = new List<string> { "seed" };
        }

        [OptimizeAJson]
        public class TaggedGen
        {
            public string Name { get; set; }
            public List<string> Tags { get; } = new List<string>();
            public Dictionary<string, int> Counts { get; } = new Dictionary<string, int>();
        }

        [OptimizeAJson]
        public class TaggedRouteGen
        {
            [AJsonConstructor]
            public TaggedRouteGen (string name)
            {
                this.Name = name;
            }

            public string Name { get; }
            public List<string> Tags { get; } = new List<string>();
        }

        public class SetHolder
        {
            public HashSet<string> Names { get; set; }
            public LinkedList<int> Order { get; set; }
            public HashSet<int> Ids { get; } = new HashSet<int>();
        }

        [OptimizeAJson]
        public class SetHolderGen
        {
            public HashSet<string> Names { get; set; }
            public HashSet<int> Ids { get; } = new HashSet<int>();
        }

        public class NullCollectionHolder
        {
            public List<string> Tags { get; }
        }

        public class IndexerHolder
        {
            public string Name { get; set; }

            public int this[int index]
            {
                get => index;
                set { }
            }
        }

        // ===========================[ Setup/Construction/Teardown ]===========================
        [TestCleanup]
        public void Cleanup ()
        {
            TypeMetadataExtensionRegistrar.ClearFor(typeof(HiddenFieldHolder));
        }

        // ===========================[ System.Numerics ]===========================
        [TestMethod]
        public void Vector2_IsWrittenAsADocumentOfItsComponents ()
        {
            JsonDocument written = WriteAndReparse(new VectorHolder { Offset = new Vector2(0.25f, 0.5f) });
            JsonDocument offset = written.ValueFor(nameof(VectorHolder.Offset)) as JsonDocument;
            Assert.IsNotNull(offset, "Offset should be written as a document");
            CollectionAssert.AreEqual(new[] { "X", "Y" }, KeysOf(offset));
            Assert.AreEqual("0.25", offset.ValueFor("X").StringValue);
            Assert.AreEqual("0.5", offset.ValueFor("Y").StringValue);
        }

        [TestMethod]
        public void Vector2_ReadsADocumentOfItsComponents ()
        {
            VectorHolder read = Read<VectorHolder>("{ \"Offset\": { \"X\": 0.2356, \"Y\": 0.2365235 } }");
            Assert.AreEqual(new Vector2(0.2356f, 0.2365235f), read.Offset);
        }

        [TestMethod]
        public void Vector2_StillReadsTheOldAngleBracketText ()
        {
            VectorHolder read = Read<VectorHolder>("{ \"Offset\": \"<0.23356, 0.26216>\" }");
            Assert.AreEqual(new Vector2(0.23356f, 0.26216f), read.Offset);
        }

        [TestMethod]
        public void Quaternion_IsWrittenAsItsFourComponentsOnly ()
        {
            JsonDocument written = WriteAndReparse(new VectorHolder { Rotation = new Quaternion(0f, 0f, 0.5f, 1f) });
            JsonDocument rotation = written.ValueFor(nameof(VectorHolder.Rotation)) as JsonDocument;
            Assert.IsNotNull(rotation, "Rotation should be written as a document");
            CollectionAssert.AreEqual(new[] { "X", "Y", "Z", "W" }, KeysOf(rotation));
        }

        [TestMethod]
        public void EveryNumericsType_RoundTrips ()
        {
            VectorHolder source = new VectorHolder
            {
                Offset = new Vector2(0.2356f, -1.5f),
                Position = new Vector3(1.5f, 2.5f, -3.5f),
                Color = new Vector4(0.1f, 0.2f, 0.3f, 1f),
                Rotation = new Quaternion(0.5f, -0.5f, 0.25f, 0.75f),
                Ground = new Plane(new Vector3(0f, 1f, 0f), -2.5f),
                Transform2D = new Matrix3x2(1f, 2f, 3f, 4f, 5.5f, 6.5f),
                Transform = new Matrix4x4(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13.5f, 14.5f, 15.5f, 16f),
            };

            VectorHolder round = RoundTrip(source, out string text);
            Assert.AreEqual(source.Offset, round.Offset, text);
            Assert.AreEqual(source.Position, round.Position, text);
            Assert.AreEqual(source.Color, round.Color, text);
            Assert.AreEqual(source.Rotation, round.Rotation, text);
            Assert.AreEqual(source.Ground, round.Ground, text);
            Assert.AreEqual(source.Transform2D, round.Transform2D, text);
            Assert.AreEqual(source.Transform, round.Transform, text);
        }

        [TestMethod]
        public void Vectors_RoundTrip_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(VectorHolderGen));

            VectorHolderGen source = new VectorHolderGen
            {
                Offset = new Vector2(0.25f, -1.5f),
                Position = new Vector3(1.5f, 2.5f, -3.5f),
                Rotation = new Quaternion(0.5f, -0.5f, 0.25f, 0.75f),
            };

            VectorHolderGen round = RoundTrip(source, out string text);
            Assert.AreEqual(source.Offset, round.Offset, text);
            Assert.AreEqual(source.Position, round.Position, text);
            Assert.AreEqual(source.Rotation, round.Rotation, text);
        }

        [TestMethod]
        public void Vectors_WriteTheSameText_OnBothPaths ()
        {
            VectorHolder reflected = new VectorHolder
            {
                Offset = new Vector2(0.25f, 0.5f),
                Position = new Vector3(1f, 2f, 3f),
                Rotation = Quaternion.Identity,
            };

            VectorHolderGen generated = new VectorHolderGen
            {
                Offset = reflected.Offset,
                Position = reflected.Position,
                Rotation = reflected.Rotation,
            };

            JsonDocument reflectedDoc = WriteAndReparse(reflected);
            JsonDocument generatedDoc = WriteAndReparse(generated);
            string[] keys = { nameof(VectorHolder.Offset), nameof(VectorHolder.Position), nameof(VectorHolder.Rotation) };
            foreach (string key in keys)
            {
                Assert.AreEqual(reflectedDoc.ValueFor(key)?.ToString(), generatedDoc.ValueFor(key)?.ToString(), key);
            }
        }

        [TestMethod]
        public void JsonBuilder_AddProperty_AndAddArrayItem_BuildAVector2AsADocument ()
        {
            // A Vector2 added by hand ends up the same shape as one written from a property
            JsonBuilder root = JsonHelper.MakeRootBuilder();
            JsonBuilder document = root.StartDocument();
            document.AddProperty("Offset", new Vector2(0.25f, 0.5f));
            document.StartProperty("Points").StartArray().AddArrayItem(new Vector2(1f, 2f));

            Json json = root.Finalize();
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument reparsed = (JsonDocument)JsonHelper.ParseText(json.ToString()).Data;
            Assert.IsInstanceOfType(reparsed.ValueFor("Offset"), typeof(JsonDocument), json.ToString());
            Assert.IsInstanceOfType(((JsonArray)reparsed.ValueFor("Points"))[0], typeof(JsonDocument), json.ToString());
        }

        // ===========================[ Public Fields ]===========================
        [TestMethod]
        public void PublicFields_RoundTrip ()
        {
            FieldHolder source = new FieldHolder { Name = "fields", Count = 3, Offset = new Vector2(0.5f, 1.5f) };
            FieldHolder round = RoundTrip(source, out string text);
            Assert.AreEqual("fields", round.Name, text);
            Assert.AreEqual(3, round.Count, text);
            Assert.AreEqual(source.Offset, round.Offset, text);
        }

        [TestMethod]
        public void PublicFields_OfAStruct_RoundTrip ()
        {
            FieldStruct round = RoundTrip(new FieldStruct { Width = 30, Height = 40 }, out string text);
            Assert.AreEqual(30, round.Width, text);
            Assert.AreEqual(40, round.Height, text);
        }

        [TestMethod]
        public void PublicFields_RoundTrip_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(FieldHolderGen));

            FieldHolderGen source = new FieldHolderGen { Name = "fields", Count = 3, Offset = new Vector2(0.5f, 1.5f) };
            FieldHolderGen round = RoundTrip(source, out string text);
            Assert.AreEqual("fields", round.Name, text);
            Assert.AreEqual(3, round.Count, text);
            Assert.AreEqual(new Vector2(0.5f, 1.5f), round.Offset, text);
        }

        [TestMethod]
        public void ReadonlyField_IsNotWritten ()
        {
            // A readonly field can not be set once the instance exists, so it is left out like a get-only property
            CollectionAssert.AreEqual(
                new[] { nameof(FieldHolder.Name), nameof(FieldHolder.Count), nameof(FieldHolder.Offset) },
                KeysOf(WriteAndReparse(new FieldHolder { Name = "n" }))
            );
        }

        [TestMethod]
        public void PublicField_BehindAPropertyOfTheSameName_IsNotWrittenTwice ()
        {
            BackedByPublicFields source = new BackedByPublicFields { X = 1.5, Width = 30 };
            CollectionAssert.AreEqual(new[] { "X", "Width" }, KeysOf(WriteAndReparse(source)));

            BackedByPublicFields round = RoundTrip(source, out string text);
            Assert.AreEqual(1.5, round.X, text);
            Assert.AreEqual(30, round.Width, text);
        }

        [TestMethod]
        public void PublicField_BehindAPropertyOfTheSameName_IsNotWrittenTwice_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(BackedByPublicFieldsGen));
            CollectionAssert.AreEqual(new[] { "X" }, KeysOf(WriteAndReparse(new BackedByPublicFieldsGen { X = 1.5 })));
        }

        [TestMethod]
        public void AliasAndIgnore_WorkOnFields_OnBothPaths ()
        {
            AttributedFields reflected = new AttributedFields { Name = "n", Scratch = 5 };
            CollectionAssert.AreEqual(new[] { "label" }, KeysOf(WriteAndReparse(reflected)), "reflection");
            AttributedFields round = RoundTrip(reflected, out string text);
            Assert.AreEqual("n", round.Name, text);
            Assert.AreEqual(0, round.Scratch, text);

            AssertIsGenerated(typeof(AttributedFieldsGen));
            AttributedFieldsGen generated = new AttributedFieldsGen { Name = "n", Scratch = 5 };
            CollectionAssert.AreEqual(new[] { "label" }, KeysOf(WriteAndReparse(generated)), "generated");
            AttributedFieldsGen roundGen = RoundTrip(generated, out text);
            Assert.AreEqual("n", roundGen.Name, text);
            Assert.AreEqual(0, roundGen.Scratch, text);
        }

        [TestMethod]
        public void HiddenField_IsNotWritten ()
        {
            TypeMetadataExtensionRegistrar.For<HiddenFieldHolder>().Hide(nameof(HiddenFieldHolder.Dropped));
            JsonDocument written = WriteAndReparse(new HiddenFieldHolder { Kept = 1, Dropped = 2 });
            CollectionAssert.AreEqual(new[] { nameof(HiddenFieldHolder.Kept) }, KeysOf(written));
        }

        // ===========================[ Get-Only Properties ]===========================
        [TestMethod]
        public void ComputedProperties_AreNotWritten_ByDefault ()
        {
            RectLike source = new RectLike { X = 1, Y = 2, Width = 30, Height = 40 };
            CollectionAssert.AreEqual(new[] { "X", "Y", "Width", "Height" }, KeysOf(WriteAndReparse(source)));

            RectLike round = RoundTrip(source, out string text);
            Assert.AreEqual(source, round, text);
        }

        [TestMethod]
        public void ComputedProperty_IsNotWritten_ByDefault_OnBothPaths ()
        {
            CollectionAssert.AreEqual(new[] { "Name" }, KeysOf(WriteAndReparse(new Labeled { Name = "n" })), "reflection");
            CollectionAssert.AreEqual(new[] { "Name" }, KeysOf(WriteAndReparse(new LabeledGen { Name = "n" })), "generated");
        }

        [TestMethod]
        public void ComputedProperty_IsWritten_WhenUseReadonlyObjectPropertiesIsOn_OnBothPaths ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings { UseReadonlyObjectProperties = true };
            string[] expected = { "Name", "DisplayName" };
            CollectionAssert.AreEqual(expected, KeysOf(WriteAndReparse(new Labeled { Name = "n" }, settings)), "reflection");
            CollectionAssert.AreEqual(expected, KeysOf(WriteAndReparse(new LabeledGen { Name = "n" }, settings)), "generated");
        }

        [TestMethod]
        public void ComputedProperty_IsNotWritten_WhenUseReadonlyObjectPropertiesIsOff_OnTheGeneratedPath ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings { UseReadonlyObjectProperties = false };
            CollectionAssert.AreEqual(new[] { "Name" }, KeysOf(WriteAndReparse(new LabeledGen { Name = "n" }, settings)));
        }

        [TestMethod]
        public void GetOnlyProperty_TheConstructorTakes_IsStillWritten_AndRoundTrips ()
        {
            Money round = RoundTrip(new Money(12.5m) { Currency = "CAD" }, out string text);
            Assert.AreEqual(12.5m, round.Amount, text);
            Assert.AreEqual("CAD", round.Currency, text);
        }

        [TestMethod]
        public void GetOnlyProperty_TheConstructorTakes_IsStillWritten_AndRoundTrips_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(MoneyGen));

            MoneyGen round = RoundTrip(new MoneyGen(12.5m) { Currency = "CAD" }, out string text);
            Assert.AreEqual(12.5m, round.Amount, text);
            Assert.AreEqual("CAD", round.Currency, text);
        }

        [TestMethod]
        public void TypeWithNothingSettable_IsStillWritten_ForARegisteredConstructorToRead ()
        {
            // Only a constructor can read a type like this back, and a registered one can read any key, so all of it is written
            JsonInterpreterSettings readSettings = new JsonInterpreterSettings();
            readSettings.RegisterCustomConstructor(ReadImmutable);

            Json written = JsonHelper.BuildJsonForObject(new Immutable("box", 4));
            string text = written.ToString();
            Immutable round = JsonHelper.BuildObjectForJson<Immutable>(JsonHelper.ParseText(text), readSettings);
            Assert.AreEqual("box", round.Name, text);
            Assert.AreEqual(4, round.Size, text);
        }

        [TestMethod]
        public void AnonymousType_IsWrittenInFull ()
        {
            CollectionAssert.AreEqual(new[] { "a", "b" }, KeysOf(WriteAndReparse(new { a = 1, b = "two" })));
        }

        [TestMethod]
        public void Dictionary_StillRoundTrips ()
        {
            // Each entry is a KeyValuePair, whose Key and Value are get-only, read back by AJson's own KeyValuePair constructor
            Dictionary<string, int> round = RoundTrip(new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }, out string text);
            Assert.AreEqual(2, round.Count, text);
            Assert.AreEqual(1, round["a"], text);
            Assert.AreEqual(2, round["b"], text);
        }

        // ===========================[ Get-Only Collections ]===========================
        [TestMethod]
        public void GetOnlyCollections_RoundTrip ()
        {
            Tagged source = new Tagged { Name = "tagged" };
            source.Tags.Add("a");
            source.Tags.Add("b");
            source.Counts.Add("one", 1);

            Tagged round = RoundTrip(source, out string text);
            CollectionAssert.AreEqual(new[] { "a", "b" }, round.Tags, text);
            Assert.AreEqual(1, round.Counts.Count, text);
            Assert.AreEqual(1, round.Counts["one"], text);
        }

        [TestMethod]
        public void GetOnlyCollection_IsReplaced_NotAddedTo ()
        {
            // What the constructor or an initializer seeds is replaced by what was written
            Seeded read = Read<Seeded>("{ \"Tags\": [ \"a\" ] }");
            CollectionAssert.AreEqual(new[] { "a" }, read.Tags);
        }

        [TestMethod]
        public void GetOnlyCollections_RoundTrip_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(TaggedGen));

            TaggedGen source = new TaggedGen { Name = "tagged" };
            source.Tags.Add("a");
            source.Counts.Add("one", 1);

            TaggedGen round = RoundTrip(source, out string text);
            CollectionAssert.AreEqual(new[] { "a" }, round.Tags, text);
            Assert.AreEqual(1, round.Counts["one"], text);
        }

        [TestMethod]
        public void GetOnlyCollection_RoundTrips_ThroughAGeneratedSerializer_BuiltByItsConstructor ()
        {
            AssertIsGenerated(typeof(TaggedRouteGen));

            TaggedRouteGen source = new TaggedRouteGen("routed");
            source.Tags.Add("a");

            TaggedRouteGen round = RoundTrip(source, out string text);
            Assert.AreEqual("routed", round.Name, text);
            CollectionAssert.AreEqual(new[] { "a" }, round.Tags, text);
        }

        [TestMethod]
        public void HashSet_AndOtherCollections_RoundTrip ()
        {
            // A HashSet or a LinkedList is a collection with Add, but no index to Insert at
            SetHolder source = new SetHolder
            {
                Names = new HashSet<string> { "a", "b" },
                Order = new LinkedList<int>(new[] { 3, 1, 2 }),
            };
            source.Ids.Add(7);

            SetHolder round = RoundTrip(source, out string text);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, round.Names?.ToArray(), text);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, round.Order?.ToArray(), text);
            CollectionAssert.AreEqual(new[] { 7 }, round.Ids.ToArray(), text);
        }

        [TestMethod]
        public void HashSets_RoundTrip_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(SetHolderGen));

            SetHolderGen source = new SetHolderGen { Names = new HashSet<string> { "a" } };
            source.Ids.Add(7);

            SetHolderGen round = RoundTrip(source, out string text);
            CollectionAssert.AreEquivalent(new[] { "a" }, round.Names?.ToArray(), text);
            CollectionAssert.AreEqual(new[] { 7 }, round.Ids.ToArray(), text);
        }

        [TestMethod]
        public void GetOnlyCollection_ThatIsNull_IsReported_NotThrown ()
        {
            Json json = JsonHelper.ParseText("{ \"Tags\": [ \"a\" ] }");
            NullCollectionHolder read = JsonHelper.BuildObjectForJson<NullCollectionHolder>(json);
            Assert.IsNotNull(read);
            Assert.IsTrue(json.HasErrors, "A get-only collection that is null has nothing to read into, and that should be reported");
        }

        // ===========================[ Indexers ]===========================
        [TestMethod]
        public void Indexer_IsNeitherWrittenNorRead ()
        {
            // An indexer is a property with parameters, and it has no value without an index
            IndexerHolder round = RoundTrip(new IndexerHolder { Name = "indexed" }, out string text);
            Assert.AreEqual("indexed", round.Name, text);
            CollectionAssert.AreEqual(new[] { "Name" }, KeysOf(WriteAndReparse(new IndexerHolder { Name = "indexed" })));
        }

        // ===========================[ Helpers ]===========================
        private static void AssertIsGenerated (Type type)
        {
            Assert.IsTrue(AJsonGeneratedDispatch.IsRegistered(type), $"The source generator did not register {type.Name}");
        }

        private static Immutable ReadImmutable (JsonValue json)
        {
            JsonDocument document = (JsonDocument)json;
            return new Immutable(document.ValueFor("Name").StringValue, int.Parse(document.ValueFor("Size").StringValue));
        }

        private static T Read<T> (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            T read = JsonHelper.BuildObjectForJson<T>(json);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            return read;
        }

        private static JsonDocument WriteAndReparse (object source, JsonBuilderSettings settings = null)
        {
            Json json = JsonHelper.BuildJsonForObject(source, settings);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            Json reparsed = JsonHelper.ParseText(json.ToString());
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            return (JsonDocument)reparsed.Data;
        }

        private static T RoundTrip<T> (T source, out string text)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            text = json.ToString();
            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);

            T round = JsonHelper.BuildObjectForJson<T>(reparsed);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);
            return round;
        }

        /// <summary>
        /// The document's keys in order, leaving out AJson's own markers (the version, a type id)
        /// </summary>
        private static string[] KeysOf (JsonDocument document)
        {
            return document.Select(kvp => kvp.Key).Where(key => !key.StartsWith("__", StringComparison.Ordinal)).ToArray();
        }
    }
}
