namespace AJut.UX.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>The UIElementRepeater rules every stack shares: <see cref="RepeaterChildSync{TChild}"/></summary>
    [TestClass]
    public class RepeaterChildSyncTests
    {
        [TestMethod]
        public void SetSource_PlacesTheItemsAheadOfTheContainersOwn ()
        {
            var own = new Child("own");
            var container = new FakeContainer(own);
            var sync = new RepeaterChildSync<Child>(container);
            ObservableCollection<Child> items = Items("a", "b");

            sync.SetSource(items);

            container.AssertChildren(items[0], items[1], own);
        }

        [TestMethod]
        public void Following_EveryKindOfChange_IsFollowed ()
        {
            var container = new FakeContainer();
            var sync = new RepeaterChildSync<Child>(container);
            ObservableCollection<Child> items = Items("a", "b", "c", "d");
            sync.SetSource(items);
            sync.StartFollowing();

            items.Add(new Child("e"));
            items.Insert(1, new Child("f"));
            items.RemoveAt(3);
            items.Move(0, 2);
            items[1] = new Child("g");

            container.AssertChildren(new List<Child>(items).ToArray());
        }

        [TestMethod]
        public void Following_Reset_EmptiesWhatWasPlacedAndLeavesTheContainersOwn ()
        {
            var own = new Child("own");
            var container = new FakeContainer(own);
            var sync = new RepeaterChildSync<Child>(container);
            ObservableCollection<Child> items = Items("a", "b");
            sync.SetSource(items);
            sync.StartFollowing();

            items.Clear();

            container.AssertChildren(own);
            Assert.AreEqual(0, sync.Placed.Count);
        }

        [TestMethod]
        public void NotFollowing_ChangesWaitUntilFollowingStarts ()
        {
            var container = new FakeContainer();
            var sync = new RepeaterChildSync<Child>(container);
            ObservableCollection<Child> items = Items("a");
            sync.SetSource(items);

            items.Add(new Child("b"));
            container.AssertChildren(items[0]);

            sync.StartFollowing();
            container.AssertChildren(items[0], items[1]);
        }

        [TestMethod]
        public void StopFollowing_LeavesNoHandlerOnTheSource ()
        {
            var source = new CountingSource(new Child("a"));
            var sync = new RepeaterChildSync<Child>(new FakeContainer());
            sync.SetSource(source);
            sync.StartFollowing();
            Assert.AreEqual(1, source.HandlerCount, "Following adds one handler");

            sync.StartFollowing();
            Assert.AreEqual(1, source.HandlerCount, "Following again adds no second one");

            sync.StopFollowing();
            Assert.AreEqual(0, source.HandlerCount, "A collection that outlives the repeater must not hold it");
        }

        [TestMethod]
        public void SetSource_NewSource_ReplacesTheOldAndStopsFollowingIt ()
        {
            var container = new FakeContainer();
            var sync = new RepeaterChildSync<Child>(container);
            var first = new CountingSource(new Child("a"), new Child("b"));
            sync.SetSource(first);
            sync.StartFollowing();

            ObservableCollection<Child> second = Items("c");
            sync.SetSource(second);
            second.Add(new Child("d"));

            container.AssertChildren(second[0], second[1]);
            Assert.AreEqual(0, first.HandlerCount, "The old source is no longer followed");
        }

        [TestMethod]
        public void SetSource_SkipsNullsRepeatsAndOtherTypes ()
        {
            var container = new FakeContainer();
            var sync = new RepeaterChildSync<Child>(container);
            var a = new Child("a");
            var b = new Child("b");

            sync.SetSource(new object[] { a, null, "not a child", a, b });

            container.AssertChildren(a, b);
        }

        [TestMethod]
        public void Sync_TouchesOnlyWhatDiffers ()
        {
            var container = new FakeContainer();
            var sync = new RepeaterChildSync<Child>(container);
            ObservableCollection<Child> items = Items("a", "b", "c");
            sync.SetSource(items);
            sync.StartFollowing();
            container.ResetCounts();

            items.Add(new Child("d"));
            Assert.AreEqual(1, container.InsertCount, "One child added, one inserted");
            Assert.AreEqual(0, container.RemoveCount, "Nothing already placed is taken out");

            container.ResetCounts();
            items.RemoveAt(0);
            Assert.AreEqual(0, container.InsertCount, "The rest stay where they are");
            Assert.AreEqual(1, container.RemoveCount);
        }

        [TestMethod]
        public void Sync_AMove_MovesOnlyThatChild ()
        {
            var container = new FakeContainer();
            var sync = new RepeaterChildSync<Child>(container);
            ObservableCollection<Child> items = Items("a", "b", "c", "d");
            sync.SetSource(items);
            sync.StartFollowing();

            container.ResetCounts();
            items.Move(0, 3);
            container.AssertChildren(items[0], items[1], items[2], items[3]);
            Assert.AreEqual(1, container.RemoveCount, "Moving forward takes out only the one moved");
            Assert.AreEqual(1, container.InsertCount, "and puts back only the one moved");

            container.ResetCounts();
            items.Move(3, 0);
            container.AssertChildren(items[0], items[1], items[2], items[3]);
            Assert.AreEqual(1, container.RemoveCount, "Moving back takes out only the one moved");
            Assert.AreEqual(1, container.InsertCount, "and puts back only the one moved");
        }

        [TestMethod]
        public void RemoveAll_ThenSync_MovesTheChildrenToAnotherContainer ()
        {
            var host = new SwitchableHost();
            var sync = new RepeaterChildSync<Child>(host);
            ObservableCollection<Child> items = Items("a", "b");
            sync.SetSource(items);

            FakeContainer first = host.Target;
            sync.RemoveAll();
            host.Target = new FakeContainer();
            sync.Sync();

            first.AssertChildren();
            host.Target.AssertChildren(items[0], items[1]);
        }

        // ===========[ Helper Methods ]========================================
        private static ObservableCollection<Child> Items (params string[] names)
        {
            var items = new ObservableCollection<Child>();
            foreach (string name in names)
            {
                items.Add(new Child(name));
            }

            return items;
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class Child
        {
            public Child (string name)
            {
                this.Name = name;
            }

            public string Name { get; }

            public override string ToString () => this.Name;
        }

        // A container that, like a toolkit's, refuses a child it already has and the removal of one it doesn't
        private sealed class FakeContainer : IRepeaterChildHost<Child>
        {
            private readonly List<Child> m_children = new List<Child>();

            public FakeContainer (params Child[] ownChildren)
            {
                m_children.AddRange(ownChildren);
            }

            public int InsertCount { get; private set; }
            public int RemoveCount { get; private set; }

            public void InsertChild (int index, Child child)
            {
                Assert.IsFalse(m_children.Contains(child), $"{child} is already in the container");
                Assert.IsTrue(index <= m_children.Count, $"{child} inserted at {index}, past the {m_children.Count} children there are");
                m_children.Insert(index, child);
                ++this.InsertCount;
            }

            public void RemoveChild (Child child)
            {
                Assert.IsTrue(m_children.Remove(child), $"{child} is not in the container");
                ++this.RemoveCount;
            }

            public void ResetCounts ()
            {
                this.InsertCount = 0;
                this.RemoveCount = 0;
            }

            public void AssertChildren (params Child[] expected)
            {
                Assert.AreEqual(string.Join(",", (object[])expected), string.Join(",", m_children), "children");
                for (int index = 0; index < expected.Length; ++index)
                {
                    Assert.AreSame(expected[index], m_children[index], $"child {index}");
                }
            }
        }

        // A host that a toolkit points at whichever container it has now
        private sealed class SwitchableHost : IRepeaterChildHost<Child>
        {
            public FakeContainer Target { get; set; } = new FakeContainer();

            public void InsertChild (int index, Child child) => this.Target.InsertChild(index, child);

            public void RemoveChild (Child child) => this.Target.RemoveChild(child);
        }

        // A source that counts the handlers it holds, so a test can see whether it still holds the sync
        private sealed class CountingSource : IEnumerable, INotifyCollectionChanged
        {
            private readonly Child[] m_items;
            private NotifyCollectionChangedEventHandler m_handlers;

            public CountingSource (params Child[] items)
            {
                m_items = items;
            }

            public event NotifyCollectionChangedEventHandler CollectionChanged
            {
                add
                {
                    m_handlers += value;
                    ++this.HandlerCount;
                }
                remove
                {
                    if ((m_handlers != null) && (value != null) && (Array.IndexOf(m_handlers.GetInvocationList(), value) >= 0))
                    {
                        --this.HandlerCount;
                    }

                    m_handlers -= value;
                }
            }

            public int HandlerCount { get; private set; }

            public IEnumerator GetEnumerator () => m_items.GetEnumerator();
        }
    }
}
