namespace AJutShowRoomAndroid
{
    using System;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Graphics;
    using Android.Graphics.Drawables;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// The PageStack demos, the WinUI3 and AppKit pages' three: horizontal, vertical, and a few pages, with a checkbox each
    /// for the dots, the page count and swiping, switching all three. The first two are stocked from code because their
    /// pages are demo data, in translucent tints of their own like the WinUI3 page's; the third declares its pages in the
    /// layout, with the selected index set before they are added.
    /// </summary>
    public sealed class PageStackPage : ShowroomPage
    {
        private const int kHorizontalPageCount = 20;
        private const int kVerticalPageCount = 6;

        private PageStack? m_horizontal;
        private PageStack? m_vertical;
        private PageStack? m_few;
        private CheckBox? m_showDotsCheck;
        private CheckBox? m_showPageCountCheck;
        private CheckBox? m_swipeCheck;
        private TextView? m_selectedIndexLabel;

        public override string Title => "PageStack";

        public override int LayoutId => Resource.Layout.demo_page_stack;

        public override int MarkupSourceId => Resource.Raw.demo_page_stack_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            float density = host.Resources!.DisplayMetrics!.Density;
            m_horizontal = root.FindViewById<PageStack>(Resource.Id.demo_page_stack_horizontal)!;
            m_vertical = root.FindViewById<PageStack>(Resource.Id.demo_page_stack_vertical)!;
            m_few = root.FindViewById<PageStack>(Resource.Id.demo_page_stack_few)!;
            for (int index = 0; index < kHorizontalPageCount; ++index)
            {
                m_horizontal.Children.Add(_MakePage(host, $"Page {index + 1}", index, density));
            }

            for (int index = 0; index < kVerticalPageCount; ++index)
            {
                m_vertical.Children.Add(_MakePage(host, $"Card {index + 1}", index, density));
            }

            m_selectedIndexLabel = root.FindViewById<TextView>(Resource.Id.demo_page_stack_selected_index)!;
            m_horizontal.SelectedIndexChanged += this.OnHorizontalSelectedIndexChanged;
            this.UpdateSelectedIndexLabel();

            m_showDotsCheck = root.FindViewById<CheckBox>(Resource.Id.demo_page_stack_show_dots)!;
            m_showPageCountCheck = root.FindViewById<CheckBox>(Resource.Id.demo_page_stack_show_page_count)!;
            m_swipeCheck = root.FindViewById<CheckBox>(Resource.Id.demo_page_stack_swipe)!;
            m_showDotsCheck.CheckedChange += this.OnShowDotsChanged;
            m_showPageCountCheck.CheckedChange += this.OnShowPageCountChanged;
            m_swipeCheck.CheckedChange += this.OnSwipeChanged;
            this.ApplyChecks();
        }

        public override void Unbind ()
        {
            if (m_horizontal != null)
            {
                m_horizontal.SelectedIndexChanged -= this.OnHorizontalSelectedIndexChanged;
            }

            if (m_showDotsCheck != null)
            {
                m_showDotsCheck.CheckedChange -= this.OnShowDotsChanged;
                m_showPageCountCheck!.CheckedChange -= this.OnShowPageCountChanged;
                m_swipeCheck!.CheckedChange -= this.OnSwipeChanged;
            }

            m_horizontal = null;
            m_vertical = null;
            m_few = null;
            m_showDotsCheck = null;
            m_showPageCountCheck = null;
            m_swipeCheck = null;
            m_selectedIndexLabel = null;
        }

        private void OnHorizontalSelectedIndexChanged (object? sender, EventArgs e) => this.UpdateSelectedIndexLabel();

        private void OnShowDotsChanged (object? sender, CompoundButton.CheckedChangeEventArgs e) => this.ApplyChecks();

        private void OnShowPageCountChanged (object? sender, CompoundButton.CheckedChangeEventArgs e) => this.ApplyChecks();

        private void OnSwipeChanged (object? sender, CompoundButton.CheckedChangeEventArgs e) => this.ApplyChecks();

        // Every checkbox switches all three stacks
        private void ApplyChecks ()
        {
            if ((m_horizontal == null) || (m_vertical == null) || (m_few == null))
            {
                return;
            }

            foreach (PageStack stack in new[] { m_horizontal, m_vertical, m_few })
            {
                stack.ShowDots = m_showDotsCheck!.Checked;
                stack.ShowPageCount = m_showPageCountCheck!.Checked;
                stack.IsSwipeEnabled = m_swipeCheck!.Checked;
            }
        }

        private void UpdateSelectedIndexLabel ()
        {
            if ((m_selectedIndexLabel != null) && (m_horizontal != null))
            {
                m_selectedIndexLabel.Text = $"SelectedIndexChanged, SelectedIndex: {m_horizontal.SelectedIndex}";
            }
        }

        // A page: a rounded tint with its name in the middle
        private static View _MakePage (MainActivity host, string text, int index, float density)
        {
            const int kTintAlpha = 0x88;
            const int kTintGreen = 0x90;
            const int kTintBlue = 0xC8;
            const float kCornerRadiusDp = 6f;
            int tint = 70 + ((index * 37) % 150);

            var background = new GradientDrawable();
            background.SetColor(Color.Argb(kTintAlpha, tint, kTintGreen, kTintBlue));
            background.SetCornerRadius(kCornerRadiusDp * density);
            return new TextView(host) { Text = text, Gravity = GravityFlags.Center, Background = background };
        }
    }
}
