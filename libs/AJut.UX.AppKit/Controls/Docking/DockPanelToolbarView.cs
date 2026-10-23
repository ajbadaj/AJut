namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Docking;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;

    // ===========[ DockPanelToolbarView ]=======================================
    // A row of buttons for adding and showing docked panels, the AppKit counterpart of WinUI3's
    // DockPanelAddRemoveToolbar. AJut.UX's DockPanelToolbarController decides everything that
    // isn't a view: which panel types get a button, which kind, when to rebuild, and what a click
    // does. This only draws the buttons it is handed and passes clicks back.
    //
    // A single instance panel type gets a DockPanelShowHideToggleButton, on while its panel shows;
    // any other gets a DockPanelAddButton, which adds another. The controller follows the docking
    // manager's UISyncVM only while this view is in a window, so a long lived manager never keeps
    // a toolbar that was dropped without being cleaned up.
    public class DockPanelToolbarView : ThemedView, IDockPanelToolbarHost
    {
        private readonly DockPanelToolbarController m_controller;
        private readonly FillStack m_row;
        private readonly List<ManagedBrushButton> m_buttonViews = new List<ManagedBrushButton>();
        private IReadOnlyList<DockPanelToolbarController.Button> m_buttons = Array.Empty<DockPanelToolbarController.Button>();

        // ===========[ Setup/Construction/Teardown ]===========================
        public DockPanelToolbarView ()
        {
            this.BackgroundColorKey = ThemedColorKeys.DockPanelToolbar_Background;
            this.BorderColorKey = ThemedColorKeys.DockPanelToolbar_Border;
            this.BorderThickness = 1.0;

            m_row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 4.0)
            {
                Padding = new NSEdgeInsets(4, 4, 4, 4),
            };
            m_row.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(m_row);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            m_controller = new DockPanelToolbarController(this);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                m_controller.DockingManager = null;
                this.ClearButtonViews();
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            if (this.Window != null)
            {
                m_controller.OnAttached();
            }
            else
            {
                m_controller.OnDetached();
            }
        }

        private void OnButtonViewActivated (object sender, EventArgs e)
        {
            int index = m_buttonViews.IndexOf((ManagedBrushButton)sender);
            if (index >= 0 && index < m_buttons.Count)
            {
                m_controller.OnButtonActivated(m_buttons[index]);
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>The docking manager whose panels the buttons add and show</summary>
        public IDockingManager DockingManager
        {
            get => m_controller.DockingManager;
            set => m_controller.DockingManager = value;
        }

        // ===========[ IDockPanelToolbarHost Implementations ]=================
        void IDockPanelToolbarHost.ShowButtons (IReadOnlyList<DockPanelToolbarController.Button> buttons)
        {
            this.ClearButtonViews();
            m_buttons = buttons;
            foreach (DockPanelToolbarController.Button button in buttons)
            {
                ManagedBrushButton view = button.IsToggle
                    ? new DockPanelShowHideToggleButton { IsChecked = button.IsChecked }
                    : new DockPanelAddButton();

                view.Title = button.Label;
                view.ToolTip = button.Label;
                view.Activated += this.OnButtonViewActivated;
                m_buttonViews.Add(view);
                m_row.Add(view, alignment: eFillStackCrossAlignment.Center);
            }
        }

        void IDockPanelToolbarHost.RefreshButtonStates ()
        {
            for (int index = 0; index < m_buttonViews.Count && index < m_buttons.Count; ++index)
            {
                if (m_buttonViews[index] is DockPanelShowHideToggleButton toggle)
                {
                    toggle.IsChecked = m_buttons[index].IsChecked;
                }
            }
        }

        // ===========[ Helper Methods ]========================================
        private void ClearButtonViews ()
        {
            foreach (ManagedBrushButton view in m_buttonViews)
            {
                view.Activated -= this.OnButtonViewActivated;
            }

            m_buttonViews.Clear();
            m_row.Clear();
            m_buttons = Array.Empty<DockPanelToolbarController.Button>();
        }
    }

    // ===========[ DockPanelAddButton ]=========================================
    // A toolbar button that adds another panel of its type, marked with a small plus at its bottom
    // right corner, the same look as WinUI3's.
    public class DockPanelAddButton : DockPanelToolbarButton
    {
        private const double kPlusFontSize = 9.0;
        private const double kPlusInset = 2.0;

        public override void DrawRect (CGRect dirtyRect)
        {
            base.DrawRect(dirtyRect);

            var plus = new NSAttributedString("+", new NSStringAttributes
            {
                Font = NSFont.BoldSystemFontOfSize((nfloat)kPlusFontSize),
                ForegroundColor = ThemedColors.Get(ThemedColorKeys.DockPanelAddButton_PlusOverlayForeground),
            });

            CGSize size = plus.Size;
            CGRect bounds = this.Bounds;
            plus.DrawAtPoint(new CGPoint(
                bounds.Right - size.Width - (nfloat)kPlusInset,
                bounds.Bottom - size.Height - (nfloat)kPlusInset + (nfloat)1
            ));
        }

        protected override SurfaceColors GetColorsForState (eManagedButtonState state)
        {
            NSColor foreground = ThemedColors.Get(ThemedColorKeys.DockPanelAddButton_NormalForeground);
            NSColor border = ThemedColors.Get(ThemedColorKeys.DockPanelAddButton_NormalBorder);
            switch (state)
            {
                case eManagedButtonState.PointerOver: return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelAddButton_PointerOverBackground), foreground, border);
                case eManagedButtonState.Pressed: return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelAddButton_PressedBackground), foreground, border);
                default: return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelAddButton_NormalBackground), foreground, border);
            }
        }
    }

    // ===========[ DockPanelShowHideToggleButton ]==============================
    // A toolbar button that shows or hides the one panel of its type, drawn checked while that
    // panel shows, the same look as WinUI3's.
    //
    // To VoiceOver it is a toggle button read with its on or off state, the way AppKit exposes a
    // stock push-on push-off NSButton: a check box role with the toggle subrole, its value 1 while
    // on. A plain button role has no state, so it read as "button" whether its panel showed or not.
    public class DockPanelShowHideToggleButton : DockPanelToolbarButton
    {
        private bool m_isChecked;

        /// <summary>Whether the button shows as on: its panel is showing. The toolbar sets it; a click asks the panel to show or hide.</summary>
        public bool IsChecked
        {
            get => m_isChecked;
            set
            {
                if (m_isChecked == value)
                {
                    return;
                }

                m_isChecked = value;
                this.NeedsDisplay = true;
                NSAccessibility.PostNotification(this, NSView.ValueChangedNotification);
            }
        }

        public override string AccessibilityRole => NSAccessibilityRoles.CheckBoxRole;
        public override string AccessibilitySubrole => NSAccessibilitySubroles.ToggleSubrole;
        public override NSObject AccessibilityValue => NSNumber.FromInt32(m_isChecked ? 1 : 0);

        protected override SurfaceColors GetColorsForState (eManagedButtonState state)
        {
            NSColor foreground = ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_NormalForeground);
            if (m_isChecked)
            {
                switch (state)
                {
                    case eManagedButtonState.PointerOver:
                        return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_CheckedPointerOverBackground), foreground, ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_CheckedPointerOverBorder));

                    case eManagedButtonState.Pressed:
                        return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_CheckedPressedBackground), foreground, ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_CheckedPressedBorder));

                    default:
                        return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_CheckedBackground), foreground, ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_CheckedBorder));
                }
            }

            NSColor border = ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_NormalBorder);
            switch (state)
            {
                case eManagedButtonState.PointerOver: return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_PointerOverBackground), foreground, border);
                case eManagedButtonState.Pressed: return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_PressedBackground), foreground, border);
                default: return new SurfaceColors(ThemedColors.Get(ThemedColorKeys.DockPanelToggleButton_NormalBackground), foreground, border);
            }
        }
    }

    // ===========[ DockPanelToolbarButton ]=====================================
    // What the two toolbar buttons share: WinUI3's size, padding and corners, at least 28 points
    // square so a short label still makes a comfortable target
    public abstract class DockPanelToolbarButton : ManagedBrushButton
    {
        private const double kMinimumSize = 28.0;

        protected DockPanelToolbarButton ()
        {
            this.Padding = new NSEdgeInsets(2, 4, 2, 4);
            this.CornerRadius = 3.0;
        }

        public override CGSize IntrinsicContentSize
        {
            get
            {
                CGSize size = base.IntrinsicContentSize;
                return new CGSize(
                    (nfloat)Math.Max((double)size.Width, kMinimumSize),
                    (nfloat)Math.Max((double)size.Height, kMinimumSize)
                );
            }
        }
    }
}
