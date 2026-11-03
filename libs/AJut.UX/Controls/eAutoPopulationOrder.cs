namespace AJut.UX.Controls
{
    /// <summary>The order an AutoGrid fills its cells in, child by child</summary>
    public enum eAutoPopulationOrder
    {
        /// <summary>Across a row, one column after another, then on to the next row. The default.</summary>
        ColumnsFirst,

        /// <summary>Down a column, one row after another, then on to the next column</summary>
        RowsFirst,

        /// <summary>
        /// Wraps at whichever count is fixed: rows first when only the row count is fixed, columns first when only the
        /// column count is, and columns first when both or neither are
        /// </summary>
        Auto
    }
}
