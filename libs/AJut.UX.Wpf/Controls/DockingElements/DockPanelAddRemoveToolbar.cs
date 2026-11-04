namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media.Imaging;
    using AJut.UX.Docking;
    using DPUtils = AJut.UX.DPUtils<DockPanelAddRemoveToolbar>;

    public class DockPanelAddRemoveToolbar : Control, IDockPanelToolbarHost
    {
        // ===========[ Fields ]===============================================
        // Which buttons exist and what they do is shared with every toolkit's toolbar; this control makes the views
        private readonly DockPanelToolbarController m_controller;
        private StackPanel m_buttonPanel;

        // ===========[ Construction ]=========================================
        static DockPanelAddRemoveToolbar ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockPanelAddRemoveToolbar), new FrameworkPropertyMetadata(typeof(DockPanelAddRemoveToolbar)));
        }

        public DockPanelAddRemoveToolbar ()
        {
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

        // ===========[ Template ]=============================================

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            m_buttonPanel = this.GetTemplateChild("PART_ButtonPanel") as StackPanel;
            m_controller.RebuildButtons();
        }

        // ===========[ IDockPanelToolbarHost ]================================

        void IDockPanelToolbarHost.ShowButtons (IReadOnlyList<DockPanelToolbarController.Button> buttons)
        {
            if (m_buttonPanel == null)
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
                    this.ConfigureButtonContent(btn, button.Entry);
                    btn.IsChecked = button.IsChecked;
                    btn.Click += this.OnToolbarButtonClicked;
                    m_buttonPanel.Children.Add(btn);
                }
                else
                {
                    var btn = new DockPanelAddButton();
                    btn.Tag = button;
                    this.ConfigureButtonContent(btn, button.Entry);
                    btn.Click += this.OnToolbarButtonClicked;
                    m_buttonPanel.Children.Add(btn);
                }
            }
        }

        void IDockPanelToolbarHost.RefreshButtonStates ()
        {
            if (m_buttonPanel == null)
            {
                return;
            }

            foreach (var child in m_buttonPanel.Children)
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
            foreach (var child in m_buttonPanel.Children)
            {
                if (child is ButtonBase button)
                {
                    button.Click -= this.OnToolbarButtonClicked;
                }
            }

            m_buttonPanel.Children.Clear();
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
        private void ConfigureButtonContent (ContentControl btn, DockPanelTypeEntry entry)
        {
            bool hasIcon = entry.IconPath.IsNotNullOrEmpty();
            bool hasName = entry.DisplayName.IsNotNullOrEmpty();

            if (hasIcon && hasName)
            {
                var stack = new StackPanel { Orientation = Orientation.Vertical };
                stack.Children.Add(new Image
                {
                    Source = new BitmapImage(new Uri(entry.IconPath, UriKind.RelativeOrAbsolute)),
                    Width = 16,
                    Height = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                stack.Children.Add(new TextBlock
                {
                    Text = entry.DisplayName,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                btn.Content = stack;
            }
            else if (hasIcon)
            {
                btn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(entry.IconPath, UriKind.RelativeOrAbsolute)),
                    Width = 16,
                    Height = 16,
                };
            }
            else
            {
                btn.Content = new TextBlock { Text = entry.DisplayName ?? entry.PanelType.Name, FontSize = 11 };
            }

            btn.ToolTip = entry.DisplayName ?? entry.PanelType.Name;
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