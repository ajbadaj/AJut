namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AJut.UX;
    using AJut.UX.Controls;
    using Microsoft.UI.Input;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;

    /// <summary>
    /// Probes that drive the real main window and its ThemedWindowRootControl: the maximize/restore chrome sync,
    /// closing a window that carries the chrome, how far pointer input reaches over the system caption buttons, what
    /// the non-client pointer source reports, the chrome state machine as it changes, and whether a drag rectangle
    /// customizer that is not ready yet is waited on. Every watch this page starts is stopped in Unloaded, and the
    /// chrome is always handed back the way it was found.
    /// </summary>
    public sealed partial class WindowChromePage : Page
    {
        private const double kThicknessMatchEpsilon = 0.5;
        private const int kThemedWindowCloseCycles = 2;

        private const double kNativeCaptionStripHeight = 32;
        private const double kNativeCloseButtonWidth = 46;
        private const double kNativeChromeButtonsWidth = 245;

        private const string kNonClientWatchStartLabel = "Probe: non-client pointer events (start)";
        private const string kNonClientWatchStopLabel = "Stop watching non-client pointer events and report";
        private const string kStateWatchStartLabel = "Probe: chrome state watch (start)";
        private const string kStateWatchStopLabel = "Stop watching chrome state";
        private const long kNoCallbackToken = -1;

        // Title bar pointer reach probe
        private bool m_titleBarPointerCaptureActive;
        private int m_titleBarPointerSamples;
        private double m_titleBarPointerMinX;
        private double m_titleBarPointerMaxX;
        private double m_titleBarPointerMinY;
        private double m_titleBarPointerMaxY;
        private bool m_titleBarPointerReachedChromeBand;
        private bool m_titleBarPointerReachedCloseBand;
        private readonly List<string> m_titleBarPointerStatesSeen = new List<string>();
        private Microsoft.UI.Xaml.Input.PointerEventHandler m_titleBarPointerHandler;

        // Non-client pointer events probe
        private bool m_nonClientWatchActive;
        private InputNonClientPointerSource m_nonClientWatchSource;
        private readonly HashSet<string> m_nonClientPairsSeen = new HashSet<string>();
        private readonly Dictionary<NonClientRegionKind, int> m_nonClientCountsByRegion = new Dictionary<NonClientRegionKind, int>();
        private int m_nonClientEventTotal;

        // Chrome state watch probe
        private bool m_stateWatchActive;
        private long m_stateWatchLastStateToken = kNoCallbackToken;
        private long m_stateWatchHoverToken = kNoCallbackToken;

        // Drag rectangle customizer readiness probe
        private bool m_customizerProbeInstalled;

        // ===========[ Setup/Construction/Teardown ]=====================================

        public WindowChromePage ()
        {
            this.InitializeComponent();
            this.Unloaded += this.OnUnloaded;
        }

        // The page is built fresh on every navigation and dropped on the way out, so anything still attached
        // to the chrome when the page leaves would outlive the page and keep writing to a dead output box.
        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            if (m_titleBarPointerCaptureActive)
            {
                this.WindowChrome_StopTitleBarPointerCapture();
            }

            if (m_nonClientWatchActive)
            {
                this.NonClientWatch_Stop();
            }

            if (m_stateWatchActive)
            {
                this.StateWatch_Stop();
            }

            this.CustomizerProbe_Restore();
        }

        // ===========[ Window Chrome probe ]=============================================
        // Repro for the maximize/restore chrome desync. Maximize and restore keep the
        // OverlappedPresenter kind (only its State flips Restored<->Maximized), so the
        // AppWindow.Changed handler that only recomputed on a presenter-kind change left
        // IsMaximizedOrFullScreened and the title-bar border stale. A fullscreen round-trip
        // (which DOES change the presenter kind) was the only thing that snapped them back.
        // This drives the real window through maximize then restore and checks the chrome
        // tracks the state at each step - it is left restored when the probe finishes.

        private async void WindowChrome_OnMaximizeRestoreClicked (object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                this.WindowChrome_Output.Text = string.Empty;

                MainWindow window = App.Instance.MainWindow;
                if (window.AppWindow.Presenter is not OverlappedPresenter)
                {
                    this.WindowChrome_AppendLine("INCONCLUSIVE - window is not using an OverlappedPresenter");
                    return;
                }

                var results = new List<string>();
                bool allPass = true;

                // Baseline: start restored so the maximize step is a real transition.
                window.PerformPresenterTask<OverlappedPresenter>(p => p.Restore());
                await this.WindowChrome_SettleAsync();

                Thickness target = window.Chrome.TargetBorderThickness;

                // 1. Maximize - IsMaximizedOrFullScreened must go true, title-bar border must collapse to 0.
                window.PerformPresenterTask<OverlappedPresenter>(p => p.Maximize());
                await this.WindowChrome_SettleAsync();
                allPass &= this.WindowChrome_Check(
                    results,
                    "After maximize",
                    expectMaximizedState: true,
                    expectIsMaximizedOrFullScreened: true,
                    expectedTitleBarBorder: new Thickness(0)
                );

                // 2. Restore - IsMaximizedOrFullScreened must go false, border back to the target.
                window.PerformPresenterTask<OverlappedPresenter>(p => p.Restore());
                await this.WindowChrome_SettleAsync();
                allPass &= this.WindowChrome_Check(
                    results,
                    "After restore",
                    expectMaximizedState: false,
                    expectIsMaximizedOrFullScreened: false,
                    expectedTitleBarBorder: target
                );

                this.WindowChrome_AppendLine(allPass
                    ? "PASS - chrome tracked maximize and restore"
                    : "FAIL - chrome went stale (maximize/restore did not update IsMaximizedOrFullScreened / title-bar border)");
                foreach (string line in results)
                {
                    this.WindowChrome_AppendLine("    " + line);
                }
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        private bool WindowChrome_Check (List<string> results, string label, bool expectMaximizedState, bool expectIsMaximizedOrFullScreened, Thickness expectedTitleBarBorder)
        {
            MainWindow window = App.Instance.MainWindow;
            eWindowState state = window.GetWindowState();
            bool stateOk = (state == eWindowState.Maximized) == expectMaximizedState;
            bool flagOk = window.Chrome.IsMaximizedOrFullScreened == expectIsMaximizedOrFullScreened;

            Thickness actualBorder = window.Chrome.PART_TitleBarBorder?.BorderThickness ?? new Thickness(double.NaN);
            bool borderOk = WindowChrome_ThicknessesMatch(actualBorder, expectedTitleBarBorder);

            bool ok = stateOk && flagOk && borderOk;
            results.Add(string.Format(
                "{0}: {1} | state={2} (want {3}), IsMaximizedOrFullScreened={4} (want {5}), titleBarBorder={6} (want {7})",
                label,
                ok ? "ok" : "MISMATCH",
                state,
                expectMaximizedState ? "Maximized" : "not Maximized",
                window.Chrome.IsMaximizedOrFullScreened,
                expectIsMaximizedOrFullScreened,
                WindowChrome_FormatThickness(actualBorder),
                WindowChrome_FormatThickness(expectedTitleBarBorder)
            ));
            return ok;
        }

        // Let the AppWindow.Changed event fire and its handler (plus any Low-priority
        // dispatcher follow-up it enqueues) fully run before the chrome is read.
        private async Task WindowChrome_SettleAsync ()
        {
            for (int i = 0; i < 4; ++i)
            {
                await LeakProbeHelpers.DrainDispatcherAsync();
                await Task.Delay(80);
            }
        }

        private static bool WindowChrome_ThicknessesMatch (Thickness a, Thickness b)
        {
            return Math.Abs(a.Left - b.Left) < kThicknessMatchEpsilon
                && Math.Abs(a.Top - b.Top) < kThicknessMatchEpsilon
                && Math.Abs(a.Right - b.Right) < kThicknessMatchEpsilon
                && Math.Abs(a.Bottom - b.Bottom) < kThicknessMatchEpsilon;
        }

        private static string WindowChrome_FormatThickness (Thickness t) => $"({t.Left},{t.Top},{t.Right},{t.Bottom})";

        private void WindowChrome_AppendLine (string line)
        {
            this.WindowChrome_Output.Text = this.WindowChrome_Output.Text.Length == 0
                ? line
                : this.WindowChrome_Output.Text + "\n" + line;
        }

        private void WindowChrome_OnClearOutputClicked (object sender, RoutedEventArgs e)
        {
            this.WindowChrome_Output.Text = string.Empty;
        }

        // ===========[ Themed-chrome window close probe ]=================================
        // Closing a window that carries ThemedWindowRootControl fails fast inside
        // Microsoft.UI.Windowing (E_POINTER, main thread) because the chrome leaves
        // Window.Activated subscriptions alive past the point the native window is gone.
        // It has never been caught because this showroom's main window is the only thing
        // that carries the chrome, and it lives for the whole process.
        //
        // A fail fast is not catchable, so this probe cannot print FAIL - the showroom
        // dying when the button is clicked IS the failure. Surviving is the pass, and the
        // weak-ref tally catches the softer variant where the window is pinned, not killed.

        private async void WindowChrome_OnCloseThemedWindowClicked (object sender, RoutedEventArgs e)
        {
            await this.WindowChrome_RunThemedWindowCloseProbe(sender as Button, "Polite consumer", disconnectBeforeClose: true);
        }

        // Same run without the DisconnectFromOwnerWindow call, which is what most consumers will
        // actually do. It is a separate button rather than a second cycle of the one above so that
        // the polite result is already on screen and readable if this one takes the process down.
        private async void WindowChrome_OnCloseThemedWindowNoDisconnectClicked (object sender, RoutedEventArgs e)
        {
            await this.WindowChrome_RunThemedWindowCloseProbe(sender as Button, "Forgetful consumer", disconnectBeforeClose: false);
        }

        private async Task WindowChrome_RunThemedWindowCloseProbe (Button button, string label, bool disconnectBeforeClose)
        {
            if (button != null) { button.IsEnabled = false; }
            try
            {
                this.WindowChrome_Output.Text = string.Empty;
                this.WindowChrome_AppendLine($"{label}: opening and closing {kThemedWindowCloseCycles} themed-chrome windows - a crash from here is the failure.");

                var weaks = new List<WeakReference>();
                for (int i = 0; i < kThemedWindowCloseCycles; ++i)
                {
                    var themedRoot = new ThemedWindowRootControl
                    {
                        TitleBarHeight = 32,
                        WindowContents = new TextBlock { Text = "Themed chrome close probe", Margin = new Thickness(16) },
                    };

                    var childWindow = new Window();
                    childWindow.Content = themedRoot;
                    themedRoot.SetupFor(childWindow);
                    childWindow.Activate();

                    await LeakProbeHelpers.WaitForLoaded(themedRoot, 3000);
                    await Task.Delay(150);

                    // Pull focus back here so the child takes a real Deactivated pass while it is
                    // still alive. That event is what the stale handlers ride in on at close time.
                    App.Instance.MainWindow.Activate();
                    await LeakProbeHelpers.DrainDispatcherAsync();
                    await Task.Delay(150);

                    if (disconnectBeforeClose)
                    {
                        themedRoot.DisconnectFromOwnerWindow();
                    }

                    // Nulling Content is the standard way to drop a child window's page graph, and
                    // it is also what guarantees a null Window.Content for anything still listening
                    // to Activated as the window goes away.
                    childWindow.Content = null;
                    themedRoot = null;

                    weaks.Add(new WeakReference(childWindow));
                    childWindow.Close();
                    childWindow = null;

                    await LeakProbeHelpers.DrainDispatcherAsync();
                    await Task.Delay(250);
                }

                await LeakProbeHelpers.SettleAndCollectAsync();

                int alive = weaks.Count(w => w.IsAlive);
                this.WindowChrome_AppendLine(alive == 0
                    ? $"PASS - all {kThemedWindowCloseCycles} themed windows opened, closed, and collected"
                    : $"PARTIAL - survived the close, but {alive}/{kThemedWindowCloseCycles} windows are still pinned after GC");
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        // ===========[ Title bar pointer reach probe ]====================================
        // The one question reading the source cannot answer: does pointer input actually reach
        // the chrome control over the native minimize / maximize / close strip? HandlePointerMoved
        // only fires CloseHover when x lands in the rightmost 46, and ChromeButtonsHover in the
        // rightmost 245 - but if the system owns that input, neither branch can ever run and the
        // geometry is beside the point. This records what the control really receives.
        //
        // The chrome's colors come off LastState, not off the template's visual states - those
        // storyboards never run, because the VisualStateGroups hang off the Grid inside
        // PART_WindowRoot rather than off the ControlTemplate root that GoToState inspects.

        private void WindowChrome_OnCaptureTitleBarPointerClicked (object sender, RoutedEventArgs e)
        {
            if (m_titleBarPointerCaptureActive)
            {
                this.WindowChrome_StopTitleBarPointerCapture();
            }
            else
            {
                this.WindowChrome_StartTitleBarPointerCapture();
            }
        }

        private void WindowChrome_StartTitleBarPointerCapture ()
        {
            m_titleBarPointerSamples = 0;
            m_titleBarPointerMinX = double.MaxValue;
            m_titleBarPointerMaxX = double.MinValue;
            m_titleBarPointerMinY = double.MaxValue;
            m_titleBarPointerMaxY = double.MinValue;
            m_titleBarPointerReachedChromeBand = false;
            m_titleBarPointerReachedCloseBand = false;
            m_titleBarPointerStatesSeen.Clear();

            if (m_titleBarPointerHandler == null)
            {
                m_titleBarPointerHandler = new Microsoft.UI.Xaml.Input.PointerEventHandler(this.WindowChrome_OnTitleBarPointerMoved);
            }

            // handledEventsToo, same as the control's own subscription - otherwise anything that
            // marks the move handled upstream would make this read a quieter window than the real one.
            ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
            chrome.RemoveHandler(UIElement.PointerMovedEvent, m_titleBarPointerHandler);
            chrome.AddHandler(UIElement.PointerMovedEvent, m_titleBarPointerHandler, true);

            m_titleBarPointerCaptureActive = true;
            this.WindowChrome_CaptureTitleBarPointerButton.Content = "Stop capturing and report";
            this.WindowChrome_Output.Text = "Capturing. Sweep the pointer slowly left to right across the whole title bar, out over the minimize / maximize / close buttons and back, then click Stop.";
        }

        private void WindowChrome_StopTitleBarPointerCapture ()
        {
            ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
            chrome.RemoveHandler(UIElement.PointerMovedEvent, m_titleBarPointerHandler);
            m_titleBarPointerCaptureActive = false;
            this.WindowChrome_CaptureTitleBarPointerButton.Content = "Probe: title bar pointer reach";

            this.WindowChrome_Output.Text = string.Empty;
            if (m_titleBarPointerSamples == 0)
            {
                this.WindowChrome_AppendLine($"INCONCLUSIVE - no pointer samples landed in the top {kNativeCaptionStripHeight:0} of the control");
                return;
            }

            double width = chrome.ActualWidth;
            this.WindowChrome_AppendLine(m_titleBarPointerReachedCloseBand
                ? "Input DOES reach the close band - so a hover miss is the hover logic, not the input"
                : "Input does NOT reach the close band - the system owns that area, so the CloseHover branch can never fire there no matter what the geometry says");

            this.WindowChrome_AppendLine($"    samples={m_titleBarPointerSamples}, control width={width:0}");
            this.WindowChrome_AppendLine($"    x reached {m_titleBarPointerMinX:0} to {m_titleBarPointerMaxX:0}, y reached {m_titleBarPointerMinY:0} to {m_titleBarPointerMaxY:0}");
            this.WindowChrome_AppendLine($"    chrome-buttons band (x > {width - kNativeChromeButtonsWidth:0}): {(m_titleBarPointerReachedChromeBand ? "reached" : "never reached")}");
            this.WindowChrome_AppendLine($"    close band (x > {width - kNativeCloseButtonWidth:0}): {(m_titleBarPointerReachedCloseBand ? "reached" : "never reached")}");
            this.WindowChrome_AppendLine($"    LastState values seen while sweeping: {string.Join(", ", m_titleBarPointerStatesSeen)}");
        }

        private void WindowChrome_OnTitleBarPointerMoved (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
            Windows.Foundation.Point point = e.GetCurrentPoint(chrome).Position;
            if (point.Y > kNativeCaptionStripHeight)
            {
                return;
            }

            ++m_titleBarPointerSamples;
            m_titleBarPointerMinX = Math.Min(m_titleBarPointerMinX, point.X);
            m_titleBarPointerMaxX = Math.Max(m_titleBarPointerMaxX, point.X);
            m_titleBarPointerMinY = Math.Min(m_titleBarPointerMinY, point.Y);
            m_titleBarPointerMaxY = Math.Max(m_titleBarPointerMaxY, point.Y);

            double width = chrome.ActualWidth;
            m_titleBarPointerReachedChromeBand |= point.X > width - kNativeChromeButtonsWidth;
            m_titleBarPointerReachedCloseBand |= point.X > width - kNativeCloseButtonWidth;

            string state = chrome.LastState ?? "(null)";
            if (!m_titleBarPointerStatesSeen.Contains(state))
            {
                m_titleBarPointerStatesSeen.Add(state);
            }
        }

        // ===========[ Non-client pointer events probe ]==================================
        // The system owns the pointer while it is over its own caption buttons, so the XAML
        // pointer events on the chrome never fire there (the reach probe above shows exactly
        // where they stop). InputNonClientPointerSource is the window's report of that input,
        // one event per region crossing with the region's kind, and the chrome's close hover
        // is built on nothing else. This listens to the same source, independently of the
        // chrome, and logs which (event, region) pairs actually arrive on this machine.

        private void WindowChrome_OnNonClientWatchClicked (object sender, RoutedEventArgs e)
        {
            if (m_nonClientWatchActive)
            {
                this.NonClientWatch_Stop();
            }
            else
            {
                this.NonClientWatch_Start();
            }
        }

        private void NonClientWatch_Start ()
        {
            m_nonClientPairsSeen.Clear();
            m_nonClientCountsByRegion.Clear();
            m_nonClientEventTotal = 0;

            this.WindowChrome_Output.Text = string.Empty;

            InputNonClientPointerSource source = InputNonClientPointerSource.GetForWindowId(App.Instance.MainWindow.GetWindowId());
            if (source == null)
            {
                this.WindowChrome_AppendLine("INCONCLUSIVE - InputNonClientPointerSource.GetForWindowId returned null for the main window, so there is nothing to listen to");
                return;
            }

            this.NonClientWatch_Unsubscribe();
            m_nonClientWatchSource = source;
            m_nonClientWatchSource.PointerEntered += this.NonClientWatch_OnPointerEntered;
            m_nonClientWatchSource.PointerMoved += this.NonClientWatch_OnPointerMoved;
            m_nonClientWatchSource.PointerExited += this.NonClientWatch_OnPointerExited;
            m_nonClientWatchSource.PointerPressed += this.NonClientWatch_OnPointerPressed;
            m_nonClientWatchSource.PointerReleased += this.NonClientWatch_OnPointerReleased;

            m_nonClientWatchActive = true;
            this.WindowChrome_NonClientWatchButton.Content = kNonClientWatchStopLabel;
            this.WindowChrome_AppendLine("Watching the main window's non-client pointer source. Sweep the pointer across the title bar and over the minimize, maximize and close buttons, press and release on one if you like, then click Stop.");
            this.WindowChrome_AppendLine("Each (event, region) pair prints once, the first time it arrives:");
        }

        private void NonClientWatch_Stop ()
        {
            this.NonClientWatch_Unsubscribe();
            m_nonClientWatchActive = false;
            this.WindowChrome_NonClientWatchButton.Content = kNonClientWatchStartLabel;

            ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
            this.WindowChrome_AppendLine("");
            this.WindowChrome_AppendLine("Summary:");
            if (m_nonClientEventTotal == 0)
            {
                this.WindowChrome_AppendLine("    no non-client pointer events arrived at all - either the pointer never crossed a non-client region, or the source is not reporting on this machine");
            }
            else
            {
                this.WindowChrome_AppendLine($"    {m_nonClientEventTotal} events across {m_nonClientCountsByRegion.Count} regions, {m_nonClientPairsSeen.Count} distinct (event, region) pairs");
                foreach (KeyValuePair<NonClientRegionKind, int> entry in m_nonClientCountsByRegion.OrderByDescending(kvp => kvp.Value))
                {
                    this.WindowChrome_AppendLine($"    {entry.Key}: {entry.Value}");
                }

                bool sawClose = m_nonClientCountsByRegion.ContainsKey(NonClientRegionKind.Close);
                this.WindowChrome_AppendLine(sawClose
                    ? "    Close region reported - the close hover has the input it needs"
                    : "    Close region never reported - the chrome cannot see a hover over the system close button on this machine, whatever it does with the events");
            }

            this.WindowChrome_AppendLine($"    chrome.HasSeenNonClientCaptionInput={chrome.HasSeenNonClientCaptionInput}");
            this.WindowChrome_AppendLine($"    chrome.LastState={chrome.LastState ?? "(null)"}, chrome.IsHoveringOverClose={chrome.IsHoveringOverClose}");
        }

        private void NonClientWatch_Unsubscribe ()
        {
            if (m_nonClientWatchSource == null)
            {
                return;
            }

            m_nonClientWatchSource.PointerEntered -= this.NonClientWatch_OnPointerEntered;
            m_nonClientWatchSource.PointerMoved -= this.NonClientWatch_OnPointerMoved;
            m_nonClientWatchSource.PointerExited -= this.NonClientWatch_OnPointerExited;
            m_nonClientWatchSource.PointerPressed -= this.NonClientWatch_OnPointerPressed;
            m_nonClientWatchSource.PointerReleased -= this.NonClientWatch_OnPointerReleased;
            m_nonClientWatchSource = null;
        }

        private void NonClientWatch_OnPointerEntered (InputNonClientPointerSource sender, NonClientPointerEventArgs args) => this.NonClientWatch_Record("PointerEntered", args);
        private void NonClientWatch_OnPointerMoved (InputNonClientPointerSource sender, NonClientPointerEventArgs args) => this.NonClientWatch_Record("PointerMoved", args);
        private void NonClientWatch_OnPointerExited (InputNonClientPointerSource sender, NonClientPointerEventArgs args) => this.NonClientWatch_Record("PointerExited", args);
        private void NonClientWatch_OnPointerPressed (InputNonClientPointerSource sender, NonClientPointerEventArgs args) => this.NonClientWatch_Record("PointerPressed", args);
        private void NonClientWatch_OnPointerReleased (InputNonClientPointerSource sender, NonClientPointerEventArgs args) => this.NonClientWatch_Record("PointerReleased", args);

        private void NonClientWatch_Record (string eventName, NonClientPointerEventArgs args)
        {
            if (!m_nonClientWatchActive)
            {
                return;
            }

            ++m_nonClientEventTotal;
            m_nonClientCountsByRegion.TryGetValue(args.RegionKind, out int regionCount);
            m_nonClientCountsByRegion[args.RegionKind] = regionCount + 1;

            string pair = $"{eventName} / {args.RegionKind}";
            if (m_nonClientPairsSeen.Add(pair))
            {
                this.WindowChrome_AppendLine($"    [{DateTime.Now:HH:mm:ss.fff}] {pair} at ({args.Point.X:0},{args.Point.Y:0})");
            }
        }

        // ===========[ Chrome state watch probe ]=========================================
        // LastState and IsHoveringOverClose are the whole chrome state machine; the border and
        // title bar brushes are pushed from them whenever either changes. Watching both change
        // with the window root's border color next to each line is the direct read on whether
        // the border really goes to the close-hover brush while the pointer is over close.

        private void WindowChrome_OnStateWatchClicked (object sender, RoutedEventArgs e)
        {
            if (m_stateWatchActive)
            {
                this.StateWatch_Stop();
            }
            else
            {
                this.StateWatch_Start();
            }
        }

        private void StateWatch_Start ()
        {
            this.StateWatch_Unregister();

            ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
            m_stateWatchLastStateToken = chrome.RegisterPropertyChangedCallback(ThemedWindowRootControl.LastStateProperty, this.StateWatch_OnLastStateChanged);
            m_stateWatchHoverToken = chrome.RegisterPropertyChangedCallback(ThemedWindowRootControl.IsHoveringOverCloseProperty, this.StateWatch_OnIsHoveringOverCloseChanged);

            m_stateWatchActive = true;
            this.WindowChrome_StateWatchButton.Content = kStateWatchStopLabel;
            this.WindowChrome_Output.Text = string.Empty;
            this.WindowChrome_AppendLine("Watching the chrome's LastState and IsHoveringOverClose. Move the pointer around the title bar and over the system buttons, then click Stop.");
            this.WindowChrome_AppendLine($"    [{DateTime.Now:HH:mm:ss.fff}] start: LastState={chrome.LastState ?? "(null)"}, IsHoveringOverClose={chrome.IsHoveringOverClose}, border={StateWatch_DescribeBorder(chrome)}");
        }

        private void StateWatch_Stop ()
        {
            this.StateWatch_Unregister();
            m_stateWatchActive = false;
            this.WindowChrome_StateWatchButton.Content = kStateWatchStartLabel;

            ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
            this.WindowChrome_AppendLine($"    [{DateTime.Now:HH:mm:ss.fff}] stop: LastState={chrome.LastState ?? "(null)"}, IsHoveringOverClose={chrome.IsHoveringOverClose}, border={StateWatch_DescribeBorder(chrome)}");
        }

        private void StateWatch_Unregister ()
        {
            ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
            if (m_stateWatchLastStateToken != kNoCallbackToken)
            {
                chrome.UnregisterPropertyChangedCallback(ThemedWindowRootControl.LastStateProperty, m_stateWatchLastStateToken);
                m_stateWatchLastStateToken = kNoCallbackToken;
            }

            if (m_stateWatchHoverToken != kNoCallbackToken)
            {
                chrome.UnregisterPropertyChangedCallback(ThemedWindowRootControl.IsHoveringOverCloseProperty, m_stateWatchHoverToken);
                m_stateWatchHoverToken = kNoCallbackToken;
            }
        }

        private void StateWatch_OnLastStateChanged (DependencyObject sender, DependencyProperty property)
        {
            if (sender is ThemedWindowRootControl chrome)
            {
                this.WindowChrome_AppendLine($"    [{DateTime.Now:HH:mm:ss.fff}] LastState -> {chrome.LastState ?? "(null)"}, border={StateWatch_DescribeBorder(chrome)}");
            }
        }

        private void StateWatch_OnIsHoveringOverCloseChanged (DependencyObject sender, DependencyProperty property)
        {
            if (sender is ThemedWindowRootControl chrome)
            {
                this.WindowChrome_AppendLine($"    [{DateTime.Now:HH:mm:ss.fff}] IsHoveringOverClose -> {chrome.IsHoveringOverClose}, border={StateWatch_DescribeBorder(chrome)}");
            }
        }

        // The border the chrome actually drew: PART_WindowRoot.BorderBrush, after the state refresh swapped in
        // whichever of BorderBrush, InactiveBorderBrush or CloseHoverBorderBrush applies.
        private static string StateWatch_DescribeBorder (ThemedWindowRootControl chrome)
        {
            if (chrome.PART_WindowRoot == null)
            {
                return "(no PART_WindowRoot)";
            }

            if (chrome.PART_WindowRoot.BorderBrush is SolidColorBrush scb)
            {
                return CoerceUtils.GetSmallestHexString(scb.Color);
            }

            return chrome.PART_WindowRoot.BorderBrush == null ? "(null)" : $"({chrome.PART_WindowRoot.BorderBrush.GetType().Name})";
        }

        // ===========[ Drag rectangle customizer readiness probe ]========================
        // ResetTitleBarDragRectangles reads the customizer's IsReadyToGenerateTitleBarDragRectangles
        // and is meant to do one of two things: generate now when it is ready, or subscribe to
        // ReadyToGenerateTitleBarDragRectangles and generate when that fires. If the subscribe
        // branch is unreachable, a customizer that is not ready at reset time is never asked, and
        // the title bar has no drag region until some later reset happens to find it ready.

        private async void WindowChrome_OnCustomizerReadinessClicked (object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                this.WindowChrome_Output.Text = string.Empty;

                ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
                if (chrome.IsFullScreened)
                {
                    this.WindowChrome_AppendLine("INCONCLUSIVE - the window is fullscreen, and ResetTitleBarDragRectangles clears the drag region without consulting the customizer in that state. Leave fullscreen and run again.");
                    return;
                }

                if (chrome.TitleBarDragRectanglesCustomizer != null)
                {
                    this.WindowChrome_AppendLine("INCONCLUSIVE - the chrome already has a TitleBarDragRectanglesCustomizer, and this probe would clobber it");
                    return;
                }

                var sizer = new ReadinessProbeDragSizer(chrome);
                m_customizerProbeInstalled = true;
                chrome.TitleBarDragRectanglesCustomizer = sizer;

                // 1. Reset while the customizer says it is not ready. The chrome should subscribe and wait.
                chrome.ResetTitleBarDragRectangles();
                int callsWhileNotReady = sizer.GenerateCallCount;
                bool subscribedWhileNotReady = sizer.HasSubscribers;
                this.WindowChrome_AppendLine($"After reset while not ready: GenerateTitleBarDragRectangles calls={callsWhileNotReady}, something subscribed to the ready event={subscribedWhileNotReady}");

                // 2. Flip ready and raise the event. A waiting chrome generates now, once.
                sizer.MarkReady();
                await LeakProbeHelpers.DrainDispatcherAsync();
                int callsAfterReady = sizer.GenerateCallCount;
                this.WindowChrome_AppendLine($"After flipping ready and raising the event: GenerateTitleBarDragRectangles calls={callsAfterReady}, still subscribed={sizer.HasSubscribers}");

                if (callsWhileNotReady > 0)
                {
                    this.WindowChrome_AppendLine("FAIL - the customizer was asked to generate while it said it was not ready, so its readiness is ignored");
                }
                else if (callsAfterReady == 1)
                {
                    this.WindowChrome_AppendLine("PASS - nothing generated while not ready, and the chrome generated exactly once after the customizer raised its ready event");
                }
                else if (callsAfterReady == 0)
                {
                    this.WindowChrome_AppendLine("FAIL - GenerateTitleBarDragRectangles was never called: nothing subscribed to ReadyToGenerateTitleBarDragRectangles while the customizer was not ready, so a customizer that is not ready at reset time never gets its drag rectangles applied");
                }
                else
                {
                    this.WindowChrome_AppendLine($"FAIL - generated {callsAfterReady} times after one ready event, so the chrome subscribed more than once or did not unsubscribe after generating");
                }
            }
            finally
            {
                this.CustomizerProbe_Restore();
                this.WindowChrome_AppendLine("Customizer removed and the window's own drag rectangles reset.");
                if (button != null) { button.IsEnabled = true; }
            }
        }

        // Hands the chrome back the way it was found: no customizer, and its own drag rectangles regenerated so
        // the title bar stays draggable. Safe to call when the probe never installed anything.
        private void CustomizerProbe_Restore ()
        {
            if (!m_customizerProbeInstalled)
            {
                return;
            }

            m_customizerProbeInstalled = false;
            ThemedWindowRootControl chrome = App.Instance.MainWindow.Chrome;
            chrome.TitleBarDragRectanglesCustomizer = null;
            chrome.ResetTitleBarDragRectangles();
        }

        // ===========[ Subclasses/structs ]==============================================

        /// <summary>
        /// A drag rectangle customizer that starts not ready, counts how many times it is asked to generate, and can
        /// be flipped ready on demand. When it is asked, it hands back the chrome's own rectangles so the title bar
        /// keeps working while the probe runs.
        /// </summary>
        private sealed class ReadinessProbeDragSizer : ITitleBarDragSizer
        {
            private readonly ThemedWindowRootControl m_chrome;
            private bool m_isReady;

            public ReadinessProbeDragSizer (ThemedWindowRootControl chrome)
            {
                m_chrome = chrome;
            }

            public event EventHandler<EventArgs> ReadyToGenerateTitleBarDragRectangles;

            /// <summary>How many times the chrome asked this customizer for rectangles.</summary>
            public int GenerateCallCount { get; private set; }

            /// <summary>Whether anything is currently subscribed to the ready event.</summary>
            public bool HasSubscribers => this.ReadyToGenerateTitleBarDragRectangles != null;

            public Windows.Graphics.RectInt32[] GenerateTitleBarDragRectangles ()
            {
                ++this.GenerateCallCount;
                ITitleBarDragSizer chromeSizer = m_chrome;
                return chromeSizer.IsReadyToGenerateTitleBarDragRectangles()
                    ? chromeSizer.GenerateTitleBarDragRectangles()
                    : Array.Empty<Windows.Graphics.RectInt32>();
            }

            public bool IsReadyToGenerateTitleBarDragRectangles () => m_isReady;

            /// <summary>Flips the customizer ready and raises the ready event, the way a real customizer would once its layout settled.</summary>
            public void MarkReady ()
            {
                m_isReady = true;
                this.ReadyToGenerateTitleBarDragRectangles?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
