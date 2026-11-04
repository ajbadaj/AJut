namespace AJut.UX.Controls
{
    /// <summary>Which picker a PathSelectionControl opens for a file path</summary>
    public enum eFileDialogType
    {
        /// <summary>Picks a file that is already there, to open</summary>
        OpenFile,

        /// <summary>Picks where a file goes, to save it, whether or not one is there yet</summary>
        SaveFile,
    }
}
