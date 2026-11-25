namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using DPUtils = AJut.UX.DPUtils<PageStackDot>;

    // ===========[ PageStackDot ]===============================================
    // One dot in a PageStack's dot strip. The PageStack makes, reuses and fills these: which page a dot goes to, what kind
    // it is (a page, the current page, or a faded edge dot standing for more pages past the window), and the brushes and
    // size it draws with, which the PageStack passes on from its own properties. How each kind looks is the KindStates
    // group in PageStack.xaml. An edge dot grows to full size under the pointer, staying faded, so it reads as something
    // to click; the kind states take the pointer into account for that, since the stock pointer states can't tell an
    // edge dot from any other.
    /// <summary>
    /// One dot in a <see cref="PageStack"/>'s dot strip. Clicking it takes the stack to <see cref="PageIndex"/>. Made and
    /// filled by the PageStack; not meant to be used on its own.
    /// </summary>
    public class PageStackDot : Button
    {
        private const string kKindStatePage = "Page";
        private const string kKindStateCurrent = "Current";
        private const string kKindStateEdge = "Edge";
        private const string kKindStateEdgePointerOver = "EdgePointerOver";

        private bool m_isPointerOver;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PageStackDot ()
        {
            this.DefaultStyleKey = typeof(PageStackDot);
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            this.ApplyKindState(useTransitions: false);
        }

        // ===========[ Events ]================================================
        protected override void OnPointerEntered (PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
            this.SetPointerOver(true);
        }

        protected override void OnPointerExited (PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            this.SetPointerOver(false);
        }

        protected override void OnPointerCanceled (PointerRoutedEventArgs e)
        {
            base.OnPointerCanceled(e);
            this.SetPointerOver(false);
        }

        protected override void OnPointerCaptureLost (PointerRoutedEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            this.SetPointerOver(false);
        }

        // ===========[ Dependency Properties ]=================================
        public static readonly DependencyProperty PageIndexProperty = DPUtils.Register(_ => _.PageIndex, 0);
        /// <summary>The page clicking this dot goes to</summary>
        public int PageIndex
        {
            get => (int)this.GetValue(PageIndexProperty);
            set => this.SetValue(PageIndexProperty, value);
        }

        public static readonly DependencyProperty KindProperty = DPUtils.Register(_ => _.Kind, ePageStackDotKind.Page, (d, e) => d.ApplyKindState(useTransitions: true));
        /// <summary>What the dot stands for, which sets how it looks</summary>
        public ePageStackDotKind Kind
        {
            get => (ePageStackDotKind)this.GetValue(KindProperty);
            set => this.SetValue(KindProperty, value);
        }

        public static readonly DependencyProperty DotBrushProperty = DPUtils.Register(_ => _.DotBrush);
        /// <summary>The fill of every dot but the current one</summary>
        public Brush DotBrush
        {
            get => (Brush)this.GetValue(DotBrushProperty);
            set => this.SetValue(DotBrushProperty, value);
        }

        public static readonly DependencyProperty CurrentDotBrushProperty = DPUtils.Register(_ => _.CurrentDotBrush);
        /// <summary>The fill of the current page's dot</summary>
        public Brush CurrentDotBrush
        {
            get => (Brush)this.GetValue(CurrentDotBrushProperty);
            set => this.SetValue(CurrentDotBrushProperty, value);
        }

        public static readonly DependencyProperty DotSizeProperty = DPUtils.Register(_ => _.DotSize, 8.0);
        /// <summary>The diameter of a full size dot; edge dots draw smaller</summary>
        public double DotSize
        {
            get => (double)this.GetValue(DotSizeProperty);
            set => this.SetValue(DotSizeProperty, value);
        }

        // ===========[ Helper Methods ]========================================
        private void ApplyKindState (bool useTransitions)
        {
            string state;
            switch (this.Kind)
            {
                case ePageStackDotKind.Current:
                    state = kKindStateCurrent;
                    break;

                case ePageStackDotKind.BackEdge:
                case ePageStackDotKind.ForwardEdge:
                    state = m_isPointerOver ? kKindStateEdgePointerOver : kKindStateEdge;
                    break;

                default:
                    state = kKindStatePage;
                    break;
            }

            VisualStateManager.GoToState(this, state, useTransitions);
        }

        private void SetPointerOver (bool isPointerOver)
        {
            if (m_isPointerOver != isPointerOver)
            {
                m_isPointerOver = isPointerOver;
                this.ApplyKindState(useTransitions: true);
            }
        }
    }
}
