namespace AJut.Application.Controls
{
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Markup;
    using System.Windows.Media;

    using DPUtils = AJut.Application.DPUtils<BumpStack>;

    [ContentProperty(nameof(Children))]
    [DefaultProperty(nameof(Children))]
    [TemplatePart(Name = nameof(PART_ScrollItemsContainer), Type = typeof(ScrollViewer))]
    [TemplatePart(Name = nameof(PART_AnteriorBumpButton), Type = typeof(ButtonBase))]
    public class BumpStack : Control, IAddChild
    {
        private int m_mouseScrollAccumulator;

        static BumpStack ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BumpStack), new FrameworkPropertyMetadata(typeof(BumpStack)));
        }

        // ============================[ Properties ]==========================================

        // Private Template Parts
        private ScrollViewer PART_ScrollItemsContainer { get; set; }
        private ButtonBase PART_AnteriorBumpButton { get; set; }

        // All UIElement children
        public ObservableCollection<UIElement> Children { get; } = new ObservableCollection<UIElement>();

        public static readonly DependencyProperty BumpIntervalProperty = DPUtils.Register(_ => _.BumpInterval, 1);
        public int BumpInterval
        {
            get => (int)this.GetValue(BumpIntervalProperty);
            set => this.SetValue(BumpIntervalProperty, value);
        }

        public static readonly DependencyProperty BumpDelayProperty = DPUtils.Register(_ => _.BumpDelay, 1);
        public int BumpDelay
        {
            get => (int)this.GetValue(BumpDelayProperty);
            set => this.SetValue(BumpDelayProperty, value);
        }

        public static readonly DependencyProperty OrientationProperty = DPUtils.Register(_ => _.Orientation, Orientation.Horizontal, (d, e) => d.OnOrientationChanged());
        public Orientation Orientation
        {
            get => (Orientation)this.GetValue(OrientationProperty);
            set => this.SetValue(OrientationProperty, value);
        }

        private static readonly DependencyPropertyKey HorizontalScrollBarVisibilityPropertyKey = DPUtils.RegisterReadOnly(_ => _.HorizontalScrollBarVisibility, ScrollBarVisibility.Hidden);
        public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty = HorizontalScrollBarVisibilityPropertyKey.DependencyProperty;
        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get => (ScrollBarVisibility)this.GetValue(HorizontalScrollBarVisibilityProperty);
            protected set => this.SetValue(HorizontalScrollBarVisibilityPropertyKey, value);
        }

        private static readonly DependencyPropertyKey VerticalScrollBarVisibilityPropertyKey = DPUtils.RegisterReadOnly(_ => _.VerticalScrollBarVisibility, ScrollBarVisibility.Disabled);
        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = VerticalScrollBarVisibilityPropertyKey.DependencyProperty;
        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get => (ScrollBarVisibility)this.GetValue(VerticalScrollBarVisibilityProperty);
            protected set => this.SetValue(VerticalScrollBarVisibilityPropertyKey, value);
        }

        public static readonly DependencyProperty EdgeClearanceOffsetProperty = DPUtils.Register(_ => _.EdgeClearanceOffset, (d, e) => d.OnRecalculateOffsetPaddings());
        public double EdgeClearanceOffset
        {
            get => (double)this.GetValue(EdgeClearanceOffsetProperty);
            set => this.SetValue(EdgeClearanceOffsetProperty, value);
        }

        private static readonly DependencyPropertyKey EdgeClearanceOffsetPaddingPropertyKey = DPUtils.RegisterReadOnly(_ => _.EdgeClearanceOffsetPadding, new Thickness(0.0));
        public static readonly DependencyProperty EdgeClearanceOffsetPaddingProperty = EdgeClearanceOffsetPaddingPropertyKey.DependencyProperty;
        public Thickness EdgeClearanceOffsetPadding
        {
            get => (Thickness)this.GetValue(EdgeClearanceOffsetPaddingProperty);
            protected set => this.SetValue(EdgeClearanceOffsetPaddingPropertyKey, value);
        }

        private static readonly DependencyPropertyKey EdgeAndButtonClearanceOffsetPaddingPropertyKey = DPUtils.RegisterReadOnly(_ => _.EdgeAndButtonClearanceOffsetPadding);
        public static readonly DependencyProperty EdgeAndButtonClearanceOffsetPaddingProperty = EdgeAndButtonClearanceOffsetPaddingPropertyKey.DependencyProperty;
        public Thickness EdgeAndButtonClearanceOffsetPadding
        {
            get => (Thickness)this.GetValue(EdgeAndButtonClearanceOffsetPaddingProperty);
            protected set => this.SetValue(EdgeAndButtonClearanceOffsetPaddingPropertyKey, value);
        }

        public static readonly DependencyProperty ButtonFontSizeProperty = DPUtils.Register(_ => _.ButtonFontSize, 16.0);
        public double ButtonFontSize
        {
            get => (double)this.GetValue(ButtonFontSizeProperty);
            set => this.SetValue(ButtonFontSizeProperty, value);
        }

        public static readonly DependencyProperty ButtonPaddingProperty = DPUtils.Register(_ => _.ButtonPadding, new Thickness(7.0));
        public Thickness ButtonPadding
        {
            get => (Thickness)this.GetValue(ButtonPaddingProperty);
            set => this.SetValue(ButtonPaddingProperty, value);
        }

        public static readonly DependencyProperty ButtonBackgroundProperty = DPUtils.Register(_ => _.ButtonBackground);
        public Brush ButtonBackground
        {
            get => (Brush)this.GetValue(ButtonBackgroundProperty);
            set => this.SetValue(ButtonBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonBorderProperty = DPUtils.Register(_ => _.ButtonBorder);
        public Brush ButtonBorder
        {
            get => (Brush)this.GetValue(ButtonBorderProperty);
            set => this.SetValue(ButtonBorderProperty, value);
        }

        public static readonly DependencyProperty ButtonForegroundProperty = DPUtils.Register(_ => _.ButtonForeground);
        public Brush ButtonForeground
        {
            get => (Brush)this.GetValue(ButtonForegroundProperty);
            set => this.SetValue(ButtonForegroundProperty, value);
        }

        public static readonly DependencyProperty InvertMouseWheelProperty = DPUtils.Register(_ => _.InvertMouseWheel);
        public bool InvertMouseWheel
        {
            get => (bool)this.GetValue(InvertMouseWheelProperty);
            set => this.SetValue(InvertMouseWheelProperty, value);
        }

        // ============================[ Methods ]==========================================
        public override void OnApplyTemplate ()
        {
            if (this.PART_AnteriorBumpButton != null)
            {
                this.PART_AnteriorBumpButton.SizeChanged -= this.OnAnteriorBumpButtonSizeChanged;
            }


            base.OnApplyTemplate();
            this.PART_ScrollItemsContainer = (ScrollViewer)this.GetTemplateChild(nameof(PART_ScrollItemsContainer));
            this.PART_AnteriorBumpButton = (ButtonBase)this.GetTemplateChild(nameof(PART_AnteriorBumpButton));

            this.PART_AnteriorBumpButton.SizeChanged -= this.OnAnteriorBumpButtonSizeChanged;
            this.PART_AnteriorBumpButton.SizeChanged += this.OnAnteriorBumpButtonSizeChanged;
            this.OnRecalculateOffsetPaddings();
        }

        public void AddChild (object value)
        {
            if (value is UIElement ui)
            {
                this.Children.Add(ui);
            }
            else
            {
                this.Children.Add(new ContentControl { Content = value });
            }
        }

        public void AddText (string text)
        {
            this.Children.Add(new TextBlock { Text = text });
        }

        protected override void OnPreviewMouseWheel (MouseWheelEventArgs e)
        {
            if (this.Orientation == Orientation.Horizontal)
            {
                int effectiveDelta = m_mouseScrollAccumulator + e.Delta;
                int lineOvers = effectiveDelta / this.BumpInterval;
                if (lineOvers == 0)
                {
                    m_mouseScrollAccumulator += e.Delta;
                }
                else
                {
                    m_mouseScrollAccumulator = 0;
                }
                bool left = this.InvertMouseWheel;
                if (lineOvers < 0)
                {
                    left = !this.InvertMouseWheel;
                    lineOvers = -lineOvers;
                }
                while (lineOvers-- > 0)
                {
                    if (left)
                    {
                        this.PART_ScrollItemsContainer.LineLeft();
                    }
                    else
                    {
                        this.PART_ScrollItemsContainer.LineRight();
                    }
                }

                e.Handled = true;
                return;
            }
            else if (this.InvertMouseWheel)
            {
                int effectiveDelta = m_mouseScrollAccumulator + e.Delta;
                int lineOvers = effectiveDelta / this.BumpInterval;
                if (lineOvers == 0)
                {
                    m_mouseScrollAccumulator += e.Delta;
                }
                else
                {
                    m_mouseScrollAccumulator = 0;
                }

                // We know it's inverted, so this logic might seem backwards... but that's because it is :)
                bool up = false;
                if (lineOvers < 0)
                {
                    up = true;
                    lineOvers = -lineOvers;
                }
                while (lineOvers-- > 0)
                {
                    if (up)
                    {
                        this.PART_ScrollItemsContainer.LineUp();
                    }
                    else
                    {
                        this.PART_ScrollItemsContainer.LineDown();
                    }
                }

                e.Handled = true;
                return;
            }

            base.OnPreviewMouseWheel(e);
        }

        protected virtual void OnOrientationChanged ()
        {
            if (this.Orientation == Orientation.Horizontal)
            {
                this.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                this.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
            else
            {
                this.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                this.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }

            this.OnRecalculateOffsetPaddings();
        }

        protected virtual void OnRecalculateOffsetPaddings ()
        {
            double offset = this.EdgeClearanceOffset;
            if (this.Orientation == Orientation.Horizontal)
            {
                this.EdgeClearanceOffsetPadding = new Thickness(offset, 0.0, offset, 0.0);

                double offsetPlusButton = offset + (this.PART_AnteriorBumpButton?.ActualWidth ?? 0.0);
                this.EdgeAndButtonClearanceOffsetPadding = new Thickness(offsetPlusButton, 0.0, offsetPlusButton, 0.0);
            }
            else
            {
                this.EdgeClearanceOffsetPadding = new Thickness(0.0, offset, 0.0, offset);

                double offsetPlusButton = offset + (this.PART_AnteriorBumpButton?.ActualHeight ?? 0.0);
                this.EdgeAndButtonClearanceOffsetPadding = new Thickness(0.0, offsetPlusButton, 0.0, offsetPlusButton);
            }
        }

        private void OnAnteriorBumpButtonSizeChanged (object sender, SizeChangedEventArgs e)
        {
            this.OnRecalculateOffsetPaddings();
        }
    }
}
