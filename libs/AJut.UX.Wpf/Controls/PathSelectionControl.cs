namespace AJut.UX.Controls
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut.IO;
    using Microsoft.Win32;
    using DPUtils = AJut.UX.DPUtils<PathSelectionControl>;

    public enum ePathType
    {
        File,
        Folder
    }

    public class PathSelectionControl : Control
    {
        static PathSelectionControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PathSelectionControl), new FrameworkPropertyMetadata(typeof(PathSelectionControl)));
        }
        public PathSelectionControl ()
        {
            this.EvaluatePath(null);

            this.CommandBindings.Add(new CommandBinding(PromptUserForNewPathCommand, _OnPromptUserForNewPathExecuted, _OnCanPromptUserForNewPath));

            void _OnCanPromptUserForNewPath (object _sender, CanExecuteRoutedEventArgs _e)
            {
                _e.CanExecute = true;
            }

            void _OnPromptUserForNewPathExecuted (object _sender, ExecutedRoutedEventArgs _e)
            {
                if (this.PathType == ePathType.File)
                {
                    string userSelectedPath = PathHelpersUI.PromptUserToPickAFile<OpenFileDialog>(this.BrowsePrompt, this.SelectedPath, this.FileFilter);
                    if (userSelectedPath != null)
                    {
                        this.SelectedPath = userSelectedPath;
                    }
                }
                else
                {
                    string userSelectedPath = PathHelpersUI.PromptUserToPickADirectory(this.BrowsePrompt, this.SelectedPath);
                    if (userSelectedPath != null)
                    {
                        this.SelectedPath = userSelectedPath;
                    }
                }
            }
        }

        public static RoutedCommand PromptUserForNewPathCommand = new RoutedCommand(nameof(PromptUserForNewPathCommand), typeof(PathSelectionControl));

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

        private void EvaluatePath (string path)
        {
            if (!this.TreatEmptyPathAsInvalid && path.IsNullOrEmpty())
            {
                this.IsPathValid = true;
                this.InvalidPathReason = null;
                this.DoesPathExist = false;
                return;
            }

            var result = PathHelpers.EvaluatePathValidity(path);
            if (!result)
            {
                _SetError(result.GetErrorReport());
            }
            else if (this.PathType == ePathType.File && !PathHelpers.FindMatchingExtensionsFromFilter(path, this.FileFilter).Any())
            {
                int extensionsDisplayLength = (int)(Math.Max(path.Length, 75) * 1.3);
                _SetError($"{this.SelectedPath}\r\nIndicated file path does not use one of the allowed file extensions:\r\n{string.Join(", ", PathHelpers.ParseExtensionsFrom(this.FileFilter)).Shorten(extensionsDisplayLength, eStringShortening.TakeFromEnd)}");
            }
            else
            {
                this.DoesPathExist = this.PathType == ePathType.File ? File.Exists(path) : Directory.Exists(path);
                if (!this.DoesPathExist && this.TreatNonExistentPathAsInvalid)
                {
                    // Ignoring _SetError which sets DoesPathExist
                    this.IsPathValid = false;
                    this.InvalidPathReason = $"{this.PathType} does not exist";
                }
                else
                {
                    this.IsPathValid = true;
                    this.InvalidPathReason = null;
                }
            }

            void _SetError (string _error)
            {
                this.DoesPathExist = false;
                this.IsPathValid = false;
                this.InvalidPathReason = _error;
            }
        }
    }
}
