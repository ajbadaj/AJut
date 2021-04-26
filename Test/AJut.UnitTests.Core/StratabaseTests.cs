namespace AJut.UnitTests.Core
{
    using AJut.Storage;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    [TestClass]
    public class StratabaseTests
    {
        [TestMethod]
        public void Stratabase_AccessBeforeSet ()
        {
            Stratabase sb = new Stratabase(1);
            var id = Guid.NewGuid();
            StrataPropertyValueAccess<double> pi = sb.GeneratePropertyAccess<double>(id, "Pi");

            sb.SetBaselinePropertyValue(id, "Pi", 2.9);
            Assert.AreEqual(2.9, pi.GetValue());

            sb.SetOverridePropertyValue(0, id, "Pi", 3.14159);
            Assert.AreEqual(3.14159, pi.GetValue());
        }

        [TestMethod]
        public void Stratabase_AccessAfterSet ()
        {
            Stratabase sb = new Stratabase(1);
            var id = Guid.NewGuid();

            sb.SetBaselinePropertyValue(id, "Pi", 2.9);
            sb.SetOverridePropertyValue(0, id, "Pi", 3.14159);

            StrataPropertyValueAccess<double> pi = sb.GeneratePropertyAccess<double>(id, "Pi");
            Assert.AreEqual(3.14159, pi.GetValue());
        }

        [TestMethod]
        public void Stratabase_ClearAndIsSet ()
        {
            Stratabase sb = new Stratabase(1);
            var id = Guid.NewGuid();
            StrataPropertyValueAccess<double> pi = sb.GeneratePropertyAccess<double>(id, "Pi");
            Assert.IsFalse(pi.IsSet);

            sb.SetBaselinePropertyValue(id, "Pi", 2.9);
            Assert.IsTrue(pi.IsSet);
            Assert.AreEqual(2.9, pi.GetValue());

            sb.SetOverridePropertyValue(0, id, "Pi", 3.14159);
            Assert.AreEqual(3.14159, pi.GetValue());

            sb.ClearPropertyOverride(0, id, "Pi");
            Assert.IsTrue(pi.IsSet);
            Assert.AreEqual(2.9, pi.GetValue());

            sb.ClearPropertyBaseline(id, "Pi");
            Assert.IsFalse(pi.IsSet);
        }

        [TestMethod]
        public void Stratabase_ClearAndIsBaselineSet ()
        {
            Stratabase sb = new Stratabase(1);
            var id = Guid.NewGuid();
            StrataPropertyValueAccess<double> pi = sb.GeneratePropertyAccess<double>(id, "Pi");
            Assert.IsFalse(pi.IsBaselineSet);
            Assert.IsFalse(pi.IsSet);

            sb.SetBaselinePropertyValue(id, "Pi", 2.9);
            Assert.IsTrue(pi.IsSet);
            Assert.AreEqual(2.9, pi.GetValue());

            sb.SetOverridePropertyValue(0, id, "Pi", 3.14159);
            Assert.AreEqual(3.14159, pi.GetValue());

            sb.ClearPropertyBaseline(id, "Pi");
            Assert.IsFalse(pi.IsBaselineSet);
            Assert.IsTrue(pi.IsSet);
        }

        [TestMethod]
        public void Stratabase_EventsFire ()
        {
            bool expectingIsBaselineSetChange = false, expectingIsSetChange = false, expectingValueChange = false;
            Stratabase sb = new Stratabase(1);
            var id = Guid.NewGuid();
            StrataPropertyValueAccess<double> pi = sb.GeneratePropertyAccess<double>(id, "Pi");
            pi.IsBaselineSetChanged += _OnIsBaselineSetChanged;
            pi.IsSetChanged += _OnIsSetChanged;
            pi.ValueChanged += _OnValueChanged;

            expectingIsBaselineSetChange = true;
            expectingIsSetChange = true;
            expectingValueChange = true;
            pi.SetBaselineValue(3.14159);
            Assert.IsFalse(expectingIsSetChange);
            Assert.IsFalse(expectingIsBaselineSetChange);
            Assert.IsFalse(expectingValueChange);

            expectingValueChange = true;
            pi.SetOverrideValue(0, 5.0);
            Assert.IsFalse(expectingValueChange);

            expectingIsBaselineSetChange = true;
            pi.ClearBaselineValue();
            Assert.IsFalse(expectingIsBaselineSetChange);

            expectingValueChange = true;
            expectingIsSetChange = true;
            pi.ClearOverrideValue(0);
            Assert.IsFalse(expectingIsSetChange);
            Assert.IsFalse(expectingValueChange);

            void _OnIsBaselineSetChanged (object sender, EventArgs e)
            {
                Assert.IsTrue(expectingIsBaselineSetChange);
                expectingIsBaselineSetChange = false;
            }

            void _OnIsSetChanged (object sender, EventArgs e)
            {
                Assert.IsTrue(expectingIsSetChange);
                expectingIsSetChange = false;
            }

            void _OnValueChanged (object sender, EventArgs e)
            {
                Assert.IsTrue(expectingValueChange);
                expectingValueChange = false;
            }
        }

        [TestMethod]
        public void Stratabase_SetData_Simple ()
        {
            var data = new TestData
            {
                Name = "Bob",
                Value = 2
            };

            Stratabase sb = new Stratabase(1);
            sb.SetBaselineFromPropertiesOf(data.Id, data);

            var name = sb.GeneratePropertyAccess<string>(data.Id, nameof(TestData.Name));
            var value = sb.GeneratePropertyAccess<int>(data.Id, nameof(TestData.Value));

            Assert.AreEqual(data.Name, name.GetValue());
            Assert.AreEqual(data.Value, value.GetValue());
        }

        [TestMethod]
        public void Stratabase_SetData_WithLists ()
        {
            var data = new TestDataWithList
            {
                Name = "Bob",
                Value = 2
            };

            data.ChildList.Add(new TestData
            {
                Name = "Mary",
                Value = 6
            });

            data.ChildList.Add(new TestData
            {
                Name = "Gillian",
                Value = 8
            });

            Stratabase sb = new Stratabase(1);
            sb.SetBaselineFromPropertiesOf(data.Id, data);

            var name = sb.GeneratePropertyAccess<string>(data.Id, nameof(TestDataWithList.Name));
            var value = sb.GeneratePropertyAccess<int>(data.Id, nameof(TestDataWithList.Value));
            var childList = sb.GenerateListPropertyAccess<TestData>(data.Id, nameof(TestDataWithList.ChildList));

            Assert.AreEqual(data.Name, name.GetValue());
            Assert.AreEqual(data.Value, value.GetValue());
            Assert.AreEqual(data.ChildList.Count, childList.GetCount());

            Assert.AreEqual(data.ChildList[0], childList.Elements[0]);
            Assert.AreEqual(data.ChildList[1], childList.Elements[1]);
        }


        [TestMethod]
        public void Stratabase_SetData_MultiDot_Test ()
        {
            MultiDotThing1 thing = new MultiDotThing1
            {
                Name = "Test",
                Other = new MultiDotThing2
                {
                    Value = 3,
                    Foo = new MultiDotThing3
                    {
                        Bar = 3.14159
                    }
                }
            };

            Guid id = Guid.NewGuid();

            Stratabase sb = new Stratabase(1);
            sb.SetBaselineFromPropertiesOf(id, thing);

            var name = sb.GeneratePropertyAccess<string>(id, "Name");
            var intValue = sb.GeneratePropertyAccess<int>(id, "Other.Value");
            var doubleValue = sb.GeneratePropertyAccess<double>(id, "Other.Foo.Bar");

            Assert.AreEqual(thing.Name, name.GetValue());
            Assert.AreEqual(thing.Other.Value, intValue.GetValue());
            Assert.AreEqual(thing.Other.Foo.Bar, doubleValue.GetValue());
        }

        [TestMethod]
        public void Stratabase_SetData_MultiDot_WithListTest ()
        {
            MultiDotThing1 thing = new MultiDotThing1
            {
                Other = new MultiDotThing2
                {
                    IndexedItems = new List<int>(new[] { 10, 11, 12, 13 }),
                }
            };

            Guid id = Guid.NewGuid();

            Stratabase sb = new Stratabase(1);
            sb.SetBaselineFromPropertiesOf(id, thing);

            var intValues = new[]{
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[0]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[1]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[2]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[3]"),
            };

            Assert.AreEqual(thing.Other.IndexedItems[0], intValues[0].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[1], intValues[1].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[2], intValues[2].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[3], intValues[3].GetValue());
        }


        [TestMethod]
        public void Stratabase_List_AddRemove_AccessCreatedLaterTracksCountProperly ()
        {
            const string kPropertyName = "List";
            Stratabase sb = new Stratabase(0);
            var id = Guid.NewGuid();
            var adder = sb.GenerateListPropertyAccess<char>(id, kPropertyName);
            Assert.AreEqual(0, adder.GetCount());
            adder.CreateAdd('A', 'B', 'C', 'D').StoreInBaseline();
            Assert.AreEqual(4, adder.GetCount());

            var list = sb.GenerateListPropertyAccess<char>(id, kPropertyName);
            Assert.AreEqual(4, list.GetCount());

            adder.CreateAdd('E').StoreInBaseline();
            Assert.AreEqual(5, list.GetCount());
        }

        [TestMethod]
        public void Stratabase_List_AddRemoveBaseline ()
        {
            const string kPropertyName = "List";
            Stratabase sb = new Stratabase(0);
            var id = Guid.NewGuid();
            var list = sb.GenerateListPropertyAccess<char>(id, kPropertyName);
            Assert.AreEqual(0, list.GetCount());

            list.CreateAdd('A', 'B', 'C', 'D').StoreInBaseline();
            Assert.AreEqual(4, list.GetCount());

            Assert.AreEqual('A', list.GetElementAt(0));
            Assert.AreEqual('B', list.GetElementAt(1));
            Assert.AreEqual('C', list.GetElementAt(2));
            Assert.AreEqual('D', list.GetElementAt(3));

            // Add to baseline
            list.CreateAdd('E').StoreInBaseline();

            Assert.AreEqual(5, list.GetCount());
            Assert.AreEqual('A', list.GetElementAt(0));
            Assert.AreEqual('B', list.GetElementAt(1));
            Assert.AreEqual('C', list.GetElementAt(2));
            Assert.AreEqual('D', list.GetElementAt(3));
            Assert.AreEqual('E', list.GetElementAt(4));

            // Insert into baseline
            list.CreateInsert(2, 'X').StoreInBaseline();

            Assert.AreEqual(6, list.GetCount());
            Assert.AreEqual('A', list.GetElementAt(0));
            Assert.AreEqual('B', list.GetElementAt(1));
            Assert.AreEqual('X', list.GetElementAt(2));
            Assert.AreEqual('C', list.GetElementAt(3));
            Assert.AreEqual('D', list.GetElementAt(4));
            Assert.AreEqual('E', list.GetElementAt(5));

            // Remove from baseline
            list.RemoveAt(2);
            Assert.AreEqual(5, list.GetCount());

            Assert.AreEqual('A', list.GetElementAt(0));
            Assert.AreEqual('B', list.GetElementAt(1));
            Assert.AreEqual('C', list.GetElementAt(2));
            Assert.AreEqual('D', list.GetElementAt(3));
            Assert.AreEqual('E', list.GetElementAt(4));
        }

        [TestMethod]
        public void Stratabase_List_AddRemoveBaselineAndOverride ()
        {
            const string kPropertyName = "List";
            Stratabase sb = new Stratabase(2);
            var id = Guid.NewGuid();
            var list = sb.GenerateListPropertyAccess<char>(id, kPropertyName);
            Assert.AreEqual(0, list.GetCount());

            list.CreateAdd('A', 'B', 'C', 'D').StoreInBaseline();
            Assert.AreEqual(4, list.GetCount());

            Assert.AreEqual('A', list.GetElementAt(0));
            Assert.AreEqual('B', list.GetElementAt(1));
            Assert.AreEqual('C', list.GetElementAt(2));
            Assert.AreEqual('D', list.GetElementAt(3));

            // Add to baseline
            list.CreateAdd('E').StoreInBaseline();

            Assert.AreEqual(5, list.GetCount());
            Assert.AreEqual('A', list.GetElementAt(0));
            Assert.AreEqual('B', list.GetElementAt(1));
            Assert.AreEqual('C', list.GetElementAt(2));
            Assert.AreEqual('D', list.GetElementAt(3));
            Assert.AreEqual('E', list.GetElementAt(4));

            // Insert into baseline
            list.CreateInsert(2, 'X').StoreInOverrideLayer(1);

            Assert.AreEqual(6, list.GetCount());
            Assert.AreEqual('A', list.GetElementAt(0));
            Assert.AreEqual('B', list.GetElementAt(1));
            Assert.AreEqual('X', list.GetElementAt(2));
            Assert.AreEqual('C', list.GetElementAt(3));
            Assert.AreEqual('D', list.GetElementAt(4));
            Assert.AreEqual('E', list.GetElementAt(5));

            // Remove from baseline
            Assert.IsTrue(list.RemoveAt(2));
            Assert.AreEqual(5, list.GetCount());

            Assert.AreEqual('A', list.GetElementAt(0));
            Assert.AreEqual('B', list.GetElementAt(1));
            Assert.AreEqual('C', list.GetElementAt(2));
            Assert.AreEqual('D', list.GetElementAt(3));
            Assert.AreEqual('E', list.GetElementAt(4));
        }

        [TestMethod]
        public void Stratabase_10000Layers ()
        {
            const int kTestCount = 10000;
            const string kPropName = "data";
            Stratabase sb = new Stratabase(kTestCount);
            var id = Guid.NewGuid();

            int value = -1;
            sb.SetBaselinePropertyValue(id, kPropName, ++value);
            while (value < kTestCount)
            {
                sb.SetOverridePropertyValue(value++, id, kPropName, value);
            }

            var data = sb.GeneratePropertyAccess<int>(id, kPropName);
            Assert.AreEqual(value, data.GetValue());
        }

        [TestMethod]
        public void Stratabase_ToFromJson ()
        {
            MultiDotThing1 thing = new MultiDotThing1
            {
                Name = "CrazyTest",
                Other = new MultiDotThing2
                {
                    IndexedItems = new List<int>(new[] { 10, 11, 12, 13 }),
                },
            };

            Guid id = Guid.NewGuid();

            Stratabase sb = new Stratabase(3);
            sb.SetBaselineFromPropertiesOf(id, thing);
            sb.SetOverridePropertyValue(2, id, "Name", "SettingSomethingElse");

            var nameAccess = sb.GeneratePropertyAccess<string>(id, "Name");
            var intValues = new[]{
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[0]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[1]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[2]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[3]"),
            };

            var foo = new MultiDotThing3() { Bar = 5.5555 };
            var fooAccess = sb.GeneratePropertyAccess<MultiDotThing3>(id, "Other.Foo");
            fooAccess.SetBaselineValue(foo);

            Assert.AreEqual("SettingSomethingElse", nameAccess.GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[0], intValues[0].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[1], intValues[1].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[2], intValues[2].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[3], intValues[3].GetValue());
            Assert.AreEqual(foo, fooAccess.GetValue());

            var json = sb.SerializeToJson();
            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());

            Stratabase sb2 = Stratabase.DeserializeFromJson(json);
            Assert.AreEqual(3, sb2.OverrideLayerCount);

            var nameAccess2 = sb2.GeneratePropertyAccess<string>(id, "Name");
            var intValues2 = new[]{
                sb2.GeneratePropertyAccess<int>(id, "Other.IndexedItems[0]"),
                sb2.GeneratePropertyAccess<int>(id, "Other.IndexedItems[1]"),
                sb2.GeneratePropertyAccess<int>(id, "Other.IndexedItems[2]"),
                sb2.GeneratePropertyAccess<int>(id, "Other.IndexedItems[3]"),
            };
            var fooAccess2 = sb2.GeneratePropertyAccess<MultiDotThing3>(id, "Other.Foo");

            Assert.AreEqual("SettingSomethingElse", nameAccess2.GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[0], intValues2[0].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[1], intValues2[1].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[2], intValues2[2].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[3], intValues2[3].GetValue());
            Assert.AreEqual(foo.Bar, fooAccess2.GetValue().Bar);

            nameAccess2.ClearOverrideValue(2);
            Assert.AreEqual(thing.Name, nameAccess2.GetValue());
        }

        [TestMethod]
        public void Stratabase_ToFromJson_Filtered ()
        {
            MultiDotThing1 thing = new MultiDotThing1
            {
                Name = "CrazyTest",
                Other = new MultiDotThing2
                {
                    IndexedItems = new List<int>(new[] { 10, 11, 12, 13 }),
                },
            };

            Guid id = Guid.NewGuid();

            Stratabase sb = new Stratabase(3);
            sb.SetBaselineFromPropertiesOf(id, thing);
            sb.SetOverridePropertyValue(2, id, "Name", "SettingSomethingElse");

            var nameAccess = sb.GeneratePropertyAccess<string>(id, "Name");
            var intValues = new[]{
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[0]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[1]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[2]"),
                sb.GeneratePropertyAccess<int>(id, "Other.IndexedItems[3]"),
            };

            var foo = new MultiDotThing3() { Bar = 5.5555 };
            var fooAccess = sb.GeneratePropertyAccess<MultiDotThing3>(id, "Other.Foo");
            fooAccess.SetBaselineValue(foo);

            Assert.AreEqual("SettingSomethingElse", nameAccess.GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[0], intValues[0].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[1], intValues[1].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[2], intValues[2].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[3], intValues[3].GetValue());
            Assert.AreEqual(foo, fooAccess.GetValue());

            var json = sb.SerializeToJson(includeBaseline: true);
            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());

            Stratabase sb2 = Stratabase.DeserializeFromJson(json);
            Assert.AreEqual(3, sb2.OverrideLayerCount);

            var nameAccess2 = sb2.GeneratePropertyAccess<string>(id, "Name");
            var intValues2 = new[]{
                sb2.GeneratePropertyAccess<int>(id, "Other.IndexedItems[0]"),
                sb2.GeneratePropertyAccess<int>(id, "Other.IndexedItems[1]"),
                sb2.GeneratePropertyAccess<int>(id, "Other.IndexedItems[2]"),
                sb2.GeneratePropertyAccess<int>(id, "Other.IndexedItems[3]"),
            };
            var fooAccess2 = sb2.GeneratePropertyAccess<MultiDotThing3>(id, "Other.Foo");

            // Not "SettingSomethingElse" because layer [2] was not included in the serialization
            Assert.AreEqual("CrazyTest", nameAccess2.GetValue());

            Assert.AreEqual(thing.Other.IndexedItems[0], intValues2[0].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[1], intValues2[1].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[2], intValues2[2].GetValue());
            Assert.AreEqual(thing.Other.IndexedItems[3], intValues2[3].GetValue());
            Assert.AreEqual(foo.Bar, fooAccess2.GetValue().Bar);

            nameAccess2.ClearOverrideValue(2);
            Assert.AreEqual(thing.Name, nameAccess2.GetValue());
        }

        [TestMethod]
        public void Stratabase_ToFromObject ()
        {
            TestDataWithList data = new TestDataWithList
            {
                Name = "Test #1",
                Value = 20,
            };
            data.ChildList.Add(new TestData
            {
                Name = "Child",
                Value = 6
            });

            Stratabase sb = new Stratabase(3);
            sb.SetBaselineFromPropertiesOf(data);

            TestDataWithList found = new TestDataWithList();
            sb.SetObjectWithProperties(data.Id, ref found);

            Assert.AreEqual(data.Name, found.Name);
            Assert.AreEqual(data.Value, found.Value);
            Assert.AreEqual(1, data.ChildList?.Count ?? 0);
            Assert.AreEqual(1, found.ChildList?.Count ?? 0);
            Assert.AreEqual(data.ChildList[0].Name, found.ChildList[0].Name);
            Assert.AreEqual(data.ChildList[0].Value, found.ChildList[0].Value);

            sb.SetOverridePropertyValue(2, data.Id, "Name", "Test #4");

            found = new TestDataWithList();
            sb.SetObjectWithProperties(data.Id, ref found);
            Assert.AreEqual("Test #4", found.Name);
        }

        [TestMethod]
        public void Stratabase_PropertyDisposal ()
        {
            Stratabase sb = new Stratabase(1);
            TestModel test = new TestModel(Guid.NewGuid(), sb);

            var singleItem = new DisposeTester();
            DisposeTester[] items = new DisposeTester[2] { new DisposeTester(), new DisposeTester() };

            test.SingleItem = singleItem;
            test.AddToList(items[0]);
            test.AddToList(items[1]);

            Assert.IsTrue(test.SingleItem != null);
            Assert.IsTrue(test.ItemList != null);

            test.Dispose();

            Assert.AreEqual(1, singleItem.DisposeCallCount);
            Assert.AreEqual(1, items[0].DisposeCallCount);
            Assert.AreEqual(1, items[1].DisposeCallCount);
        }

        [TestMethod]
        public void Stratabase_NewUnsetAccess_DoesNotGiveIsSetTrue ()
        {
            Stratabase sb = new Stratabase(1);

            // First check with an id that was not already present
            Guid id = Guid.NewGuid();
            var access = sb.GeneratePropertyAccess<int>(id, "ThisDoesNotExist");
            Assert.IsFalse(access.IsBaselineSet);
            Assert.IsFalse(access.IsSet);
            access.Dispose();

            // Next check with an id that has data present
            id = Guid.NewGuid();
            sb.SetBaselinePropertyValue(id, "Test", 28);
            access = sb.GeneratePropertyAccess<int>(id, "ThisDoesNotExist");
            Assert.IsFalse(access.IsBaselineSet);
            Assert.IsFalse(access.IsSet);
        }
        private class TestModel : StratabaseBackedModel, IDisposable
        {
            private Property<DisposeTester> m_singleItem;
            private ListProperty<DisposeTester> m_itemList;


            public TestModel (Guid id, Stratabase sb) : base(id, sb)
            {
                m_singleItem = this.GenerateProperty<DisposeTester>(nameof(SingleItem));
                m_itemList = this.GenerateListProperty<DisposeTester>("");
            }

            public DisposeTester SingleItem
            {
                get => m_singleItem.Value;
                set => m_singleItem.Access.SetBaselineValue(value);
            }

            public ReadOnlyObservableCollection<DisposeTester> ItemList => m_itemList.Access.Elements;
            public void AddToList (DisposeTester item)
            {
                m_itemList.Access.CreateAdd(item).StoreInBaseline();
            }

            public void Dispose ()
            {
                m_singleItem.Dispose();
                m_singleItem = null;

                m_itemList.Dispose();
                m_itemList = null;
            }
        }

        public class DisposeTester : IDisposable
        {
            public int DisposeCallCount { get; private set; }
            public void Dispose ()
            {
                ++this.DisposeCallCount;
            }
        }

        [TestMethod]
        public void Stratabase_ClearAll ()
        {
            Stratabase sb = new Stratabase(2);
            Guid testId = Guid.NewGuid();

            var test0 = sb.GeneratePropertyAccess<int>(testId, "Test0");
            var test1 = sb.GeneratePropertyAccess<int>(testId, "Test1");
            var test2 = sb.GeneratePropertyAccess<int>(testId, "Test2");

            test0.SetBaselineValue(0);
            test1.SetBaselineValue(1);
            test2.SetBaselineValue(2);

            test0.SetOverrideValue(0, 10);
            test1.SetOverrideValue(0, 20);
            test2.SetOverrideValue(0, 30);

            test1.SetOverrideValue(1, 40);

            Assert.AreEqual(10, test0.GetValue());
            Assert.AreEqual(40, test1.GetValue());
            Assert.AreEqual(30, test2.GetValue());

            Guid otherId = Guid.NewGuid();
            var other0 = sb.GeneratePropertyAccess<int>(otherId, "Other0");
            other0.SetBaselineValue(100);
            other0.SetOverrideValue(0, 200);
            other0.SetOverrideValue(1, 300);

            Assert.AreEqual(300, other0.GetValue());

            sb.ClearAll();
            Assert.IsFalse(test0.IsSet);
            Assert.IsFalse(test1.IsSet);
            Assert.IsFalse(test2.IsSet);
            Assert.IsFalse(other0.IsSet);
        }

        [TestMethod]
        public void Stratabase_ClearAllFor ()
        {
            Stratabase sb = new Stratabase(2);
            Guid testId = Guid.NewGuid();

            var test0 = sb.GeneratePropertyAccess<int>(testId, "Test0");
            var test1 = sb.GeneratePropertyAccess<int>(testId, "Test1");
            var test2 = sb.GeneratePropertyAccess<int>(testId, "Test2");

            test0.SetBaselineValue(0);
            test1.SetBaselineValue(1);
            test2.SetBaselineValue(2);

            test0.SetOverrideValue(0, 10);
            test1.SetOverrideValue(0, 20);
            test2.SetOverrideValue(0, 30);

            test1.SetOverrideValue(1, 40);

            Assert.AreEqual(10, test0.GetValue());
            Assert.AreEqual(40, test1.GetValue());
            Assert.AreEqual(30, test2.GetValue());

            Guid otherId = Guid.NewGuid();
            var other0 = sb.GeneratePropertyAccess<int>(otherId, "Other0");
            other0.SetBaselineValue(100);
            other0.SetOverrideValue(0, 200);
            other0.SetOverrideValue(1, 300);

            Assert.AreEqual(300, other0.GetValue());

            sb.ClearAllFor(testId);
            Assert.IsFalse(test0.IsSet);
            Assert.IsFalse(test1.IsSet);
            Assert.IsFalse(test2.IsSet);

            Assert.IsTrue(other0.IsSet);
            Assert.AreEqual(300, other0.GetValue());
        }

        [TestMethod]
        public void Stratabase_ChangeEvents ()
        {
            Guid? changedItemId = null;

            string changedProp = null;
            object changedData = null;
            bool? changeWasRemoval = null;
            int? changedLayer = null;
            bool? changeWasBaseline = null;

            Stratabase sb = new Stratabase(1);
            sb.BaselineDataChanged += _BaselineDataChanged;
            sb.OverrideDataChanged += _OverrideDataChanged;

            Guid item = Guid.NewGuid();
            sb.SetBaselinePropertyValue(item, "Test", 1);
            _AssertBaselineMatch(item, "Test", false, 1);

            sb.ClearPropertyBaseline(item, "Test");
            _AssertBaselineMatch(item, "Test", true);

            sb.SetOverridePropertyValue(0, item, "Test", 2);
            _AssertOverrideMatch(0, item, "Test", false, 2);

            sb.ClearPropertyOverride(0, item, "Test");
            _AssertOverrideMatch(0, item, "Test", true);

            // Test End
            sb.BaselineDataChanged -= _BaselineDataChanged;
            sb.OverrideDataChanged -= _OverrideDataChanged;

            void _AssertBaselineMatch (Guid id, string prop, bool wasRemoval, object data = null)
            {
                _AssertDoesMatch(id, prop, data, wasRemoval);
            }

            void _AssertOverrideMatch (int layer, Guid id, string prop, bool wasRemoval, object data = null)
            {
                _AssertDoesMatch(id, prop, data, wasRemoval, layer);
            }

            void _AssertDoesMatch (Guid expectedId, string expectedProp, object expectedData, bool expectedWasRemoval, int? expectedOverrideLayer = null)
            {
                bool expectedIsBaseline = !expectedOverrideLayer.HasValue;
                Assert.IsTrue(changeWasBaseline.HasValue);
                Assert.AreEqual(expectedIsBaseline, changeWasBaseline.Value);

                Assert.IsTrue(changedItemId.HasValue);
                Assert.AreEqual(expectedId, changedItemId.Value);

                Assert.AreEqual(expectedProp, changedProp);
                Assert.AreEqual(expectedData, changedData);

                Assert.IsTrue(changeWasRemoval.HasValue);
                Assert.AreEqual(expectedWasRemoval, changeWasRemoval.Value);

                if (!expectedIsBaseline)
                {
                    Assert.IsTrue(changedLayer.HasValue);
                    Assert.AreEqual(expectedOverrideLayer.Value, changedLayer.Value);
                }


                changedItemId = null;
                changedProp = null;
                changedData = null;
                changeWasBaseline = null;
                changedLayer = null;
            }


            void _BaselineDataChanged (object sender, BaselineStratumModificationEventArgs e)
            {
                changeWasBaseline = true;
                changedItemId = e.ItemId;
                changedProp = e.PropertyName;
                changedData = e.NewData;
                changeWasRemoval = e.WasPropertyRemoved;
            }

            void _OverrideDataChanged (object sender, OverrideStratumModificationEventArgs e)
            {
                changeWasBaseline = false;
                changedItemId = e.ItemId;
                changedProp = e.PropertyName;
                changedData = e.NewData;
                changedLayer = e.LayerIndex;
                changeWasRemoval = e.WasPropertyRemoved;
            }
        }

        [TestMethod]
        public void Stratabase_ReferenceLists_TreeTest ()
        {
            Kernal32_Timer timer = new Kernal32_Timer();

            /*      Root
             *      /|\
             *     / | \
             *    A  B  C
             *       |
             *       D
             * */
            timer.Start();
            TreeItem R = new TreeItem { Name = "Root" };
            TreeItem A = new TreeItem { Name = "A" };
            TreeItem B = new TreeItem { Name = "B" };
            TreeItem C = new TreeItem { Name = "C" };
            TreeItem D = new TreeItem { Name = "D" };
            R.Children.Add(A);
            R.Children.Add(C);

            B.Children.Add(D);
            double setup = timer.Stop();

            timer.Stop();
            Stratabase sb = new Stratabase(1);
            sb.SetBaselineFromPropertiesOf(R);
            double sballocs = timer.Stop();

            timer.Start();
            StrataTreeItem r = new StrataTreeItem(sb, R.Id);
            StrataTreeItem a = r.Children[0];
            StrataTreeItem b = r.InsertItemIntoBaseline(1, B);
            StrataTreeItem c = r.Children[2];
            StrataTreeItem d = b.Children[0];
            double reftree = timer.Stop();


            Assert.AreEqual(R.Name, r.Name);
            Assert.AreEqual(A.Name, a.Name);
            Assert.AreEqual(B.Name, b.Name);
            Assert.AreEqual(C.Name, c.Name);
            Assert.AreEqual(D.Name, d.Name);

#if false // Speed test & output
            timer.Start();
            const int kAutomated = 100;
            for (int i = 0; i < kAutomated; ++i)
            {
                c.InsertIntoBaseline(i, new TreeItem { Name = $"Automated #{i}" });
            }
            double followupGeneration = timer.Stop();

            Console.WriteLine($"Setup took {setup * 1000} miliseconds");
            Console.WriteLine($"Stratabase allocs took {sballocs * 1000} miliseconds");
            Console.WriteLine($"Ref tree allocs took {reftree * 1000} miliseconds");
            Console.WriteLine($"Followup ref generation of {kAutomated} took {followupGeneration * 1000} miliseconds");
#endif
        }

        [TestMethod]
        public void Stratabase_PropertyReference ()
        {
            TestDataContainer tdc = new TestDataContainer
            {
                StrValue = "TDC",
                Foo = new TestData
                {
                    Name = "TDC: Child",
                    Value = 4
                }
            };

            Stratabase sb = new Stratabase(2);
            sb.SetBaselineFromPropertiesOf(tdc);

            var strValueAccess = sb.GeneratePropertyAccess<string>(tdc.Id, nameof(TestDataContainer.StrValue));

            var fooRef = sb.GeneratePropertyAccess<Guid>(tdc.Id, nameof(TestDataContainer.Foo));
            StrataPropertyAdapter<Guid, StrataTestData> fooAdapter = new StrataPropertyAdapter<Guid, StrataTestData>(fooRef, (_sb, _id) => new StrataTestData(_sb, _id));
            var foo = fooAdapter.Value;

            Assert.AreEqual("TDC", strValueAccess.GetValue());
            Assert.AreEqual("TDC: Child", foo.Name);
            Assert.AreEqual(4, foo.Value);
        }

        [TestMethod]
        public void Stratabase_TESTFAIL_Arrays ()
        {
            ThingWithArr arrThing = new ThingWithArr();
            arrThing.Ints = new int[] { 1, 2, 3 };
            Stratabase sb = new Stratabase(2);

            Guid thing = Guid.NewGuid();
            sb.SetBaselineFromPropertiesOf(thing, arrThing);

            var intsAccess = sb.GeneratePropertyAccess<int[]>(thing, nameof(ThingWithArr.Ints));
            Assert.IsNotNull(intsAccess.GetValue());
            Assert.AreEqual(3, intsAccess.GetValue().Length);
            Assert.AreEqual(1, intsAccess.GetValue()[0]);
            Assert.AreEqual(2, intsAccess.GetValue()[1]);
            Assert.AreEqual(3, intsAccess.GetValue()[2]);
        }

        [TestMethod]
        public void Stratabase_TESTFAIL_InheritedData ()
        {
            Base v = new GrandChild
            {
                Id = Guid.NewGuid(),
                IntValue = 5,
                StringValue = "55",
                BoolValue = true,
            };

            Stratabase sb = new Stratabase(2);
            sb.SetBaselineFromPropertiesOf(v);

            GrandChild gc = new GrandChild { Id = v.Id };
            sb.SetObjectWithProperties(ref gc);

            Assert.AreEqual(((GrandChild)v).IntValue, gc.IntValue);
            Assert.AreEqual(((GrandChild)v).StringValue, gc.StringValue);
            Assert.AreEqual(((GrandChild)v).BoolValue, gc.BoolValue);
        }

        [TestMethod]
        public void Stratabase_REGRESSION_CertainTypesFailing()
        {
            var data = new StrataTestTypeFailures();
            Stratabase sb = new Stratabase(1);
            sb.SetBaselineFromPropertiesOf(data);
        }

        [TestMethod]
        public void Stratabase_REGRESSION_SetObjectWithStructValuesDoesNotWork ()
        {
            var data = new StrataTestDataWithStructies();
            Stratabase sb = new Stratabase(1);
            sb.SetBaselineFromPropertiesOf(data);

            string wholeStructProp = nameof(StrataTestDataWithStructies.NormalStructPoint);

            List<string> allProps = sb.GetAllBaselinePropertiesFor(data.Id).ToList();
            Assert.IsTrue(allProps.Contains(wholeStructProp), $"Missing property: '{wholeStructProp}'");
            Assert.IsTrue(allProps.Contains(_DotPt("X")), $"Missing property: '{_DotPt("X")}'");
            Assert.IsTrue(allProps.Contains(_DotPt("Y")), $"Missing property: '{_DotPt("Y")}'");

            var x = sb.GeneratePropertyAccess<double>(data.Id, _DotPt("X"));
            x.SetBaselineValue(83.0);

            var structAccess = sb.GeneratePropertyAccess<FakePointStructyThing>(data.Id, wholeStructProp);
            structAccess.SetOverrideValue(0, new FakePointStructyThing(1.1, 1.1));

            var output = new StrataTestDataWithStructies { Id = data.Id };
            sb.SetObjectWithProperties(ref output);

            Assert.AreEqual(83.0, output.DotStorePoint.X);
            Assert.AreEqual(1.1, output.NormalStructPoint.Y);

            string _DotPt (string part) => $"{nameof(StrataTestDataWithStructies.DotStorePoint)}.{part}";
        }


        public class Base
        {
            [StratabaseId]
            public Guid Id { get; set; }
            public int IntValue { get; set; }
        }

        public class Child : Base
        {
            public string StringValue { get; set; }
        }

        public class GrandChild : Child
        {
            public bool BoolValue { get; set; }
        }

        public class ThingWithArr
        {
            [StrataListConfig(eStrataListConfig.StoreListDirectly)]
            public int[] Ints { get; set; }
        }


        public class TestData
        {
            public TestData ()
            {
                this.Id = Guid.NewGuid();
            }
            public TestData (Guid id)
            {
                this.Id = id;
            }

            [StratabaseId]
            public Guid Id { get; }
            public string Name { get; set; }
            public int Value { get; set; }
        }

        public class TestDataWithList : TestData
        {
            public TestDataWithList () : base() { }
            public TestDataWithList (Guid id) : base(id) { }

            [StrataListConfig(eStrataListConfig.GenerateInsertOverrides)]
            public List<TestData> ChildList { get; set; } = new List<TestData>();
        }

        public class MultiDotThing1
        {
            public string Name { get; set; }
            public MultiDotThing2 Other { get; set; }
        }

        public class MultiDotThing2
        {
            public int Value { get; set; }

            [StrataListConfig(eStrataListConfig.GenerateIndexedSubProperties)]
            public List<int> IndexedItems { get; set; }

            public MultiDotThing3 Foo { get; set; }
        }

        public class MultiDotThing3
        {
            public double Bar { get; set; }

            static MultiDotThing3 ()
            {
                JsonHelper.RegisterTypeId("TEST_MultiDotThing3", typeof(MultiDotThing3));
            }
        }

        public class TreeItem
        {
            [StratabaseId]
            public Guid Id { get; } = Guid.NewGuid();

            public string Name { get; set; }

            [StrataListConfig(eStrataListConfig.GenerateInsertOverrides, buildReferenceList: true)]
            public List<TreeItem> Children { get; set; } = new List<TreeItem>();
        }

        public class StrataTreeItem : NotifyPropertyChanged
        {
            private readonly Stratabase m_sb;
            private readonly StrataPropertyValueAccess<string> m_name;
            private readonly StrataPropertyListAccess<Guid> m_childrenIds;
            private readonly StrataListPropertyAdapter<Guid, StrataTreeItem> m_children;

            public StrataTreeItem (Stratabase sb, Guid id)
            {
                this.Id = id;
                m_sb = sb;
                m_name = sb.GeneratePropertyAccess<string>(id, nameof(TreeItem.Name));
                m_name.ValueChanged += (o, e) => this.RaisePropertyChanged(nameof(Name));

                m_childrenIds = sb.GenerateListPropertyAccess<Guid>(id, nameof(TreeItem.Children));
                m_children = new StrataListPropertyAdapter<Guid, StrataTreeItem>(m_childrenIds, _GenerateItem);

                StrataTreeItem _GenerateItem (Stratabase _sb, Guid _id)
                {
                    return new StrataTreeItem(_sb, _id);
                }
            }

            public Guid Id { get; }
            public string Name
            {
                get => m_name.GetValue();
                set => m_name.SetBaselineValue(value);
            }

            public ReadOnlyObservableCollection<StrataTreeItem> Children => m_children.Elements;

            public StrataTreeItem InsertItemIntoBaseline (int index, TreeItem item)
            {
                if (!m_sb.Contains(item.Id) && !m_sb.SetBaselineFromPropertiesOf(item))
                {
                    return null;
                }

                m_childrenIds.CreateInsert(index, item.Id).StoreInBaseline();
                return this.Children[index];
            }

            public bool Remove (StrataTreeItem child)
            {
                return this.RemoveAt(this.Children.IndexOf(child));
            }

            public bool RemoveAt (int index)
            {
                if (index < 0 || index >= this.Children.Count)
                {
                    return false;
                }

                return m_childrenIds.RemoveAt(index);
            }
        }

        public class TestDataContainer
        {
            [StratabaseId]
            public Guid Id { get; } = Guid.NewGuid();

            public string StrValue { get; set; }

            [StratabaseReference]
            public TestData Foo { get; set; }
        }

        public class StrataTestData
        {
            StrataPropertyValueAccess<string> m_name;
            StrataPropertyValueAccess<int> m_value;

            public StrataTestData (Stratabase sb, Guid id)
            {
                m_name = sb.GeneratePropertyAccess<string>(id, nameof(TestData.Name));
                m_value = sb.GeneratePropertyAccess<int>(id, nameof(TestData.Value));
            }

            public string Name => m_name.GetValue();
            public int Value => m_value.GetValue();
        }

        public class StrataTestTypeFailures
        {
            [StratabaseId]
            public Guid Id { get; } = Guid.NewGuid();
            public DateTime DT { get; } = DateTime.Now;
        }

        public struct FakePointStructyThing
        {
            public double X { get; set; }
            public double Y { get; set; }

            public FakePointStructyThing (double x = 0.0, double y = 0.0)
            {
                this.X = x;
                this.Y = y;
            }
        }

        public class StrataTestDataWithStructies
        {
            [StratabaseId]
            public Guid Id { get;  init; } = Guid.NewGuid();
            public FakePointStructyThing NormalStructPoint { get; set; } = new FakePointStructyThing { X = 3.0, Y = 5.0 };
            
            [StrataStoreAsDotElements]
            public FakePointStructyThing DotStorePoint { get; set; } = new FakePointStructyThing { X = -3.0, Y = -5.0 };
        }
    }
}
