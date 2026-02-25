namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using Windows.Storage.Pickers;
    using DPUtils = AJut.UX.DPUtils<PathSelectionControl>;

    // ===========[ PathSelectionControl ]==============================================
    // WinUI3 path selection control. Shows a TextBox for path entry and a browse
    // button that opens a file or folder picker. Optional "show in explorer" button.
    //
    // Visual feedback: the outer border turns red when the path is invalid and
    // accent-colored when the control is focused (using VSM BorderStates group).
    //
    // Template parts:
    //   PART_Root              - outer Border (receives border-brush VSM changes)
    //   PART_PathTextBox       - inner TextBox (path entry + Ctrl+Up in Folder mode)
    //   PART_BrowseButton      - Button that opens the OS picker
    //   PART_ShowInExplorerButton - Button that opens Explorer at the path (optional)

    public enum ePathType { File, Folder }
    public enum eFileDialogType { OpenFile, SaveFile }

    [TemplatePart(Name = nameof(PART_Root),                 Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_PathTextBox),          Type = typeof(TextBox))]
    [TemplatePart(Name = nameof(PART_BrowseButton),         Type = typeof(ButtonBase))]
    [TemplatePart(Name = nameof(PART_ShowInExplorerButton), Type = typeof(ButtonBase))]
    public class PathSelectionControl : Control
    {
        // ===========[ Instance fields ]==========================================
        private Border PART_Root;
        private TextBox PART_PathTextBox;
        private ButtonBase PART_BrowseButton;
        private ButtonBase PART_ShowInExplorerButton;

        private bool m_blockPathSync;
        private bool m_isFocused;

        // ===========[ Construction ]=============================================
        public PathSelectionControl ()
        {
            this.DefaultStyleKey = typeof(PathSelectionControl);
            this.GotFocus  += this.OnGotFocus;
            this.LostFocus += this.OnLostFocus;
            this.EvaluatePath(null);
        }

        // ===========[ Dependency Properties ]====================================

        public static readonly DependencyProperty PathTypeProperty = DPUtils.Register(
            _ => _.PathType,
            (d, e) => d.EvaluatePath(d.SelectedPath));
        public ePathType PathType
        {
            get => (ePathType)this.GetValue(PathTypeProperty);
            set => this.SetValue(PathTypeProperty, value);
        }

        public static readonly DependencyProperty FileFilterProperty = DPUtils.Register(
            _ => _.FileFilter, "*.*",
            (d, e) => d.EvaluatePath(d.SelectedPath));
        public string FileFilter
        {
            get => (string)this.GetValue(FileFilterProperty);
            set => this.SetValue(FileFilterProperty, value);
        }

        public static readonly DependencyProperty FileDialogTypeProperty = DPUtils.Register(
            _ => _.FileDialogType, eFileDialogType.OpenFile);
        public eFileDialogType FileDialogType
        {
            get => (eFileDialogType)this.GetValue(FileDialogTypeProperty);
            set => this.SetValue(FileDialogTypeProperty, value);
        }

        public static readonly DependencyProperty SelectedPathProperty = DPUtils.Register(
            _ => _.SelectedPath,
            (d, e) => d.OnSelectedPathChanged(e.NewValue));
        public string SelectedPath
        {
            get => (string)this.GetValue(SelectedPathProperty);
            set => this.SetValue(SelectedPathProperty, value);
        }

        public static readonly DependencyProperty IsPathValidProperty = DPUtils.Register(
            _ => _.IsPathValid, false);
        public bool IsPathValid
        {
            get => (bool)this.GetValue(IsPathValidProperty);
            private set => this.SetValue(IsPathValidProperty, value);
        }

        public static readonly DependencyProperty DoesPathExistProperty = DPUtils.Register(
            _ => _.DoesPathExist, false);
        public bool DoesPathExist
        {
            get => (bool)this.GetValue(DoesPathExistProperty);
            private set => this.SetValue(DoesPathExistProperty, value);
        }

        public static readonly DependencyProperty InvalidPathReasonProperty = DPUtils.Register(
            _ => _.InvalidPathReason);
        public string InvalidPathReason
        {
            get => (string)this.GetValue(InvalidPathReasonProperty);
            private set => this.SetValue(InvalidPathReasonProperty, value);
        }

        public static readonly DependencyProperty TreatEmptyPathAsInvalidProperty = DPUtils.Register(
            _ => _.TreatEmptyPathAsInvalid,
            (d, e) => d.EvaluatePath(d.SelectedPath));
        public bool TreatEmptyPathAsInvalid
        {
            get => (bool)this.GetValue(TreatEmptyPathAsInvalidProperty);
            set => this.SetValue(TreatEmptyPathAsInvalidProperty, value);
        }

        public static readonly DependencyProperty TreatNonExistentPathAsInvalidProperty = DPUtils.Register(
            _ => _.TreatNonExistentPathAsInvalid,
            (d, e) => d.EvaluatePath(d.SelectedPath));
        public bool TreatNonExistentPathAsInvalid
        {
            get => (bool)this.GetValue(TreatNonExistentPathAsInvalidProperty);
            set => this.SetValue(TreatNonExistentPathAsInvalidProperty, value);
        }

        public static readonly DependencyProperty FixedRootPathProperty = DPUtils.Register(
            _ => _.FixedRootPath,
            (d, e) => d.EvaluatePath(d.SelectedPath));
        /// <summary>A directory that the SelectedPath must be located under.</summary>
        public string FixedRootPath
        {
            get => (string)this.GetValue(FixedRootPathProperty);
            set => this.SetValue(FixedRootPathProperty, value);
        }

        public static readonly DependencyProperty InitialBrowseRootProperty = DPUtils.Register(
            _ => _.InitialBrowseRoot);
        /// <summary>Starting directory for the picker when no valid path is selected.</summary>
        public string InitialBrowseRoot
        {
            get => (string)this.GetValue(InitialBrowseRootProperty);
            set => this.SetValue(InitialBrowseRootProperty, value);
        }

        public static readonly DependencyProperty ShortenPathToFixedRootProperty = DPUtils.Register(
            _ => _.ShortenPathToFixedRoot,
            (d, e) => d.EvaluatePath(d.SelectedPath));
        /// <summary>When true, SelectedPath is stored relative to FixedRootPath.</summary>
        public bool ShortenPathToFixedRoot
        {
            get => (bool)this.GetValue(ShortenPathToFixedRootProperty);
            set => this.SetValue(ShortenPathToFixedRootProperty, value);
        }

        public static readonly DependencyProperty BrowsePromptProperty = DPUtils.Register(
            _ => _.BrowsePrompt, "Select");
        public string BrowsePrompt
        {
            get => (string)this.GetValue(BrowsePromptProperty);
            set => this.SetValue(BrowsePromptProperty, value);
        }

        public static readonly DependencyProperty UnsetTextPromptProperty = DPUtils.Register(
            _ => _.UnsetTextPrompt);
        public string UnsetTextPrompt
        {
            get => (string)this.GetValue(UnsetTextPromptProperty);
            set => this.SetValue(UnsetTextPromptProperty, value);
        }

        public static readonly DependencyProperty IsOpenInExplorerButtonAllowedProperty = DPUtils.Register(
            _ => _.IsOpenInExplorerButtonAllowed,
            (d, e) => d.UpdateVisualState());
        public bool IsOpenInExplorerButtonAllowed
        {
            get => (bool)this.GetValue(IsOpenInExplorerButtonAllowedProperty);
            set => this.SetValue(IsOpenInExplorerButtonAllowedProperty, value);
        }

        public static readonly DependencyProperty DefaultButtonMDL2IconProperty = DPUtils.Register(
            _ => _.DefaultButtonMDL2Icon, "\xE712");
        public string DefaultButtonMDL2Icon
        {
            get => (string)this.GetValue(DefaultButtonMDL2IconProperty);
            set => this.SetValue(DefaultButtonMDL2IconProperty, value);
        }

        public static readonly DependencyProperty OpenInExplorerMDL2IconProperty = DPUtils.Register(
            _ => _.OpenInExplorerMDL2Icon, "\xE8A7");
        public string OpenInExplorerMDL2Icon
        {
            get => (string)this.GetValue(OpenInExplorerMDL2IconProperty);
            set => this.SetValue(OpenInExplorerMDL2IconProperty, value);
        }

        // ===========[ Template application ]=====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // Unhook previous template children
            if (this.PART_PathTextBox != null)
            {
                this.PART_PathTextBox.KeyDown      -= this.PathTextBox_OnKeyDown;
                this.PART_PathTextBox.LostFocus    -= this.PathTextBox_OnLostFocus;
                this.PART_PathTextBox.TextChanged  -= this.PathTextBox_OnTextChanged;
            }

            if (this.PART_BrowseButton != null)
            {
                this.PART_BrowseButton.Click -= this.BrowseButton_OnClick;
            }

            if (this.PART_ShowInExplorerButton != null)
            {
                this.PART_ShowInExplorerButton.Click -= this.ShowInExplorerButton_OnClick;
            }

            this.PART_Root                 = (Border)this.GetTemplateChild(nameof(PART_Root));
            this.PART_PathTextBox          = (TextBox)this.GetTemplateChild(nameof(PART_PathTextBox));
            this.PART_BrowseButton         = (ButtonBase)this.GetTemplateChild(nameof(PART_BrowseButton));
            this.PART_ShowInExplorerButton = (ButtonBase)this.GetTemplateChild(nameof(PART_ShowInExplorerButton));

            if (this.PART_PathTextBox != null)
            {
                // Sync current SelectedPath value → TextBox (handles template-applied-after-value case)
                m_blockPathSync = true;
                try
                {
                    this.PART_PathTextBox.Text = this.SelectedPath ?? string.Empty;
                }
                finally
                {
                    m_blockPathSync = false;
                }

                this.PART_PathTextBox.KeyDown     += this.PathTextBox_OnKeyDown;
                this.PART_PathTextBox.LostFocus   += this.PathTextBox_OnLostFocus;
                this.PART_PathTextBox.TextChanged += this.PathTextBox_OnTextChanged;
            }

            if (this.PART_BrowseButton != null)
            {
                this.PART_BrowseButton.Click += this.BrowseButton_OnClick;
            }

            if (this.PART_ShowInExplorerButton != null)
            {
                this.PART_ShowInExplorerButton.Click += this.ShowInExplorerButton_OnClick;
            }

            this.UpdateVisualState();
        }

        // ===========[ Public helpers ]============================================
        public string GetFullyExpandedPath (string path = null)
        {
            path = path ?? this.SelectedPath ?? string.Empty;
            if (this.FixedRootPath?.Length == 0 || Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(this.FixedRootPath, path);
        }

        // ===========[ Property change handlers ]================================
        private void OnSelectedPathChanged (string newPath)
        {
            if (m_blockPathSync)
            {
                return;
            }

            if (this.PART_PathTextBox != null)
            {
                m_blockPathSync = true;
                try
                {
                    this.PART_PathTextBox.Text = newPath ?? string.Empty;
                    // Scroll to end so the file/folder name (not the drive letter) is visible
                    this.DispatcherQueue?.TryEnqueue(
                        Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                        () => this.PART_PathTextBox.Select(this.PART_PathTextBox.Text.Length, 0));
                }
                finally
                {
                    m_blockPathSync = false;
                }
            }

            this.EvaluatePath(newPath);
        }

        // ===========[ TextBox event handlers ]===================================
        private void PathTextBox_OnTextChanged (object sender, TextChangedEventArgs e)
        {
            if (m_blockPathSync)
            {
                return;
            }

            string text = this.PART_PathTextBox.Text;
            m_blockPathSync = true;
            try
            {
                this.SelectedPath = text;
            }
            finally
            {
                m_blockPathSync = false;
            }

            // EvaluatePath is triggered by OnSelectedPathChanged
        }

        private void PathTextBox_OnKeyDown (object sender, KeyRoutedEventArgs e)
        {
            // Ctrl+Up: navigate to parent directory (Folder mode only)
            if (this.PathType != ePathType.Folder || e.Key != Windows.System.VirtualKey.Up)
            {
                return;
            }

            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
                Windows.System.VirtualKey.Control);
            bool isCtrlDown = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            if (!isCtrlDown)
            {
                return;
            }

            try
            {
                var dir = new DirectoryInfo(this.SelectedPath ?? "");
                if (dir.Parent != null)
                {
                    this.SelectedPath = dir.Parent.FullName;
                    e.Handled = true;
                }
            }
            catch { }
        }

        private void PathTextBox_OnLostFocus (object sender, RoutedEventArgs e)
        {
            string path = this.SelectedPath;
            if (this.TryToApplyPathShorteningIfAny(ref path))
            {
                this.SelectedPath = path;
            }
        }

        // ===========[ Button event handlers ]====================================
        private async void BrowseButton_OnClick (object sender, RoutedEventArgs e)
        {
            string userSelectedPath = null;
            var hwnd = this.GetWindowHandle();

            try
            {
                if (this.PathType == ePathType.File)
                {
                    userSelectedPath = await this.PickFileAsync(hwnd);
                }
                else
                {
                    userSelectedPath = await this.PickFolderAsync(hwnd);
                }
            }
            catch { }

            if (userSelectedPath != null)
            {
                this.TryToApplyPathShorteningIfAny(ref userSelectedPath);
                this.SelectedPath = userSelectedPath;
            }
        }

        private void ShowInExplorerButton_OnClick (object sender, RoutedEventArgs e)
        {
            if (!this.IsPathValid)
            {
                return;
            }

            string path = this.GetFullyExpandedPath();
            if (this.PathType == ePathType.File)
            {
                System.Diagnostics.Process.Start("explorer", $"/select,\"{path}\"");
            }
            else
            {
                System.Diagnostics.Process.Start("explorer", $"/n,{path}");
            }
        }

        // ===========[ Focus state handlers ]=====================================
        private void OnGotFocus  (object sender, RoutedEventArgs e) { m_isFocused = true;  this.UpdateVisualState(); }
        private void OnLostFocus (object sender, RoutedEventArgs e) { m_isFocused = false; this.UpdateVisualState(); }

        // ===========[ Path evaluation ]==========================================
        private void EvaluatePath (string path)
        {
            // Empty path handling
            if (string.IsNullOrEmpty(path))
            {
                if (this.TreatEmptyPathAsInvalid)
                {
                    this.SetInvalid("No path specified");
                }
                else
                {
                    this.IsPathValid = true;
                    this.InvalidPathReason = null;
                    this.DoesPathExist = false;
                    this.UpdateVisualState();
                }

                return;
            }

            // Basic path syntax check
            string expandedPath;
            try
            {
                expandedPath = this.GetFullyExpandedPath(path);
                Path.GetFullPath(expandedPath); // throws on invalid chars
            }
            catch (Exception ex)
            {
                this.SetInvalid($"Invalid path: {ex.Message}");
                return;
            }

            // FixedRootPath constraint
            if (this.FixedRootPath?.Length > 0
                && Path.IsPathRooted(path)
                && !expandedPath.StartsWith(this.FixedRootPath, StringComparison.OrdinalIgnoreCase))
            {
                this.SetInvalid($"Path must be under: {this.FixedRootPath}");
                return;
            }

            // File extension filter check (File mode only)
            if (this.PathType == ePathType.File
                && this.FileFilter?.Length > 0
                && this.FileFilter != "*.*"
                && this.FileFilter != "*"
                && !DoesFileMatchFilter(path, this.FileFilter))
            {
                this.SetInvalid("File extension is not in the allowed filter");
                return;
            }

            // Existence checks
            bool existsAsFile = false;
            bool existsAsDir  = false;
            try { existsAsFile = File.Exists(expandedPath); }      catch { }
            try { existsAsDir  = Directory.Exists(expandedPath); } catch { }

            if (this.PathType == ePathType.File && existsAsDir)
            {
                this.SetInvalid("Expected a file but path points to a directory");
                return;
            }

            if (this.PathType == ePathType.Folder && existsAsFile)
            {
                this.SetInvalid("Expected a directory but path points to a file");
                return;
            }

            this.DoesPathExist = this.PathType == ePathType.File ? existsAsFile : existsAsDir;

            if (!this.DoesPathExist && this.TreatNonExistentPathAsInvalid)
            {
                this.IsPathValid = false;
                this.InvalidPathReason = $"{this.PathType} does not exist";
            }
            else
            {
                this.IsPathValid = true;
                this.InvalidPathReason = null;
            }

            this.UpdateVisualState();
        }

        private void SetInvalid (string reason)
        {
            this.DoesPathExist = false;
            this.IsPathValid = false;
            this.InvalidPathReason = reason;
            this.UpdateVisualState();
        }

        // ===========[ Visual state management ]===================================
        private void UpdateVisualState ()
        {
            // Invalid path takes priority over focused border
            string borderState = !this.IsPathValid && !string.IsNullOrEmpty(this.SelectedPath)
                ? "PathInvalid"
                : m_isFocused ? "Focused" : "Normal";
            VisualStateManager.GoToState(this, borderState, false);

            // Warning icon
            VisualStateManager.GoToState(this,
                !this.IsPathValid && !string.IsNullOrEmpty(this.SelectedPath)
                    ? "WarningShown" : "WarningHidden",
                false);

            // Explorer button - visible only when allowed and path is valid
            VisualStateManager.GoToState(this,
                this.IsOpenInExplorerButtonAllowed && this.IsPathValid
                    ? "ExplorerButtonShown" : "ExplorerButtonHidden",
                false);
        }

        // ===========[ OS picker helpers ]=========================================
        private async System.Threading.Tasks.Task<string> PickFileAsync (IntPtr hwnd)
        {
            if (this.FileDialogType == eFileDialogType.OpenFile)
            {
                var picker = new FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                picker.SuggestedStartLocation = PickerLocationId.Desktop;
                ApplyFileTypeFilterToList(picker.FileTypeFilter, this.FileFilter);
                var file = await picker.PickSingleFileAsync();
                return file?.Path;
            }
            else
            {
                var picker = new FileSavePicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                picker.SuggestedStartLocation = PickerLocationId.Desktop;
                ApplyFileTypeFilterToDictionary(picker.FileTypeChoices, this.FileFilter);
                var file = await picker.PickSaveFileAsync();
                return file?.Path;
            }
        }

        private async System.Threading.Tasks.Task<string> PickFolderAsync (IntPtr hwnd)
        {
            var picker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }

        private IntPtr GetWindowHandle ()
        {
            try
            {
                Microsoft.UI.WindowId? appWindowId = this.XamlRoot?.ContentIslandEnvironment?.AppWindowId;
                if (appWindowId is not null)
                {
                    return Microsoft.UI.Win32Interop.GetWindowFromWindowId(appWindowId.Value);
                }
            }
            catch { }

            return IntPtr.Zero;
        }

        // ===========[ Path helpers ]=============================================
        private bool TryToApplyPathShorteningIfAny (ref string target)
        {
            if (this.FixedRootPath?.Length > 0
                && this.ShortenPathToFixedRoot
                && target != null
                && target.StartsWith(this.FixedRootPath, StringComparison.OrdinalIgnoreCase))
            {
                target = target.Substring(this.FixedRootPath.Length).TrimStart('\\', '/');
                return true;
            }

            return false;
        }

        private static bool DoesFileMatchFilter (string path, string filter)
        {
            string ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";

            // Support both "*.ext" and "|"-separated WPF-style filters
            foreach (string part in filter.Split('|'))
            {
                string p = part.Trim().ToLowerInvariant();
                if (p == "*.*" || p == "*") return true;
                // WPF pattern segment like "*.cs;*.txt"
                foreach (string pattern in p.Split(';'))
                {
                    string trimmed = pattern.Trim();
                    if (trimmed == "*.*" || trimmed == "*") return true;
                    if (trimmed.StartsWith("*.") && trimmed.Substring(1) == ext) return true;
                }
            }

            return false;
        }

        private static void ApplyFileTypeFilterToList (
            IList<string> target,
            string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter == "*.*" || filter == "*")
            {
                target.Add("*");
                return;
            }

            string[] parts = filter.Split('|');
            for (int i = 1; i < parts.Length; i += 2)
            {
                foreach (string pattern in parts[i].Split(';'))
                {
                    string ext = pattern.Trim();
                    if (ext == "*.*" || ext == "*")
                    {
                        target.Add("*");
                    }
                    else if (ext.StartsWith("*."))
                    {
                        target.Add(ext.Substring(1));
                    }
                }
            }

            if (target.Count == 0)
            {
                target.Add("*");
            }
        }

        private static void ApplyFileTypeFilterToDictionary (
            IDictionary<string, IList<string>> target,
            string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter == "*.*" || filter == "*")
            {
                target["All files"] = new List<string> { "*" };
                return;
            }

            string[] parts = filter.Split('|');
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                string desc = parts[i].Trim();
                var exts = new List<string>();
                foreach (string pattern in parts[i + 1].Split(';'))
                {
                    string ext = pattern.Trim();
                    if (ext == "*.*" || ext == "*")
                    {
                        exts.Add("*");
                    }
                    else if (ext.StartsWith("*."))
                    {
                        exts.Add(ext.Substring(1));
                    }
                }

                if (exts.Count > 0)
                {
                    target[desc] = exts;
                }
            }

            if (target.Count == 0)
            {
                target["All files"] = new List<string> { "*" };
            }
        }
    }
}
