namespace AJut.UX.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using DPUtils = AJut.UX.DPUtils<PopupMenuButton>;

    // ===========[ PopupMenuButton ]============================================
    // A button that opens a MenuFlyout when clicked. The button displays its
    // "checked" (active) visual state while the flyout is open and returns to
    // its unchecked state when the flyout closes.
    //
    // Populate MenuItems with MenuFlyoutItemBase-derived objects (MenuFlyoutItem,
    // MenuFlyoutSeparator, MenuFlyoutSubItem, etc.).
    //
    // Template parts:
    //   PART_Button  - ToggleButton that triggers and reflects the flyout state

    [TemplatePart(Name = nameof(PART_Button), Type = typeof(ToggleButton))]
    public class PopupMenuButton : Control
    {
        // ===========[ Instance fields ]==========================================
        private ToggleButton PART_Button;
        private MenuFlyout m_flyout;
        private bool m_blockReentrancy;

        // ===========[ Construction ]=============================================
        public PopupMenuButton ()
        {
            this.DefaultStyleKey = typeof(PopupMenuButton);
            this.MenuItems = new ObservableCollection<MenuFlyoutItemBase>();
            this.MenuItems.CollectionChanged += this.MenuItems_OnCollectionChanged;
        }

        // ===========[ Events ]===================================================
        public event EventHandler FlyoutOpened;
        public event EventHandler FlyoutClosed;

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty ButtonContentProperty = DPUtils.Register(_ => _.ButtonContent);
        public object ButtonContent
        {
            get => this.GetValue(ButtonContentProperty);
            set => this.SetValue(ButtonContentProperty, value);
        }

        public static readonly DependencyProperty ButtonContentTemplateProperty = DPUtils.Register(_ => _.ButtonContentTemplate);
        public DataTemplate ButtonContentTemplate
        {
            get => (DataTemplate)this.GetValue(ButtonContentTemplateProperty);
            set => this.SetValue(ButtonContentTemplateProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty = DPUtils.Register(_ => _.IsOpen, (d, e) => d.OnIsOpenChanged(e.NewValue));
        public bool IsOpen
        {
            get => (bool)this.GetValue(IsOpenProperty);
            set => this.SetValue(IsOpenProperty, value);
        }

        // ===========[ Properties ]===============================================
        public ObservableCollection<MenuFlyoutItemBase> MenuItems { get; }

        // ===========[ Template application ]=====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_Button != null)
            {
                this.PART_Button.Checked -= this.Button_OnChecked;
                this.PART_Button.Unchecked -= this.Button_OnUnchecked;
            }

            this.PART_Button = this.GetTemplateChild(nameof(PART_Button)) as ToggleButton;
            if (this.PART_Button != null)
            {
                this.PART_Button.Checked += this.Button_OnChecked;
                this.PART_Button.Unchecked += this.Button_OnUnchecked;
            }

            this.RebuildFlyout();
        }

        // ===========[ Events ]===================================================
        private void Button_OnChecked (object sender, RoutedEventArgs e)
        {
            if (!m_blockReentrancy)
            {
                this.OpenFlyout();
            }
        }

        private void Button_OnUnchecked (object sender, RoutedEventArgs e)
        {
            if (!m_blockReentrancy)
            {
                this.CloseFlyout();
            }
        }

        private void Flyout_OnOpened (object sender, object e)
        {
            if (!m_blockReentrancy)
            {
                m_blockReentrancy = true;
                try
                {
                    this.IsOpen = true;
                    if (this.PART_Button != null)
                    {
                        this.PART_Button.IsChecked = true;
                    }
                }
                finally
                {
                    m_blockReentrancy = false;
                }
            }

            this.FlyoutOpened?.Invoke(this, EventArgs.Empty);
        }

        private void Flyout_OnClosed (object sender, object e)
        {
            m_blockReentrancy = true;
            try
            {
                this.IsOpen = false;
                if (this.PART_Button != null)
                {
                    this.PART_Button.IsChecked = false;
                }
            }
            finally
            {
                m_blockReentrancy = false;
            }

            this.FlyoutClosed?.Invoke(this, EventArgs.Empty);
        }

        private void MenuItems_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e) => this.RebuildFlyout();

        // ===========[ Property change handlers ]=================================
        private void OnIsOpenChanged (bool newValue)
        {
            if (m_blockReentrancy)
            {
                return;
            }

            if (newValue)
            {
                this.OpenFlyout();
            }
            else
            {
                this.CloseFlyout();
            }
        }

        // ===========[ Helpers ]==================================================
        private void OpenFlyout ()
        {
            if (m_flyout != null && this.PART_Button != null)
            {
                m_flyout.ShowAt(this.PART_Button);
            }
        }

        private void CloseFlyout ()
        {
            m_flyout?.Hide();
        }

        private void RebuildFlyout ()
        {
            if (m_flyout != null)
            {
                m_flyout.Opened -= this.Flyout_OnOpened;
                m_flyout.Closed -= this.Flyout_OnClosed;
            }

            m_flyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };
            m_flyout.Opened += this.Flyout_OnOpened;
            m_flyout.Closed += this.Flyout_OnClosed;

            foreach (MenuFlyoutItemBase item in this.MenuItems)
            {
                m_flyout.Items.Add(item);

                // Auto-close the flyout when a leaf item is clicked.
                if (item is MenuFlyoutItem flyoutItem)
                {
                    flyoutItem.Click += (s, e) => this.CloseFlyout();
                }
            }
        }
    }
}
