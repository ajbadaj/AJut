namespace AJut.UX.Controls
{
    using AJut.UX.Theming;
    using AppKit;

    // ===========[ BrushButton ]================================================
    // A button whose background, foreground and border colors are set per state: twelve colors,
    // the same twelve as WinUI3's BrushButton. Each starts as the themed color for that part and
    // state (the Button* keys when themed, macOS control colors when not), so a BrushButton with
    // nothing set looks like an AJut button; set any of them to restyle just that part, or to
    // null to draw nothing for it.
    public class BrushButton : ManagedBrushButton
    {
        private NSColor m_normalBackground = ThemedColors.Get(ThemedColorKeys.BrushButton_NormalBackground);
        private NSColor m_normalForeground = ThemedColors.Get(ThemedColorKeys.BrushButton_NormalForeground);
        private NSColor m_normalBorder = ThemedColors.Get(ThemedColorKeys.BrushButton_NormalBorder);
        private NSColor m_pointerOverBackground = ThemedColors.Get(ThemedColorKeys.BrushButton_PointerOverBackground);
        private NSColor m_pointerOverForeground = ThemedColors.Get(ThemedColorKeys.BrushButton_PointerOverForeground);
        private NSColor m_pointerOverBorder = ThemedColors.Get(ThemedColorKeys.BrushButton_PointerOverBorder);
        private NSColor m_pressedBackground = ThemedColors.Get(ThemedColorKeys.BrushButton_PressedBackground);
        private NSColor m_pressedForeground = ThemedColors.Get(ThemedColorKeys.BrushButton_PressedForeground);
        private NSColor m_pressedBorder = ThemedColors.Get(ThemedColorKeys.BrushButton_PressedBorder);
        private NSColor m_disabledBackground = ThemedColors.Get(ThemedColorKeys.BrushButton_DisabledBackground);
        private NSColor m_disabledForeground = ThemedColors.Get(ThemedColorKeys.BrushButton_DisabledForeground);
        private NSColor m_disabledBorder = ThemedColors.Get(ThemedColorKeys.BrushButton_DisabledBorder);

        // ===========[ Properties ]============================================
        public NSColor NormalBackground { get => m_normalBackground; set => this.Set(ref m_normalBackground, value); }
        public NSColor NormalForeground { get => m_normalForeground; set => this.Set(ref m_normalForeground, value); }
        public NSColor NormalBorder { get => m_normalBorder; set => this.Set(ref m_normalBorder, value); }
        public NSColor PointerOverBackground { get => m_pointerOverBackground; set => this.Set(ref m_pointerOverBackground, value); }
        public NSColor PointerOverForeground { get => m_pointerOverForeground; set => this.Set(ref m_pointerOverForeground, value); }
        public NSColor PointerOverBorder { get => m_pointerOverBorder; set => this.Set(ref m_pointerOverBorder, value); }
        public NSColor PressedBackground { get => m_pressedBackground; set => this.Set(ref m_pressedBackground, value); }
        public NSColor PressedForeground { get => m_pressedForeground; set => this.Set(ref m_pressedForeground, value); }
        public NSColor PressedBorder { get => m_pressedBorder; set => this.Set(ref m_pressedBorder, value); }
        public NSColor DisabledBackground { get => m_disabledBackground; set => this.Set(ref m_disabledBackground, value); }
        public NSColor DisabledForeground { get => m_disabledForeground; set => this.Set(ref m_disabledForeground, value); }
        public NSColor DisabledBorder { get => m_disabledBorder; set => this.Set(ref m_disabledBorder, value); }

        // ===========[ Helper Methods ]========================================
        protected override SurfaceColors GetColorsForState (eManagedButtonState state)
        {
            switch (state)
            {
                case eManagedButtonState.PointerOver: return new SurfaceColors(m_pointerOverBackground, m_pointerOverForeground, m_pointerOverBorder);
                case eManagedButtonState.Pressed: return new SurfaceColors(m_pressedBackground, m_pressedForeground, m_pressedBorder);
                case eManagedButtonState.Disabled: return new SurfaceColors(m_disabledBackground, m_disabledForeground, m_disabledBorder);
                default: return new SurfaceColors(m_normalBackground, m_normalForeground, m_normalBorder);
            }
        }

        private void Set (ref NSColor field, NSColor value)
        {
            field = value;
            this.NeedsDisplay = true;
        }
    }
}
