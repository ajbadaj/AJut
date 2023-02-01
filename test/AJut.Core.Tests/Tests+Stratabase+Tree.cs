namespace AJut.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using AJut.Storage;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class Tests_Stratabase_Tree
    {
        [TestMethod]
        public void BasicStrataTreeTest ()
        {
            const int kStratumCount = 2;
            const int kBasicOverrideLayer = 0;
            const int kSecondaryOverrideLayer = 1;
            var sb = new Stratabase(kStratumCount);
            var tree = new AnOddStrataTree(sb);

            tree.SetRootInBaseline(Guid.NewGuid());
            Assert.IsNotNull(tree.Root);

            tree.Root.ChildrenAccess.AddElementIntoOverrideLayer(kBasicOverrideLayer, Guid.NewGuid());

            Guid next = Guid.NewGuid();
            Assert.IsTrue(sb.SetBaselinePropertyValue(next, nameof(AnOddStrataTreeItem.TrackedInd), 3));
            Assert.IsTrue(sb.SetBaselinePropertyValue(next, nameof(AnOddStrataTreeItem.Name), "Dude #3"));
            Assert.IsTrue(tree.Root.ChildrenAccess.AddElementIntoOverrideLayer(kBasicOverrideLayer, next));

            Assert.IsTrue(tree.Root.ChildrenAccess.InsertElementIntoOverrideLayer(kBasicOverrideLayer, 0, Guid.NewGuid()));
            Assert.IsTrue(tree.Root.ChildrenAccess.InsertElementIntoOverrideLayer(kBasicOverrideLayer, 0, Guid.NewGuid()));
            Assert.IsTrue(tree.Root.ChildrenAccess.InsertElementIntoOverrideLayer(kBasicOverrideLayer, 0, Guid.NewGuid()));
            Assert.IsTrue(tree.Root.ChildrenAccess.InsertElementIntoOverrideLayer(kBasicOverrideLayer, 0, Guid.NewGuid()));
            Assert.IsTrue(tree.Root.ChildrenAccess.InsertElementIntoOverrideLayer(kBasicOverrideLayer, 0, Guid.NewGuid()));
            Assert.IsTrue(tree.Root.ChildrenAccess.InsertElementIntoOverrideLayer(kBasicOverrideLayer, 0, Guid.NewGuid()));
            Assert.IsTrue(tree.Root.ChildrenAccess.InsertElementIntoOverrideLayer(kBasicOverrideLayer, 0, Guid.NewGuid()));

            Assert.AreEqual(9, tree.Root.Children.Count);

            Assert.IsTrue(tree.Root.ChildrenAccess.InsertElementIntoOverrideLayer(kBasicOverrideLayer, 5, Guid.NewGuid()));

            // Inserting into a later layer will give that layer priority, and it has a new list, so 
            Assert.IsTrue(tree.Root.ChildrenAccess.AddElementIntoOverrideLayer(kSecondaryOverrideLayer, Guid.NewGuid()));
            Assert.AreEqual(1, tree.Root.Children.Count);

            // Nuking that override layer
            tree.Root.ChildrenAccess.ObliterateLayer(kSecondaryOverrideLayer);
            Assert.AreEqual(kBasicOverrideLayer, tree.Root.ChildrenAccess.ActiveLayerIndex);
            Assert.AreEqual(9, tree.Root.Children.Count);


            // Copy elements and create new layer
            tree.Root.ChildrenAccess.ResetLayerByCopyingElements(kBasicOverrideLayer, kSecondaryOverrideLayer);
            Assert.AreEqual(kSecondaryOverrideLayer, tree.Root.ChildrenAccess.ActiveLayerIndex);
            Assert.AreEqual(9, tree.Root.Children.Count);

            //#error Update on the below #error, the above is about done in describing what I want the interrface to look like and work

            //#error This is a big problem, how to rationalize insertions across layers. 
            // What happens if you add to the baseline?
            // What happens if you add to override 1, override 0, then override 2?
            //
            // My current thinking is to limit list composition to a single layer,
            //  that would mean if I had 3 elements in baseline, and I added one to override #6
            //  then the collection would contain 1 element. Then make special list helpers
            //  to copy overwrite from layer to baseline, from baseline to layer, from layer to layer
            //  so then you would do:
            //      list.Access.CopyOverwriteList(0, 5)
            //      list.Access.Insert(5, 2, new Obj())
            //  That would be copy the full state from layer override 0, clear and reset layer override 5
            //  then insert a new Obj into layer 5's version at index 2.
            //  Similarly:
            //      list.Access.CopyOverwriteListFromBaseline(5)
            //          Baseline → 5
            //      list.Access.CopyOverwriteToBaselineFrom(5)
            //          5 → Baseline
            //
            //  Also need TearDownOverrideList(5), so if there were elements described in baseline, 0, and 5, then
            //  that teardown would destroy all 
            //
            //  This also means we don't ever need the insertion routine. We just store elements either directly
            //      or as guid ids that get translated into sub objects. The key is, the layers don't blend a final
            //      result. They are self contained and represent the whole of the list.
        }

        public class AnOddStrataTree : StratabaseBackedModel
        {
            private readonly AdaptedProperty<Guid, AnOddStrataTreeItem> m_root;

            public AnOddStrataTree (Stratabase sb) : base (Guid.NewGuid(), sb)
            {
                m_root = this.GenerateAdaptedProperty<Guid, AnOddStrataTreeItem>(nameof(Root), (sb, value) => new AnOddStrataTreeItem(sb, value));
            }

            public AnOddStrataTreeItem Root
            {
                get => m_root.Value;
            }

            public void SetRootInBaseline (Guid id)
            {
                m_root.Access.SetBaselineValue(id);
            }
            public void SetRootInOverride (int layer, Guid id)
            {
                m_root.Access.SetOverrideValue(layer, id);
            }
        }

        public class AnOddStrataTreeItem : StratabaseBackedModel
        {
            private static int g_tracker = 0;
            private readonly AdaptedListProperty<Guid, AnOddStrataTreeItem> m_children;
            private readonly Property<string> m_name;
            private readonly Property<int> m_trackedInd;

            public AnOddStrataTreeItem (Stratabase sb, Guid? id = null) : base(id ?? Guid.NewGuid(), sb)
            {
                m_children = this.GenerateAdaptedListProperty<Guid, AnOddStrataTreeItem>(nameof(Children), (sb, strataElement) => new AnOddStrataTreeItem(sb, strataElement));
                m_name = this.GenerateProperty<string>(nameof(Name));
                m_trackedInd = this.GenerateProperty<int>(nameof(TrackedInd), -1);
                m_trackedInd.Access.ValueChanged += _OnTrackedIndChanged;

                if (this.TrackedInd == -1)
                {
                    this.TrackedInd = g_tracker++;
                }

                void _OnTrackedIndChanged (object _sender, EventArgs _e)
                {
                    this.NameAccess.SetOverrideValue(0, $"Item #{this.TrackedInd}");
                }
            }

            public ReadOnlyObservableCollection<AnOddStrataTreeItem> Children => m_children.Value;
            public StrataPropertyListAccess<Guid> ChildrenAccess => m_children.Access;
            public StrataPropertyValueAccess<string> NameAccess => m_name.Access;
            public StrataPropertyValueAccess<int> TrackerIndexAccess => m_trackedInd.Access;

            public string Name
            {
                get => m_name.Value;
                set => m_name.Access.SetBaselineValue(value);
            }

            public int TrackedInd
            {
                get => m_trackedInd.Value;
                set => m_trackedInd.Access.SetBaselineValue(value);
            }

            public override string ToString ()
            {
                return this.Name;
            }

            //// Child stuff
            //public void AddChild (int layer, Guid newElementId)
            //{
            //    m_children.Access.AddElementIntoOverrideLayer(layer, newElementId);
            //}

            //public void InsertChild (int layer, int index, Guid newElementId)
            //{
            //    m_children.Access.InsertElementIntoOverrideLayer(layer, index, newElementId);
            //}

            //public bool RemoveChild (Guid element)
            //{
            //    return m_children.Access.RemoveElementFromActiveLayer(element);
            //}

            //// Name stuff
            //public void SetOverrideName (int layer, string name)
            //{
            //    m_name.Access.SetOverrideValue(layer, name);
            //}

            //// Tracked ind
            //public void SetOverrideTrackedInd (int layer, int ind)
            //{
            //    m_trackedInd.Access.SetOverrideValue(layer, ind);
            //}
        }
    }
}
