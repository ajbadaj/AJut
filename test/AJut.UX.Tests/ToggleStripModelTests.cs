#nullable enable
namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The toggle strip's item model in AJut.UX, which every toolkit's ToggleStrip runs on: selection rules, the change
    /// it reports to its strip, naming, order flags, and keeping in step with an items source.
    /// </summary>
    [TestClass]
    public class ToggleStripModelTests
    {
        // ===========[ Selection ]===================================
        [TestMethod]
        public void SingleSelect_SelectingAnItem_DeselectsTheOthers_AndReportsOneChange ()
        {
            var host = new TestHost();
            ToggleStripItemCollection items = Make(host, "A", "B", "C");
            host.Changes.Clear();

            items[1].IsSelected = true;

            Assert.IsFalse(items[0].IsSelected);
            Assert.IsTrue(items[1].IsSelected);
            Assert.AreEqual(1, host.Changes.Count, "A single-select swap is one change");
            ToggleStripSelectionChange change = host.Changes[0];
            Assert.AreSame(items[1], change.Selected);
            Assert.AreEqual("B", change.SelectedItem);
            CollectionAssert.AreEqual(new[] { "B" }, change.CurrentSelection);
        }

        [TestMethod]
        public void DeselectingTheLastSelectedItem_IsRefused_AndStillRaisesIsSelected ()
        {
            var host = new TestHost();
            ToggleStripItemCollection items = Make(host, "A", "B");
            host.Changes.Clear();
            var raised = new List<string?>();
            items[0].PropertyChanged += _OnPropertyChanged;

            items[0].IsSelected = false;
            items[0].PropertyChanged -= _OnPropertyChanged;

            Assert.IsTrue(items[0].IsSelected, "The only selected item stays selected");
            CollectionAssert.Contains(raised, nameof(ToggleStripItem.IsSelected), "A refused deselect still raises, so a flipped toggle flips back");
            Assert.AreEqual(0, host.Changes.Count);

            void _OnPropertyChanged (object? sender, PropertyChangedEventArgs e) => raised.Add(e.PropertyName);
        }

        [TestMethod]
        public void AllowNoSelection_LetsTheLastItemBeDeselected ()
        {
            var host = new TestHost { AllowNoSelection = true };
            ToggleStripItemCollection items = Make(host, "A", "B");
            items[0].IsSelected = true;
            host.Changes.Clear();

            items[0].IsSelected = false;

            Assert.IsFalse(items[0].IsSelected);
            Assert.AreEqual(1, host.Changes.Count);
            Assert.IsNull(host.Changes[0].Selected, "A deselect has no selected item");
            Assert.AreEqual(0, host.Changes[0].CurrentSelection.Count);
        }

        [TestMethod]
        public void MultiSelect_KeepsEverySelection_InStripOrder ()
        {
            var host = new TestHost { AllowMultiSelect = true };
            ToggleStripItemCollection items = Make(host, "A", "B", "C");

            items[2].IsSelected = true;

            CollectionAssert.AreEqual(new[] { "A", "C" }, host.Changes.Last().CurrentSelection);
            Assert.AreEqual("C", host.Changes.Last().SelectedItem);

            items[0].IsSelected = false;
            ToggleStripSelectionChange deselect = host.Changes.Last();
            Assert.IsNull(deselect.Selected);
            CollectionAssert.AreEqual(new[] { "A", "C" }, deselect.PreviousSelection);
            CollectionAssert.AreEqual(new[] { "C" }, deselect.CurrentSelection);
        }

        [TestMethod]
        public void SelectData_SelectsTheMatchingItem ()
        {
            var host = new TestHost();
            ToggleStripItemCollection items = Make(host, "A", "B");

            Assert.IsTrue(items.SelectData("B"));
            Assert.IsTrue(items[1].IsSelected);
            Assert.IsFalse(items.SelectData("Z"), "No item has that data");
        }

        // ===========[ Items source ]===================================
        [TestMethod]
        public void ResetFromSource_SelectsTheFirstItem ()
        {
            var host = new TestHost();
            ToggleStripItemCollection items = Make(host, "A", "B");
            Assert.IsTrue(items[0].IsSelected);
            Assert.AreEqual("A", items.SelectedItem);
        }

        [TestMethod]
        public void ResetFromSource_RestoresThePreviousSelectionByValue ()
        {
            var host = new TestHost();
            var items = new ToggleStripItemCollection(host);
            items.ResetFromSource(new object[] { 1, 2, 3 }, previousSelection: 2);

            Assert.IsTrue(items[1].IsSelected, "A boxed 2 matches the new boxed 2");
            Assert.IsFalse(items[0].IsSelected);
        }

        [TestMethod]
        public void ResetFromSource_WithNoSelectionAllowed_SelectsNothing ()
        {
            var host = new TestHost { AllowNoSelection = true };
            ToggleStripItemCollection items = Make(host, "A", "B");
            Assert.IsFalse(items.Any(item => item.IsSelected));
        }

        [TestMethod]
        public void ResetFromSource_Null_ClearsTheItems ()
        {
            var host = new TestHost();
            ToggleStripItemCollection items = Make(host, "A", "B");
            items.ResetFromSource(null, previousSelection: null);
            Assert.AreEqual(0, items.Count);
        }

        [TestMethod]
        public void ApplySourceChange_AddAtTheEnd_AppendsAnItem ()
        {
            var host = new TestHost();
            var source = new ObservableCollection<string> { "A", "B" };
            var items = new ToggleStripItemCollection(host);
            items.ResetFromSource(source, previousSelection: null);
            source.CollectionChanged += _OnSourceChanged;

            source.Add("C");
            source.CollectionChanged -= _OnSourceChanged;

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, items.Select(item => item.Data).ToArray());

            void _OnSourceChanged (object? sender, NotifyCollectionChangedEventArgs e) => items.ApplySourceChange(e);
        }

        [TestMethod]
        public void ApplySourceChange_ResetOfAnEmptiedSource_ClearsTheItems ()
        {
            var host = new TestHost();
            var source = new List<string> { "A", "B" };
            var items = new ToggleStripItemCollection(host);
            items.ResetFromSource(source, previousSelection: null);
            host.Changes.Clear();

            source.Clear();
            items.ApplySourceChange(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            Assert.AreEqual(0, items.Count);
            Assert.AreEqual(1, host.Changes.Count, "Losing the selection to a reset is a selection change");
            Assert.AreEqual(0, host.Changes[0].CurrentSelection.Count);
        }

        [TestMethod]
        public void ApplySourceChange_InsertAtAnIndex_InsertsThere ()
        {
            var host = new TestHost();
            var source = new ObservableCollection<string> { "A", "B", "C" };
            using SourceFollower follow = Follow(host, source);
            ToggleStripItemCollection items = follow.Items;

            source.Insert(1, "X");

            Assert.AreEqual("A,X,B,C", string.Join(",", items.Select(item => item.Data)));
        }

        [TestMethod]
        public void ApplySourceChange_Remove_RemovesThatItem ()
        {
            var host = new TestHost();
            var source = new ObservableCollection<string> { "A", "B", "C" };
            using SourceFollower follow = Follow(host, source);
            ToggleStripItemCollection items = follow.Items;

            source.RemoveAt(1);

            Assert.AreEqual("A,C", string.Join(",", items.Select(item => item.Data)));
        }

        [TestMethod]
        public void ApplySourceChange_Move_MovesThatItem ()
        {
            var host = new TestHost();
            var source = new ObservableCollection<string> { "A", "B", "C" };
            using SourceFollower follow = Follow(host, source);
            ToggleStripItemCollection items = follow.Items;

            source.Move(0, 2);

            Assert.AreEqual("B,C,A", string.Join(",", items.Select(item => item.Data)));
        }

        [TestMethod]
        public void ApplySourceChange_Replace_ReplacesInPlace ()
        {
            var host = new TestHost();
            var source = new ObservableCollection<string> { "A", "B", "C" };
            using SourceFollower follow = Follow(host, source);
            ToggleStripItemCollection items = follow.Items;

            source[1] = "Y";

            Assert.AreEqual("A,Y,C", string.Join(",", items.Select(item => item.Data)));
        }

        [TestMethod]
        public void ApplySourceChange_RemovingTheSelectedItem_SelectsAnotherAndReportsIt ()
        {
            var host = new TestHost();
            var source = new ObservableCollection<string> { "A", "B", "C" };
            using SourceFollower follow = Follow(host, source);
            ToggleStripItemCollection items = follow.Items;
            host.Changes.Clear();

            source.RemoveAt(0);

            Assert.IsTrue(items.Any(item => item.IsSelected), "A single-select strip that needs a selection keeps one");
            Assert.AreEqual(1, host.Changes.Count, "Losing the selected item is a selection change");
            CollectionAssert.AreEqual(new[] { "A" }, host.Changes[0].PreviousSelection);
            CollectionAssert.AreEqual(new[] { "B" }, host.Changes[0].CurrentSelection);
            Assert.AreEqual("B", items.SelectedItem);
        }

        [TestMethod]
        public void ApplySourceChange_RemovingASelectedItem_InMultiSelect_ReportsIt ()
        {
            var host = new TestHost { AllowMultiSelect = true };
            var source = new ObservableCollection<string> { "A", "B", "C" };
            using SourceFollower follow = Follow(host, source);
            ToggleStripItemCollection items = follow.Items;
            items[2].IsSelected = true;
            host.Changes.Clear();

            source.RemoveAt(2);

            Assert.AreEqual(1, host.Changes.Count);
            CollectionAssert.AreEqual(new[] { "A", "C" }, host.Changes[0].PreviousSelection);
            CollectionAssert.AreEqual(new[] { "A" }, host.Changes[0].CurrentSelection);
            Assert.AreEqual("A", items.SelectedItem, "The selected item was the one removed, so it moves to the last one left");
        }

        [TestMethod]
        public void ApplySourceChange_ReplacingTheSelectedItem_ReportsIt ()
        {
            var host = new TestHost();
            var source = new ObservableCollection<string> { "A", "B" };
            using SourceFollower follow = Follow(host, source);
            ToggleStripItemCollection items = follow.Items;
            host.Changes.Clear();

            source[0] = "Z";

            Assert.IsTrue(items.Any(item => item.IsSelected), "A single-select strip that needs a selection keeps one");
            Assert.AreEqual(1, host.Changes.Count);
            CollectionAssert.AreEqual(new[] { "A" }, host.Changes[0].PreviousSelection);
        }

        [TestMethod]
        public void ApplySourceChange_Reset_RebuildsFromTheSource ()
        {
            var host = new TestHost();
            var source = new List<string> { "A", "B" };
            var items = new ToggleStripItemCollection(host);
            items.ResetFromSource(source, previousSelection: null);

            // A source that is not an ObservableCollection reports a bulk change as a Reset.
            source.Add("C");
            items.ApplySourceChange(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            Assert.AreEqual("A,B,C", string.Join(",", items.Select(item => item.Data)));
            Assert.AreEqual("A", items.SelectedItem, "The selection is restored by value");
        }

        [TestMethod]
        public void SingleSelectSwap_PreviousSelection_HasTheItemSwappedOut ()
        {
            var host = new TestHost();
            ToggleStripItemCollection items = Make(host, "A", "B");
            host.Changes.Clear();

            items[1].IsSelected = true;

            CollectionAssert.AreEqual(new[] { "A" }, host.Changes.Single().PreviousSelection);
        }

        [TestMethod]
        public void DeselectingTheSelectedItem_MovesSelectedItemToTheLastOneLeft ()
        {
            var host = new TestHost { AllowMultiSelect = true };
            ToggleStripItemCollection items = Make(host, "A", "B", "C");
            items[2].IsSelected = true;
            Assert.AreEqual("C", items.SelectedItem);

            items[2].IsSelected = false;

            Assert.AreEqual("A", items.SelectedItem);
            Assert.AreEqual("A", host.Changes.Last().SelectedItem);
        }

        [TestMethod]
        public void DeselectVeto_CountsTheItemsStillSelected_AfterOneIsRemoved ()
        {
            var host = new TestHost { AllowMultiSelect = true };
            ToggleStripItemCollection items = Make(host, "A", "B");
            items[1].IsSelected = true;

            // The items source removes B, which leaves A as the only selected item.
            items.RemoveAt(1);
            items[0].IsSelected = false;

            Assert.IsTrue(items[0].IsSelected, "The last selected item cannot be deselected");
        }

        // ===========[ Order and naming ]===================================
        [TestMethod]
        public void OrderFlags_FollowInsertions ()
        {
            var host = new TestHost();
            ToggleStripItemCollection items = Make(host, "A", "B");
            Assert.IsTrue(items[0].IsFirstItem);
            Assert.IsTrue(items[1].IsLastItem);

            items.Add(new ToggleStripItem(host, "C", -1));

            Assert.IsFalse(items[1].IsLastItem, "The old last item stops being last");
            Assert.IsTrue(items[2].IsLastItem);
            Assert.AreEqual(2, items[2].ItemIndex);
        }

        [TestMethod]
        public void Naming_UsesThePath_ThenToString_ThenPosition ()
        {
            var host = new TestHost { DisplayPropertyPath = nameof(Named.Label) };
            var items = new ToggleStripItemCollection(host);
            items.ResetFromSource(new object[] { new Named("first"), new object() }, previousSelection: null);

            Assert.AreEqual("first", items[0].Name, "The display property path");
            Assert.AreEqual("Item 1", items[1].Name, "A type-name ToString falls back to the position");

            host.DisplayPropertyPath = null;
            items.ResetNames();
            Assert.AreEqual("Named first", items[0].Name, "An overridden ToString");
        }

        [TestMethod]
        public void Naming_TheSelectorWinsOverThePath ()
        {
            var host = new TestHost { DisplayPropertyPath = nameof(Named.Label), ItemNameSelector = data => "picked" };
            var items = new ToggleStripItemCollection(host);
            items.ResetFromSource(new object[] { new Named("first") }, previousSelection: null);
            Assert.AreEqual("picked", items[0].Name);
        }

        // ===========[ Helper Methods ]===================================
        private static ToggleStripItemCollection Make (TestHost host, params string[] data)
        {
            var items = new ToggleStripItemCollection(host);
            items.ResetFromSource(data, previousSelection: null);
            return items;
        }

        // Items that follow an observable source, the way a strip wires its ItemsSource, until the follower is disposed.
        private static SourceFollower Follow (TestHost host, ObservableCollection<string> source)
        {
            var items = new ToggleStripItemCollection(host);
            items.ResetFromSource(source, previousSelection: null);
            return new SourceFollower(source, items);
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class SourceFollower : IDisposable
        {
            private readonly ObservableCollection<string> m_source;

            public SourceFollower (ObservableCollection<string> source, ToggleStripItemCollection items)
            {
                m_source = source;
                this.Items = items;
                m_source.CollectionChanged += this.OnSourceChanged;
            }

            public ToggleStripItemCollection Items { get; }

            public void Dispose () => m_source.CollectionChanged -= this.OnSourceChanged;

            private void OnSourceChanged (object? sender, NotifyCollectionChangedEventArgs e) => this.Items.ApplySourceChange(e);
        }

        private sealed class TestHost : IToggleStripHost
        {
            public bool AllowMultiSelect { get; set; }

            public bool AllowNoSelection { get; set; }

            public string? DisplayPropertyPath { get; set; }

            public Func<object, string?>? ItemNameSelector { get; set; }

            public List<ToggleStripSelectionChange> Changes { get; } = new List<ToggleStripSelectionChange>();

            public void OnSelectionChanged (ToggleStripSelectionChange change) => this.Changes.Add(change);
        }

        private sealed class Named
        {
            public Named (string label)
            {
                this.Label = label;
            }

            public string Label { get; }

            public override string ToString () => $"Named {this.Label}";
        }
    }
}
