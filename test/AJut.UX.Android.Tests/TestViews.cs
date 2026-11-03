namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Android.Views;
    using Android.Widget;

    /// <summary>What the view tests need beyond <see cref="TestHost"/>: a sized attach, waiting for a layout pass, and finding views.</summary>
    public static class TestViews
    {
        /// <summary>
        /// Puts <paramref name="view"/> in the test area at the area's width and <paramref name="heightDp"/> tall, for a view
        /// (a list) that needs a height of its own to lay out its rows. Dispose the result to take it out again.
        /// </summary>
        public static IDisposable AttachSized (View view, int heightDp)
        {
            IDisposable attached = TestHost.Attach(view);
            view.LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ToPixels(heightDp));
            view.RequestLayout();
            return attached;
        }

        /// <summary>dp to pixels on the test device's screen</summary>
        public static int ToPixels (double dp) => (int)Math.Round(dp * (TestHost.RequiredContext.Resources?.DisplayMetrics?.Density ?? 1f));

        /// <summary>
        /// Waits for the main looper to lay <paramref name="view"/> out: until it has been laid out and has no layout
        /// pending, or a second has passed. A test awaiting this lets the looper run the layout the change it made asked for.
        /// </summary>
        public static async Task LayOutAsync (View view)
        {
            const int kPollMilliseconds = 16;
            const int kMaxPolls = 60;

            // At least one frame, so a change made just before is laid out rather than found already done
            await Task.Delay(kPollMilliseconds);
            for (int poll = 0; poll < kMaxPolls; ++poll)
            {
                if (view.IsLaidOut && !view.IsLayoutRequested)
                {
                    return;
                }

                await Task.Delay(kPollMilliseconds);
            }
        }

        /// <summary>The middle of <paramref name="view"/>, in the pixels of <paramref name="ancestor"/>, one of the views it is inside</summary>
        public static Android.Graphics.PointF CenterIn (View view, View ancestor)
        {
            float x = view.Width / 2f;
            float y = view.Height / 2f;
            for (View? at = view; (at != null) && !ReferenceEquals(at, ancestor); at = at.Parent as View)
            {
                x += at.Left - ((at.Parent as View)?.ScrollX ?? 0);
                y += at.Top - ((at.Parent as View)?.ScrollY ?? 0);
            }

            return new Android.Graphics.PointF(x, y);
        }

        /// <summary>Every view of type <typeparamref name="T"/> under <paramref name="root"/>, root included, depth first</summary>
        public static IEnumerable<T> Descendants<T> (View root) where T : View
        {
            if (root is T match)
            {
                yield return match;
            }

            if (root is ViewGroup group)
            {
                for (int index = 0; index < group.ChildCount; ++index)
                {
                    View? child = group.GetChildAt(index);
                    if (child == null)
                    {
                        continue;
                    }

                    foreach (T descendant in Descendants<T>(child))
                    {
                        yield return descendant;
                    }
                }
            }
        }
    }
}
