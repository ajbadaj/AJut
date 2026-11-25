namespace AJut.UX.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AJut.UX.Controls;
    using Android.OS;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PageStackTests
    {
        private const int kStackHeightDp = 200;

        // ===========[ Selection ]=============================================
        [TestMethod]
        public void SelectedIndexPastTheEnd_ShowsTheLastPage_AndKeepsTheIndexGiven ()
        {
            PageStack stack = _Stack(4);
            stack.SelectedIndex = 9;

            Assert.AreEqual(9, stack.SelectedIndex, "Kept as given, so one set before the pages are added still lands");
            Assert.IsTrue(_IsShown(stack.Children[3]));
            Assert.IsFalse(_IsShown(stack.Children[2]));
            Assert.IsFalse(_Button(stack, "Next page").Enabled);
        }

        [TestMethod]
        public void InsertingAndRemovingPages_KeepsTheShownPage ()
        {
            PageStack stack = _Stack(5);
            stack.SelectedIndex = 2;
            View shown = stack.Children[2];

            stack.Children.Insert(0, new View(TestHost.RequiredContext));
            Assert.AreEqual(3, stack.SelectedIndex);
            Assert.IsTrue(_IsShown(shown));

            stack.Children.RemoveAt(1);
            Assert.AreEqual(2, stack.SelectedIndex);
            Assert.IsTrue(_IsShown(shown));
        }

        [TestMethod]
        public void RemovingTheShownPage_ShowsThePageThatTookItsPlace ()
        {
            PageStack stack = _Stack(4);
            stack.SelectedIndex = 1;
            View next = stack.Children[2];
            int changes = 0;
            stack.SelectedIndexChanged += _Count;

            stack.Children.RemoveAt(1);

            Assert.AreEqual(1, stack.SelectedIndex);
            Assert.IsTrue(_IsShown(next));
            Assert.AreEqual(0, changes, "The index stays 1, so nothing changed");
            stack.SelectedIndexChanged -= _Count;

            void _Count (object? sender, EventArgs e) => ++changes;
        }

        [TestMethod]
        public void InflatedPages_BecomeTheChildren_AndTheIndexSetFirstLands ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.page_stack_test, null)!;
            var stack = root.FindViewById<PageStack>(Resource.Id.page_stack)!;

            Assert.AreEqual(3, stack.Children.Count);
            Assert.AreSame(root.FindViewById(Resource.Id.page_stack_first), stack.Children[0]);
            Assert.AreEqual(2, stack.SelectedIndex);
            Assert.IsTrue(_IsShown(root.FindViewById(Resource.Id.page_stack_last)!));
            Assert.IsTrue(stack.ShowDots);
            Assert.IsFalse(stack.IsSwipeEnabled);
        }

        // ===========[ Page buttons and dots ]=================================
        [TestMethod]
        public void PageButtons_EnableOnlyWhereThereIsAPage_AndATapPages ()
        {
            PageStack stack = _Stack(3);
            View back = _Button(stack, "Previous page");
            View forward = _Button(stack, "Next page");
            Assert.IsFalse(back.Enabled);
            Assert.IsTrue(forward.Enabled);

            forward.PerformClick();
            Assert.AreEqual(1, stack.SelectedIndex);
            Assert.IsTrue(back.Enabled);

            forward.PerformClick();
            Assert.AreEqual(2, stack.SelectedIndex);
            Assert.IsFalse(forward.Enabled);

            back.PerformClick();
            Assert.AreEqual(1, stack.SelectedIndex);
        }

        [TestMethod]
        public void ADotTap_GoesToItsPage ()
        {
            PageStack stack = _Stack(7);
            stack.ShowDots = true;

            _Button(stack, "Page 4 of 7").PerformClick();

            Assert.AreEqual(3, stack.SelectedIndex);
            Assert.IsTrue(_IsShown(stack.Children[3]));
        }

        // ===========[ Swiping ]===============================================
        [TestMethod]
        public async Task AQuickFlick_PagesForward ()
        {
            PageStack stack = _Stack(5);
            using IDisposable attached = TestViews.AttachSized(stack, kStackHeightDp);
            await TestViews.LayOutAsync(stack);

            _Swipe(stack, alongDistances: new[] { -40f, -120f, -200f, -240f }, stepMilliseconds: 10);

            Assert.AreEqual(1, stack.SelectedIndex);
            Assert.IsTrue(_IsShown(stack.Children[1]));
        }

        [TestMethod]
        public async Task AShortSlowSwipe_SpringsBack ()
        {
            PageStack stack = _Stack(5);
            stack.SelectedIndex = 2;
            using IDisposable attached = TestViews.AttachSized(stack, kStackHeightDp);
            await TestViews.LayOutAsync(stack);

            _Swipe(stack, alongDistances: new float[] { -TestViews.ToPixels(12), -TestViews.ToPixels(16), -TestViews.ToPixels(18), -TestViews.ToPixels(18) }, stepMilliseconds: 200);

            Assert.AreEqual(2, stack.SelectedIndex);
            Assert.IsTrue(_IsShown(stack.Children[2]));
            Assert.AreEqual(0f, ((View)stack.Children[2].Parent!).TranslationX, 0.5f, "Back where it was");
        }

        [TestMethod]
        public async Task SwipeDisabled_IgnoresAFlick ()
        {
            PageStack stack = _Stack(5);
            stack.IsSwipeEnabled = false;
            using IDisposable attached = TestViews.AttachSized(stack, kStackHeightDp);
            await TestViews.LayOutAsync(stack);

            _Swipe(stack, alongDistances: new[] { -40f, -120f, -200f, -240f }, stepMilliseconds: 10);

            Assert.AreEqual(0, stack.SelectedIndex);
            Assert.AreEqual(0f, ((View)stack.Children[0].Parent!).TranslationX, 0.5f, "The pages never moved");
        }

        [TestMethod]
        public async Task ADragAcrossTheStack_IsNotTaken ()
        {
            PageStack stack = _Stack(5);
            using IDisposable attached = TestViews.AttachSized(stack, kStackHeightDp);
            await TestViews.LayOutAsync(stack);

            // Down the stack, drifting a little along it: still mostly across
            _Swipe(stack, alongDistances: new[] { -10f, -20f, -30f, -30f }, stepMilliseconds: 10, acrossDistances: new[] { 40f, 120f, 200f, 240f });

            Assert.AreEqual(0, stack.SelectedIndex);
            Assert.AreEqual(0f, ((View)stack.Children[0].Parent!).TranslationX, 0.5f, "The pages never moved");
        }

        // A stack of plain pages that jumps straight to a page, so a test sees where it lands without waiting on a transition
        private static PageStack _Stack (int pageCount)
        {
            var stack = new PageStack(TestHost.RequiredContext) { TransitionDuration = TimeSpan.Zero };
            for (int index = 0; index < pageCount; ++index)
            {
                stack.Children.Add(new View(TestHost.RequiredContext));
            }

            return stack;
        }

        // The page shown draws fully opaque; every other page fades
        private static bool _IsShown (View page) => ((View)page.Parent!).Alpha >= 0.999f;

        // A page button or dot, by what it reads out as
        private static View _Button (PageStack stack, string description)
        {
            return TestViews.Descendants<View>(stack).First(view => view.ContentDescription == description);
        }

        // A finger down in the middle of the stack, moved by each distance in turn (along the stack, and across it when
        //  given) a step apart, and lifted at the last
        private static void _Swipe (PageStack stack, float[] alongDistances, int stepMilliseconds, float[]? acrossDistances = null)
        {
            float startX = stack.Width / 2f;
            float startY = stack.Height / 2f;
            long downTime = SystemClock.UptimeMillis();
            _Send(stack, downTime, downTime, MotionEventActions.Down, startX, startY);
            for (int step = 0; step < alongDistances.Length; ++step)
            {
                long eventTime = downTime + ((step + 1) * stepMilliseconds);
                float across = acrossDistances?[step] ?? 0f;
                MotionEventActions action = step == alongDistances.Length - 1 ? MotionEventActions.Up : MotionEventActions.Move;
                _Send(stack, downTime, eventTime, action, startX + alongDistances[step], startY + across);
            }
        }

        private static void _Send (PageStack stack, long downTime, long eventTime, MotionEventActions action, float x, float y)
        {
            using MotionEvent motion = MotionEvent.Obtain(downTime, eventTime, action, x, y, 0)!;
            stack.DispatchTouchEvent(motion);
        }
    }
}
