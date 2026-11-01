namespace AJutShowRoomAppKit.UI
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;

    // A column of navigation buttons, one selected at a time, with room for header and footer
    // views. The window's area list and each area's page list are both one of these, each on
    // its own level of the showroom's surfaces.
    public sealed class NavColumn : ThemedView
    {
        private const double kColumnWidth = 210.0;

        private readonly FillStack m_stack;
        private readonly FillStack m_itemStack;
        private readonly FillStack m_footerStack;
        private readonly List<NSButton> m_buttons = new List<NSButton>();
        private int m_selectedIndex = -1;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>A column on <paramref name="backgroundColorKey"/>, one of ShowroomStyle's surface keys</summary>
        public NavColumn (string backgroundColorKey)
        {
            ShowroomStyle.EnsureRegistered();
            this.BackgroundColorKey = backgroundColorKey;
            this.TranslatesAutoresizingMaskIntoConstraints = false;
            this.WidthAnchor.ConstraintEqualTo((nfloat)kColumnWidth).Active = true;

            m_stack = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 10.0)
            {
                Padding = new NSEdgeInsets(12, 12, 12, 12),
            };
            m_stack.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(m_stack);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_stack.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_stack.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_stack.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_stack.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            m_itemStack = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 2.0);
            m_footerStack = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 8.0);
            m_stack.Add(m_itemStack);
            m_stack.Add(new NSView(), grows: true);
            m_stack.Add(m_footerStack);
        }

        public void Teardown ()
        {
            foreach (NSButton button in m_buttons)
            {
                button.Activated -= this.OnItemActivated;
            }
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when the user picks an item, or <see cref="Select"/> picks one</summary>
        public event EventHandler<EventArgs> SelectionChanged;

        private void OnItemActivated (object sender, EventArgs e)
        {
            this.Select(m_buttons.IndexOf((NSButton)sender));
        }

        // ===========[ Properties ]============================================
        public int SelectedIndex => m_selectedIndex;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Adds a view above the items, such as the area's title and blurb</summary>
        public void AddHeader (NSView view)
        {
            m_stack.Insert(m_stack.ChildCount - 3, view);
        }

        /// <summary>Adds a view to the bottom of the column</summary>
        public void AddFooter (NSView view)
        {
            m_footerStack.Add(view);
        }

        public void AddItem (string title)
        {
            var button = new NSButton { Title = title };
            button.SetButtonType(NSButtonType.PushOnPushOff);
            button.BezelStyle = NSBezelStyle.Recessed;
            button.Alignment = NSTextAlignment.Left;
            button.Activated += this.OnItemActivated;
            m_buttons.Add(button);
            m_itemStack.Add(button);
        }

        public void Select (int index)
        {
            if (index < 0 || index >= m_buttons.Count)
            {
                return;
            }

            m_selectedIndex = index;
            for (int i = 0; i < m_buttons.Count; ++i)
            {
                m_buttons[i].State = i == index ? NSCellStateValue.On : NSCellStateValue.Off;
            }

            this.SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
