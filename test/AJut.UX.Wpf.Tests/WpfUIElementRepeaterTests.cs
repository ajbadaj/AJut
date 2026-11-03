namespace AJut.UX.Tests
{
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// How UIElementRepeater keeps its container's children in step with DisplayChildren. The repeated children come first
    /// in the container, in the collection's order, ahead of any the container already had.
    /// </summary>
    [TestClass]
    public class WpfUIElementRepeaterTests
    {
        [TestMethod]
        public void WpfUIElementRepeater_SettingDisplayChildren_PlacesThemAheadOfTheContainersOwn ()
        {
            StaTestThread.Run(() =>
            {
                var own = new Border();
                var container = new StackPanel();
                container.Children.Add(own);
                var items = _Items(2);
                var repeater = new UIElementRepeater { Container = container, DisplayChildren = items };

                _AssertChildren(container, items[0], items[1], own);
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_AddAndInsert_PlaceEachAtItsIndex ()
        {
            StaTestThread.Run(() =>
            {
                var container = new StackPanel();
                var items = _Items(2);
                var repeater = new UIElementRepeater { Container = container, DisplayChildren = items };

                var added = new Border();
                items.Add(added);
                var inserted = new Border();
                items.Insert(1, inserted);

                _AssertChildren(container, items[0], inserted, items[2], added);
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_RemoveMoveAndReplace_FollowTheCollection ()
        {
            StaTestThread.Run(() =>
            {
                var container = new StackPanel();
                var items = _Items(4);
                var repeater = new UIElementRepeater { Container = container, DisplayChildren = items };

                items.RemoveAt(1);
                items.Move(0, 2);
                var replacement = new Border();
                items[0] = replacement;

                _AssertChildren(container, items.ToArray());
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_NewDisplayChildren_ReplaceTheOldAndStopFollowingIt ()
        {
            StaTestThread.Run(() =>
            {
                var container = new StackPanel();
                var first = _Items(2);
                var repeater = new UIElementRepeater { Container = container, DisplayChildren = first };

                var second = _Items(1);
                repeater.DisplayChildren = second;
                first.Add(new Border());

                _AssertChildren(container, second[0]);
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_ClearingTheCollection_EmptiesTheContainer ()
        {
            StaTestThread.Run(() =>
            {
                var own = new Border();
                var container = new StackPanel();
                container.Children.Add(own);
                var items = _Items(2);
                var repeater = new UIElementRepeater { Container = container, DisplayChildren = items };

                items.Clear();

                _AssertChildren(container, own);
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_DisplayChildrenBeforeContainer_PlacesThemWhenTheContainerArrives ()
        {
            StaTestThread.Run(() =>
            {
                var items = _Items(2);
                var repeater = new UIElementRepeater { DisplayChildren = items };
                var container = new StackPanel();
                repeater.Container = container;

                _AssertChildren(container, items.ToArray());
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_NewContainer_TakesTheChildrenFromTheOld ()
        {
            StaTestThread.Run(() =>
            {
                var first = new StackPanel();
                var items = _Items(2);
                var repeater = new UIElementRepeater { Container = first, DisplayChildren = items };

                var second = new StackPanel();
                repeater.Container = second;
                items.Add(new Border());

                _AssertChildren(first);
                _AssertChildren(second, items.ToArray());
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_TakenOutOfItsWindow_LeavesNoHandlerOnTheCollection ()
        {
            StaTestThread.Run(() =>
            {
                // A collection that outlives the view, as a view model's can, must not hold the repeater once it is gone
                var items = new CountingItems { new Border() };
                var repeater = new UIElementRepeater { Container = new StackPanel(), DisplayChildren = items };
                using (HwndSource source = _HiddenSource())
                {
                    source.RootVisual = repeater;
                    _PumpDispatcher();
                    Assert.AreEqual(1, items.HandlerCount, "In a window, the repeater follows the collection");

                    source.RootVisual = null;
                    _PumpDispatcher();
                }

                Assert.AreEqual(0, items.HandlerCount, "Out of its window, the collection still holds the repeater through its handler");
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_TakenOutOfItsWindow_LetsGoOfItsContainer ()
        {
            StaTestThread.Run(() =>
            {
                // The views a long-lived collection holds hold their container, so the repeater lets go of the container
                //  and the collection keeps only that alive, never the repeater or the window above it. The container keeps
                //  its views, so it comes back as it was.
                var items = new CountingItems { new Border(), new Border() };
                var container = new StackPanel();
                var repeater = new UIElementRepeater { Container = container, DisplayChildren = items };
                using HwndSource source = _HiddenSource();
                source.RootVisual = repeater;
                _PumpDispatcher();
                Assert.IsTrue(repeater.IsAncestorOf(container), "In a window, the container is in the repeater");

                source.RootVisual = null;
                _PumpDispatcher();
                Assert.IsNull(VisualTreeHelper.GetParent(container), "Out of its window, the repeater still holds its container in its tree");
                foreach (UIElement item in items)
                {
                    Assert.AreSame(container, VisualTreeHelper.GetParent(item), "Each view stays in the container");
                }

                source.RootVisual = repeater;
                _PumpDispatcher();
                Assert.IsTrue(repeater.IsAncestorOf(container), "Back in a window, the container is back in the repeater");
                _AssertChildren(container, items.ToArray());
            });
        }

        [TestMethod]
        public void WpfUIElementRepeater_BackInAWindow_CatchesUpOnChangesMadeWhileOut ()
        {
            StaTestThread.Run(() =>
            {
                var items = new CountingItems { new Border() };
                var container = new StackPanel();
                var repeater = new UIElementRepeater { Container = container, DisplayChildren = items };
                using HwndSource source = _HiddenSource();
                source.RootVisual = repeater;
                _PumpDispatcher();
                source.RootVisual = null;
                _PumpDispatcher();

                items.Add(new Border());
                items.RemoveAt(0);
                source.RootVisual = repeater;
                _PumpDispatcher();

                _AssertChildren(container, items.ToArray());
                Assert.AreEqual(1, items.HandlerCount, "Following again, once");
            });
        }

        // ===========[ Helper Methods ]========================================
        // A presentation source that is never shown: no WS_VISIBLE, so the window is made but never put on screen, and
        //  off any screen besides. Setting its root visual still loads what is in it, and clearing it unloads it.
        private static HwndSource _HiddenSource ()
        {
            const int kOffScreen = -32000;
            var parameters = new HwndSourceParameters("AJut test source")
            {
                WindowStyle = 0,
                PositionX = kOffScreen,
                PositionY = kOffScreen,
                Width = 100,
                Height = 100,
            };

            return new HwndSource(parameters);
        }

        // Runs everything queued on this thread's dispatcher, which is where Loaded and Unloaded are raised
        private static void _PumpDispatcher ()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new DispatcherOperationCallback(_EndFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private static object _EndFrame (object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }

        private static ObservableCollection<UIElement> _Items (int count)
        {
            var items = new ObservableCollection<UIElement>();
            for (int index = 0; index < count; ++index)
            {
                items.Add(new Border());
            }

            return items;
        }

        private static void _AssertChildren (Panel container, params UIElement[] expected)
        {
            UIElement[] actual = container.Children.Cast<UIElement>().ToArray();
            Assert.AreEqual(expected.Length, actual.Length, "child count");
            for (int index = 0; index < expected.Length; ++index)
            {
                Assert.AreSame(expected[index], actual[index], $"child {index}");
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // An observable collection that counts the handlers it holds, so a test can see whether it still holds a repeater
        private sealed class CountingItems : ObservableCollection<UIElement>
        {
            private NotifyCollectionChangedEventHandler m_handlers;

            public override event NotifyCollectionChangedEventHandler CollectionChanged
            {
                add
                {
                    m_handlers += value;
                    base.CollectionChanged += value;
                }
                remove
                {
                    m_handlers -= value;
                    base.CollectionChanged -= value;
                }
            }

            public int HandlerCount => m_handlers?.GetInvocationList().Length ?? 0;
        }
    }
}
