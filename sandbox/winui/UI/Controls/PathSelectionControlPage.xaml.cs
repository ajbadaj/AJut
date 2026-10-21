namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The PathSelectionControl demos: an open-file picker, a folder picker, and a save-file picker with a filter.
    /// </summary>
    public sealed partial class PathSelectionControlPage : Page
    {
        public PathSelectionControlPage ()
        {
            this.InitializeComponent();
        }

        public string DemoCode { get; } = @"<ajut:PathSelectionControl PathType=""File""
                           TextBoxHeight=""22""
                           UnsetTextPrompt=""Select a file...""
                           TreatEmptyPathAsInvalid=""True""
                           TreatNonExistentPathAsInvalid=""True""/>

<ajut:PathSelectionControl PathType=""Folder""
                           UnsetTextPrompt=""Select a folder...""
                           IsOpenInExplorerButtonAllowed=""True""
                           TreatNonExistentPathAsInvalid=""True""
                           TextBoxPadding=""0""/>

<ajut:PathSelectionControl PathType=""File""
                           FileDialogType=""SaveFile""
                           FileFilter=""Text files (*.txt)|*.txt""
                           UnsetTextPrompt=""Save path...""/>";
    }
}
