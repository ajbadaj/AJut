namespace AJut.UX.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows.Input;
    using AJut.Storage;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // ===========[ Test helpers ]==========================================

    public class ControllerTestHost : IPropertyGridHost
    {
        public IEnumerable ItemsSource { get; set; }
        public object SingleItemSource { get; set; }
        public int ShowRootItemsCount { get; private set; }
        public IReadOnlyList<IObservableTreeNode> ShownRootItems { get; private set; }

        public void ShowRootItems (IReadOnlyList<IObservableTreeNode> rootItems)
        {
            ++this.ShowRootItemsCount;
            this.ShownRootItems = rootItems;
        }
    }

    /// <summary>A source that can say how many handlers are listening to its PropertyChanged</summary>
    public class CountedSubscriptionSource : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler m_propertyChanged;
        private string m_name = "start";

        public event PropertyChangedEventHandler PropertyChanged
        {
            add => m_propertyChanged += value;
            remove => m_propertyChanged -= value;
        }

        public int SubscriberCount => m_propertyChanged?.GetInvocationList().Length ?? 0;

        [PGEditor("Text")]
        public string Name
        {
            get => m_name;
            set
            {
                m_name = value;
                m_propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        /// <summary>Changes Name without raising PropertyChanged, the way an external writer that skips INPC would</summary>
        public void SetNameSilently (string name) => m_name = name;
    }

    /// <summary>An observable collection that can say how many handlers are listening to its CollectionChanged</summary>
    public class CountedSubscriptionCollection : IEnumerable, INotifyCollectionChanged
    {
        private readonly List<object> m_items = new List<object>();
        private NotifyCollectionChangedEventHandler m_collectionChanged;

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => m_collectionChanged += value;
            remove => m_collectionChanged -= value;
        }

        public int SubscriberCount => m_collectionChanged?.GetInvocationList().Length ?? 0;

        public void Add (object item)
        {
            m_items.Add(item);
            m_collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, m_items.Count - 1));
        }

        public void RemoveAt (int index)
        {
            object item = m_items[index];
            m_items.RemoveAt(index);
            m_collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        public IEnumerator GetEnumerator () => m_items.GetEnumerator();
    }

    /// <summary>
    /// An observable collection that raises CollectionChanged with another object as the sender, the way a wrapper or
    /// proxy that forwards an inner collection's events can
    /// </summary>
    public class ForwardedSenderCollection : IEnumerable, INotifyCollectionChanged
    {
        private readonly List<object> m_items = new List<object>();
        private readonly object m_forwarder = new object();

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void Add (object item)
        {
            m_items.Add(item);
            this.CollectionChanged?.Invoke(m_forwarder, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, m_items.Count - 1));
        }

        public IEnumerator GetEnumerator () => m_items.GetEnumerator();
    }

    public sealed record ControllerFixture (ControllerTestHost Host, PropertyGridController Controller);

    public class SilentButtonSource
    {
        [PGEditor("Int")]
        public int Counter { get; set; }

        [PGButton("Bump")]
        public void Bump ()
        {
            ++this.Counter;
        }
    }

    public class ReorderableListSource
    {
        [PGList]
        public List<int> Numbers { get; set; } = new List<int> { 1, 2, 3 };
    }

    public class DefaultedValueSource
    {
        [PGOverrideDefault(5)]
        public int Size { get; set; } = 7;

        public CountedSubscriptionSource Child { get; set; } = new CountedSubscriptionSource();

        [PGShowReadonly]
        [PGOverrideDefault(3)]
        public int Locked { get; } = 4;
    }

    // ===========[ Tests ]=================================================

    [TestClass]
    public class PropertyGridControllerTests
    {
        // ===========[ Sources ]===============================================
        [TestMethod]
        public void PGController_SingleSourceSet_ShowsTopLevelTargets ()
        {
            var source = new CountedSubscriptionSource();
            var (host, controller) = MakeWithSingleSource(source);

            Assert.AreEqual(1, host.ShowRootItemsCount);
            Assert.IsNotNull(host.ShownRootItems);
            Assert.IsTrue(host.ShownRootItems.OfType<PropertyEditTarget>().Any(t => t.PropertyPathTarget == nameof(CountedSubscriptionSource.Name)));
            Assert.AreSame(host.ShownRootItems, controller.RootItems);
        }

        [TestMethod]
        public void PGController_SingleSourceSet_SubscribesOnce ()
        {
            var source = new CountedSubscriptionSource();
            MakeWithSingleSource(source);
            Assert.AreEqual(1, source.SubscriberCount);
        }

        [TestMethod]
        public void PGController_ExternalSourceChange_RecachesEditValue ()
        {
            var source = new CountedSubscriptionSource();
            var (_, controller) = MakeWithSingleSource(source);

            source.Name = "changed";

            Assert.AreEqual("changed", FindTarget(controller, nameof(CountedSubscriptionSource.Name)).EditValue);
        }

        [TestMethod]
        public void PGController_SingleSourceCleared_ClearsRowsAndUnsubscribes ()
        {
            var source = new CountedSubscriptionSource();
            var (host, controller) = MakeWithSingleSource(source);

            host.SingleItemSource = null;
            controller.OnSingleItemSourceChanged(null);

            Assert.IsNull(host.ShownRootItems);
            Assert.IsNull(controller.RootItems);
            Assert.AreEqual(0, source.SubscriberCount);
        }

        [TestMethod]
        public void PGController_Detached_StopsFollowingTheSingleSource ()
        {
            var source = new CountedSubscriptionSource();
            var (_, controller) = MakeWithSingleSource(source);

            controller.OnDetached();

            Assert.AreEqual(0, source.SubscriberCount);
        }

        [TestMethod]
        public void PGController_Reattached_ResubscribesOnceAndCatchesUp ()
        {
            var source = new CountedSubscriptionSource();
            var (_, controller) = MakeWithSingleSource(source);

            controller.OnDetached();
            source.Name = "changed while away";
            controller.OnAttached();

            Assert.AreEqual(1, source.SubscriberCount);
            Assert.AreEqual("changed while away", FindTarget(controller, nameof(CountedSubscriptionSource.Name)).EditValue);
        }

        [TestMethod]
        public void PGController_ObservableItemsSourceCleared_UnsubscribesCollectionAndItems ()
        {
            var item = new CountedSubscriptionSource();
            var items = new CountedSubscriptionCollection();
            items.Add(item);
            var (host, controller) = MakeWithItemsSource(items);

            host.ItemsSource = null;
            controller.OnItemsSourceChanged(null);

            Assert.AreEqual(0, items.SubscriberCount);
            Assert.AreEqual(0, item.SubscriberCount);
            Assert.IsNull(host.ShownRootItems);
        }

        [TestMethod]
        public void PGController_ItemsSourceCollectionChange_Rebuilds ()
        {
            var items = new CountedSubscriptionCollection();
            items.Add(new CountedSubscriptionSource());
            var (host, _) = MakeWithItemsSource(items);
            int showCountBefore = host.ShowRootItemsCount;

            items.Add(new CountedSubscriptionSource());

            Assert.AreEqual(showCountBefore + 1, host.ShowRootItemsCount);
        }

        [TestMethod]
        public void PGController_Detached_StopsFollowingTheItemsSourceCollection ()
        {
            var items = new CountedSubscriptionCollection();
            items.Add(new CountedSubscriptionSource());
            var (_, controller) = MakeWithItemsSource(items);

            controller.OnDetached();

            Assert.AreEqual(0, items.SubscriberCount, "A detached grid must not leave the ItemsSource collection holding it");
        }

        [TestMethod]
        public void PGController_ItemAddedWhileDetached_RebuildsOnReattach ()
        {
            var items = new CountedSubscriptionCollection();
            items.Add(new CountedSubscriptionSource());
            var (host, controller) = MakeWithItemsSource(items);

            controller.OnDetached();
            items.Add(new CountedSubscriptionSource());
            int showCountBefore = host.ShowRootItemsCount;
            controller.OnAttached();

            Assert.AreEqual(showCountBefore + 1, host.ShowRootItemsCount, "A grid coming back must show the items its source gained while it was away");
        }

        [TestMethod]
        public void PGController_ItemRemovedWhileDetached_RebuildsOnReattach ()
        {
            var items = new CountedSubscriptionCollection();
            items.Add(new CountedSubscriptionSource());
            items.Add(new CountedSubscriptionSource());
            var (host, controller) = MakeWithItemsSource(items);

            controller.OnDetached();
            items.RemoveAt(0);
            int showCountBefore = host.ShowRootItemsCount;
            controller.OnAttached();

            Assert.AreEqual(showCountBefore + 1, host.ShowRootItemsCount, "A grid coming back must stop showing the items its source lost while it was away");
        }

        [TestMethod]
        public void PGController_ReattachedWithItemsSourceUnchanged_KeepsItsTree ()
        {
            var items = new CountedSubscriptionCollection();
            items.Add(new CountedSubscriptionSource());
            var (host, controller) = MakeWithItemsSource(items);
            int showCountBefore = host.ShowRootItemsCount;

            controller.OnDetached();
            controller.OnAttached();

            Assert.AreEqual(showCountBefore, host.ShowRootItemsCount, "Rebuilding on every reattach would collapse every expanded row");
        }

        [TestMethod]
        public void PGController_ItemsSourceChangeRaisedByAnotherSender_StillFollowsTheItems ()
        {
            var first = new CountedSubscriptionSource();
            var items = new ForwardedSenderCollection();
            items.Add(first);
            var (_, controller) = MakeWithItemsSource(items);

            var second = new CountedSubscriptionSource();
            items.Add(second);

            Assert.AreEqual(1, first.SubscriberCount);
            Assert.AreEqual(1, second.SubscriberCount);
        }

        [TestMethod]
        public void PGController_Dispose_LetsGoOfTheSingleSource ()
        {
            var source = new CountedSubscriptionSource();
            var (_, controller) = MakeWithSingleSource(source);

            controller.Dispose();

            Assert.AreEqual(0, source.SubscriberCount, "A host that only calls Dispose must not leave the source holding the grid");
        }

        [TestMethod]
        public void PGController_Dispose_LetsGoOfTheItemsSource ()
        {
            var item = new CountedSubscriptionSource();
            var items = new CountedSubscriptionCollection();
            items.Add(item);
            var (_, controller) = MakeWithItemsSource(items);

            controller.Dispose();

            Assert.AreEqual(0, items.SubscriberCount);
            Assert.AreEqual(0, item.SubscriberCount);
        }

        [TestMethod]
        public void PGController_ItemAddedToItemsSource_IsFollowed ()
        {
            var items = new CountedSubscriptionCollection();
            items.Add(new CountedSubscriptionSource());
            MakeWithItemsSource(items);

            var added = new CountedSubscriptionSource();
            items.Add(added);

            Assert.AreEqual(1, added.SubscriberCount, "An item added to the ItemsSource must have its edits followed");
        }

        [TestMethod]
        public void PGController_ItemRemovedFromItemsSource_IsNoLongerFollowed ()
        {
            var removed = new CountedSubscriptionSource();
            var items = new CountedSubscriptionCollection();
            items.Add(removed);
            items.Add(new CountedSubscriptionSource());
            MakeWithItemsSource(items);

            items.RemoveAt(0);

            Assert.AreEqual(0, removed.SubscriberCount, "An item removed from the ItemsSource must not keep the grid subscribed");
        }

        [TestMethod]
        public void PGController_PlainListItemsSourceReplaced_StopsFollowingItsItems ()
        {
            var item = new CountedSubscriptionSource();
            var plainList = new List<object> { item };
            var (host, controller) = MakeWithItemsSource(plainList);

            var replacement = new List<object> { new CountedSubscriptionSource() };
            host.ItemsSource = replacement;
            controller.OnItemsSourceChanged(replacement);

            Assert.AreEqual(0, item.SubscriberCount, "The items of a replaced plain list ItemsSource must not stay subscribed");
        }

        [TestMethod]
        public void PGController_PlainListItemsSourceCleared_StopsFollowingItsItems ()
        {
            var item = new CountedSubscriptionSource();
            var plainList = new List<object> { item };
            var (host, controller) = MakeWithItemsSource(plainList);

            host.ItemsSource = null;
            controller.OnItemsSourceChanged(null);

            Assert.AreEqual(0, item.SubscriberCount, "The items of a cleared plain list ItemsSource must not stay subscribed");
        }

        // ===========[ Edits ]=================================================
        [TestMethod]
        public void PGController_EditThroughATarget_RaisesPropertyTreeChanged ()
        {
            var source = new CountedSubscriptionSource();
            var (_, controller) = MakeWithSingleSource(source);
            int raised = 0;
            controller.PropertyTreeChanged += _OnTreeChanged;

            FindTarget(controller, nameof(CountedSubscriptionSource.Name)).EditValue = "edited";

            controller.PropertyTreeChanged -= _OnTreeChanged;
            Assert.AreEqual(1, raised);
            Assert.AreEqual("edited", source.Name);

            void _OnTreeChanged (object sender, EventArgs e) => ++raised;
        }

        [TestMethod]
        public void PGController_ButtonRun_RecachesTheOtherTargets ()
        {
            var source = new SilentButtonSource();
            var (_, controller) = MakeWithSingleSource(source);
            PropertyEditTarget button = controller.Manager.Items.First(t => t.Editor == "Button");

            ((ICommand)button.EditContext).Execute(null);

            Assert.AreEqual(1, FindTarget(controller, nameof(SilentButtonSource.Counter)).EditValue);
        }

        [TestMethod]
        public void PGController_Disposed_DropsTargetSubscriptions ()
        {
            var source = new CountedSubscriptionSource();
            var (_, controller) = MakeWithSingleSource(source);
            PropertyEditTarget name = FindTarget(controller, nameof(CountedSubscriptionSource.Name));
            int raised = 0;
            controller.PropertyTreeChanged += _OnTreeChanged;

            controller.Dispose();
            name.EditValue = "after dispose";

            controller.PropertyTreeChanged -= _OnTreeChanged;
            Assert.AreEqual(0, raised);

            void _OnTreeChanged (object sender, EventArgs e) => ++raised;
        }

        // ===========[ Selection ]=============================================
        [TestMethod]
        public void PGController_ResolveSourceObject_OutsideAnyListIsNull ()
        {
            var (_, controller) = MakeWithSingleSource(new CountedSubscriptionSource());
            Assert.IsNull(PropertyGridController.ResolveSourceObject(FindTarget(controller, nameof(CountedSubscriptionSource.Name))));
            Assert.IsNull(PropertyGridController.ResolveSourceObject(null));
        }

        [TestMethod]
        public void PGController_SelectionChange_ReportsOnlyWhenTheSourceObjectChanges ()
        {
            var (_, controller) = MakeWithSingleSource(new CountedSubscriptionSource());
            PropertyEditTarget name = FindTarget(controller, nameof(CountedSubscriptionSource.Name));

            // Nothing selected to a row outside any list: both resolve to null, so no change
            Assert.IsFalse(controller.OnSelectedTargetChanged(name));
            Assert.IsFalse(controller.OnSelectedTargetChanged(null));
        }

        [TestMethod]
        public void PGController_FindTargetForSourceObject_NoMatchIsNull ()
        {
            var (_, controller) = MakeWithSingleSource(new CountedSubscriptionSource());
            Assert.IsNull(controller.FindTargetForSourceObject(new object()));
        }

        // ===========[ Rows and reordering ]===================================
        [TestMethod]
        public void PGRowRules_CanResetToDefault_ValueWithDefaultYesExpandableNo ()
        {
            var (_, controller) = MakeWithSingleSource(new DefaultedValueSource());
            Assert.IsTrue(PropertyGridRowRules.CanResetToDefault(FindTarget(controller, nameof(DefaultedValueSource.Size))));
            Assert.IsFalse(PropertyGridRowRules.CanResetToDefault(FindTarget(controller, nameof(DefaultedValueSource.Child))));
        }

        [TestMethod]
        public void PGRowRules_CanResetToDefault_ShownPropertyWithNoSetterNo ()
        {
            // It has a default, and it is away from it, but with no setter a reset has nowhere to go
            var (_, controller) = MakeWithSingleSource(new DefaultedValueSource());
            PropertyEditTarget locked = FindTarget(controller, nameof(DefaultedValueSource.Locked));
            Assert.IsTrue(locked.IsReadOnly);
            Assert.IsTrue(locked.HasDefaultValue);
            Assert.IsFalse(PropertyGridRowRules.CanResetToDefault(locked));
            Assert.IsFalse(PropertyGridRowRules.CanResetToDefault(locked, isGridReadOnly: false));
        }

        [TestMethod]
        public void PGController_ListElements_DragOnlyWithinTheirOwnList ()
        {
            var (_, controller) = MakeWithSingleSource(new ReorderableListSource());
            PropertyEditTarget list = FindTarget(controller, nameof(ReorderableListSource.Numbers));
            PropertyEditTarget[] elements = list.Children.OfType<PropertyEditTarget>().ToArray();

            Assert.IsTrue(PropertyGridController.CanDragItem(elements[0]));
            Assert.IsFalse(PropertyGridController.CanDragItem(list));
            Assert.IsTrue(PropertyGridController.CanDropItem(elements[0], list));
            Assert.IsFalse(PropertyGridController.CanDropItem(elements[0], elements[1]));
        }

        // ===[ A read-only grid ]===

        [TestMethod]
        public void PGController_ReadOnly_RefusesListDrags ()
        {
            var (_, controller) = MakeWithSingleSource(new ReorderableListSource());
            PropertyEditTarget element = FindTarget(controller, nameof(ReorderableListSource.Numbers)).Children.OfType<PropertyEditTarget>().First();

            Assert.IsTrue(controller.CanDragRow(element), "An editable grid lets a reorderable element drag");
            controller.IsReadOnly = true;
            Assert.IsFalse(controller.CanDragRow(element), "A read-only grid refuses it");
            controller.IsReadOnly = false;
            Assert.IsTrue(controller.CanDragRow(element), "And lets it again once editable");
        }

        [TestMethod]
        public void PGController_ReadOnly_OffersNoResetToDefault ()
        {
            var (_, controller) = MakeWithSingleSource(new DefaultedValueSource());
            PropertyEditTarget size = FindTarget(controller, nameof(DefaultedValueSource.Size));

            Assert.IsTrue(PropertyGridRowRules.CanResetToDefault(size, isGridReadOnly: false));
            Assert.IsFalse(PropertyGridRowRules.CanResetToDefault(size, isGridReadOnly: true));
        }

        [TestMethod]
        public void PGController_ReadOnly_NoValueEditsAndNoRemove ()
        {
            var (_, controller) = MakeWithSingleSource(new ReorderableListSource());
            PropertyEditTarget element = FindTarget(controller, nameof(ReorderableListSource.Numbers)).Children.OfType<PropertyEditTarget>().First();

            Assert.AreEqual(!element.IsReadOnly, PropertyGridRowRules.CanEdit(element, isGridReadOnly: false), "Editable follows the property's own setter");
            Assert.IsFalse(PropertyGridRowRules.CanEdit(element, isGridReadOnly: true));
            Assert.AreEqual(element.CanRemoveFromList, PropertyGridRowRules.CanRemoveFromList(element, isGridReadOnly: false));
            Assert.IsFalse(PropertyGridRowRules.CanRemoveFromList(element, isGridReadOnly: true));
        }

        [TestMethod]
        public void PGController_TryHandleReorder_MovesTheElementAndCancels ()
        {
            var source = new ReorderableListSource();
            var (_, controller) = MakeWithSingleSource(source);
            PropertyEditTarget list = FindTarget(controller, nameof(ReorderableListSource.Numbers));
            PropertyEditTarget first = list.Children.OfType<PropertyEditTarget>().First();

            // Insert index 3 is after the last element; with the first lifted out that is index 2
            var e = new FlatTreeReorderEventArgs(new IObservableTreeNode[] { first }, list, 3);
            Assert.IsTrue(PropertyGridController.TryHandleReorder(e));

            Assert.IsTrue(e.Cancel);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, source.Numbers);
        }

        [TestMethod]
        public void PGController_TryHandleReorder_IgnoresNonListRows ()
        {
            var (_, controller) = MakeWithSingleSource(new CountedSubscriptionSource());
            var e = new FlatTreeReorderEventArgs(new IObservableTreeNode[] { FindTarget(controller, nameof(CountedSubscriptionSource.Name)) }, null, 0);

            Assert.IsFalse(PropertyGridController.TryHandleReorder(e));
            Assert.IsFalse(e.Cancel);
        }

        // ===========[ Helpers ]===============================================
        internal static ControllerFixture MakeWithSingleSource (object source)
        {
            var host = new ControllerTestHost { SingleItemSource = source };
            var controller = new PropertyGridController(host);
            controller.OnSingleItemSourceChanged(source);
            return new ControllerFixture(host, controller);
        }

        internal static ControllerFixture MakeWithItemsSource (IEnumerable items)
        {
            var host = new ControllerTestHost { ItemsSource = items };
            var controller = new PropertyGridController(host);
            controller.OnItemsSourceChanged(items);
            return new ControllerFixture(host, controller);
        }

        internal static PropertyEditTarget FindTarget (PropertyGridController controller, string path)
        {
            return controller.Manager.Items.First(t => t.PropertyPathTarget == path);
        }
    }
}
