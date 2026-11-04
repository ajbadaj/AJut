namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AppKit;

    /// <summary>The model the PathSelectionControl page binds its fixed root sample to</summary>
    public sealed class PathSampleModel : NotifyPropertyChanged
    {
        private string m_documentPath;

        public string DocumentPath
        {
            get => m_documentPath;
            set => this.SetAndRaiseIfChanged(ref m_documentPath, value);
        }
    }

    public sealed class PathSelectionControlPage : ShowroomPage
    {
        private const double kControlWidth = 360.0;

        private readonly PathSampleModel m_model = new PathSampleModel();
        private readonly BindingSet m_bindings = new BindingSet();
        private readonly PathSelectionControl m_openFile;
        private readonly NSTextField m_openFileReadout = ShowroomStyle.Understated(String.Empty);

        public PathSelectionControlPage ()
            : base("PathSelectionControl", "A path field with a browse button that opens the Mac's file picker, and an optional button that shows the path in Finder. The path is checked as it is typed: an invalid one gets a red border and a warning glyph, with the reason as the tooltip. The rules are the ones every stack's PathSelectionControl follows, so the same path gets the same reason on Windows.")
        {
            // 1. Open a file: empty and missing both count as invalid, and Finder can show it once it is there
            m_openFile = MakeControl();
            m_openFile.PathType = ePathType.File;
            m_openFile.FileDialogType = eFileDialogType.OpenFile;
            m_openFile.UnsetTextPrompt = "Pick or type a file";
            m_openFile.TreatEmptyPathAsInvalid = true;
            m_openFile.TreatNonExistentPathAsInvalid = true;
            m_openFile.IsOpenInExplorerButtonAllowed = true;
            m_openFile.PathEvaluated += this.OnOpenFileEvaluated;
            this.AddLabeledDemo("Open a file (empty or missing is invalid)", m_openFile);
            this.AddLabeledDemo("It says", m_openFileReadout);
            this.RefreshOpenFileReadout();

            // 2. A folder: Command-Up goes to the folder above
            PathSelectionControl folder = MakeControl();
            folder.PathType = ePathType.Folder;
            folder.UnsetTextPrompt = "Pick or type a folder";
            folder.TreatNonExistentPathAsInvalid = true;
            folder.IsOpenInExplorerButtonAllowed = true;
            folder.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            this.AddLabeledDemo("A folder (Command-Up goes up)", folder);

            // 3. Save to a text file: the picker only offers .txt, and a typed path must end in it
            PathSelectionControl saveFile = MakeControl();
            saveFile.PathType = ePathType.File;
            saveFile.FileDialogType = eFileDialogType.SaveFile;
            saveFile.FileFilter = "Text files (*.txt)|*.txt";
            saveFile.BrowsePrompt = "Save the notes as";
            saveFile.UnsetTextPrompt = "Somewhere to save notes.txt";
            this.AddLabeledDemo("Save as text (*.txt)", saveFile);

            // 4. Under a fixed root, kept relative to it, and bound to a model
            PathSelectionControl underHome = MakeControl();
            underHome.PathType = ePathType.File;
            underHome.FileFilter = "Documents|*.txt;*.md;*.pdf";
            underHome.FixedRootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            underHome.ShortenPathToFixedRoot = true;
            underHome.TextBoxHeader = "A document in your home folder (.txt, .md or .pdf)";
            m_bindings.BindSelectedPath(underHome, m_model, nameof(PathSampleModel.DocumentPath), this.ReadDocumentPath, this.WriteDocumentPath);
            this.AddLabeledDemo("Fixed root, shortened", underHome);

            NSTextField modelReadout = ShowroomStyle.Understated(String.Empty);
            m_bindings.BindLabel(modelReadout, m_model, nameof(PathSampleModel.DocumentPath), this.ReadDocumentPathText);
            this.AddLabeledDemo("Model says", modelReadout);

            // 5. Disabled
            PathSelectionControl disabled = MakeControl();
            disabled.SelectedPath = "/Applications";
            disabled.PathType = ePathType.Folder;
            disabled.Enabled = false;
            this.AddLabeledDemo("Disabled", disabled);

            this.AddCode(
@"var path = new PathSelectionControl
{
    PathType = ePathType.File,
    FileFilter = ""Documents|*.txt;*.md;*.pdf"",
    FixedRootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ShortenPathToFixedRoot = true,
};
bindings.BindSelectedPath(
    path, model, nameof(Model.DocumentPath),
    () => model.DocumentPath, value => model.DocumentPath = value
);"
            );
        }

        protected override void OnTeardown ()
        {
            m_openFile.PathEvaluated -= this.OnOpenFileEvaluated;
            m_bindings.Dispose();
        }

        private void OnOpenFileEvaluated (object sender, EventArgs e) => this.RefreshOpenFileReadout();

        private void RefreshOpenFileReadout ()
        {
            m_openFileReadout.StringValue = m_openFile.IsPathValid
                ? (m_openFile.DoesPathExist ? "Valid, and there" : "Valid")
                : "Invalid: " + m_openFile.InvalidPathReason;
        }

        private string ReadDocumentPath () => m_model.DocumentPath;
        private void WriteDocumentPath (string value) => m_model.DocumentPath = value;
        private string ReadDocumentPathText () => m_model.DocumentPath ?? "(none)";

        private static PathSelectionControl MakeControl ()
        {
            var control = new PathSelectionControl();
            control.TranslatesAutoresizingMaskIntoConstraints = false;
            control.WidthAnchor.ConstraintEqualTo((nfloat)kControlWidth).Active = true;
            return control;
        }
    }
}
