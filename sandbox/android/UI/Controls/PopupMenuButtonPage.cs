namespace AJutShowRoomAndroid
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;

    /// <summary>The PopupMenuButton demo: a menu of plain, checkable, disabled and submenu items, and a line saying what was picked.</summary>
    public sealed class PopupMenuButtonPage : ShowroomPage
    {
        private readonly List<PopupMenuItem> m_leaves = new List<PopupMenuItem>();
        private TextView? m_value;

        public override string Title => "PopupMenuButton";

        public override int LayoutId => Resource.Layout.demo_popup_menu_button;

        public override int MarkupSourceId => Resource.Raw.demo_popup_menu_button_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_value = root.FindViewById<TextView>(Resource.Id.demo_popup_menu_value)!;
            var button = root.FindViewById<PopupMenuButton>(Resource.Id.demo_popup_menu)!;

            var recent = new PopupMenuItem("Recent");
            recent.Items.Add(_Leaf(new PopupMenuItem("notes.txt")));
            recent.Items.Add(_Leaf(new PopupMenuItem("plan.md")));

            button.MenuItems.Add(_Leaf(new PopupMenuItem("Open")));
            button.MenuItems.Add(_Leaf(new PopupMenuItem("Save")));
            button.MenuItems.Add(recent);
            button.MenuItems.Add(PopupMenuItem.CreateSeparator());
            button.MenuItems.Add(_Leaf(new PopupMenuItem("Word wrap") { IsCheckable = true, IsChecked = true }));
            button.MenuItems.Add(_Leaf(new PopupMenuItem("Line numbers") { IsCheckable = true }));
            button.MenuItems.Add(PopupMenuItem.CreateSeparator());
            button.MenuItems.Add(_Leaf(new PopupMenuItem("Delete") { IsEnabled = false }));

            PopupMenuItem _Leaf (PopupMenuItem item)
            {
                item.Click += this.OnItemClick;
                m_leaves.Add(item);
                return item;
            }
        }

        public override void Unbind ()
        {
            foreach (PopupMenuItem item in m_leaves)
            {
                item.Click -= this.OnItemClick;
            }

            m_leaves.Clear();
            m_value = null;
        }

        private void OnItemClick (object? sender, EventArgs e)
        {
            if ((m_value != null) && (sender is PopupMenuItem item))
            {
                m_value.Text = item.IsCheckable ? $"{item.Text} is {(item.IsChecked ? "on" : "off")}." : $"Picked {item.Text}.";
            }
        }
    }
}
