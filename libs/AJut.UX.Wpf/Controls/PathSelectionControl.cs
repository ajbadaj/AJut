// ePathType and eFileDialogType moved to AJut.UX, in the same namespace, so every stack's PathSelectionControl and the
// rules they share use one of each. These forward anything built against them here to where they are now.
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(AJut.UX.Controls.ePathType))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(AJut.UX.Controls.eFileDialogType))]

namespace AJut.UX.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.IO;
    using Microsoft.Win32;
    using DPUtils = AJut.UX.DPUtils<PathSelectionControl>;

    [TemplatePart(Name = nameof(PART_PathTextBox), Type = typeof(TextBox))]
    public class PathSelectionControl : Control
    {
        private TextBox PART_PathTextBox;
        static PathSelectionControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PathSelectionControl), new FrameworkPropertyMetadata(typeof(PathSelectionControl)));
        }
        public PathSelectionControl ()
        {
            this.EvaluatePath(null);

            this.CommandBindings.Add(new CommandBinding(PromptUserForNewPathCommand, _OnPromptUserForNewPathExecuted, _OnCanPromptUserForNewPath));
            this.CommandBindings.Add(new CommandBinding(ShowPathInExplorerCommand, _OnShowPathInExplorerExecuted, _OnCanShowPathInExplorer));

            void _OnCanPromptUserForNewPath (object _sender, CanExecuteRoutedEventArgs _e)
            {
                _e.CanExecute = !this.IsReadOnly;
            }

            void _OnPromptUserForNewPathExecuted (object _sender, ExecutedRoutedEventArgs _e)
            {
                string _root = PathSelectionRules.FindBrowseStartFolder(this.SelectedPath, this.PathType, this.InitialBrowseRoot, this.FixedRootPath);
                if (this.PathType == ePathType.File)
                {
                    string userSelectedPath =
                        this.FileDialogType == eFileDialogType.OpenFile
                            ? PathHelpersUI.PromptUserToPickAFile<OpenFileDialog>(this.BrowsePrompt, _root, this.FileFilter)
                            : PathHelpersUI.PromptUserToPickAFile<SaveFileDialog>(this.BrowsePrompt, _root, this.FileFilter);

                    _SetText(userSelectedPath);
                }
                else
                {
                    string userSelectedPath = PathHelpersUI.PromptUserToPickADirectory(this.BrowsePrompt, _root);
                    _SetText(userSelectedPath);
                }
            }

            void _OnCanShowPathInExplorer (object _sender, CanExecuteRoutedEventArgs _e)
            {
                _e.CanExecute = this.IsPathValid;
            }

            void _OnShowPathInExplorerExecuted (object _sender, ExecutedRoutedEventArgs _e)
            {
                if (this.PathType == ePathType.File)
                {
                    System.Diagnostics.Process.Start("explorer", $"/select,\"{this.GetFullyExpandedPath()}\"");
                }
                else
                {
                    System.Diagnostics.Process.Start("explorer", $"/n,{this.GetFullyExpandedPath()}");
                }
            }

            void _SetText (string _userSelectedPath)
            {
                if (_userSelectedPath != null)
                {
                    this.TryToApplyPathShorteningIfAny(ref _userSelectedPath);
                    this.SelectedPath = _userSelectedPath;
                    this.Dispatcher.InvokeAsync(() =>
                    {
                        // For some reason it's ScrollToEnd and ScrollToHorizontalOffset of the normal viewport width fail in some circumstances :facpalm:
                        this.PART_PathTextBox.ScrollToHorizontalOffset(this.PART_PathTextBox.ViewportWidth * 1.5f);
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // Named handlers rather than local functions, so a template applied again takes the old text box's handlers off it
            if (this.PART_PathTextBox != null)
            {
                this.PART_PathTextBox.PreviewKeyDown -= this.PathTextBox_OnPreviewKeyDown;
                this.PART_PathTextBox.LostFocus -= this.PathTextBox_OnLostFocus;
                this.PART_PathTextBox = null;
            }

            this.PART_PathTextBox = (TextBox)this.GetTemplateChild(nameof(PART_PathTextBox));
            this.PART_PathTextBox.PreviewKeyDown += this.PathTextBox_OnPreviewKeyDown;
            this.PART_PathTextBox.LostFocus += this.PathTextBox_OnLostFocus;
        }

        private void PathTextBox_OnPreviewKeyDown (object sender, KeyEventArgs e)
        {
            // Ctrl+Up steps out to the folder above
            if ((e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                && (e.Key == Key.Up)
                && this.TryMoveToParentFolder())
            {
                e.Handled = true;
            }
        }

        private void PathTextBox_OnLostFocus (object sender, RoutedEventArgs e)
        {
            string path = this.SelectedPath;
            if (this.TryToApplyPathShorteningIfAny(ref path))
            {
                this.SelectedPath = path;
            }
        }

        public static RoutedCommand PromptUserForNewPathCommand = new RoutedCommand(nameof(PromptUserForNewPathCommand), typeof(PathSelectionControl));
        public static RoutedCommand ShowPathInExplorerCommand = new RoutedCommand(nameof(ShowPathInExplorerCommand), typeof(PathSelectionControl));

        public static readonly DependencyProperty PathTypeProperty = DPUtils.Register(_ => _.PathType, (d, e) => d.EvaluatePath(d.SelectedPath));
        public ePathType PathType
        {
            get => (ePathType)this.GetValue(PathTypeProperty);
            set => this.SetValue(PathTypeProperty, value);
        }

        public static readonly DependencyProperty FileFilterProperty = DPUtils.Register(_ => _.FileFilter, PathHelpers.kAnyFileFilter, (d, e) => d.EvaluatePath(d.SelectedPath));
        public string FileFilter
        {
            get => (string)this.GetValue(FileFilterProperty);
            set => this.SetValue(FileFilterProperty, value);
        }

        public static readonly DependencyProperty UnsetTextPromptProperty = DPUtils.Register(_ => _.UnsetTextPrompt);
        public string UnsetTextPrompt
        {
            get => (string)this.GetValue(UnsetTextPromptProperty);
            set => this.SetValue(UnsetTextPromptProperty, value);
        }

        public static readonly DependencyProperty BrowsePromptProperty = DPUtils.Register(_ => _.BrowsePrompt, "Select");
        public string BrowsePrompt
        {
            get => (string)this.GetValue(BrowsePromptProperty);
            set => this.SetValue(BrowsePromptProperty, value);
        }

        public static readonly DependencyProperty SelectedPathProperty = DPUtils.Register(_ => _.SelectedPath, (d, e) => d.EvaluatePath(e.NewValue));
        public string SelectedPath
        {
            get => (string)this.GetValue(SelectedPathProperty);
            set => this.SetValue(SelectedPathProperty, value);
        }

        private static readonly DependencyPropertyKey IsPathValidPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsPathValid);
        public static readonly DependencyProperty IsPathValidProperty = IsPathValidPropertyKey.DependencyProperty;
        public bool IsPathValid
        {
            get => (bool)this.GetValue(IsPathValidProperty);
            protected set => this.SetValue(IsPathValidPropertyKey, value);
        }

        public static readonly DependencyProperty TreatEmptyPathAsInvalidProperty = DPUtils.Register(_ => _.TreatEmptyPathAsInvalid, (d, e) => d.EvaluatePath(d.SelectedPath));
        public bool TreatEmptyPathAsInvalid
        {
            get => (bool)this.GetValue(TreatEmptyPathAsInvalidProperty);
            set => this.SetValue(TreatEmptyPathAsInvalidProperty, value);
        }

        public static readonly DependencyProperty TreatNonExistentPathAsInvalidProperty = DPUtils.Register(_ => _.TreatNonExistentPathAsInvalid, (d, e) => d.EvaluatePath(d.SelectedPath));
        public bool TreatNonExistentPathAsInvalid
        {
            get => (bool)this.GetValue(TreatNonExistentPathAsInvalidProperty);
            set => this.SetValue(TreatNonExistentPathAsInvalidProperty, value);
        }

        private static readonly DependencyPropertyKey DoesPathExistPropertyKey = DPUtils.RegisterReadOnly(_ => _.DoesPathExist);
        public static readonly DependencyProperty DoesPathExistProperty = DoesPathExistPropertyKey.DependencyProperty;
        public bool DoesPathExist
        {
            get => (bool)this.GetValue(DoesPathExistProperty);
            protected set => this.SetValue(DoesPathExistPropertyKey, value);
        }

        private static readonly DependencyPropertyKey InvalidPathReasonPropertyKey = DPUtils.RegisterReadOnly(_ => _.InvalidPathReason);
        public static readonly DependencyProperty InvalidPathReasonProperty = InvalidPathReasonPropertyKey.DependencyProperty;
        public string InvalidPathReason
        {
            get => (string)this.GetValue(InvalidPathReasonProperty);
            protected set => this.SetValue(InvalidPathReasonPropertyKey, value);
        }

        public static readonly DependencyProperty FocusedBorderBrushProperty = DPUtils.RegisterFP(_ => _.FocusedBorderBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush FocusedBorderBrush
        {
            get => (Brush)this.GetValue(FocusedBorderBrushProperty);
            set => this.SetValue(FocusedBorderBrushProperty, value);
        }

        public static readonly DependencyProperty InvalidPathBorderBrushProperty = DPUtils.RegisterFP(_ => _.InvalidPathBorderBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush InvalidPathBorderBrush
        {
            get => (Brush)this.GetValue(InvalidPathBorderBrushProperty);
            set => this.SetValue(InvalidPathBorderBrushProperty, value);
        }

        /// <summary>
        /// The glow drawn around the control while its path is invalid. Its own color rather than the invalid border's,
        /// since a border color dark enough to read on a dark surface is too dark to glow on it.
        /// </summary>
        public static readonly DependencyProperty InvalidPathGlowBrushProperty = DPUtils.RegisterFP(_ => _.InvalidPathGlowBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush InvalidPathGlowBrush
        {
            get => (Brush)this.GetValue(InvalidPathGlowBrushProperty);
            set => this.SetValue(InvalidPathGlowBrushProperty, value);
        }

        public static readonly DependencyProperty InvalidForegroundSymbolProperty = DPUtils.RegisterFP(_ => _.InvalidForegroundSymbol, null, null, CoerceUtils.CallbackForBrush);
        public Brush InvalidForegroundSymbol
        {
            get => (Brush)this.GetValue(InvalidForegroundSymbolProperty);
            set => this.SetValue(InvalidForegroundSymbolProperty, value);
        }


        public static readonly DependencyProperty ButtonBackgroundProperty = DPUtils.RegisterFP(_ => _.ButtonBackground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonBackground
        {
            get => (Brush)this.GetValue(ButtonBackgroundProperty);
            set => this.SetValue(ButtonBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonForegroundProperty = DPUtils.RegisterFP(_ => _.ButtonForeground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonForeground
        {
            get => (Brush)this.GetValue(ButtonForegroundProperty);
            set => this.SetValue(ButtonForegroundProperty, value);
        }

        public static readonly DependencyProperty ButtonHoverBackgroundProperty = DPUtils.RegisterFP(_ => _.ButtonHoverBackground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonHoverBackground
        {
            get => (Brush)this.GetValue(ButtonHoverBackgroundProperty);
            set => this.SetValue(ButtonHoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonHoverForegroundProperty = DPUtils.RegisterFP(_ => _.ButtonHoverForeground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonHoverForeground
        {
            get => (Brush)this.GetValue(ButtonHoverForegroundProperty);
            set => this.SetValue(ButtonHoverForegroundProperty, value);
        }

        public static readonly DependencyProperty ButtonPressedBackgroundProperty = DPUtils.RegisterFP(_ => _.ButtonPressedBackground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonPressedBackground
        {
            get => (Brush)this.GetValue(ButtonPressedBackgroundProperty);
            set => this.SetValue(ButtonPressedBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonPressedForegroundProperty = DPUtils.RegisterFP(_ => _.ButtonPressedForeground, null, null, CoerceUtils.CallbackForBrush);
        public Brush ButtonPressedForeground
        {
            get => (Brush)this.GetValue(ButtonPressedForegroundProperty);
            set => this.SetValue(ButtonPressedForegroundProperty, value);
        }

        public static readonly DependencyProperty TextBoxBorderBrushProperty = DPUtils.RegisterFP(_ => _.TextBoxBorderBrush, null, null, CoerceUtils.CallbackForBrush);
        /// <summary>
        /// The path text box's inset edge, drawn along its top and left
        /// </summary>
        public Brush TextBoxBorderBrush
        {
            get => (Brush)this.GetValue(TextBoxBorderBrushProperty);
            set => this.SetValue(TextBoxBorderBrushProperty, value);
        }

        public static readonly DependencyProperty FileDialogTypeProperty = DPUtils.Register(_ => _.FileDialogType, eFileDialogType.OpenFile);
        public eFileDialogType FileDialogType
        {
            get => (eFileDialogType)this.GetValue(FileDialogTypeProperty);
            set => this.SetValue(FileDialogTypeProperty, value);
        }

        public static readonly DependencyProperty DefaultButtonMDL2IconProperty = DPUtils.Register(_ => _.DefaultButtonMDL2Icon);
        public string DefaultButtonMDL2Icon
        {
            get => (string)this.GetValue(DefaultButtonMDL2IconProperty);
            set => this.SetValue(DefaultButtonMDL2IconProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly, (d, e) => CommandManager.InvalidateRequerySuggested());
        /// <summary>
        /// Shows the path without letting it change: it takes no typing, and the browse button is off. Showing the path in
        /// Explorer still works, since that changes nothing. The default style takes this from the inherited
        /// <see cref="AJut.UX.AttachedProperties.Edit.IsReadOnlyProperty"/>, so a read-only PropertyGrid, or anything else
        /// that sets Edit.IsReadOnly, makes the path read-only too.
        /// </summary>
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty IsOpenInExplorerButtonAllowedProperty = DPUtils.Register(_ => _.IsOpenInExplorerButtonAllowed);
        public bool IsOpenInExplorerButtonAllowed
        {
            get => (bool)this.GetValue(IsOpenInExplorerButtonAllowedProperty);
            set => this.SetValue(IsOpenInExplorerButtonAllowedProperty, value);
        }

        public static readonly DependencyProperty OpenInExplorerMDL2IconProperty = DPUtils.Register(_ => _.OpenInExplorerMDL2Icon);
        public string OpenInExplorerMDL2Icon
        {
            get => (string)this.GetValue(OpenInExplorerMDL2IconProperty);
            set => this.SetValue(OpenInExplorerMDL2IconProperty, value);
        }

        public static readonly DependencyProperty FixedRootPathProperty = DPUtils.Register(_ => _.FixedRootPath, (d, e) => d.EvaluatePath(d.SelectedPath));
        /// <summary>
        /// A rooted directory path which the <see cref="SelectedPath"/> must adhere to
        /// </summary>
        public string FixedRootPath
        {
            get => (string)this.GetValue(FixedRootPathProperty);
            set => this.SetValue(FixedRootPathProperty, value);
        }

        public static readonly DependencyProperty InitialBrowseRootProperty = DPUtils.Register(_ => _.InitialBrowseRoot);
        /// <summary>
        /// The browse root used when there is no valid path selected (if <see cref="FixedRootPath"/> is set, this will be overwritten)
        /// </summary>
        public string InitialBrowseRoot
        {
            get => (string)this.GetValue(InitialBrowseRootProperty);
            set => this.SetValue(InitialBrowseRootProperty, value);
        }

        public static readonly DependencyProperty ShortenPathToFixedRootProperty = DPUtils.Register(_ => _.ShortenPathToFixedRoot, (d, e) => d.EvaluatePath(d.SelectedPath));
        /// <summary>
        /// If <c>true</c>, then the <see cref="SelectedPath"/> will be displayed relative to the <see cref="FixedRootPath"/>
        /// </summary>
        public bool ShortenPathToFixedRoot
        {
            get => (bool)this.GetValue(ShortenPathToFixedRootProperty);
            set => this.SetValue(ShortenPathToFixedRootProperty, value);
        }

        private bool TryToApplyPathShorteningIfAny (ref string target)
        {
            if (this.ShortenPathToFixedRoot && PathSelectionRules.TryShortenToFixedRoot(target, this.FixedRootPath, out string shortenedPath))
            {
                target = shortenedPath;
                return true;
            }

            return false;
        }

        public string GetFullyExpandedPath (string path = null)
        {
            return PathSelectionRules.GetFullyExpandedPath(path ?? this.SelectedPath, this.FixedRootPath);
        }

        /// <summary>
        /// What Ctrl+Up does in the path box: a folder path moves up to its parent folder, unless it's read-only, since that
        /// changes the path. Returns whether it moved.
        /// </summary>
        internal bool TryMoveToParentFolder ()
        {
            if (this.IsReadOnly
                || (this.PathType != ePathType.Folder)
                || !PathSelectionRules.TryGetParentFolder(this.SelectedPath, this.FixedRootPath, out string parentFolder))
            {
                return false;
            }

            this.SelectedPath = parentFolder;
            return true;
        }

        // The rules are every stack's, so the same path gets the same answer and reason on each one
        private void EvaluatePath (string path)
        {
            PathSelectionRules.Evaluation evaluation = PathSelectionRules.Evaluate(path, this.PathType, this.FileFilter, this.FixedRootPath, this.TreatEmptyPathAsInvalid, this.TreatNonExistentPathAsInvalid);
            this.DoesPathExist = evaluation.DoesPathExist;
            this.IsPathValid = evaluation.IsPathValid;
            this.InvalidPathReason = evaluation.InvalidPathReason;
        }
    }
}
