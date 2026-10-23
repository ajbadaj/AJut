namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Collections.Generic;

    /// <summary>The editors the PropertyGrid supplies itself, used when the consumer has not registered one under that key</summary>
    public enum eBuiltInPropertyEditor
    {
        /// <summary>No built-in editor applies: use the consumer's registered editor for the key, or the default one</summary>
        None,

        /// <summary>The nullable wrapper ("Nullable"): an inner editor for the underlying type, plus set and unset</summary>
        Nullable,

        /// <summary>A button ("Button") that runs the target's ActionCommand</summary>
        Button,

        /// <summary>The list header ("List"): the element count and an add button</summary>
        List,
    }

    // ===========[ PropertyGridRowRules ]=======================================
    // The small decisions every toolkit's PropertyGrid row makes the same way: which editor a
    // target gets, how wide the label column is at a given depth and how the label splitter
    // moves and fits it, which label look a target gets,
    // whether it offers a reset to its default, and where its tooltip shows. Kept here so every stack
    // makes them identically.
    public static class PropertyGridRowRules
    {
        /// <summary>The editor key of a nullable property's wrapper editor</summary>
        public const string kNullableEditorKey = "Nullable";

        /// <summary>The editor key of [PGButton] rows and PropertyEditTarget.CreateButton rows</summary>
        public const string kButtonEditorKey = "Button";

        /// <summary>The editor key of a [PGList] property's header row</summary>
        public const string kListEditorKey = "List";

        /// <summary>The key a target with no editor key is looked up by, which matches no registered editor</summary>
        public const string kInvalidEditorKey = "__Invalid";

        /// <summary>
        /// How far into the label, back from the label and editor boundary, the pointer brings up the label splitter.
        /// There is no zone on the editor side: editors take clicks and labels don't.
        /// </summary>
        public const double kLabelSplitterHitDistance = 10.0;

        // The splitter never makes the label column narrower than this, or leaves the editors less than
        // kMinimumEditorWidth of the grid
        private const double kMinimumLabelColumnWidth = 40.0;
        private const double kMinimumEditorWidth = 60.0;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>The key to look a target's editor up by: its Editor, or <see cref="kInvalidEditorKey"/> when it has none</summary>
        public static string GetEditorKey (PropertyEditTarget target)
        {
            return target.Editor ?? kInvalidEditorKey;
        }

        /// <summary>
        /// Which built-in editor a target gets, if any. A consumer's editor registered under the same key always
        /// wins, so pass whether a key is registered with the toolkit's editor table.
        /// </summary>
        public static eBuiltInPropertyEditor GetBuiltInEditor (PropertyEditTarget target, Func<string, bool> isKeyRegistered)
        {
            switch (target.Editor)
            {
                case kNullableEditorKey when !isKeyRegistered(kNullableEditorKey):
                    return eBuiltInPropertyEditor.Nullable;

                case kButtonEditorKey when !isKeyRegistered(kButtonEditorKey):
                    return eBuiltInPropertyEditor.Button;

                case kListEditorKey when !isKeyRegistered(kListEditorKey):
                    return eBuiltInPropertyEditor.List;

                default:
                    return eBuiltInPropertyEditor.None;
            }
        }

        /// <summary>
        /// The width of a row's label, so every row's editor starts at the same x whatever its depth: the grid's
        /// label column width less the row's indent and its expander column, never below 0. A NaN label column
        /// width means the label sizes to its content, and NaN comes back.
        /// </summary>
        public static double ComputeLabelWidth (double labelColumnWidth, double indentWidth, double expanderColumnWidth)
        {
            if (double.IsNaN(labelColumnWidth))
            {
                return double.NaN;
            }

            double available = labelColumnWidth - indentWidth - expanderColumnWidth;
            return available > 0 ? available : 0;
        }

        /// <summary>
        /// The label column width that gives a row this label width: the reverse of <see cref="ComputeLabelWidth"/>.
        /// Used to start a splitter drag from what a row shows, and to fit the column to a row's label.
        /// </summary>
        public static double ToLabelColumnWidth (double labelWidth, double indentWidth, double expanderColumnWidth)
        {
            return labelWidth + indentWidth + expanderColumnWidth;
        }

        /// <summary>
        /// Whether the pointer is near enough the label and editor boundary to bring up the label splitter: on the label
        /// side, within <see cref="kLabelSplitterHitDistance"/> of it. A splitter's hit area is that span, from
        /// boundaryX - kLabelSplitterHitDistance to boundaryX, with its line along its right edge.
        /// </summary>
        public static bool IsNearLabelBoundary (double pointerX, double boundaryX)
        {
            return (pointerX <= boundaryX)
                && (pointerX >= boundaryX - kLabelSplitterHitDistance);
        }

        /// <summary>
        /// The label column width while the splitter is dragged: the width it started at moved by the pointer, kept
        /// to at least a narrow column and leaving the editors some room. <paramref name="availableWidth"/> is the
        /// width the rows have; NaN or 0 puts no upper limit on it.
        /// </summary>
        public static double ComputeDraggedLabelColumnWidth (double startLabelColumnWidth, double pointerDelta, double availableWidth)
        {
            return ClampLabelColumnWidth(startLabelColumnWidth + pointerDelta, availableWidth);
        }

        /// <summary>
        /// The label column width that fits every given row's label, from each row's <see cref="ToLabelColumnWidth"/>
        /// of the width its label wants, kept within the same limits as a drag. NaN when there are no rows.
        /// </summary>
        public static double FitLabelColumnWidth (IEnumerable<double> rowLabelColumnWidths, double availableWidth)
        {
            double widest = double.NaN;
            foreach (double width in rowLabelColumnWidths)
            {
                if (double.IsNaN(widest) || width > widest)
                {
                    widest = width;
                }
            }

            return double.IsNaN(widest) ? double.NaN : ClampLabelColumnWidth(widest, availableWidth);
        }

        /// <summary>
        /// Whether a row's label shows the default-value look rather than the modified (bold) one. An expandable
        /// row (a sub-object) always shows the default look: its value is an object reference, which is never
        /// meaningfully at or away from a default, so bold there would only mislead. No target counts as default.
        /// </summary>
        public static bool UsesDefaultValueLabel (PropertyEditTarget? target)
        {
            return (target?.IsAtDefaultValue ?? true) || (target?.IsExpandable == true);
        }

        /// <summary>
        /// Whether a row offers "Set to default": it has a default, it is a value rather than an expandable object, and it
        /// can be set (a property shown by [PGShowReadonly] with no setter has nowhere for the reset to go)
        /// </summary>
        public static bool CanResetToDefault (PropertyEditTarget target)
        {
            return target.HasDefaultValue && !target.IsExpandable && !target.IsReadOnly;
        }

        /// <summary>Whether a row offers "Set to default" in a grid that may be read-only: never in a read-only grid, otherwise as <see cref="CanResetToDefault(PropertyEditTarget)"/></summary>
        public static bool CanResetToDefault (PropertyEditTarget target, bool isGridReadOnly)
        {
            return !isGridReadOnly && CanResetToDefault(target);
        }

        /// <summary>
        /// Whether a value editor takes edits: not in a read-only grid, and not for a property with no setter (shown by
        /// [PGShowReadonly]). Only for value editors: a button row or a list header has no setter of its own, so it
        /// follows the grid alone, and a read-only grid disables those as well.
        /// </summary>
        public static bool CanEdit (PropertyEditTarget target, bool isGridReadOnly)
        {
            return !isGridReadOnly && !target.IsReadOnly;
        }

        /// <summary>Whether a list element row shows its remove button: its list allows removing, and the grid isn't read-only</summary>
        public static bool CanRemoveFromList (PropertyEditTarget target, bool isGridReadOnly)
        {
            return !isGridReadOnly && target.CanRemoveFromList;
        }

        /// <summary>Whether a row's tooltip shows on its property name under this placement</summary>
        public static bool ShowsToolTipOnName (ePropertyToolTipPlacement placement)
        {
            return placement == ePropertyToolTipPlacement.PropertyNameOnly
                || placement == ePropertyToolTipPlacement.PropertyNameAndValue;
        }

        /// <summary>Whether a row's tooltip shows on its value editor under this placement</summary>
        public static bool ShowsToolTipOnValue (ePropertyToolTipPlacement placement)
        {
            return placement == ePropertyToolTipPlacement.ValueOnly
                || placement == ePropertyToolTipPlacement.PropertyNameAndValue;
        }

        // ===========[ Helper Methods ]========================================
        // The editors' room wins over the minimum when the grid is too narrow for both
        private static double ClampLabelColumnWidth (double width, double availableWidth)
        {
            double clamped = Math.Max(width, kMinimumLabelColumnWidth);
            if (!double.IsNaN(availableWidth) && availableWidth > 0)
            {
                clamped = Math.Min(clamped, availableWidth - kMinimumEditorWidth);
            }

            return Math.Max(clamped, 0.0);
        }
    }
}
