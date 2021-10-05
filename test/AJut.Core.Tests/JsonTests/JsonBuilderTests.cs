namespace AJut.Core.UnitTests.AJson
{
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [TestClass]
    public class JsonBuilderTests
    {
        [TestMethod]
        public void AJson_BuilderBasicTests_UseBuilder_Works()
        {
            JsonBuilder testBuilder = JsonHelper.MakeRootBuilder();

            testBuilder
                    .StartDocument()
                        .StartProperty("name")
                            .StartDocument()
                                .AddProperty("first", "Gobi")
                                .AddProperty("last", "Nadir")
                                .AddProperty("age", 46)
                            .End()
                        .StartProperty("children")
                            .StartArray()
                                .StartDocument()
                                    .AddProperty("name", "Abed")
                                .End()
                            .End();

            Json output = testBuilder.Finalize();
            Assert.IsNotNull(output);
            Assert.IsFalse(output.HasErrors);
            output.AssertSourceIsValid();

            Json reparsedBeans = JsonHelper.ParseText(output.Data.ToString());
            Assert.IsNotNull(reparsedBeans);
            Assert.IsFalse(reparsedBeans.HasErrors, reparsedBeans.BuildJsonErrorReport());

            reparsedBeans.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_JsonHelper_BuildJsonForAnonymous_SimpleObject()
        {
            float Floaty = 3.14f;
            var d = new { What = 3, Noice = true, Floaty};
            Json json = JsonHelper.BuildJsonForObject(d);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsNotNull(json.Data);
            Assert.IsTrue(json.Data.IsDocument);
            Assert.AreEqual(3, ((JsonDocument)json.Data).Count);
            
            // What
            JsonValue what = ((JsonDocument)json.Data).ValueFor("What");
            Assert.IsNotNull(what);
            Assert.AreEqual("3", what.StringValue);

            // Noice
            JsonValue noice = ((JsonDocument)json.Data).ValueFor("Noice");
            Assert.IsNotNull(noice);
            Assert.AreEqual(true.ToString(), noice.StringValue);

            JsonValue floaty = ((JsonDocument)json.Data).ValueFor("Floaty");
            Assert.IsNotNull(floaty);
            Assert.AreEqual(Floaty.ToString(), floaty.StringValue);

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_JsonHelper_BuildJsonForDictionary ()
        {
            var d = new Dictionary<string, string>();
            d.Add("Test", "Value");
            Json json = JsonHelper.BuildJsonForObject(d);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsNotNull(json.Data);
            Assert.IsTrue(json.Data.IsArray);
            Assert.AreEqual(1, ((JsonArray)json.Data).Count);
        }

        [TestMethod]
        public void AJson_JsonHelper_BuildJsonForDictionary_ProperKVPTypingWithKeyValueTypeIdToWriteSetting ()
        {
            var dictionary = new Dictionary<string, int> { { "Test", 10 } };
            Json json = JsonHelper.BuildJsonForObject(dictionary);
            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());

            // Verify we've made a useless read for the dictionary values, but the shape is correct
            Dictionary<object,object> generatedDictionary = JsonHelper.BuildObjectForJson<Dictionary<object, object>>(json);
            Assert.AreEqual(1, generatedDictionary.Count);
            Assert.AreEqual(1, generatedDictionary.Keys.Count);
            Assert.AreEqual(1, generatedDictionary.Values.Count);

            object generatedObjKey = generatedDictionary.Keys.First();
            Assert.IsNotNull(generatedObjKey);

            object generatedObjValue = generatedDictionary[generatedObjKey];
            Assert.IsNotNull(generatedObjValue);
            Assert.AreEqual(typeof(Object), generatedObjValue.GetType());

            // === Try 2: With kvp option set ===
            json = JsonHelper.BuildJsonForObject(dictionary, new JsonBuilder.Settings {
                KeyValuePairKeyTypeIdToWrite = eTypeIdInfo.FullyQualifiedSystemType,
                KeyValuePairValueTypeIdToWrite = eTypeIdInfo.FullyQualifiedSystemType
            });
            generatedDictionary = JsonHelper.BuildObjectForJson<Dictionary<object, object>>(json);
            Assert.AreEqual(1, generatedDictionary.Count);
            Assert.AreEqual(1, generatedDictionary.Keys.Count);
            Assert.AreEqual(1, generatedDictionary.Values.Count);

            string generatedStrKey = generatedDictionary.Keys.First() as string;
            Assert.IsNotNull(generatedStrKey);
            Assert.AreEqual("Test", generatedStrKey);

            generatedObjValue = generatedDictionary[generatedStrKey];
            Assert.IsNotNull(generatedObjValue);
            Assert.AreEqual(typeof(int), generatedObjValue.GetType());
            Assert.AreEqual(10, (int)generatedObjValue);
        }

        [TestMethod]
        public void AJson_JsonHelper_BuildJsonForObject_SimpleObject()
        {
            SimpleGuy d = new SimpleGuy() { What = 3, Noice = 5 };
            Json json = JsonHelper.BuildJsonForObject(d);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_JsonHelper_BuildJsonForObject_ObjectWithItems()
        {
            ItemsGuy d = new ItemsGuy("Bob");
            d.Items.AddRange(new[] { 1, 2, 3, 4 });
            Json json = JsonHelper.BuildJsonForObject(d);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_JsonHelper_BuildJsonForObject_ComplexObject()
        {
            Json json = JsonHelper.BuildJsonForObject(ComplexGuy.MakeOne());
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            json.AssertSourceIsValid();
        }


        [TestMethod]
        public void AJson_JsonHelper_BuildObjectForJson_ComplexObject()
        {
            ComplexGuy theDudeGoinIn = ComplexGuy.MakeOne();
            Json json = JsonHelper.BuildJsonForObject(theDudeGoinIn);
            ComplexGuy theDude = JsonHelper.BuildObjectForJson<ComplexGuy>(json);

            Assert.IsTrue(theDudeGoinIn.Equals(theDude));

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_JsonBuilding_DeserializeJsonToObject_WorksWithSimpleGuy()
        {
            SimpleGuy guy = new SimpleGuy();
            guy.What = 2;
            guy.Noice = 3;
            Json json = JsonHelper.BuildJsonForObject(guy);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            string simpleGuyJson = json.Data.StringValue;
            SimpleGuy serializedGuy =  JsonHelper.BuildObjectForJson<SimpleGuy>(json);
            Assert.IsTrue(guy.Equals(serializedGuy));
        }

        [TestInitialize]
        public void Setup()
        {
            TypeIdRegistrar.RegisterAllTypeIds(typeof(JsonBuilderTests).Assembly);
        }

        [TestMethod]
        public void AJson_JsonBuilding_DeserializeJsonToObject_PicksDerivedTypesProperly()
        {
            ThingWithDerived thing = new ThingWithDerived() { Guy = new DerivedGuy(462) };
            thing.Guy.What = 5;
            thing.Guy.Noice = 106;

            Json json = JsonHelper.BuildJsonForObject(thing, new JsonBuilder.Settings() { TypeIdToWrite = eTypeIdInfo.TypeIdAttributed | eTypeIdInfo.FullyQualifiedSystemType });
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            ThingWithDerived serializedThing = JsonHelper.BuildObjectForJson<ThingWithDerived>(json);
            Assert.IsInstanceOfType(serializedThing.Guy, typeof(DerivedGuy));

            Assert.AreEqual(462, (serializedThing.Guy as DerivedGuy)?.TheDerivedSpecificProperty);
        }

        [TestMethod]
        public void AJson_JsonBuilding_DeserializeJsonToObject_PicksDerivedTypesProperly_UseTypeId()
        {
            var thing = new OtherThingWithDerived() { Guy = new OtherDerivedGuy { Other = 2, Special = "dood" } };
            Json json = JsonHelper.BuildJsonForObject(thing, new JsonBuilder.Settings() { TypeIdToWrite = eTypeIdInfo.TypeIdAttributed | eTypeIdInfo.FullyQualifiedSystemType });
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            Assert.IsTrue(json.Data.StringValue.Contains("other-derived-guy (this is the type id)"));

            var serializedThing = JsonHelper.BuildObjectForJson<OtherThingWithDerived>(json);
            Assert.IsInstanceOfType(serializedThing.Guy, typeof(OtherDerivedGuy));
            Assert.AreEqual("dood", (serializedThing.Guy as OtherDerivedGuy)?.Special);
        }

        [TestMethod]
        public void AJson_JsonBuilding_HandleAttributeProperly_JsonWithJsonPropertyAsSelfAttribute()
        {
            var thing = new ThingThatHoldsElevator { SuperAwesomeInt = new OnlyCareAboutValue<int>(3) };
            Json json = JsonHelper.BuildJsonForObject(thing);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            Assert.IsFalse(json.Data.StringValue.Contains("Value"), "We should be elevating out of value, instead it looks like we're showing the whole object.");

            var serializedThing = JsonHelper.BuildObjectForJson<ThingThatHoldsElevator>(json);
            Assert.IsInstanceOfType(serializedThing.SuperAwesomeInt, typeof(OnlyCareAboutValue<int>));
            Assert.AreEqual(3, serializedThing.SuperAwesomeInt.Value);
        }

        [TestMethod]
        public void AJson_JsonBuilding_CanBuildListFromArrays()
        {
            Json json = JsonHelper.ParseText("[ { StringThing: \"Foo\", IntThing: 1}, { StringThing: \"Bar\", IntThing: 2}]");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsArray, "Json should have parsed an array");
            
            List<StringAndInt> result = JsonHelper.BuildObjectListForJson<StringAndInt>((JsonArray)json.Data);
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsNotNull(result);

            Assert.AreEqual("Foo", result[0].StringThing);
            Assert.AreEqual(1, result[0].IntThing);

            Assert.AreEqual("Bar", result[1].StringThing);
            Assert.AreEqual(2, result[1].IntThing);
        }

        [TestMethod]
        public void AJson_JsonBuilding_CanBuildObjectPropertyOfSimpleTypes()
        {
            double pi = 3.14159;
            RuntimeTypeEvaluatorObject obj = new RuntimeTypeEvaluatorObject();
            obj.Obj = pi;

            Json json = JsonHelper.BuildJsonForObject(obj);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());
            var result = JsonHelper.BuildObjectForJson<RuntimeTypeEvaluatorObject>(json);

            Assert.IsTrue(result.Obj is double, "Result is not a double");
            Assert.AreEqual(pi, (double)result.Obj);
        }

        [TestMethod]
        public void AJson_JsonBuilding_CanBuildStruct_FromJson ()
        {
            int expectedFoo = 3;
            string expectedBar = "bar-tho";
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty(nameof(TestStruct.Foo), expectedFoo)
                                .AddProperty(nameof(TestStruct.Bar), expectedBar)
                            .End().Finalize();

            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            TestStruct result = JsonHelper.BuildObjectForJson<TestStruct>(json);
            Assert.AreEqual(expectedFoo, result.Foo);
            Assert.AreEqual(expectedBar, result.Bar);
        }

        [TestMethod]
        public void AJson_JsonBuilding_CanJson_FromStruct ()
        {
            int expectedFoo = 3;
            string expectedBar = "bar-tho";

            TestStruct test = new TestStruct
            {
                Foo = expectedFoo,
                Bar = expectedBar
            };

            Json json = JsonHelper.BuildJsonForObject(test);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());


            Assert.IsTrue(json.Data is JsonDocument);
            var doc = (JsonDocument)json.Data;
            Assert.IsTrue(doc.TryGetValue(nameof(TestStruct.Foo), out int foo));
            Assert.AreEqual(expectedFoo, foo);

            Assert.IsTrue(doc.TryGetValue(nameof(TestStruct.Bar), out string bar));
            Assert.AreEqual(expectedBar, bar);
        }

        public class RuntimeTypeEvaluatorObject
        {
            [JsonRuntimeTypeEval]
            public object Obj { get; set; }
        }

        public class ThingThatHoldsElevator
        {
            public OnlyCareAboutValue<int> SuperAwesomeInt { get; set; }
        }

        [JsonPropertyAsSelf("Value")]
        public class OnlyCareAboutValue<T>
        {
            public OnlyCareAboutValue() { }
            public OnlyCareAboutValue (T value) { this.Value = value; }
            public T Value { get; set; }
        }

        public class ThingWithDerived
        {
            public SimpleGuy Guy { get; set; }
        }

        public class StringAndInt
        {
            public string StringThing { get; set; }
            public int IntThing { get; set; }
        }

        public class SimpleGuy : IEquatable<SimpleGuy>
        {
            public int What { get; set; }
            public int Noice { get; set; }

            public bool Equals(SimpleGuy other)
            {
                return this.What == other.What && this.Noice == other.Noice;
            }
        }

        public class DerivedGuy : SimpleGuy
        {
            public int TheDerivedSpecificProperty { get; set; }

            public DerivedGuy() : base() { }
            public DerivedGuy(int dgsp) : this()
            {
                this.TheDerivedSpecificProperty = dgsp;
            }
        }

        public class OtherThingWithDerived
        {
            public OtherSimpleGuy Guy { get; set; }
        }

        [TypeId("other-simple-guy")]
        public class OtherSimpleGuy
        {
            public int Other { get; set; }
        }

        [TypeId("other-derived-guy (this is the type id)")]
        public class OtherDerivedGuy : OtherSimpleGuy
        {
            public string Special { get; set; } = "special";
        }

        public class ItemsGuy : IEquatable<ItemsGuy>
        {
            public string Name { get; set; }
            public List<int> Items { get; set; }

            public ItemsGuy()
            {
                this.Items = new List<int>();
            }
            public ItemsGuy(string name, params int[] items) : this()
            {
                this.Name = name;
                this.Items.AddRange(items);
            }

            public bool Equals(ItemsGuy other)
            {
                if (this.Name != other.Name)
                {
                    return false;
                }

                if (this.Items.Count != other.Items.Count)
                {
                    return false;
                }

                for(int ind =0; ind < this.Items.Count; ++ind)
                {
                    if(this.Items[ind] != other.Items[ind])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public class ComplexGuy : IEquatable<ComplexGuy>
        {
            public int FavoriteNumber { get; set; }

            public SimpleGuy Kidnapee { get; set; }

            public List<ItemsGuy> SuperItemsy { get; set; }

            public ComplexGuy()
            {
                this.SuperItemsy = new List<ItemsGuy>();
            }

            public static ComplexGuy MakeOne()
            {
                ComplexGuy d = new ComplexGuy { FavoriteNumber = 1234 };
                d.Kidnapee = new SimpleGuy() { Noice = 40, What = -2 };
                d.SuperItemsy.AddRange(new[] {
                    new ItemsGuy("billtina", 1, 2, 3, 4),
                    new ItemsGuy("jocobey", 31, 34),
                    new ItemsGuy("miranda", 1, 43, 24),
                    new ItemsGuy("mike", 0),
                });

                return d;
            }

            public bool Equals(ComplexGuy other)
            {
                if (this.FavoriteNumber != other.FavoriteNumber)
                {
                    return false;
                }

                if (!this.Kidnapee.Equals(other.Kidnapee))
                {
                    return false;
                }

                if (this.SuperItemsy.Count != other.SuperItemsy.Count)
                {
                    return false;
                }

                for (int ind = 0; ind < this.SuperItemsy.Count; ++ind)
                {
                    if (!this.SuperItemsy[ind].Equals(other.SuperItemsy[ind]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public struct TestStruct
        {
            public int Foo { get; set; }
            public string Bar { get; set; }
        }
    }
}
