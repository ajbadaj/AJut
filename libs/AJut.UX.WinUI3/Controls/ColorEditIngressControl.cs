namespace AJut.UX.Controls
{
    using AJut.UX.Helpers;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Automation;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<ColorEditIngressControl>;

    // ===========[ ColorEditIngressControl ]====================================
    // WinUI3 color editor for PropertyGrid DataTemplates.  Shows a solid-color
    // swatch; tap/click opens a Flyout containing the native WinUI3 ColorPicker.
    //
    // Edit lifecycle (mirrors WPF ColorEditIngressControl):
    //   Tap swatch    -> cache EditColor, sync ColorPicker, ShowAt swatch
    //   ColorChanged  -> update EditColor in real time (live preview)
    //   Flyout closed -> fire UserEditComplete if color actually changed
    //
    // IsReadOnly blocks the flyout from opening.
    //
    // Eyedropper: a button under the ColorPicker (shown while AllowPickFromScreen) picks EditColor from
    // anywhere on screen through ScreenColorPicker, with a magnifier following the cursor unless
    // ShowPickFromScreenMagnifier is off. With HideWhilePickingFromScreen, the flyout is hidden before the
    // pick, so it is not in the pick's frozen capture. Either way the flyout is back after the pick, and its
    // closing for the pick does not end the edit: UserEditComplete still fires once, when the flyout really
    // closes, with the color from before the edit as the old value.
    //
    // No shared view model is needed - WinUI3's ColorPicker handles all
    // color-space math natively, so the WPF TextBox-based ColorValueEditControl
    // has no equivalent here.
    //
    // Template parts:
    //   PART_Root    - outer Border (BorderBrush driven by EditingStates VSM group)
    //   PART_Swatch  - inner Border whose Background is kept in sync with EditColor
    //
    // VSM groups:
    //   CommonStates  - Normal (gloss visible) / PointerOver (gloss hidden, edit overlay shown)
    //   EditingStates - EditingClosed (normal border) / EditingOpen (highlight border)

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_Swatch), Type = typeof(Border))]
    public class ColorEditIngressControl : Control, IUserEditNotifier
    {
        private const string kPickFromScreenButtonStyleKey = "PickFromScreenButtonStyle";

        // ===========[ Instance fields ]==========================================
        private Border PART_Root;
        private Border PART_Swatch;
        private Flyout m_flyout;
        private ColorPicker m_colorPicker;
        private Button m_pickFromScreenButton;
        private Color? m_editCache;
        private bool m_isPickingFromScreen;
        private CancellationTokenSource m_pickCancel;
        private TaskCompletionSource m_flyoutHiddenForPick;

        // ===========[ Construction ]=============================================
        public ColorEditIngressControl()
        {
            this.DefaultStyleKey = typeof(ColorEditIngressControl);
            this.PointerEntered += this.OnPointerEntered;
            this.PointerExited += this.OnPointerExited;
            this.Unloaded += this.OnUnloaded;
        }

        // ===========[ IUserEditNotifier ]========================================
        /// <inheritdoc/>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        // ===========[ Dependency Properties ]====================================

        // EditColor: the color being edited.  Bind TwoWay to PropertyEditTarget.EditValue.
        public static readonly DependencyProperty EditColorProperty = DPUtils.Register(_ => _.EditColor, (d, e) => d.OnEditColorChanged(e.NewValue));
        public Color EditColor
        {
            get => (Color)this.GetValue(EditColorProperty);
            set => this.SetValue(EditColorProperty, value);
        }

        // IsReadOnly: prevents the flyout from opening.
        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        // ShowEditDisplay: true while the flyout is open; drives border highlight + VSM state.
        public static readonly DependencyProperty ShowEditDisplayProperty = DPUtils.Register(_ => _.ShowEditDisplay, (d, e) => d.OnShowEditDisplayChanged((bool)e.NewValue));
        public bool ShowEditDisplay
        {
            get => (bool)this.GetValue(ShowEditDisplayProperty);
            private set => this.SetValue(ShowEditDisplayProperty, value);
        }

        public static readonly DependencyProperty IsPointerOverProperty = DPUtils.Register(_ => _.IsPointerOver);
        public bool IsPointerOver
        {
            get => (bool)this.GetValue(IsPointerOverProperty);
            set => this.SetValue(IsPointerOverProperty, value);
        }

        public static readonly DependencyProperty AllowPickFromScreenProperty = DPUtils.Register(_ => _.AllowPickFromScreen, true, (d, e) => d.UpdatePickFromScreenButtonVisibility());
        /// <summary>Whether the flyout offers its eyedropper, which picks the color from anywhere on screen</summary>
        public bool AllowPickFromScreen
        {
            get => (bool)this.GetValue(AllowPickFromScreenProperty);
            set => this.SetValue(AllowPickFromScreenProperty, value);
        }

        public static readonly DependencyProperty HideWhilePickingFromScreenProperty = DPUtils.Register(_ => _.HideWhilePickingFromScreen);
        /// <summary>
        /// Whether the flyout hides before the eyedropper's pick starts. The pick works from a frozen capture of the
        /// screen, so a flyout left showing is in that capture, over whatever is behind it.
        /// </summary>
        public bool HideWhilePickingFromScreen
        {
            get => (bool)this.GetValue(HideWhilePickingFromScreenProperty);
            set => this.SetValue(HideWhilePickingFromScreenProperty, value);
        }

        public static readonly DependencyProperty ShowPickFromScreenMagnifierProperty = DPUtils.Register(_ => _.ShowPickFromScreenMagnifier, true);
        /// <summary>Whether a magnifier follows the cursor during an eyedropper pick, showing what it would take</summary>
        public bool ShowPickFromScreenMagnifier
        {
            get => (bool)this.GetValue(ShowPickFromScreenMagnifierProperty);
            set => this.SetValue(ShowPickFromScreenMagnifierProperty, value);
        }

        public static readonly DependencyProperty PickFromScreenMagnifierPixelCountProperty = DPUtils.Register(_ => _.PickFromScreenMagnifierPixelCount, ScreenColorPickOptions.kDefaultMagnifierPixelCount);
        /// <summary>How many pixels across the eyedropper's magnifier shows, rounded up to odd so one sits at its center</summary>
        public int PickFromScreenMagnifierPixelCount
        {
            get => (int)this.GetValue(PickFromScreenMagnifierPixelCountProperty);
            set => this.SetValue(PickFromScreenMagnifierPixelCountProperty, value);
        }

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (this.PART_Swatch != null)
            {
                this.PART_Swatch.Tapped -= this.Swatch_OnTapped;
            }

            this.PART_Root = (Border)this.GetTemplateChild(nameof(PART_Root));
            this.PART_Swatch = (Border)this.GetTemplateChild(nameof(PART_Swatch));
            if (this.PART_Swatch == null)
            {
                return;
            }

            this.PART_Swatch.Background = new SolidColorBrush(this.EditColor);
            this.PART_Swatch.Tapped += this.Swatch_OnTapped;

            // Seed VSM into initial states on first template application.
            VisualStateManager.GoToState(this, "Normal", false);
            VisualStateManager.GoToState(this, "EditingClosed", false);

            // Build flyout + ColorPicker once; reuse across opens.
            // Created in code rather than XAML to avoid GetTemplateChild limitations
            // on items inside attached-property flyouts in WinUI3 ControlTemplates.
            if (m_flyout == null)
            {
                m_colorPicker = new ColorPicker
                {
                    IsAlphaEnabled = true,
                    IsMoreButtonVisible = false,
                };
                m_colorPicker.ColorChanged += this.ColorPicker_OnColorChanged;

                // The eyedropper sits under the native picker, which can't be templated to take it, so it joins the
                // picker in the flyout content built here. Its look is the template's PickFromScreenButtonStyle.
                const string kPickFromScreenText = "Pick a color from the screen";
                m_pickFromScreenButton = new Button();
                ToolTipService.SetToolTip(m_pickFromScreenButton, kPickFromScreenText);
                AutomationProperties.SetName(m_pickFromScreenButton, kPickFromScreenText);
                m_pickFromScreenButton.Click += this.PickFromScreenButton_OnClick;

                var flyoutContent = new StackPanel();
                flyoutContent.Children.Add(m_colorPicker);
                flyoutContent.Children.Add(m_pickFromScreenButton);

                m_flyout = new Flyout
                {
                    Content = flyoutContent,
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                };
                m_flyout.Closed += this.Flyout_OnClosed;
            }

            if (this.PART_Root?.Resources.TryGetValue(kPickFromScreenButtonStyleKey, out object buttonStyle) ?? false)
            {
                m_pickFromScreenButton.Style = buttonStyle as Style;
            }

            this.UpdatePickFromScreenButtonVisibility();
        }

        // ===========[ Event handlers ]==========================================
        private void Swatch_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            if (this.IsReadOnly || m_flyout == null)
            {
                return;
            }

            m_editCache = this.EditColor;
            m_colorPicker.Color = this.EditColor;
            this.ShowEditDisplay = true;
            m_flyout.ShowAt(this.PART_Swatch);
        }

        private void ColorPicker_OnColorChanged(ColorPicker sender, ColorChangedEventArgs e)
        {
            this.EditColor = e.NewColor;
        }

        private void Flyout_OnClosed(object sender, object e)
        {
            // Hidden for a pick from screen, which carries the edit on
            if (m_isPickingFromScreen)
            {
                m_flyoutHiddenForPick?.TrySetResult();
                return;
            }

            this.EndEdit();
        }

        private async void PickFromScreenButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (this.IsReadOnly || !this.AllowPickFromScreen || m_isPickingFromScreen || m_flyout == null)
            {
                return;
            }

            m_isPickingFromScreen = true;
            m_pickCancel = new CancellationTokenSource();
            try
            {
                // 1. When asked, get the flyout out of the way, and let a frame go by so it is gone from the screen too.
                //    Left up, it is in the pick's frozen capture, and the pick's overlay taking activation light
                //    dismisses it behind that capture either way, which the edit carries on through.
                if (this.HideWhilePickingFromScreen)
                {
                    m_flyoutHiddenForPick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    m_flyout.Hide();
                    await m_flyoutHiddenForPick.Task;
                    await this.YieldToLowPriorityAsync();
                }

                // 2. Pick
                var options = new ScreenColorPickOptions
                {
                    Magnifier = this.ShowPickFromScreenMagnifier ? new ScreenColorPickMagnifier.Host(this.ActualTheme) : null,
                    MagnifierPixelCount = this.PickFromScreenMagnifierPixelCount,
                };

                ColorArgb? picked = await ScreenColorPicker.PickAsync(options, m_pickCancel.Token);
                if (picked.HasValue)
                {
                    this.EditColor = picked.Value.ToWindowsColor();
                }

                // 3. Back into the editor, unless the control went away meanwhile, which ends the edit instead. The
                //    pick is over first, so the next close of the flyout ends the edit as usual.
                m_isPickingFromScreen = false;
                if (!m_pickCancel.IsCancellationRequested && this.IsLoaded && this.PART_Swatch != null)
                {
                    m_colorPicker.Color = this.EditColor;
                    if (!m_flyout.IsOpen)
                    {
                        m_flyout.ShowAt(this.PART_Swatch);
                    }
                }
                else
                {
                    this.EndEdit();
                }
            }
            catch (Exception exc)
            {
                Logger.LogError("[SCREEN-COLOR-PICK] Picking a color from the screen failed", exc);
                this.EndEdit();
            }
            finally
            {
                m_isPickingFromScreen = false;
                m_flyoutHiddenForPick = null;
                m_pickCancel.Dispose();
                m_pickCancel = null;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // A control taken away mid-pick takes the pick's overlay down with it
            m_pickCancel?.Cancel();
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            this.IsPointerOver = true;
            VisualStateManager.GoToState(this, "PointerOver", true);
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            this.IsPointerOver = false;
            // Only revert to Normal if the flyout is not open; if it is, keep
            // the edit overlay visible as an indicator that an edit is in progress.
            if (!this.ShowEditDisplay)
            {
                VisualStateManager.GoToState(this, "Normal", true);
            }
        }

        // ===========[ Helper Methods ]==========================================
        // Ends the edit the swatch tap started: fires UserEditComplete if the color changed
        private void EndEdit()
        {
            this.ShowEditDisplay = false;

            if (m_editCache.HasValue && m_editCache.Value != this.EditColor)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(m_editCache.Value, this.EditColor));
            }

            m_editCache = null;
        }

        private void UpdatePickFromScreenButtonVisibility()
        {
            if (m_pickFromScreenButton != null)
            {
                m_pickFromScreenButton.Visibility = this.AllowPickFromScreen ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Finishes after the UI thread has worked through everything more pressing, a render pass included
        private Task YieldToLowPriorityAsync()
        {
            var yielded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, yielded.SetResult))
            {
                yielded.SetResult();
            }

            return yielded.Task;
        }

        // ===========[ Property change handlers ]================================
        private void OnEditColorChanged(Color newColor)
        {
            if (this.PART_Swatch != null)
            {
                // Replace brush rather than mutate it - SolidColorBrush.Color
                // is animatable so mutation can interfere with the ColorPicker.
                this.PART_Swatch.Background = new SolidColorBrush(newColor);
            }
        }

        private void OnShowEditDisplayChanged(bool isOpen)
        {
            // Keep edit overlay visible while open; restore based on pointer position on close.
            VisualStateManager.GoToState(this, isOpen || this.IsPointerOver ? "PointerOver" : "Normal", true);
            VisualStateManager.GoToState(this, isOpen ? "EditingOpen" : "EditingClosed", true);
        }
    }
}
