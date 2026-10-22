namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// One option in a toggle strip: the data it stands for, the name it shows, and whether it is selected. Setting
    /// <see cref="IsSelected"/> runs the strip's selection rules through the collection the item is in: a single-select
    /// strip deselects the others, and deselecting the last selected item is refused unless the strip allows no
    /// selection.
    /// </summary>
    public class ToggleStripItem : INotifyPropertyChanged
    {
        private string? m_name;
        private bool m_isSelected;
        private int m_itemIndex;
        private bool m_hasDefaultName;
        private bool m_isFirstItem;
        private bool m_isLastItem;
        private IToggleStripItemList? m_list;

        // ===========[ Setup/Construction/Teardown ]===================================
        /// <summary>Makes the item for <paramref name="data"/>, named the way <paramref name="host"/> says; <paramref name="index"/> is -1 when not known yet.</summary>
        public ToggleStripItem (IToggleStripHost host, object data, int index)
        {
            this.Host = host;
            this.Data = data;
            m_itemIndex = index;
            this.ResetName();
        }

        // ===========[ Events ]===================================
        public event PropertyChangedEventHandler? PropertyChanged;

        // ===========[ Properties ]===================================
        /// <summary>The source object this item stands for.</summary>
        public object Data { get; }

        /// <summary>The strip this item belongs to.</summary>
        public IToggleStripHost Host { get; }

        /// <summary>The item's position in its collection; -1 until it is in one.</summary>
        public int ItemIndex
        {
            get => m_itemIndex;
            internal set
            {
                if (this.SetAndRaiseIfChanged(ref m_itemIndex, value))
                {
                    this.IsFirstItem = (m_itemIndex == 0);
                    this.IsLastItem = m_list?.IsLast(this) ?? false;
                    if (m_hasDefaultName)
                    {
                        this.ResetNameWithDefault();
                    }
                }
            }
        }

        /// <summary>The name the strip shows for this item.</summary>
        public string? Name
        {
            get => m_name;
            set => this.SetAndRaiseIfChanged(ref m_name, value);
        }

        /// <summary>
        /// Whether the item is selected. A deselect the strip does not allow (the last selected item, with no selection
        /// disallowed) is refused, and PropertyChanged(IsSelected) is raised anyway: every toolkit's toggle has already
        /// flipped its own state by the time the edit reaches here, and it flips back when it hears the value again.
        /// </summary>
        public bool IsSelected
        {
            get => m_isSelected;
            set
            {
                if (!value && (m_list != null) && !m_list.CanBeDeselected())
                {
                    // Deselect rejected - raise PropertyChanged to revert the view's toggle
                    this.RaisePropertyChanged(nameof(IsSelected));
                    return;
                }

                if (this.SetAndRaiseIfChanged(ref m_isSelected, value))
                {
                    if (value)
                    {
                        m_list?.HandleWasSelected(this);
                    }
                    else
                    {
                        m_list?.HandleWasDeselected(this, isPartOfSelectionSwap: false);
                    }
                }
            }
        }

        /// <summary>True for the first item in the strip.</summary>
        public bool IsFirstItem
        {
            get => m_isFirstItem;
            private set => this.SetAndRaiseIfChanged(ref m_isFirstItem, value);
        }

        /// <summary>True for the last item in the strip.</summary>
        public bool IsLastItem
        {
            get => m_isLastItem;
            private set => this.SetAndRaiseIfChanged(ref m_isLastItem, value);
        }

        // ===========[ Public Interface Methods ]===================================
        internal void AttachTo (IToggleStripItemList list)
        {
            m_list = list;
        }

        // Re-evaluates the index, first and last flags even when the index itself did not change: adding an item after
        // this one keeps this one's index but takes its last-item flag away.
        internal void EvaluateOrder ()
        {
            m_itemIndex = -1;
            this.ItemIndex = m_list?.IndexOf(this) ?? -1;
        }

        // Names the item from the host's selector, else its display property path, else the data itself.
        internal void ResetName ()
        {
            Func<object, string?>? selector = this.Host.ItemNameSelector;
            if (selector != null)
            {
                string? selected = selector(this.Data);
                if (selected != null)
                {
                    this.Name = selected;
                    m_hasDefaultName = false;
                    return;
                }
            }

            string? displayPropertyPath = this.Host.DisplayPropertyPath;
            if (displayPropertyPath?.Length > 0)
            {
                try
                {
                    var prop = this.Data?.GetType().GetProperty(displayPropertyPath);
                    string? resolved = prop?.GetValue(this.Data)?.ToString();
                    if (resolved != null)
                    {
                        this.Name = resolved;
                        m_hasDefaultName = false;
                        return;
                    }
                }
                catch (Exception ex) when ((ex is AmbiguousMatchException) || (ex is TargetInvocationException) || (ex is TargetParameterCountException) || (ex is MethodAccessException))
                {
                    // The path names a property that cannot be read on this data (ambiguous, an indexer, inaccessible, or
                    // its getter threw). The item is still named, from the data itself.
                    Logger.LogError($"[ToggleStrip] Could not read '{displayPropertyPath}' from a {this.Data?.GetType().Name} to name its item, so it is named from the data instead", ex);
                }
            }

            this.ResetNameWithDefault();
        }

        internal void SetSelectionWithoutChecking (bool isSelected)
        {
            m_isSelected = isSelected;
            this.RaisePropertyChanged(nameof(IsSelected));
            if (!isSelected)
            {
                // This runs only as the deselect half of a single-select swap (the follow-on select
                // raises SelectionChanged for the whole change), so flag it to suppress a duplicate raise.
                m_list?.HandleWasDeselected(this, isPartOfSelectionSwap: true);
            }
        }

        // ===========[ Helper Methods ]===================================
        private bool SetAndRaiseIfChanged<T> (ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            this.RaisePropertyChanged(propertyName);
            return true;
        }

        private void RaisePropertyChanged (string? propertyName)
            => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void ResetNameWithDefault ()
        {
            // Use Data.ToString() when it is meaningfully overridden (i.e. not
            // just the default type-name). Works naturally for strings, enums,
            // and value types. Falls back to positional label otherwise.
            string? fromData = this.Data?.ToString();
            bool usable = fromData != null && fromData != this.Data!.GetType().FullName;
            this.Name = usable ? fromData : $"Item {m_itemIndex}";
            m_hasDefaultName = !usable;
        }
    }

    /// <summary>What an item reaches in the collection it belongs to, without knowing the collection's item type.</summary>
    internal interface IToggleStripItemList
    {
        bool CanBeDeselected ();

        void HandleWasSelected (ToggleStripItem newlySelected);

        void HandleWasDeselected (ToggleStripItem item, bool isPartOfSelectionSwap);

        int IndexOf (ToggleStripItem item);

        bool IsLast (ToggleStripItem item);
    }
}
