namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Windows.Input;

    // ===========[ PopupMenuItem ]==============================================
    // One entry in a PopupMenuButton's menu, for the toolkits that build their menus in code (Android
    // and UIKit). It covers what WinUI3's MenuFlyoutItem, ToggleMenuFlyoutItem, MenuFlyoutSubItem and
    // MenuFlyoutSeparator do between them, and WPF's MenuItem and Separator: a plain item, a checkable
    // one, one that opens a submenu of its own Items, or a separator. A toolkit turns these into its own
    // menu each time the menu opens, so a change made while it is closed shows the next time it opens.
    public class PopupMenuItem : NotifyPropertyChanged
    {
        private string m_text;
        private bool m_isEnabled = true;
        private bool m_isCheckable;
        private bool m_isChecked;
        private ICommand? m_command;
        private object? m_commandParameter;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PopupMenuItem () : this(string.Empty)
        {
        }

        public PopupMenuItem (string text, ICommand? command = null)
        {
            m_text = text;
            m_command = command;
        }

        private PopupMenuItem (bool isSeparator) : this(string.Empty)
        {
            this.IsSeparator = isSeparator;
        }

        /// <summary>A line between groups of items, which a toolkit draws its own way</summary>
        public static PopupMenuItem CreateSeparator () => new PopupMenuItem(isSeparator: true);

        // ===========[ Events ]================================================
        /// <summary>Raised when the item is picked, after a checkable item flips <see cref="IsChecked"/></summary>
        public event EventHandler<EventArgs>? Click;

        // ===========[ Properties ]============================================
        public string Text
        {
            get => m_text;
            set => this.SetAndRaiseIfChanged(ref m_text, value);
        }

        /// <summary>False shows the item and does nothing when it is picked</summary>
        public bool IsEnabled
        {
            get => m_isEnabled;
            set => this.SetAndRaiseIfChanged(ref m_isEnabled, value);
        }

        /// <summary>True shows a check when <see cref="IsChecked"/>, and picking the item flips it, as ToggleMenuFlyoutItem does</summary>
        public bool IsCheckable
        {
            get => m_isCheckable;
            set => this.SetAndRaiseIfChanged(ref m_isCheckable, value);
        }

        public bool IsChecked
        {
            get => m_isChecked;
            set => this.SetAndRaiseIfChanged(ref m_isChecked, value);
        }

        /// <summary>Run when the item is picked. An item whose command cannot run is shown disabled, as WinUI3 shows one.</summary>
        public ICommand? Command
        {
            get => m_command;
            set => this.SetAndRaiseIfChanged(ref m_command, value);
        }

        public object? CommandParameter
        {
            get => m_commandParameter;
            set => this.SetAndRaiseIfChanged(ref m_commandParameter, value);
        }

        /// <summary>True for a separator (<see cref="CreateSeparator"/>), which shows no text and cannot be picked</summary>
        public bool IsSeparator { get; }

        /// <summary>The item's submenu. With any items in it, the item opens them rather than being picked.</summary>
        public ObservableCollection<PopupMenuItem> Items { get; } = new ObservableCollection<PopupMenuItem>();

        public bool HasSubmenu => this.Items.Count > 0;

        /// <summary>Whether picking the item would do anything now: it is enabled, is not a separator, and its command can run</summary>
        public bool CanInvoke => this.IsEnabled && !this.IsSeparator && (this.Command?.CanExecute(this.CommandParameter) ?? true);

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// What a toolkit calls when the item is picked: a checkable item flips <see cref="IsChecked"/>, then
        /// <see cref="Click"/> is raised and <see cref="Command"/> run. Does nothing for an item that cannot be picked or
        /// that opens a submenu.
        /// </summary>
        public void Invoke ()
        {
            if (!this.CanInvoke || this.HasSubmenu)
            {
                return;
            }

            if (this.IsCheckable)
            {
                this.IsChecked = !this.IsChecked;
            }

            this.Click?.Invoke(this, EventArgs.Empty);
            if (this.Command?.CanExecute(this.CommandParameter) == true)
            {
                this.Command.Execute(this.CommandParameter);
            }
        }

        /// <summary>
        /// <paramref name="items"/> split at their separators, in order: each run between separators is one group, which
        /// a toolkit draws with a divider between it and the next. A separator first, last or beside another adds no
        /// empty group.
        /// </summary>
        public static IReadOnlyList<IReadOnlyList<PopupMenuItem>> GroupBySeparators (IEnumerable<PopupMenuItem> items)
        {
            var groups = new List<IReadOnlyList<PopupMenuItem>>();
            var current = new List<PopupMenuItem>();
            foreach (PopupMenuItem item in items)
            {
                if (!item.IsSeparator)
                {
                    current.Add(item);
                    continue;
                }

                if (current.Count > 0)
                {
                    groups.Add(current);
                    current = new List<PopupMenuItem>();
                }
            }

            if (current.Count > 0)
            {
                groups.Add(current);
            }

            return groups;
        }
    }
}
