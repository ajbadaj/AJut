namespace AJut.UX.Controls
{
    using System;
    using System.Collections;

    /// <summary>
    /// What a toggle strip's item model (<see cref="ToggleStripItem"/> and <see cref="ToggleStripItemCollection{TItem}"/>)
    /// needs from the strip that shows it. Each UI toolkit's ToggleStrip implements this. The selection rules live in
    /// the collection, once, so a strip only draws the items, mirrors each selection change into its own state, and
    /// raises its own event.
    /// </summary>
    public interface IToggleStripHost
    {
        /// <summary>Whether more than one item can be selected at once.</summary>
        bool AllowMultiSelect { get; }

        /// <summary>Whether the last selected item can be deselected, leaving nothing selected.</summary>
        bool AllowNoSelection { get; }

        /// <summary>The property of each item's data to show as its name; empty to name items by their data's ToString.</summary>
        string? DisplayPropertyPath { get; }

        /// <summary>
        /// Names an item from its data, and wins over <see cref="DisplayPropertyPath"/> when set. Unlike the path it needs
        /// no reflection, so it keeps working in a trimmed or AOT build. Null to use the path.
        /// </summary>
        Func<object, string?>? ItemNameSelector { get; }

        /// <summary>
        /// Called once for each change to the selection, after the collection has applied it (a single-select swap is one
        /// change, not a deselect and a select). The strip stores the change in its own selection state and raises its
        /// selection changed event.
        /// </summary>
        void OnSelectionChanged (ToggleStripSelectionChange change);
    }

    /// <summary>One change to a toggle strip's selection, worked out by its <see cref="ToggleStripItemCollection{TItem}"/>.</summary>
    /// <param name="SelectedItem">The data of the most recently selected item that is still selected, or null for none.</param>
    /// <param name="CurrentSelection">The data of every selected item, in strip order, after the change.</param>
    /// <param name="PreviousSelection">The data of every item that was selected before the change, in strip order; a copy, which nothing changes afterward.</param>
    /// <param name="Selected">The item whose selection caused the change, or null when the change was a deselect.</param>
    public sealed record ToggleStripSelectionChange (object? SelectedItem, IList CurrentSelection, IList PreviousSelection, ToggleStripItem? Selected);
}
