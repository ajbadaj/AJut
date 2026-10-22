namespace AJut.UX.Controls
{
    using AJut.UX.Theming;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// A button whose per state colors are plain properties - set them, bind them, or hand them a dynamic
    /// light/dark color. Every slot defaults to the stock button key the WinUI3 one uses (ButtonBackground and the rest)
    /// through ThemedColors: a UIKit system color with no theme loaded, the AJut palette with one, so an untouched
    /// BrushButton behaves like a bordered system button - full pressed / disabled
    /// feedback, and pointer over on iPad - and you override only the slots you care about. For "one color,
    /// everything derived" use AutoBrushButton instead.
    /// </summary>
    public class BrushButton : _ManagedBrushButton
    {
        // ===========[ Instance Fields ]===================================
        private UIColor m_normalBackground = ThemedColors.Get(ThemedColorKeys.ButtonBackground);
        private UIColor m_normalForeground = ThemedColors.Get(ThemedColorKeys.ButtonForeground);
        private UIColor m_normalBorderBrush = ThemedColors.Get(ThemedColorKeys.ButtonBorderBrush);
        private UIColor m_pointerOverBackground = ThemedColors.Get(ThemedColorKeys.ButtonBackgroundPointerOver);
        private UIColor m_pointerOverForeground = ThemedColors.Get(ThemedColorKeys.ButtonForegroundPointerOver);
        private UIColor m_pointerOverBorderBrush = ThemedColors.Get(ThemedColorKeys.ButtonBorderBrushPointerOver);
        private UIColor m_pressedBackground = ThemedColors.Get(ThemedColorKeys.ButtonBackgroundPressed);
        private UIColor m_pressedForeground = ThemedColors.Get(ThemedColorKeys.ButtonForegroundPressed);
        private UIColor m_pressedBorderBrush = ThemedColors.Get(ThemedColorKeys.ButtonBorderBrushPressed);
        private UIColor m_disabledBackground = ThemedColors.Get(ThemedColorKeys.ButtonBackgroundDisabled);
        private UIColor m_disabledForeground = ThemedColors.Get(ThemedColorKeys.ButtonForegroundDisabled);
        private UIColor m_disabledBorderBrush = ThemedColors.Get(ThemedColorKeys.ButtonBorderBrushDisabled);

        // ===========[ Setup/Construction/Teardown ]===================================
        public BrushButton ()
        {
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native button.</summary>
        protected BrushButton (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]===================================
        public UIColor NormalBackground
        {
            get => m_normalBackground;
            set => this.SetSlot(ref m_normalBackground, value);
        }

        public UIColor NormalForeground
        {
            get => m_normalForeground;
            set => this.SetSlot(ref m_normalForeground, value);
        }

        public UIColor NormalBorderBrush
        {
            get => m_normalBorderBrush;
            set => this.SetSlot(ref m_normalBorderBrush, value);
        }

        public UIColor PointerOverBackground
        {
            get => m_pointerOverBackground;
            set => this.SetSlot(ref m_pointerOverBackground, value);
        }

        public UIColor PointerOverForeground
        {
            get => m_pointerOverForeground;
            set => this.SetSlot(ref m_pointerOverForeground, value);
        }

        public UIColor PointerOverBorderBrush
        {
            get => m_pointerOverBorderBrush;
            set => this.SetSlot(ref m_pointerOverBorderBrush, value);
        }

        public UIColor PressedBackground
        {
            get => m_pressedBackground;
            set => this.SetSlot(ref m_pressedBackground, value);
        }

        public UIColor PressedForeground
        {
            get => m_pressedForeground;
            set => this.SetSlot(ref m_pressedForeground, value);
        }

        public UIColor PressedBorderBrush
        {
            get => m_pressedBorderBrush;
            set => this.SetSlot(ref m_pressedBorderBrush, value);
        }

        public UIColor DisabledBackground
        {
            get => m_disabledBackground;
            set => this.SetSlot(ref m_disabledBackground, value);
        }

        public UIColor DisabledForeground
        {
            get => m_disabledForeground;
            set => this.SetSlot(ref m_disabledForeground, value);
        }

        public UIColor DisabledBorderBrush
        {
            get => m_disabledBorderBrush;
            set => this.SetSlot(ref m_disabledBorderBrush, value);
        }

        // ===========[ Public Interface Methods ]===================================
        protected override SurfaceColors GetColorsForState (eManagedButtonState state)
        {
            return state switch
            {
                eManagedButtonState.PointerOver => new SurfaceColors(m_pointerOverBackground, m_pointerOverForeground, m_pointerOverBorderBrush),
                eManagedButtonState.Pressed => new SurfaceColors(m_pressedBackground, m_pressedForeground, m_pressedBorderBrush),
                eManagedButtonState.Disabled => new SurfaceColors(m_disabledBackground, m_disabledForeground, m_disabledBorderBrush),
                _ => new SurfaceColors(m_normalBackground, m_normalForeground, m_normalBorderBrush),
            };
        }

        // ===========[ Helper Methods ]===================================
        private void SetSlot (ref UIColor slot, UIColor value)
        {
            slot = value;
            this.RefreshActiveStateColors();
        }
    }
}
