namespace AJutShowRoomUIKit.UI.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using AJut.UX.Controls;
    using CoreGraphics;
    using UIKit;

    /// <summary>
    /// Whether a control is collected once it leaves the window and is dropped without Dispose, as AppKit's Leak Probe
    /// does. Each probe shows a fresh control in this page with its pages on screen, takes it out, drops it, and then
    /// collects with pauses between passes, so UIKit has let go of the views and any deferred work has run before the last
    /// pass looks. A UIKit view that something it retains holds back is never collected, which is what this catches. A new
    /// probe is one entry in the list in BuildPage.
    /// </summary>
    public sealed class LeakProbePage : ShowroomPageController
    {
        private const float kProbeWidth = 320f;
        private const float kProbeHeight = 200f;

        private readonly List<Probe> m_probes = new List<Probe>();
        private readonly UIView m_probeArea = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
        private readonly UILabel m_result = new UILabel { Text = "Nothing probed yet", Lines = 0 };
        private bool m_isProbing;

        // ===========[ Setup/Construction/Teardown ]===================================
        public LeakProbePage () : base("Leak Probe")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            foreach (Probe probe in m_probes)
            {
                probe.Button.PrimaryActionTriggered -= this.ProbeButton_OnPrimaryActionTriggered;
                probe.Button.PrimaryActionTriggered += this.ProbeButton_OnPrimaryActionTriggered;
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            foreach (Probe probe in m_probes)
            {
                probe.Button.PrimaryActionTriggered -= this.ProbeButton_OnPrimaryActionTriggered;
            }
        }

        // ===========[ Events ]===================================
        private void ProbeButton_OnPrimaryActionTriggered (object sender, EventArgs e)
        {
            Probe probe = m_probes.Find(candidate => ReferenceEquals(candidate.Button, sender));
            if (probe != null)
            {
                _ = this.ProbeAsync(probe.Name, probe.Make);
            }
        }

        // ===========[ Public Interface Methods ]===================================
        protected override void BuildPage ()
        {
            this.AddHeading("Leak Probe");
            this.AddText("Shows a control in this page with its pages on screen, takes it out, drops it without Dispose, and says whether it was collected. Collected is the pass; still alive means something it holds leads back to it.");

            // The probes: a name and what makes the control, with its pages on screen
            this.AddProbe("PageStack", "Probe a PageStack, with dots", MakePageStack);
            this.AddProbe("DateTimeEditIngressControl", "Probe a DateTimeEditIngressControl, date and time", MakeDateTimeEditIngressControl);

            this.AddDemo(m_result);
            m_probeArea.WidthAnchor.ConstraintEqualTo(kProbeWidth).Active = true;
            m_probeArea.HeightAnchor.ConstraintEqualTo(kProbeHeight).Active = true;
            this.AddDemo(m_probeArea);
        }

        private void AddProbe (string name, string title, Func<UIView> make)
        {
            UIButton button = UIButton.FromType(UIButtonType.System);
            button.SetTitle(title, UIControlState.Normal);
            m_probes.Add(new Probe(button, name, make));
            this.AddDemo(button);
        }

        private async Task ProbeAsync (string name, Func<UIView> make)
        {
            const int kPasses = 4;
            const int kPauseMilliseconds = 150;

            if (m_isProbing || (this.View.Window == null))
            {
                return;
            }

            m_isProbing = true;
            WeakReference dropped = this.ShowThenDrop(make);
            m_result.Text = $"{name}: probing";
            for (int pass = 0; pass < kPasses; ++pass)
            {
                // A pause lets the run loop drain its autorelease pool and UIKit let go of the views
                await Task.Delay(kPauseMilliseconds);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            m_result.Text = dropped.IsAlive ? $"{name}: still alive" : $"{name}: collected";
            m_isProbing = false;
        }

        // Kept out of line, so no reference to the control is left behind in the caller while it collects
        [MethodImpl(MethodImplOptions.NoInlining)]
        private WeakReference ShowThenDrop (Func<UIView> make)
        {
            UIView probed = make();
            probed.Frame = m_probeArea.Bounds;
            m_probeArea.AddSubview(probed);
            probed.LayoutIfNeeded();

            var dropped = new WeakReference(probed);
            probed.RemoveFromSuperview();
            return dropped;
        }

        // A stack mid way through its pages, so both edge dots show and the dot range is full
        private static UIView MakeDateTimeEditIngressControl ()
        {
            var editor = new DateTimeEditIngressControl
            {
                Frame = new CGRect(0, 0, kProbeWidth, kProbeHeight),
                IncludeTime = true,
                EditDateTime = DateTime.Now,
            };

            editor.LayoutIfNeeded();
            return editor;
        }

        private static UIView MakePageStack ()
        {
            const int kPageCount = 12;
            const int kShownPage = 6;

            var stack = new PageStack
            {
                Frame = new CGRect(0, 0, kProbeWidth, kProbeHeight),
                ShowDots = true,
                ShowPageCount = true,
                DotReach = 2,
            };

            for (int index = 0; index < kPageCount; ++index)
            {
                stack.Children.Add(new UILabel { Text = $"Page {index + 1}", TextAlignment = UITextAlignment.Center });
            }

            stack.SelectedIndex = kShownPage;
            stack.LayoutIfNeeded();
            return stack;
        }

        // ===========[ Subclasses/structs ]===================================
        // One probe: its button, the control's name as the result reports it, and what makes a fresh control to probe
        private sealed record Probe (UIButton Button, string Name, Func<UIView> Make);
    }
}
