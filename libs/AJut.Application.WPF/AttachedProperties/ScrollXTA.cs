namespace AJut.Application.AttachedProperties
{
    using System;
    using System.Windows;
    using System.Windows.Controls;

    public static class ScrollXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(ScrollXTA));

        public static DependencyProperty IsScrollWatchEnabledProperty = APUtils.Register(GetIsScrollWatchEnabled, SetIsScrollWatchEnabled, OnIsScrollWatchEnabledChanged);
        public static bool GetIsScrollWatchEnabled (DependencyObject obj) => (bool)obj.GetValue(IsScrollWatchEnabledProperty);
        public static void SetIsScrollWatchEnabled (DependencyObject obj, bool value) => obj.SetValue(IsScrollWatchEnabledProperty, value);
        private static void OnIsScrollWatchEnabledChanged (DependencyObject target, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (e.HasNewValue && e.NewValue && target is ScrollViewer scrollViewer)
            {
                SetWatchHelper(target, new ScrollWatchHelper(scrollViewer));
            }
            else
            {
                ClearWatchHelper(target);
            }
        }

        private static DependencyPropertyKey CanScrollLeftPropertyKey = APUtils.RegisterReadOnly(GetCanScrollLeft, SetCanScrollLeft);
        public static DependencyProperty CanScrollLeftProperty = CanScrollLeftPropertyKey.DependencyProperty;
        public static bool GetCanScrollLeft (DependencyObject obj) => (bool)obj.GetValue(CanScrollLeftProperty);
        private static void SetCanScrollLeft (DependencyObject obj, bool value) => obj.SetValue(CanScrollLeftPropertyKey, value);

        private static DependencyPropertyKey CanScrollRightPropertyKey = APUtils.RegisterReadOnly(GetCanScrollRight, SetCanScrollRight);
        public static DependencyProperty CanScrollRightProperty = CanScrollRightPropertyKey.DependencyProperty;
        public static bool GetCanScrollRight (DependencyObject obj) => (bool)obj.GetValue(CanScrollRightProperty);
        private static void SetCanScrollRight (DependencyObject obj, bool value) => obj.SetValue(CanScrollRightPropertyKey, value);

        private static DependencyPropertyKey CanScrollHorizontallyPropertyKey = APUtils.RegisterReadOnly(GetCanScrollHorizontally, SetCanScrollHorizontally);
        public static DependencyProperty CanScrollHorizontallyProperty = CanScrollHorizontallyPropertyKey.DependencyProperty;
        public static bool GetCanScrollHorizontally (DependencyObject obj) => (bool)obj.GetValue(CanScrollHorizontallyProperty);
        private static void SetCanScrollHorizontally (DependencyObject obj, bool value) => obj.SetValue(CanScrollHorizontallyPropertyKey, value);

        private static DependencyPropertyKey CanScrollUpPropertyKey = APUtils.RegisterReadOnly(GetCanScrollUp, SetCanScrollUp);
        public static DependencyProperty CanScrollUpProperty = CanScrollUpPropertyKey.DependencyProperty;
        public static bool GetCanScrollUp (DependencyObject obj) => (bool)obj.GetValue(CanScrollUpProperty);
        private static void SetCanScrollUp (DependencyObject obj, bool value) => obj.SetValue(CanScrollUpPropertyKey, value);

        private static DependencyPropertyKey CanScrollDownPropertyKey = APUtils.RegisterReadOnly(GetCanScrollDown, SetCanScrollDown);
        public static DependencyProperty CanScrollDownProperty = CanScrollDownPropertyKey.DependencyProperty;
        public static bool GetCanScrollDown (DependencyObject obj) => (bool)obj.GetValue(CanScrollDownProperty);
        private static void SetCanScrollDown (DependencyObject obj, bool value) => obj.SetValue(CanScrollDownPropertyKey, value);

        private static DependencyPropertyKey CanScrollVerticallyPropertyKey = APUtils.RegisterReadOnly(GetCanScrollVertically, SetCanScrollVertically);
        public static DependencyProperty CanScrollVerticallyProperty = CanScrollVerticallyPropertyKey.DependencyProperty;
        public static bool GetCanScrollVertically (DependencyObject obj) => (bool)obj.GetValue(CanScrollVerticallyProperty);
        private static void SetCanScrollVertically (DependencyObject obj, bool value) => obj.SetValue(CanScrollVerticallyPropertyKey, value);

        private static DependencyPropertyKey WatchHelperPropertyKey = APUtils.RegisterReadOnly(GetWatchHelper, SetWatchHelper);
        public static DependencyProperty WatchHelperProperty = WatchHelperPropertyKey.DependencyProperty;
        public static ScrollWatchHelper GetWatchHelper (DependencyObject obj) => (ScrollWatchHelper)obj.GetValue(WatchHelperProperty);
        private static void SetWatchHelper (DependencyObject obj, ScrollWatchHelper value) => obj.SetValue(WatchHelperPropertyKey, value);
        private static void ClearWatchHelper (DependencyObject obj)
        {
            GetWatchHelper(obj)?.Dispose();
            SetWatchHelper(obj, null);
            obj.ClearValue(WatchHelperPropertyKey);
        }

        public class ScrollWatchHelper : IDisposable
        {
            private readonly ScrollViewer m_scrollViewer;

            public ScrollWatchHelper (ScrollViewer scrollViewer)
            {
                m_scrollViewer = scrollViewer;
                m_scrollViewer.ScrollChanged += this.ScrollViewer_OnScrollChanged;

                if (m_scrollViewer.IsLoaded)
                {
                    this.RecalculateScrollAvailabillity();
                }
                else
                {
                    m_scrollViewer.Loaded += _WhenScrollViewerFirstLoads;
                }

                void _WhenScrollViewerFirstLoads (object sender, RoutedEventArgs e)
                {
                    m_scrollViewer.Loaded -= _WhenScrollViewerFirstLoads;
                    this.RecalculateScrollAvailabillity();
                }
            }

            public void Dispose ()
            {
                m_scrollViewer.ScrollChanged -= this.ScrollViewer_OnScrollChanged;
            }

            private void ScrollViewer_OnScrollChanged (object sender, ScrollChangedEventArgs e)
            {
                this.RecalculateScrollAvailabillity();
            }

            private void RecalculateScrollAvailabillity ()
            {
                SetCanScrollLeft(m_scrollViewer, m_scrollViewer.HorizontalOffset != 0);
                SetCanScrollRight(m_scrollViewer, m_scrollViewer.ContentHorizontalOffset < m_scrollViewer.ScrollableWidth);
                SetCanScrollHorizontally(m_scrollViewer, m_scrollViewer.ScrollableWidth != 0.0);

                SetCanScrollUp(m_scrollViewer, m_scrollViewer.VerticalOffset != 0);
                SetCanScrollDown(m_scrollViewer, m_scrollViewer.ContentVerticalOffset < m_scrollViewer.ScrollableHeight);
                SetCanScrollVertically(m_scrollViewer, m_scrollViewer.ScrollableHeight != 0.0);
            }
        }
    }
}
