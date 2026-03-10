namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Specialized;
    using System.Linq;
    using AJut.UX.Docking;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using DPUtils = AJut.UX.DPUtils<DockPanelAddRemoveToolbar>;

    // ===========[ DockPanelAddRemoveToolbar ]=================================
    // Toolbar that creates toggle/add buttons based on the DockingManager's
    // UISyncVM panel type entries. Buttons are dynamically created when entries
    // change - this is structural (what buttons exist), not visual (how they look).
    // Each button is a proper Control with its own XAML template.
    //
    // Template part:
    //   PART_ButtonPanel - StackPanel hosting the dynamically created buttons

    [TemplatePart(Name = "PART_ButtonPanel", Type = typeof(StackPanel))]
    public sealed class DockPanelAddRemoveToolbar : Control
    {
        // ===========[ Fields ]===============================================
        private DockPanelAddRemoveUISync m_subscribedSync;

        // ===========[ Construction ]=========================================
        public DockPanelAddRemoveToolbar ()
        {
            this.DefaultStyleKey = typeof(DockPanelAddRemoveToolbar);
        }

        // ===========[ Dependency Properties ]================================

        public static readonly DependencyProperty DockingManagerProperty = DPUtils.Register(_ => _.DockingManager, (d, e) => d.OnDockingManagerChanged());
        public DockingManager DockingManager
        {
            get => (DockingManager)this.GetValue(DockingManagerProperty);
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
            this.RebuildButtons();
        }

        // ===========[ Private Methods ]======================================

        private void OnDockingManagerChanged ()
        {
            // Unsubscribe old
            if (m_subscribedSync != null)
            {
                ((INotifyCollectionChanged)m_subscribedSync.PanelTypeEntries).CollectionChanged -= this.OnEntriesChanged;
                m_subscribedSync.PanelStateChanged -= this.OnPanelStateChanged;
                m_subscribedSync = null;
            }

            // Subscribe new
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
            if (this.PART_ButtonPanel == null)
            {
                return;
            }

            this.PART_ButtonPanel.Children.Clear();

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

                string displayName = entry.DisplayName ?? entry.PanelType.Name;

                if (entry.IsSingleInstance)
                {
                    var btn = new DockPanelShowHideToggleButton();
                    btn.Tag = entry;
                    btn.Content = displayName;
                    btn.IsChecked = entry.HasActiveInstance;
                    btn.Click += this.OnToggleButtonClicked;
                    ToolTipService.SetToolTip(btn, displayName);
                    this.PART_ButtonPanel.Children.Add(btn);
                }
                else
                {
                    var btn = new DockPanelAddButton();
                    btn.Tag = entry;
                    btn.Content = displayName;
                    btn.Click += this.OnAddButtonClicked;
                    ToolTipService.SetToolTip(btn, displayName);
                    this.PART_ButtonPanel.Children.Add(btn);
                }
            }
        }

        private void UpdateButtonStates ()
        {
            if (this.PART_ButtonPanel == null)
            {
                return;
            }

            foreach (var child in this.PART_ButtonPanel.Children)
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
