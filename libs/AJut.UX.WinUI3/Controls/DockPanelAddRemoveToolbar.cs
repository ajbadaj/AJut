namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Docking;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using DPUtils = AJut.UX.DPUtils<DockPanelAddRemoveToolbar>;

    // ===========[ DockPanelAddRemoveToolbar ]=================================
    // Toolbar that creates toggle/add buttons for the DockingManager's panel types, as worked out by
    // AJut.UX's DockPanelToolbarController. Buttons are dynamically created when entries
    // change - this is structural (what buttons exist), not visual (how they look).
    // Each button is a proper Control with its own XAML template.
    //
    // Template part:
    //   PART_ButtonPanel - StackPanel hosting the dynamically created buttons

    [TemplatePart(Name = "PART_ButtonPanel", Type = typeof(StackPanel))]
    public sealed class DockPanelAddRemoveToolbar : Control, IDockPanelToolbarHost
    {
        // ===========[ Fields ]===============================================
        // Which buttons exist and what they do is shared with every toolkit's toolbar; this control makes the views
        private readonly DockPanelToolbarController m_controller;

        // ===========[ Construction ]=========================================
        public DockPanelAddRemoveToolbar ()
        {
            this.DefaultStyleKey = typeof(DockPanelAddRemoveToolbar);
            m_controller = new DockPanelToolbarController(this);

            // The toolbar follows the manager only while it is in a window, so a toolbar dropped from its window
            // is not kept alive by a manager that outlives it. Its own events, so they need no unhooking.
            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;
        }

        // ===========[ Dependency Properties ]================================

        public static readonly DependencyProperty DockingManagerProperty = DPUtils.Register(_ => _.DockingManager, (d, e) => d.OnDockingManagerChanged());
        public IDockingManager DockingManager
        {
            get => (IDockingManager)this.GetValue(DockingManagerProperty);
            set => this.SetValue(DockingManagerProperty, value);
        }

        // ===========[ Template Parts ]=======================================

        private StackPanel PART_ButtonPanel { get; set; }

        // ===========[ Template ]=============================================

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            this.PART_ButtonPanel = this.GetTemplateChild(nameof(this.PART_ButtonPanel)) as StackPanel;
            VisualStateManager.GoToState(this, "Normal", false);
            m_controller.RebuildButtons();
        }

        // ===========[ IDockPanelToolbarHost ]================================

        void IDockPanelToolbarHost.ShowButtons (IReadOnlyList<DockPanelToolbarController.Button> buttons)
        {
            if (this.PART_ButtonPanel == null)
            {
                return;
            }

            this.ReleaseButtons();
            foreach (DockPanelToolbarController.Button button in buttons)
            {
                if (button.IsToggle)
                {
                    var btn = new DockPanelShowHideToggleButton();
                    btn.Tag = button;
                    btn.Content = button.Label;
                    btn.IsChecked = button.IsChecked;
                    btn.Click += this.OnToolbarButtonClicked;
                    ToolTipService.SetToolTip(btn, button.Label);
                    this.PART_ButtonPanel.Children.Add(btn);
                }
                else
                {
                    var btn = new DockPanelAddButton();
                    btn.Tag = button;
                    btn.Content = button.Label;
                    btn.Click += this.OnToolbarButtonClicked;
                    ToolTipService.SetToolTip(btn, button.Label);
                    this.PART_ButtonPanel.Children.Add(btn);
                }
            }
        }

        void IDockPanelToolbarHost.RefreshButtonStates ()
        {
            if (this.PART_ButtonPanel == null)
            {
                return;
            }

            foreach (var child in this.PART_ButtonPanel.Children)
            {
                if (child is DockPanelShowHideToggleButton toggle && toggle.Tag is DockPanelToolbarController.Button button)
                {
                    toggle.IsChecked = button.IsChecked;
                }
            }
        }

        // ===========[ Private Methods ]======================================

        // Drops the buttons, unhooking each one's Click first so a dropped button can't hold the toolbar
        private void ReleaseButtons ()
        {
            foreach (var child in this.PART_ButtonPanel.Children)
            {
                if (child is ButtonBase button)
                {
                    button.Click -= this.OnToolbarButtonClicked;
                }
            }

            this.PART_ButtonPanel.Children.Clear();
        }

        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            m_controller.OnAttached();
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            m_controller.OnDetached();
        }

        private void OnDockingManagerChanged ()
        {
            m_controller.DockingManager = this.DockingManager;
        }

        private void OnToolbarButtonClicked (object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is DockPanelToolbarController.Button button)
            {
                m_controller.OnButtonActivated(button);
            }
        }
    }
}