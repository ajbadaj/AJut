namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Runtime;
    using Android.Util;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// A button whose per state colors are plain properties - set them in code, bind them, or set them from a
    /// layout with the ajutNormalBackground style attributes. Every slot left unset paints the standard button
    /// color for that state, so an untouched BrushButton behaves like a normal button, with full pressed, hover
    /// and disabled feedback, and you override only the slots you care about. For "one color, everything
    /// derived" use AutoBrushButton instead.
    /// </summary>
    [Register("ajut.ux.controls.BrushButton")]
    public class BrushButton : _ManagedBrushButton
    {
        // The standard button colors an unset slot falls back to. The keys are the button keys AJut's WinUI3 theme
        // overrides, so a themed BrushButton matches a themed WinUI3 button; the literals are the unthemed look.
        private static readonly ThemedColor kNormalBackground = ThemedColor.FromArgb("ButtonBackground", 0xFFF3F3F3, 0xFF2D2D2D);
        private static readonly ThemedColor kNormalForeground = ThemedColor.FromArgb("ButtonForeground", 0xFF1A1A1A, 0xFFFFFFFF);
        private static readonly ThemedColor kNormalBorder = ThemedColor.FromArgb("ButtonBorderBrush", 0xFFC8C8C8, 0xFF3F3F3F);
        private static readonly ThemedColor kPointerOverBackground = ThemedColor.FromArgb("ButtonBackgroundPointerOver", 0xFFE8E8E8, 0xFF363636);
        private static readonly ThemedColor kPointerOverForeground = ThemedColor.FromArgb("ButtonForegroundPointerOver", 0xFF1A1A1A, 0xFFFFFFFF);
        private static readonly ThemedColor kPointerOverBorder = ThemedColor.FromArgb("ButtonBorderBrushPointerOver", 0xFFB4B4B4, 0xFF4A4A4A);
        private static readonly ThemedColor kPressedBackground = ThemedColor.FromArgb("ButtonBackgroundPressed", 0xFFDADADA, 0xFF242424);
        private static readonly ThemedColor kPressedForeground = ThemedColor.FromArgb("ButtonForegroundPressed", 0xFF5C5C5C, 0xFFCFCFCF);
        private static readonly ThemedColor kPressedBorder = ThemedColor.FromArgb("ButtonBorderBrushPressed", 0xFFC8C8C8, 0xFF3F3F3F);
        private static readonly ThemedColor kDisabledBackground = ThemedColor.FromArgb("ButtonBackgroundDisabled", 0xFFF3F3F3, 0xFF2A2A2A);
        private static readonly ThemedColor kDisabledForeground = ThemedColor.FromArgb("ButtonForegroundDisabled", 0xFFA0A0A0, 0xFF6E6E6E);
        private static readonly ThemedColor kDisabledBorder = ThemedColor.FromArgb("ButtonBorderBrushDisabled", 0xFFE0E0E0, 0xFF333333);

        private AndroidColor? m_normalBackground;
        private AndroidColor? m_normalForeground;
        private AndroidColor? m_normalBorder;
        private AndroidColor? m_pointerOverBackground;
        private AndroidColor? m_pointerOverForeground;
        private AndroidColor? m_pointerOverBorder;
        private AndroidColor? m_pressedBackground;
        private AndroidColor? m_pressedForeground;
        private AndroidColor? m_pressedBorder;
        private AndroidColor? m_disabledBackground;
        private AndroidColor? m_disabledForeground;
        private AndroidColor? m_disabledBorder;

        // ===========[ Setup/Construction/Teardown ]===================================
        public BrushButton (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public BrushButton (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public BrushButton (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected BrushButton (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>The background at rest. Null paints the standard button background.</summary>
        public AndroidColor? NormalBackground
        {
            get => m_normalBackground;
            set => this.SetSlot(ref m_normalBackground, value);
        }

        /// <summary>The text color at rest. Null paints the standard button text color.</summary>
        public AndroidColor? NormalForeground
        {
            get => m_normalForeground;
            set => this.SetSlot(ref m_normalForeground, value);
        }

        /// <summary>The border at rest. Null paints the standard button border.</summary>
        public AndroidColor? NormalBorder
        {
            get => m_normalBorder;
            set => this.SetSlot(ref m_normalBorder, value);
        }

        /// <summary>The background while a mouse or stylus hovers. Null paints the standard hover background.</summary>
        public AndroidColor? PointerOverBackground
        {
            get => m_pointerOverBackground;
            set => this.SetSlot(ref m_pointerOverBackground, value);
        }

        /// <summary>The text color while a mouse or stylus hovers. Null paints the standard hover text color.</summary>
        public AndroidColor? PointerOverForeground
        {
            get => m_pointerOverForeground;
            set => this.SetSlot(ref m_pointerOverForeground, value);
        }

        /// <summary>The border while a mouse or stylus hovers. Null paints the standard hover border.</summary>
        public AndroidColor? PointerOverBorder
        {
            get => m_pointerOverBorder;
            set => this.SetSlot(ref m_pointerOverBorder, value);
        }

        /// <summary>The background while pressed. Null paints the standard pressed background.</summary>
        public AndroidColor? PressedBackground
        {
            get => m_pressedBackground;
            set => this.SetSlot(ref m_pressedBackground, value);
        }

        /// <summary>The text color while pressed. Null paints the standard pressed text color.</summary>
        public AndroidColor? PressedForeground
        {
            get => m_pressedForeground;
            set => this.SetSlot(ref m_pressedForeground, value);
        }

        /// <summary>The border while pressed. Null paints the standard pressed border.</summary>
        public AndroidColor? PressedBorder
        {
            get => m_pressedBorder;
            set => this.SetSlot(ref m_pressedBorder, value);
        }

        /// <summary>The background while disabled. Null paints the standard disabled background.</summary>
        public AndroidColor? DisabledBackground
        {
            get => m_disabledBackground;
            set => this.SetSlot(ref m_disabledBackground, value);
        }

        /// <summary>The text color while disabled. Null paints the standard disabled text color.</summary>
        public AndroidColor? DisabledForeground
        {
            get => m_disabledForeground;
            set => this.SetSlot(ref m_disabledForeground, value);
        }

        /// <summary>The border while disabled. Null paints the standard disabled border.</summary>
        public AndroidColor? DisabledBorder
        {
            get => m_disabledBorder;
            set => this.SetSlot(ref m_disabledBorder, value);
        }

        // ===========[ Public Interface Methods ]===================================
        protected override SurfaceStateColors GetColorsForState (eManagedButtonState state)
        {
            return state switch
            {
                eManagedButtonState.PointerOver => this.Pick(m_pointerOverBackground, kPointerOverBackground, m_pointerOverForeground, kPointerOverForeground, m_pointerOverBorder, kPointerOverBorder),
                eManagedButtonState.Pressed => this.Pick(m_pressedBackground, kPressedBackground, m_pressedForeground, kPressedForeground, m_pressedBorder, kPressedBorder),
                eManagedButtonState.Disabled => this.Pick(m_disabledBackground, kDisabledBackground, m_disabledForeground, kDisabledForeground, m_disabledBorder, kDisabledBorder),
                _ => this.Pick(m_normalBackground, kNormalBackground, m_normalForeground, kNormalForeground, m_normalBorder, kNormalBorder),
            };
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutBrushButton);
                m_normalBackground = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutNormalBackground);
                m_normalForeground = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutNormalForeground);
                m_normalBorder = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutNormalBorder);
                m_pointerOverBackground = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutPointerOverBackground);
                m_pointerOverForeground = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutPointerOverForeground);
                m_pointerOverBorder = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutPointerOverBorder);
                m_pressedBackground = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutPressedBackground);
                m_pressedForeground = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutPressedForeground);
                m_pressedBorder = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutPressedBorder);
                m_disabledBackground = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutDisabledBackground);
                m_disabledForeground = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutDisabledForeground);
                m_disabledBorder = _ReadColor(values, Resource.Styleable.AJutBrushButton_ajutDisabledBorder);
                values.Recycle();
            }

            this.CompleteInitialization();
        }

        private void SetSlot (ref AndroidColor? slot, AndroidColor? value)
        {
            slot = value;
            this.RefreshActiveStateColors();
        }

        private SurfaceStateColors Pick (AndroidColor? background, ThemedColor backgroundDefault, AndroidColor? foreground, ThemedColor foregroundDefault, AndroidColor? border, ThemedColor borderDefault)
        {
            return new SurfaceStateColors(
                background?.ToColorArgb() ?? ThemeColors.Resolve(this.Context!, backgroundDefault),
                foreground?.ToColorArgb() ?? ThemeColors.Resolve(this.Context!, foregroundDefault),
                border?.ToColorArgb() ?? ThemeColors.Resolve(this.Context!, borderDefault)
            );
        }

        private static AndroidColor? _ReadColor (TypedArray values, int index)
        {
            return values.HasValue(index) ? new AndroidColor(values.GetColor(index, 0)) : null;
        }
    }
}
