namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using CoreAnimation;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    // ===========[ ColorEditIngressControl ]====================================
    // UIKit color editor. Shows a solid color swatch; a tap presents the system UIColorPickerViewController,
    // as a popover anchored on the swatch on iPad and as a sheet on iPhone (UIKit adapts the popover).
    //
    // Edit lifecycle (mirrors the WPF and WinUI3 ColorEditIngressControl):
    //   Tap swatch      -> cache EditColor, sync the picker, present it
    //   Color picked    -> update EditColor in real time (live preview), and send ValueChanged
    //   Picker finished -> fire UserEditComplete if the color actually changed
    //
    // IsReadOnly blocks the picker from opening.
    //
    // No shared view model is needed - UIColorPickerViewController handles all the color space math, the
    // same way the native ColorPicker does for WinUI3.

    public class ColorEditIngressControl : UIControl, IUserEditNotifier
    {
        // ===========[ Instance Fields ]===================================
        private readonly UIView m_swatch = new UIView { UserInteractionEnabled = false };
        private readonly CAGradientLayer m_gloss = new CAGradientLayer();
        private readonly UIView m_editOverlay = new UIView { UserInteractionEnabled = false, Hidden = true };
        private readonly UIImageView m_editGlyph = new UIImageView { UserInteractionEnabled = false };
        private ColorArgb m_editColor = new ColorArgb(255, 255, 255, 255);
        private ColorArgb? m_editCache;
        private bool m_isPointerOver;
        private bool m_showEditDisplay;
        private UIColorPickerViewController m_picker;
        private PickerDelegate m_pickerDelegate;

        // ===========[ Setup/Construction/Teardown ]===================================
        public ColorEditIngressControl () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native control.</summary>
        protected ColorEditIngressControl (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        private void Setup ()
        {
            const float kCornerRadius = 4f;
            const float kBorderWidth = 1f;
            const float kEditOverlayAlpha = 0.376f;

            // White under the swatch so the alpha channel of a translucent color is visible.
            this.BackgroundColor = UIColor.White;
            this.ClipsToBounds = true;
            this.Layer.CornerRadius = kCornerRadius;
            this.Layer.BorderWidth = kBorderWidth;

            this.AddSubview(m_swatch);

            // The resting gloss: a soft light top half over a soft shade bottom half, the same stops as the
            // WinUI3 template (#25FFFFFF, #0CFFFFFF, #0C000000, #23000000). Hidden while pressed, hovered or
            // editing, when the edit overlay shows instead.
            m_gloss.Colors = new[]
            {
                UIColor.FromWhiteAlpha(1f, 0.145f).CGColor,
                UIColor.FromWhiteAlpha(1f, 0.047f).CGColor,
                UIColor.FromWhiteAlpha(0f, 0.047f).CGColor,
                UIColor.FromWhiteAlpha(0f, 0.137f).CGColor,
            };
            m_gloss.Locations = new[] { new Foundation.NSNumber(0.0), new Foundation.NSNumber(0.5), new Foundation.NSNumber(0.5), new Foundation.NSNumber(1.0) };
            this.Layer.AddSublayer(m_gloss);

            m_editOverlay.BackgroundColor = UIColor.FromWhiteAlpha(0f, kEditOverlayAlpha);
            m_editGlyph.Image = UIImage.GetSystemImage("pencil");
            m_editGlyph.TintColor = UIColor.White;
            m_editGlyph.ContentMode = UIViewContentMode.ScaleAspectFit;
            m_editOverlay.AddSubview(m_editGlyph);
            this.AddSubview(m_editOverlay);

            this.TouchUpInside += this.OnTouchUpInside;

            // A custom UIControl is not in the accessibility tree by default, so VoiceOver could not reach the swatch.
            // It reads as a button named "Color" (set AccessibilityLabel to name what it colors), with the color's hex
            // as its value, and activating it opens the picker.
            this.IsAccessibilityElement = true;
            this.AccessibilityTraits = UIAccessibilityTrait.Button;
            this.AccessibilityLabel = "Color";

            // The view keeps its gesture recognizers alive, so a handler that captured this control would tie
            // the two into a cycle across native objects. The handler is static and finds the control through
            // the recognizer's View instead.
            this.AddGestureRecognizer(new UIHoverGestureRecognizer(OnHover));

            this.ApplyEditColor();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.TouchUpInside -= this.OnTouchUpInside;
                if (m_picker != null)
                {
                    m_picker.Delegate = null;
                    m_picker = null;
                }

                m_pickerDelegate = null;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]===================================
        /// <inheritdoc/>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        private void OnTouchUpInside (object sender, EventArgs e)
        {
            if (this.IsReadOnly)
            {
                return;
            }

            UIViewController presenter = this.FindViewController();
            if (presenter == null)
            {
                return;
            }

            // A controller that is already presenting something cannot present again, so present from
            // the top of whatever stack is showing.
            while (presenter.PresentedViewController != null)
            {
                presenter = presenter.PresentedViewController;
            }

            if (m_picker == null)
            {
                m_pickerDelegate = new PickerDelegate(this);
                m_picker = new UIColorPickerViewController { Delegate = m_pickerDelegate };
            }

            m_picker.SupportsAlpha = this.SupportsAlpha;
            m_picker.SelectedColor = m_editColor.ToUIColor();
            m_picker.ModalPresentationStyle = UIModalPresentationStyle.Popover;
            if (m_picker.PopoverPresentationController != null)
            {
                m_picker.PopoverPresentationController.SourceView = this;
                m_picker.PopoverPresentationController.SourceRect = this.Bounds;
            }

            m_editCache = m_editColor;
            this.ShowEditDisplay = true;
            presenter.PresentViewController(m_picker, true, null);
        }

        private static void OnHover (UIHoverGestureRecognizer recognizer)
        {
            if (recognizer.View is ColorEditIngressControl owner)
            {
                owner.m_isPointerOver = (recognizer.State == UIGestureRecognizerState.Began)
                    || (recognizer.State == UIGestureRecognizerState.Changed);
                owner.RefreshOverlay();
            }
        }

        private void OnPickerColorSelected (UIColor color)
        {
            // The picker can hand back a wide gamut (Display P3) color, which ColorArgb's 8 bit sRGB channels clamp:
            // the same values every other stack's EditColor holds, at the cost of the most saturated P3 colors.
            this.EditColor = color.ToColorArgb();
            this.SendActionForControlEvents(UIControlEvent.ValueChanged);
        }

        private void OnPickerFinished ()
        {
            this.ShowEditDisplay = false;
            if (m_editCache.HasValue && (m_editCache.Value != m_editColor))
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(m_editCache.Value, m_editColor));
            }

            m_editCache = null;
        }

        // ===========[ Properties ]===================================
        /// <summary>
        /// The color being edited, as AJut.UX's <see cref="ColorArgb"/>, the type EditColor is on every stack (and the
        /// PropertyGrid's "ColorArgb" editor key), so one model color binds the same everywhere. Setting it in code updates
        /// the swatch and does not send ValueChanged. <see cref="ColorMathExtensions"/> converts to and from UIColor.
        /// </summary>
        public ColorArgb EditColor
        {
            get => m_editColor;
            set
            {
                m_editColor = value;
                this.ApplyEditColor();
            }
        }

        /// <summary>Prevents the picker from opening.</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>Whether the picker offers an opacity control. Defaults to true, as WinUI3's does.</summary>
        public bool SupportsAlpha { get; set; } = true;

        /// <summary>True while the picker is open; drives the border highlight and keeps the edit overlay up.</summary>
        public bool ShowEditDisplay
        {
            get => m_showEditDisplay;
            private set
            {
                m_showEditDisplay = value;
                this.RefreshOverlay();
                this.SetNeedsLayout();
            }
        }

        public override bool Highlighted
        {
            get => base.Highlighted;
            set
            {
                base.Highlighted = value;
                this.RefreshOverlay();
            }
        }

        public override CGSize IntrinsicContentSize
        {
            get
            {
                // The same resting size as the WinUI3 control's MinWidth / MinHeight
                const float kWidth = 60f;
                const float kHeight = 28f;
                return new CGSize(kWidth, kHeight);
            }
        }

        // ===========[ Public Interface Methods ]===================================
        public override void LayoutSubviews ()
        {
            base.LayoutSubviews();

            const float kGlyphSize = 14f;
            const float kGlyphInset = 3f;

            m_swatch.Frame = this.Bounds;
            m_editOverlay.Frame = this.Bounds;
            m_editGlyph.Frame = new CGRect(
                this.Bounds.Width - kGlyphSize - kGlyphInset,
                this.Bounds.Height - kGlyphSize - kGlyphInset,
                kGlyphSize,
                kGlyphSize
            );

            // Layer frames are not animated by UIKit layout, so set them with implicit animation off.
            CATransaction.Begin();
            CATransaction.DisableActions = true;
            m_gloss.Frame = this.Bounds;
            CATransaction.Commit();

            // A layer border is a CGColor, which freezes a dynamic color, so it is resolved here: UIKit
            // calls LayoutSubviews again whenever the appearance (light/dark) changes.
            UIColor border = m_showEditDisplay
                ? (ThemedColors.Get(ThemedColorKeys.ColorEditIngressControl_HighlightBorderBrush) ?? this.TintColor)
                : ThemedColors.Get(ThemedColorKeys.ColorEditIngressControl_NormalBorderBrush);
            this.Layer.BorderColor = border.GetResolvedColor(this.TraitCollection).CGColor;
        }

        /// <summary>VoiceOver's activate opens the picker, the same as a tap, unless the control is read only.</summary>
        public override bool AccessibilityActivate ()
        {
            if (this.IsReadOnly)
            {
                return false;
            }

            this.OnTouchUpInside(this, EventArgs.Empty);
            return true;
        }

        public override void TintColorDidChange ()
        {
            base.TintColorDidChange();
            this.SetNeedsLayout();
        }

        // ===========[ Helper Methods ]===================================
        private void ApplyEditColor ()
        {
            m_swatch.BackgroundColor = m_editColor.ToUIColor();
            this.AccessibilityValue = AJutColorHelper.GetSmallestHexString(m_editColor.A, m_editColor.R, m_editColor.G, m_editColor.B);
        }

        private void RefreshOverlay ()
        {
            bool showEdit = m_showEditDisplay || m_isPointerOver || this.Highlighted;
            m_editOverlay.Hidden = !showEdit;
            m_gloss.Hidden = showEdit;
        }

        // ===========[ Subclasses/structs ]===================================
        // The picker keeps its delegate alive, so the delegate holds the control weakly; otherwise the
        // control, the picker and the delegate form a cycle across native objects that the GC cannot see.
        private sealed class PickerDelegate : UIColorPickerViewControllerDelegate
        {
            private readonly WeakReference<ColorEditIngressControl> m_owner;

            public PickerDelegate (ColorEditIngressControl owner)
            {
                m_owner = new WeakReference<ColorEditIngressControl>(owner);
            }

            public override void DidSelectColor (UIColorPickerViewController viewController, UIColor color, bool continuously)
            {
                if (m_owner.TryGetTarget(out ColorEditIngressControl owner))
                {
                    owner.OnPickerColorSelected(color);
                }
            }

            public override void DidFinish (UIColorPickerViewController viewController)
            {
                if (m_owner.TryGetTarget(out ColorEditIngressControl owner))
                {
                    owner.OnPickerFinished();
                }
            }
        }
    }
}
