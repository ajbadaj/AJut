namespace AJut.UX.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Markup;
    using DPUtils = AJut.UX.DPUtils<PopupMenuButton>;

    [ContentProperty("MenuItems")]
    [TemplatePart(Name = nameof(PART_Popup), Type = typeof(Popup))]
    public class PopupMenuButton : Control, IDisposable
    {
        private Popup PART_Popup { get; set; }
        private Window m_currWindow;
        static PopupMenuButton ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PopupMenuButton), new FrameworkPropertyMetadata(typeof(PopupMenuButton)));
        }

        public PopupMenuButton()
        {
            this.AddHandler(Button.ClickEvent, new RoutedEventHandler(_HandleClick));
            void _HandleClick(object _sender, RoutedEventArgs _e)
            {
                if (this.PART_Popup != null)
                {
                    this.PART_Popup.IsOpen = true;
                }
            }

            this.MenuItems.CollectionChanged += this.OnMenuItemsChanged;

            this.Loaded += this.PopupMenuButton_OnLoaded;
        }


        public static readonly DependencyProperty MenuPopupAlignmentProperty = DPUtils.Register(_ => _.MenuPopupAlignment, HorizontalAlignment.Left);
        public HorizontalAlignment MenuPopupAlignment
        {
            get => (HorizontalAlignment)this.GetValue(MenuPopupAlignmentProperty);
            set => this.SetValue(MenuPopupAlignmentProperty, value);
        }

        public static readonly DependencyProperty MenuPopupAnimationProperty = DPUtils.Register(_ => _.MenuPopupAnimation, PopupAnimation.Slide);
        public PopupAnimation MenuPopupAnimation
        {
            get => (PopupAnimation)this.GetValue(MenuPopupAnimationProperty);
            set => this.SetValue(MenuPopupAnimationProperty, value);
        }

        private void PopupMenuButton_OnLoaded (object sender, RoutedEventArgs e)
        {
            this.ClearWindowRef();
            m_currWindow = Window.GetWindow(this);
            m_currWindow.LocationChanged += this.OnCurrWindowMoved;
        }

        private void OnCurrWindowMoved (object sender, EventArgs e)
        {
            if (this.PART_Popup != null)
            {
                this.PART_Popup.IsOpen = false;
            }
        }

        private void OnMenuItemsChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (MenuItem item in e.OldItems)
                {
                    item.Click -= _OnItemClicked;
                }
            }

            if (e.NewItems != null)
            {
                foreach (MenuItem item in e.NewItems)
                {
                    item.Click -= _OnItemClicked;
                    item.Click += _OnItemClicked;
                }
            }

            void _OnItemClicked (object sender, RoutedEventArgs e)
            {
                this.PART_Popup.IsOpen = false;
            }
        }

        public override void OnApplyTemplate ()
        {
            if (this.PART_Popup != null)
            {
                this.PART_Popup.IsOpen = false;
            }

            base.OnApplyTemplate();
            this.PART_Popup = (Popup)this.GetTemplateChild(nameof(PART_Popup));
        }

        public void Dispose ()
        {
            this.ClearWindowRef();
        }

        private void ClearWindowRef ()
        {
            if (m_currWindow != null)
            {
                m_currWindow.LocationChanged -= this.OnCurrWindowMoved;
                m_currWindow = null;
            }
        }

        public ObservableCollection<MenuItem> MenuItems { get; } = new ObservableCollection<MenuItem>();

        public static readonly DependencyProperty ButtonStyleProperty = DPUtils.Register(_ => _.ButtonStyle);
        public Style ButtonStyle
        {
            get => (Style)this.GetValue(ButtonStyleProperty);
            set => this.SetValue(ButtonStyleProperty, value);
        }

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
    }
}
