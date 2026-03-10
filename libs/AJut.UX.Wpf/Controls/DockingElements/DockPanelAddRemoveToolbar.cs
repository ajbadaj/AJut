namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media.Imaging;
    using AJut.UX.Docking;
    using DPUtils = AJut.UX.DPUtils<DockPanelAddRemoveToolbar>;

    public class DockPanelAddRemoveToolbar : Control
    {
        // ===========[ Fields ]===============================================
        private StackPanel m_buttonPanel;
        private DockPanelAddRemoveUISync m_subscribedSync;

        // ===========[ Construction ]=========================================
        static DockPanelAddRemoveToolbar ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockPanelAddRemoveToolbar), new FrameworkPropertyMetadata(typeof(DockPanelAddRemoveToolbar)));
        }

        // ===========[ Dependency Properties ]================================

        public static readonly DependencyProperty DockingManagerProperty = DPUtils.Register(_ => _.DockingManager, (d, e) => d.OnDockingManagerChanged());
        public DockingManager DockingManager
        {
            get => (DockingManager)this.GetValue(DockingManagerProperty);
            set => this.SetValue(DockingManagerProperty, value);
        }

        // ===========[ Template ]=============================================

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            m_buttonPanel = this.GetTemplateChild("PART_ButtonPanel") as StackPanel;
            this.RebuildButtons();
        }

        // ===========[ Private Methods ]======================================

        private void OnDockingManagerChanged ()
        {
            if (m_subscribedSync != null)
            {
                ((INotifyCollectionChanged)m_subscribedSync.PanelTypeEntries).CollectionChanged -= this.OnEntriesChanged;
                m_subscribedSync.PanelStateChanged -= this.OnPanelStateChanged;
                m_subscribedSync = null;
            }

            if (this.DockingManager?.UISyncVM != null)
            {
                m_subscribedSync = this.DockingManager.UISyncVM;
                ((INotifyCollectionChanged)m_subscribedSync.PanelTypeEntries).CollectionChanged += this.OnEntriesChanged;
                m_subscribedSync.PanelStateChanged += this.OnPanelStateChanged;
            }

            this.RebuildButtons();
        }

        private void OnEntriesChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.RebuildButtons();
        }

        private void OnPanelStateChanged (object sender, DockPanelAddRemoveUISync.PanelStateChangedEventArgs e)
        {
            if (e.IsStructuralChange)
            {
                this.RebuildButtons();
            }
            else
            {
                this.UpdateButtonStates();
            }
        }

        private void RebuildButtons ()
        {
            if (m_buttonPanel == null)
            {
                return;
            }

            m_buttonPanel.Children.Clear();

            if (m_subscribedSync == null)
            {
                return;
            }

            foreach (DockPanelTypeEntry entry in m_subscribedSync.PanelTypeEntries)
            {
                if (entry.IsHiddenFromToolbar)
                {
                    continue;
                }

                if (entry.IsSingleInstance)
                {
                    var btn = new DockPanelShowHideToggleButton();
                    btn.Tag = entry;
                    this.ConfigureButtonContent(btn, entry);
                    btn.IsChecked = entry.HasActiveInstance;
                    btn.Click += this.OnToggleButtonClicked;
                    m_buttonPanel.Children.Add(btn);
                }
                else
                {
                    var btn = new DockPanelAddButton();
                    btn.Tag = entry;
                    this.ConfigureButtonContent(btn, entry);
                    btn.Click += this.OnAddButtonClicked;
                    m_buttonPanel.Children.Add(btn);
                }
            }
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

        private void UpdateButtonStates ()
        {
            if (m_buttonPanel == null)
            {
                return;
            }

            foreach (var child in m_buttonPanel.Children)
            {
                if (child is DockPanelShowHideToggleButton toggle && toggle.Tag is DockPanelTypeEntry entry)
                {
                    toggle.IsChecked = entry.HasActiveInstance;
                }
            }
        }

        private void OnToggleButtonClicked (object sender, RoutedEventArgs e)
        {
            if (sender is DockPanelShowHideToggleButton btn && btn.Tag is DockPanelTypeEntry entry)
            {
                m_subscribedSync?.RequestSetToggleState(entry.PanelType);
            }
        }

        private void OnAddButtonClicked (object sender, RoutedEventArgs e)
        {
            if (sender is DockPanelAddButton btn && btn.Tag is DockPanelTypeEntry entry)
            {
                m_subscribedSync?.RequestAdd(entry.PanelType);
            }
        }
    }
}
