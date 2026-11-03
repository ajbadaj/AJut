namespace AJut.UX.Controls
{
    using System;
    using System.IO;
    using System.Linq;
    using AJut.IO;
    using AJut.UX.Theming;
    using AppKit;
    using CoreAnimation;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;
    using UniformTypeIdentifiers;

    // ===========[ PathSelectionControl ]=======================================
    // A path field with a browse button and an optional show in Finder button, the AppKit port of WinUI3's and WPF's
    // PathSelectionControl. Whether the path is valid, and why not, whether it is there, where browsing starts, how it is
    // kept relative to FixedRootPath and the folder above it are all AJut.UX's PathSelectionRules, which every stack's
    // control follows, so a path gets the same answer and reason here as on Windows. This class shows the result and runs
    // the Mac's own pickers (NSOpenPanel, NSSavePanel), as a sheet on the control's window.
    //
    // The look is WinUI3's: a bordered row (thicker along the bottom, rounded at the right end) holding the text in a well
    // that lights up while focused, the warning glyph while the path is invalid, then the buttons. The border is the
    // accent while focus is inside, and the invalid color while the path is invalid and not empty, which wins.
    //
    // Editing, as on the other stacks:
    //  - Every keystroke sets SelectedPath and judges it again.
    //  - Leaving the field, or Return, makes the path relative to FixedRootPath when ShortenPathToFixedRoot is set.
    //  - Command-Up in Folder mode goes to the folder above, Finder's own key for the enclosing folder (the other stacks'
    //    Ctrl+Up, which macOS keeps for Mission Control).
    //  - A browse puts the pick in, shortened the same way.
    //
    // Out of editing, a path too long for the field shows its end (the file or folder's name), cut at the front, the way
    // WinUI3 scrolls its field to the end.
    //
    // The text field's edits are read through one NSTextFieldDelegate rather than the field's C# events, because Command-Up
    // only reaches a text field through its delegate (doCommandBySelector), and the C# events are a delegate under the
    // hood: using both would have one replace the other.
    //
    // Lifetime: the field, its delegate, the buttons and a running picker all hold the control weakly, since the control
    //  retains them; the well and the frame hold nothing of it.
    /// <summary>
    /// A file or folder path field with a browse button that opens the Mac's file picker, and an optional button that shows
    /// the path in Finder. It judges the path as it is typed (syntax, the fixed root, the filter, whether it is there) and
    /// shows an invalid one with a red border, a warning glyph and the reason as its tooltip.
    /// </summary>
    public class PathSelectionControl : ThemedView
    {
        private const double kButtonWidth = 26.0;
        private const double kCornerRadius = 3.0;
        private const double kBorderThickness = 1.0;
        private const double kWarningGlyphWidth = 22.0;
        private const double kFontSize = 12.0;
        private const double kHeaderSpacing = 4.0;

        private readonly FillStack m_layout;
        private readonly NSTextField m_header;
        private readonly PathFrame m_frame;
        private readonly ThemedView m_bottomEdge;
        private readonly FillStack m_row;
        private readonly PathWell m_well;
        private readonly PathTextField m_field;
        private readonly PathFieldDelegate m_fieldDelegate;
        private readonly NSImageView m_warningGlyph;
        private readonly PathButton m_revealButton;
        private readonly PathButton m_browseButton;
        private readonly NSLayoutConstraint m_wellHeight;
        private readonly NSLayoutConstraint m_fieldLeading;
        private readonly NSLayoutConstraint m_fieldTrailing;

        private string m_selectedPath;
        private ePathType m_pathType = ePathType.File;
        private string m_fileFilter = "*.*";
        private eFileDialogType m_fileDialogType = eFileDialogType.OpenFile;
        private string m_fixedRootPath;
        private string m_initialBrowseRoot;
        private bool m_shortenPathToFixedRoot;
        private bool m_treatEmptyPathAsInvalid;
        private bool m_treatNonExistentPathAsInvalid;
        private string m_browsePrompt = "Select";
        private string m_unsetTextPrompt;
        private bool m_isOpenInExplorerButtonAllowed;
        private string m_textBoxHeader;
        private double m_textBoxHeight = 22.0;
        private NSEdgeInsets m_textBoxPadding = new NSEdgeInsets(2, 2, 2, 2);
        private bool m_isEnabled = true;
        private bool m_isFocused;
        private PathSelectionRules.Evaluation m_evaluation;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PathSelectionControl ()
        {
            // 1. The header, above the frame while there is one
            m_header = NSTextField.CreateLabel(String.Empty);
            m_header.TextColor = ThemedColors.Get(ThemedColorKeys.PathSelection_Text);

            // 2. The frame and its thicker bottom edge
            m_frame = new PathFrame
            {
                BackgroundColorKey = ThemedColorKeys.PathSelection_Background,
                BorderColorKey = ThemedColorKeys.PathSelection_NormalBorder,
                BorderThickness = kBorderThickness,
                CornerRadius = kCornerRadius,
            };

            m_bottomEdge = new ThemedView
            {
                BackgroundColorKey = ThemedColorKeys.PathSelection_NormalBorder,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };

            // 3. The field, borderless in its well
            m_field = new PathTextField(this)
            {
                Bordered = false,
                DrawsBackground = false,
                FocusRingType = NSFocusRingType.None,
                Font = NSFont.MonospacedSystemFont((nfloat)kFontSize, NSFontWeight.Regular),
                TextColor = ThemedColors.Get(ThemedColorKeys.PathSelection_Text),
                UsesSingleLineMode = true,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            m_field.Cell.Wraps = false;
            m_field.Cell.Scrollable = false;
            m_field.Cell.LineBreakMode = NSLineBreakMode.TruncatingHead;
            m_field.Cell.SetSendsActionOnEndEditing(false);
            m_fieldDelegate = new PathFieldDelegate(this);
            m_field.Delegate = m_fieldDelegate;

            m_well = new PathWell();
            m_well.AddSubview(m_field);
            m_fieldLeading = m_field.LeadingAnchor.ConstraintEqualTo(m_well.LeadingAnchor);
            m_fieldTrailing = m_field.TrailingAnchor.ConstraintEqualTo(m_well.TrailingAnchor);
            m_wellHeight = m_well.HeightAnchor.ConstraintEqualTo((nfloat)0);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_fieldLeading,
                m_fieldTrailing,
                m_field.CenterYAnchor.ConstraintEqualTo(m_well.CenterYAnchor),
                m_wellHeight,
            });

            // 4. The warning glyph, in the row only while the path is invalid (see RefreshVisualState)
            m_warningGlyph = new NSImageView
            {
                Image = NSImage.GetSystemSymbol("exclamationmark.triangle.fill", null),
                ContentTintColor = ThemedColors.Get(ThemedColorKeys.PathSelection_WarningIconForeground),
            };
            m_warningGlyph.TranslatesAutoresizingMaskIntoConstraints = false;
            m_warningGlyph.WidthAnchor.ConstraintEqualTo((nfloat)kWarningGlyphWidth).Active = true;

            // 5. The buttons: show in Finder (in the row only while it applies) and browse, which rounds the right end
            m_revealButton = new PathButton(this, isBrowse: false) { ToolTip = "Show in Finder" };
            m_browseButton = new PathButton(this, isBrowse: true) { ToolTip = m_browsePrompt };
            foreach (PathButton button in new[] { m_revealButton, m_browseButton })
            {
                button.TranslatesAutoresizingMaskIntoConstraints = false;
                button.WidthAnchor.ConstraintEqualTo((nfloat)kButtonWidth).Active = true;
            }

            // 6. Layout: the row inside the frame's border, and above its thicker bottom edge
            m_row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal);
            m_row.Add(m_well, grows: true);
            m_row.Add(m_browseButton);
            m_row.TranslatesAutoresizingMaskIntoConstraints = false;
            m_frame.AddSubview(m_row);
            m_frame.AddSubview(m_bottomEdge);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_row.TopAnchor.ConstraintEqualTo(m_frame.TopAnchor, (nfloat)kBorderThickness),
                m_row.LeadingAnchor.ConstraintEqualTo(m_frame.LeadingAnchor, (nfloat)kBorderThickness),
                m_row.TrailingAnchor.ConstraintEqualTo(m_frame.TrailingAnchor, (nfloat)(-kBorderThickness)),
                m_row.BottomAnchor.ConstraintEqualTo(m_bottomEdge.TopAnchor),
                m_bottomEdge.HeightAnchor.ConstraintEqualTo((nfloat)kBorderThickness),
                m_bottomEdge.BottomAnchor.ConstraintEqualTo(m_frame.BottomAnchor, (nfloat)(-kBorderThickness)),
                m_bottomEdge.LeadingAnchor.ConstraintEqualTo(m_frame.LeadingAnchor),
                m_bottomEdge.TrailingAnchor.ConstraintEqualTo(m_frame.TrailingAnchor, (nfloat)(-kCornerRadius)),
            });

            m_layout = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: kHeaderSpacing);
            m_layout.Add(m_frame);
            m_layout.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(m_layout);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_layout.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_layout.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_layout.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_layout.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            this.ApplyTextBoxMetrics();
            this.ApplyPlaceholder();
            this.ApplyAccessibilityLabel();
            this.Evaluate();
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native control whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native control keeps the subviews it already has.
        /// None of its managed state survives a dispose, so a call that needs that state can still fail: dispose it only
        /// once AppKit is done with it.
        /// </summary>
        protected PathSelectionControl (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing && (m_field != null))
            {
                m_field.Delegate = null;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when <see cref="SelectedPath"/> changes: from typing, a browse, Command-Up, shortening or code</summary>
        public event EventHandler<EventArgs> SelectedPathChanged;

        /// <summary>
        /// Raised each time the path has been judged, so <see cref="IsPathValid"/>, <see cref="DoesPathExist"/> and
        /// <see cref="InvalidPathReason"/> are current: after the path or any setting it is judged by changes
        /// </summary>
        public event EventHandler<EventArgs> PathEvaluated;

        // ===========[ Properties ]============================================
        /// <summary>Whether the path names a file (the default) or a folder, which decides the picker and the checks</summary>
        public ePathType PathType
        {
            get => m_pathType;
            set
            {
                m_pathType = value;
                this.ApplyAccessibilityLabel();
                this.Evaluate();
            }
        }

        /// <summary>
        /// The file filter, as Description|*.ext;*.ext2 (groups separated by '|') or just its patterns (*.ext), applied to the
        /// picker and to a typed path. *.*, the default, takes any file.
        /// </summary>
        public string FileFilter
        {
            get => m_fileFilter;
            set
            {
                m_fileFilter = value;
                this.Evaluate();
            }
        }

        /// <summary>Whether browsing for a file opens it (the default) or saves to it. Only for <see cref="ePathType.File"/>.</summary>
        public eFileDialogType FileDialogType
        {
            get => m_fileDialogType;
            set => m_fileDialogType = value;
        }

        /// <summary>The path, as typed or picked; relative to <see cref="FixedRootPath"/> once shortened</summary>
        public string SelectedPath
        {
            get => m_selectedPath;
            set
            {
                if (m_selectedPath == value)
                {
                    return;
                }

                m_selectedPath = value;
                this.ShowText(value);
                this.Evaluate();
                this.SelectedPathChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Whether the path can be used: an empty path counts, unless <see cref="TreatEmptyPathAsInvalid"/></summary>
        public bool IsPathValid => m_evaluation.IsPathValid;

        /// <summary>Whether the path is there, as the kind of thing <see cref="PathType"/> says</summary>
        public bool DoesPathExist => m_evaluation.DoesPathExist;

        /// <summary>Why the path can't be used, or null while it can</summary>
        public string InvalidPathReason => m_evaluation.InvalidPathReason;

        /// <summary>What the control's tooltip shows: the path while it is valid, else why it isn't</summary>
        public string CurrentToolTip => m_evaluation.IsPathValid ? m_selectedPath : m_evaluation.InvalidPathReason;

        /// <summary>Whether an empty path is invalid, rather than just unset</summary>
        public bool TreatEmptyPathAsInvalid
        {
            get => m_treatEmptyPathAsInvalid;
            set
            {
                m_treatEmptyPathAsInvalid = value;
                this.Evaluate();
            }
        }

        /// <summary>Whether a path that isn't there is invalid</summary>
        public bool TreatNonExistentPathAsInvalid
        {
            get => m_treatNonExistentPathAsInvalid;
            set
            {
                m_treatNonExistentPathAsInvalid = value;
                this.Evaluate();
            }
        }

        /// <summary>A folder a rooted path must be under; a relative path is taken to be under it</summary>
        public string FixedRootPath
        {
            get => m_fixedRootPath;
            set
            {
                m_fixedRootPath = value;
                this.Evaluate();
            }
        }

        /// <summary>The folder browsing starts in while no path is selected (<see cref="FixedRootPath"/> comes first, when there is one)</summary>
        public string InitialBrowseRoot
        {
            get => m_initialBrowseRoot;
            set => m_initialBrowseRoot = value;
        }

        /// <summary>Whether a path under <see cref="FixedRootPath"/> is kept relative to it: after a browse, and when typing ends</summary>
        public bool ShortenPathToFixedRoot
        {
            get => m_shortenPathToFixedRoot;
            set
            {
                m_shortenPathToFixedRoot = value;
                this.Evaluate();
            }
        }

        /// <summary>The message the picker shows, and the browse button's tooltip</summary>
        public string BrowsePrompt
        {
            get => m_browsePrompt;
            set
            {
                m_browsePrompt = value;
                m_browseButton.ToolTip = value;
            }
        }

        /// <summary>The prompt the field shows while no path is set</summary>
        public string UnsetTextPrompt
        {
            get => m_unsetTextPrompt;
            set
            {
                m_unsetTextPrompt = value;
                this.ApplyPlaceholder();
                this.ApplyAccessibilityLabel();
            }
        }

        /// <summary>Whether the show in Finder button is offered. It shows while the path is valid and there.</summary>
        public bool IsOpenInExplorerButtonAllowed
        {
            get => m_isOpenInExplorerButtonAllowed;
            set
            {
                m_isOpenInExplorerButtonAllowed = value;
                this.RefreshVisualState();
            }
        }

        /// <summary>A header shown above the field, or null for none</summary>
        public string TextBoxHeader
        {
            get => m_textBoxHeader;
            set
            {
                m_textBoxHeader = value;
                bool hasHeader = !String.IsNullOrEmpty(value);
                m_header.StringValue = value ?? String.Empty;
                bool isShown = m_header.Superview == m_layout;
                if (hasHeader && !isShown)
                {
                    m_layout.Insert(0, m_header);
                }
                else if (!hasHeader && isShown)
                {
                    m_layout.Remove(m_header);
                }

                this.ApplyAccessibilityLabel();
            }
        }

        /// <summary>The height of the field's well, in points</summary>
        public double TextBoxHeight
        {
            get => m_textBoxHeight;
            set
            {
                m_textBoxHeight = value;
                this.ApplyTextBoxMetrics();
            }
        }

        /// <summary>The space between the field's well and its text</summary>
        public NSEdgeInsets TextBoxPadding
        {
            get => m_textBoxPadding;
            set
            {
                m_textBoxPadding = value;
                this.ApplyTextBoxMetrics();
            }
        }

        /// <summary>Whether the path can be typed or browsed for. A disabled control shows its path dimmed.</summary>
        public bool Enabled
        {
            get => m_isEnabled;
            set
            {
                m_isEnabled = value;
                m_field.Enabled = value;
                m_field.TextColor = ThemedColors.Get(value ? ThemedColorKeys.PathSelection_Text : ThemedColorKeys.PathSelection_DisabledText);
                m_browseButton.Enabled = value;
                m_revealButton.Enabled = value;
            }
        }

        /// <summary>The text field inside, for anything not passed through here (font, for one)</summary>
        public NSTextField TextField => m_field;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// <paramref name="path"/> (the selected path when null) as a rooted path: a relative path is taken to be under
        /// <see cref="FixedRootPath"/>
        /// </summary>
        public string GetFullyExpandedPath (string path = null)
        {
            return PathSelectionRules.GetFullyExpandedPath(path ?? m_selectedPath ?? String.Empty, m_fixedRootPath);
        }

        // ===========[ Helper Methods ]========================================
        private void Evaluate ()
        {
            m_evaluation = PathSelectionRules.Evaluate(m_selectedPath, m_pathType, m_fileFilter, m_fixedRootPath, m_treatEmptyPathAsInvalid, m_treatNonExistentPathAsInvalid);

            string toolTip = this.CurrentToolTip;
            m_frame.ToolTip = toolTip;
            m_field.ToolTip = toolTip;
            this.RefreshVisualState();
            this.PathEvaluated?.Invoke(this, EventArgs.Empty);
        }

        // The invalid border wins over the focused one, but only for a path that has something in it; the well's focused
        //  look follows focus alone, so an invalid path shows its border around it rather than taking it away mid-edit
        private void RefreshVisualState ()
        {
            bool isShowingInvalid = !m_evaluation.IsPathValid && !String.IsNullOrEmpty(m_selectedPath);
            string borderKey = isShowingInvalid
                ? ThemedColorKeys.PathSelection_InvalidBorder
                : (m_isFocused ? ThemedColorKeys.PathSelection_FocusedBorder : ThemedColorKeys.PathSelection_NormalBorder);
            m_frame.BorderColorKey = borderKey;
            m_bottomEdge.BackgroundColorKey = borderKey;
            m_well.IsFocused = m_isFocused;

            // A FillStack keeps a hidden child's space, so the glyph and the Finder button go into the row only while they
            //  apply. Finder can only show what is there, so its button waits for the path to be there.
            this.ShowInRow(m_warningGlyph, isShowingInvalid, eFillStackCrossAlignment.Center);
            this.ShowInRow(m_revealButton, m_isOpenInExplorerButtonAllowed && m_evaluation.IsPathValid && m_evaluation.DoesPathExist, eFillStackCrossAlignment.Stretch);

            m_warningGlyph.ToolTip = m_evaluation.InvalidPathReason;
            if (m_warningGlyph.Image != null)
            {
                m_warningGlyph.Image.AccessibilityDescription = m_evaluation.InvalidPathReason;
            }
        }

        // The row is: well, warning glyph, show in Finder, browse; whichever of the middle two are shown keep that order
        private void ShowInRow (NSView view, bool isShown, eFillStackCrossAlignment alignment)
        {
            bool isInRow = view.Superview == m_row;
            if (isShown && !isInRow)
            {
                int index = (view == m_revealButton) ? m_row.ChildCount - 1 : 1;
                m_row.Insert(index, view, alignment: alignment);
            }
            else if (!isShown && isInRow)
            {
                m_row.Remove(view);
            }
        }

        private void SetFocused (bool isFocused)
        {
            if (m_isFocused != isFocused)
            {
                m_isFocused = isFocused;
                this.RefreshVisualState();
            }
        }

        // Writing the text a field already shows loses the caret, so only a difference is written: a keystroke's own path is
        //  already there, and a different one set while handling it (a binding writing back) still gets in. While the field
        //  is being edited its text lives in the window's field editor, so it is written through that as an edit, which
        //  registers it with the editor's undo and keeps the typing undone before it lined up with the text it undoes.
        private void ShowText (string text)
        {
            text = text ?? String.Empty;
            if (m_field.StringValue == text)
            {
                return;
            }

            NSText editor = m_field.CurrentEditor;
            if (editor == null)
            {
                m_field.StringValue = text;
                return;
            }

            var whole = new NSRange(0, editor.Value?.Length ?? 0);
            if ((editor is NSTextView textView) && textView.ShouldChangeText(whole, text))
            {
                textView.Replace(whole, text);
                textView.DidChangeText();
            }
            else
            {
                editor.Value = text;
            }

            editor.SelectedRange = new NSRange(text.Length, 0);
        }

        private void OnTextEdited (string text)
        {
            this.SelectedPath = text;
        }

        private void ApplyPathShorteningIfAny ()
        {
            if (m_shortenPathToFixedRoot && PathSelectionRules.TryShortenToFixedRoot(m_selectedPath, m_fixedRootPath, out string shortenedPath))
            {
                this.SelectedPath = shortenedPath;
            }
        }

        private bool TryStepUpAFolder ()
        {
            if ((m_pathType != ePathType.Folder) || !PathSelectionRules.TryGetParentFolder(m_selectedPath, m_fixedRootPath, out string parentFolder))
            {
                return false;
            }

            this.SelectedPath = parentFolder;
            return true;
        }

        private void ApplyPickedPath (string path)
        {
            if (m_shortenPathToFixedRoot && PathSelectionRules.TryShortenToFixedRoot(path, m_fixedRootPath, out string shortenedPath))
            {
                path = shortenedPath;
            }

            this.SelectedPath = path;
        }

        private void ApplyTextBoxMetrics ()
        {
            m_wellHeight.Constant = (nfloat)(m_textBoxHeight + (2.0 * PathWell.kMargin));
            m_fieldLeading.Constant = (nfloat)(PathWell.kMargin + (double)m_textBoxPadding.Left);
            m_fieldTrailing.Constant = (nfloat)(-(PathWell.kMargin + (double)m_textBoxPadding.Right));
        }

        private void ApplyPlaceholder ()
        {
            m_field.PlaceholderAttributedString = new NSAttributedString(
                m_unsetTextPrompt ?? String.Empty,
                new NSStringAttributes
                {
                    ForegroundColor = ThemedColors.Get(ThemedColorKeys.PathSelection_PlaceholderText),
                    Font = m_field.Font,
                }
            );
        }

        private void ApplyAccessibilityLabel ()
        {
            string label = !String.IsNullOrEmpty(m_textBoxHeader)
                ? m_textBoxHeader
                : (!String.IsNullOrEmpty(m_unsetTextPrompt) ? m_unsetTextPrompt : (m_pathType == ePathType.Folder ? "Folder path" : "File path"));
            m_field.AccessibilityLabel = label;
        }

        // The Mac's own pickers, as a sheet on the control's window, or on their own while the control is in none. The
        //  start folder follows the same rule on every stack, and macOS honors it.
        private void Browse ()
        {
            NSSavePanel panel;
            if (m_pathType == ePathType.Folder)
            {
                NSOpenPanel openPanel = NSOpenPanel.OpenPanel;
                openPanel.CanChooseDirectories = true;
                openPanel.CanChooseFiles = false;
                openPanel.AllowsMultipleSelection = false;
                openPanel.CanCreateDirectories = true;
                panel = openPanel;
            }
            else if (m_fileDialogType == eFileDialogType.OpenFile)
            {
                NSOpenPanel openPanel = NSOpenPanel.OpenPanel;
                openPanel.CanChooseDirectories = false;
                openPanel.CanChooseFiles = true;
                openPanel.AllowsMultipleSelection = false;
                panel = openPanel;
                this.ApplyFilterTo(panel);
            }
            else
            {
                panel = NSSavePanel.SavePanel;
                panel.CanCreateDirectories = true;
                string fileName = String.IsNullOrEmpty(m_selectedPath) ? null : Path.GetFileName(this.GetFullyExpandedPath());
                if (!String.IsNullOrEmpty(fileName))
                {
                    panel.NameFieldStringValue = fileName;
                }

                this.ApplyFilterTo(panel);
            }

            panel.Message = m_browsePrompt ?? String.Empty;
            string startFolder = PathSelectionRules.FindBrowseStartFolder(m_selectedPath, m_pathType, m_initialBrowseRoot, m_fixedRootPath);
            if (startFolder != null)
            {
                panel.DirectoryUrl = NSUrl.FromFilename(startFolder);
            }

            var handler = new PanelHandler(this, panel);
            if (this.Window != null)
            {
                panel.BeginSheet(this.Window, handler.OnPanelEnded);
            }
            else
            {
                handler.OnPanelEnded(panel.RunModal());
            }
        }

        // The picker shows only files of the filter's types. An extension macOS knows no type for drops the restriction,
        //  rather than hiding files the filter takes; a typed or picked path is still judged against the filter itself.
        private void ApplyFilterTo (NSSavePanel panel)
        {
            if (String.IsNullOrEmpty(m_fileFilter))
            {
                return;
            }

            string[] extensions = PathHelpers.ParseExtensionsFrom(m_fileFilter);
            if ((extensions.Length == 0) || extensions.Contains("*"))
            {
                return;
            }

            UTType[] types = extensions
                .Select(_extension => UTType.CreateFromExtension(_extension.Substring(_extension.LastIndexOf('.') + 1)))
                .ToArray();

            if (types.All(_type => _type != null))
            {
                panel.AllowedContentTypes = types;
            }
        }

        // Shows a file selected in its Finder window, or opens a folder in one. A package (an app, a bundle) is a folder on
        //  disk that Finder shows as a file, and opening one launches it, so it is shown selected, as a file is.
        private void RevealInFinder ()
        {
            if (!m_evaluation.IsPathValid || !m_evaluation.DoesPathExist)
            {
                return;
            }

            string path = Path.GetFullPath(this.GetFullyExpandedPath());
            NSWorkspace workspace = NSWorkspace.SharedWorkspace;
            if ((m_pathType == ePathType.File) || workspace.IsFilePackage(path))
            {
                workspace.ActivateFileViewer(new[] { NSUrl.FromFilename(path) });
            }
            else
            {
                workspace.SelectFile(null, path);
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The bordered frame: rounded at its right end only, as WinUI3's is, where the browse button sits
        private sealed class PathFrame : ThemedView
        {
            public PathFrame ()
            {
                this.TranslatesAutoresizingMaskIntoConstraints = false;
            }

            private PathFrame (NativeHandle handle) : base(handle)
            {
            }

            protected override void ApplyLayerColors (CALayer layer)
            {
                base.ApplyLayerColors(layer);
                layer.MaskedCorners = CACornerMask.MaxXMinYCorner | CACornerMask.MaxXMaxYCorner;
            }
        }

        // The well the text sits in, inset from the row: while focused it fills, with a dark edge along its top and left and
        //  a light one along its bottom and right, WinUI3's rim lighting
        private sealed class PathWell : NSView
        {
            public const double kMargin = 3.0;
            private const double kRimThickness = 1.0;

            private bool m_isFocused;

            public PathWell ()
            {
                this.TranslatesAutoresizingMaskIntoConstraints = false;
            }

            private PathWell (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            public bool IsFocused
            {
                get => m_isFocused;
                set
                {
                    if (m_isFocused != value)
                    {
                        m_isFocused = value;
                        this.NeedsDisplay = true;
                    }
                }
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if (!m_isFocused)
                {
                    return;
                }

                CGRect well = this.Bounds.Inset((nfloat)kMargin, (nfloat)kMargin);
                ThemedColors.Get(ThemedColorKeys.PathSelection_FocusedWellBackground).SetFill();
                NSBezierPath.FillRect(well);

                // The view is flipped, so the top edge is at the well's lowest y
                ThemedColors.Get(ThemedColorKeys.PathSelection_FocusRimDark).SetFill();
                NSBezierPath.FillRect(new CGRect(well.X, well.Y, well.Width, (nfloat)kRimThickness));
                NSBezierPath.FillRect(new CGRect(well.X, well.Y, (nfloat)kRimThickness, well.Height));

                ThemedColors.Get(ThemedColorKeys.PathSelection_FocusRimBright).SetFill();
                NSBezierPath.FillRect(new CGRect(well.X, well.Bottom - (nfloat)kRimThickness, well.Width, (nfloat)kRimThickness));
                NSBezierPath.FillRect(new CGRect(well.Right - (nfloat)kRimThickness, well.Y, (nfloat)kRimThickness, well.Height));
            }

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }
        }

        // Knows when it takes focus. Losing focus shows up as the end of editing, in PathFieldDelegate.
        private sealed class PathTextField : NSTextField
        {
            private readonly WeakReference<PathSelectionControl> m_owner;

            public PathTextField (PathSelectionControl owner)
            {
                m_owner = new WeakReference<PathSelectionControl>(owner);
            }

            private PathTextField (NativeHandle handle) : base(handle)
            {
            }

            public override bool BecomeFirstResponder ()
            {
                bool became = base.BecomeFirstResponder();
                if (became && (m_owner != null) && m_owner.TryGetTarget(out PathSelectionControl owner))
                {
                    owner.SetFocused(true);
                }

                return became;
            }
        }

        // The field's one delegate: keystrokes, the end of editing, Return and Command-Up
        private sealed class PathFieldDelegate : NSTextFieldDelegate
        {
            // The up arrow's virtual key code (kVK_UpArrow), the same on every keyboard layout
            private const ushort kUpArrowKeyCode = 126;

            private readonly WeakReference<PathSelectionControl> m_owner;

            public PathFieldDelegate (PathSelectionControl owner)
            {
                m_owner = new WeakReference<PathSelectionControl>(owner);
            }

            private PathFieldDelegate (NativeHandle handle) : base(handle)
            {
            }

            public override void Changed (NSNotification notification)
            {
                if (this.TryGetOwner(out PathSelectionControl owner))
                {
                    owner.OnTextEdited(owner.m_field.StringValue);
                }
            }

            public override void EditingEnded (NSNotification notification)
            {
                if (this.TryGetOwner(out PathSelectionControl owner))
                {
                    owner.ApplyPathShorteningIfAny();
                    owner.SetFocused(false);
                }
            }

            public override bool DoCommandBySelector (NSControl control, NSTextView textView, Selector commandSelector)
            {
                if (!this.TryGetOwner(out PathSelectionControl owner))
                {
                    return false;
                }

                // Command-Up arrives as moveToBeginningOfDocument: under the standard key bindings, which a user's own
                //  bindings can change, so the key is read from the event itself
                NSEvent current = NSApplication.SharedApplication.CurrentEvent;
                if ((current != null)
                    && (current.Type == NSEventType.KeyDown)
                    && (current.KeyCode == kUpArrowKeyCode)
                    && current.ModifierFlags.HasFlag(NSEventModifierMask.CommandKeyMask))
                {
                    return owner.TryStepUpAFolder();
                }

                if (commandSelector.Name == "insertNewline:")
                {
                    // Return keeps the field focused and makes the path relative to the fixed root, as leaving it does
                    owner.ApplyPathShorteningIfAny();
                    return true;
                }

                return false;
            }

            private bool TryGetOwner (out PathSelectionControl owner)
            {
                owner = null;
                return (m_owner != null) && m_owner.TryGetTarget(out owner);
            }
        }

        // Browse or show in Finder: no fill at rest, so the buttons read as part of the control, then the highlight under the
        //  pointer and while pressed, with the glyph lit on it. The browse button rounds the control's right end.
        private sealed class PathButton : ManagedButton
        {
            private const double kEndCornerRadius = 2.0;
            private const double kGlyphLineWidth = 1.2;

            private readonly WeakReference<PathSelectionControl> m_owner;
            private readonly bool m_isBrowse;

            public PathButton (PathSelectionControl owner, bool isBrowse)
            {
                m_owner = new WeakReference<PathSelectionControl>(owner);
                m_isBrowse = isBrowse;
                this.Activated += this.OnActivated;
            }

            private PathButton (NativeHandle handle) : base(handle)
            {
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    this.Activated -= this.OnActivated;
                }

                base.Dispose(disposing);
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                eManagedButtonState state = this.CurrentState;
                string backgroundKey = BackgroundKeyFor(state);
                if (backgroundKey != null)
                {
                    ThemedColors.Get(backgroundKey).SetFill();
                    if (m_isBrowse)
                    {
                        RightRoundedRect(this.Bounds, kEndCornerRadius).Fill();
                    }
                    else
                    {
                        NSBezierPath.FillRect(this.Bounds);
                    }
                }

                NSColor glyphColor = ThemedColors.Get(ForegroundKeyFor(state));
                double centerX = (double)this.Bounds.Width / 2.0;
                double centerY = (double)this.Bounds.Height / 2.0;
                if (m_isBrowse)
                {
                    DrawEllipsis(centerX, centerY, glyphColor);
                }
                else
                {
                    DrawOpenOutward(centerX, centerY, glyphColor);
                }
            }

            private void OnActivated (object sender, EventArgs e)
            {
                if ((m_owner == null) || !m_owner.TryGetTarget(out PathSelectionControl owner))
                {
                    return;
                }

                if (m_isBrowse)
                {
                    owner.Browse();
                }
                else
                {
                    owner.RevealInFinder();
                }
            }

            // Three dots, the browse glyph the other stacks use
            private static void DrawEllipsis (double centerX, double centerY, NSColor color)
            {
                const double kDotRadius = 1.3;
                const double kDotSpacing = 4.5;

                color.SetFill();
                for (int index = -1; index <= 1; ++index)
                {
                    double x = centerX + (index * kDotSpacing);
                    NSBezierPath.FromOvalInRect(new CGRect(x - kDotRadius, centerY - kDotRadius, kDotRadius * 2.0, kDotRadius * 2.0)).Fill();
                }
            }

            // A box with an arrow out of its top right corner, the other stacks' open in a new window glyph. The view is
            //  flipped, so up is toward lower y.
            private static void DrawOpenOutward (double centerX, double centerY, NSColor color)
            {
                const double kHalfBox = 4.5;
                const double kArrowHead = 3.0;

                double left = centerX - kHalfBox;
                double right = centerX + kHalfBox;
                double top = centerY - kHalfBox;
                double bottom = centerY + kHalfBox;

                var glyph = new NSBezierPath
                {
                    LineWidth = (nfloat)kGlyphLineWidth,
                    LineCapStyle = NSLineCapStyle.Round,
                    LineJoinStyle = NSLineJoinStyle.Round,
                };

                // The box, open at its top right where the arrow leaves it
                glyph.MoveTo(new CGPoint(centerX, top));
                glyph.LineTo(new CGPoint(left, top));
                glyph.LineTo(new CGPoint(left, bottom));
                glyph.LineTo(new CGPoint(right, bottom));
                glyph.LineTo(new CGPoint(right, centerY));

                // The arrow, from the box's middle out past its corner
                glyph.MoveTo(new CGPoint(centerX, centerY));
                glyph.LineTo(new CGPoint(right + 1.0, top - 1.0));
                glyph.MoveTo(new CGPoint(right + 1.0 - kArrowHead, top - 1.0));
                glyph.LineTo(new CGPoint(right + 1.0, top - 1.0));
                glyph.LineTo(new CGPoint(right + 1.0, top - 1.0 + kArrowHead));

                color.SetStroke();
                glyph.Stroke();
            }

            private static NSBezierPath RightRoundedRect (CGRect rect, double radius)
            {
                var path = new NSBezierPath();
                path.MoveTo(new CGPoint(rect.Left, rect.Top));
                path.LineTo(new CGPoint(rect.Right - (nfloat)radius, rect.Top));
                path.CurveTo(new CGPoint(rect.Right, rect.Top + (nfloat)radius), new CGPoint(rect.Right, rect.Top), new CGPoint(rect.Right, rect.Top));
                path.LineTo(new CGPoint(rect.Right, rect.Bottom - (nfloat)radius));
                path.CurveTo(new CGPoint(rect.Right - (nfloat)radius, rect.Bottom), new CGPoint(rect.Right, rect.Bottom), new CGPoint(rect.Right, rect.Bottom));
                path.LineTo(new CGPoint(rect.Left, rect.Bottom));
                path.ClosePath();
                return path;
            }

            private static string BackgroundKeyFor (eManagedButtonState state)
            {
                switch (state)
                {
                    case eManagedButtonState.PointerOver: return ThemedColorKeys.PathSelection_ButtonPointerOverBackground;
                    case eManagedButtonState.Pressed: return ThemedColorKeys.PathSelection_ButtonPressedBackground;
                    default: return null;
                }
            }

            private static string ForegroundKeyFor (eManagedButtonState state)
            {
                switch (state)
                {
                    case eManagedButtonState.PointerOver:
                    case eManagedButtonState.Pressed:
                        return ThemedColorKeys.PathSelection_ButtonHighlightForeground;

                    default:
                        return ThemedColorKeys.PathSelection_ButtonIconForeground;
                }
            }
        }

        // Hears the picker close and puts its pick in. It holds the control weakly, since a picker open as a sheet can
        //  outlive a control taken out of its window, and the panel strongly, until the panel is done with it.
        private sealed class PanelHandler
        {
            private readonly WeakReference<PathSelectionControl> m_owner;
            private NSSavePanel m_panel;

            public PanelHandler (PathSelectionControl owner, NSSavePanel panel)
            {
                m_owner = new WeakReference<PathSelectionControl>(owner);
                m_panel = panel;
            }

            public void OnPanelEnded (nint result)
            {
                NSSavePanel panel = m_panel;
                m_panel = null;
                if ((panel == null) || (result != (nint)(long)NSModalResponse.OK))
                {
                    return;
                }

                string path = panel.Url?.Path;
                if (!String.IsNullOrEmpty(path) && m_owner.TryGetTarget(out PathSelectionControl owner))
                {
                    owner.ApplyPickedPath(path);
                }
            }
        }
    }
}
