namespace AJutShowRoomAndroid
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Content;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// Whether a control is collected once it leaves the page and is dropped without Dispose, as the AppKit showroom's
    /// Leak Probe checks. Each probe shows a fresh control on this page, lets it lay out and draw, takes it out, drops it,
    /// and then collects with pauses between passes, so Android has let go of the views and any deferred work (a frame
    /// callback, a posted message) has run before the last pass looks. Each pass collects on both sides of the bridge: a
    /// managed collect, then a Java one, then a managed one again, since a view's managed peer only goes once its Java
    /// view is unreachable. A new probe is one line in <see cref="kProbes"/>.
    /// </summary>
    public sealed class LeakProbePage : ShowroomPage
    {
        private const int kPasses = 4;
        private const int kPauseMilliseconds = 150;
        private const int kShownMilliseconds = 300;

        private static readonly Probe[] kProbes =
        {
            new Probe("Probe a PageStack, with dots", "PageStack", MakePageStack, StartPageStackTransition),
        };

        private FrameLayout? m_probeArea;
        private TextView? m_result;
        private bool m_isProbing;

        // ===========[ Properties ]===================================
        public override string Title => "Leak Probe";

        public override int LayoutId => Resource.Layout.page_leak_probe;

        // ===========[ Public Interface Methods ]===================================
        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_probeArea = root.FindViewById<FrameLayout>(Resource.Id.leak_probe_area)!;
            m_result = root.FindViewById<TextView>(Resource.Id.leak_probe_result)!;
            var buttons = root.FindViewById<LinearLayout>(Resource.Id.leak_probe_buttons)!;
            foreach (Probe probe in kProbes)
            {
                var button = new Button(host) { Text = probe.Title };
                buttons.AddView(button);
                binder.OnClick(button, () => _ = this.ProbeAsync(probe));
            }
        }

        public override void Unbind ()
        {
            m_probeArea = null;
            m_result = null;
        }

        private async Task ProbeAsync (Probe probe)
        {
            if (m_isProbing || (m_probeArea == null) || (m_result == null))
            {
                return;
            }

            m_isProbing = true;
            m_result.Text = $"{probe.Name}: probing";

            // 1. On the page long enough to lay out, draw and settle
            WeakReference shown = Show(probe, m_probeArea);
            await Task.Delay(kShownMilliseconds);

            // 2. Taken out, with whatever it does as it goes already going
            TakeOut(probe, shown, m_probeArea);

            // 3. Collected with pauses, so the frame callbacks and messages it left have run
            for (int pass = 0; pass < kPasses; ++pass)
            {
                await Task.Delay(kPauseMilliseconds);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Java.Lang.Runtime.GetRuntime()?.Gc();
                GC.Collect();
            }

            if (m_result != null)
            {
                m_result.Text = shown.IsAlive ? $"{probe.Name}: still alive" : $"{probe.Name}: collected";
            }

            m_isProbing = false;
        }

        // Kept out of line, so no reference to the control is left behind in the caller while it collects
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference Show (Probe probe, FrameLayout area)
        {
            View probed = probe.Make(area.Context!);
            area.AddView(probed, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
            return new WeakReference(probed);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TakeOut (Probe probe, WeakReference shown, FrameLayout area)
        {
            if (shown.Target is View probed)
            {
                probe.BeforeTakingOut?.Invoke(probed);
            }

            area.RemoveAllViews();
        }

        private static View MakePageStack (Context context)
        {
            const int kPageCount = 12;
            const int kShownPage = 6;

            var stack = new PageStack(context) { ShowDots = true, DotReach = 2, ShowPageCount = true };
            for (int index = 0; index < kPageCount; ++index)
            {
                stack.Children.Add(new Button(context) { Text = $"Page {index + 1}" });
            }

            stack.SelectedIndex = kShownPage;
            return stack;
        }

        // Taken out mid transition, so the frame callback it was stepping on has to be let go of too
        private static void StartPageStackTransition (View probed)
        {
            if (probed is PageStack stack)
            {
                stack.SelectedIndex = stack.SelectedIndex + 1;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>One probe: its button's title, the name it reports under, how to make the control, and anything to do to it just before it is taken out</summary>
        private sealed record Probe (string Title, string Name, Func<Context, View> Make, Action<View>? BeforeTakingOut = null);
    }
}
