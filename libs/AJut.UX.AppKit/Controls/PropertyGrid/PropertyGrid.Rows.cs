namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.CodeBinding;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using ObjCRuntime;

    public partial class PropertyGrid
    {
        // ===========[ Subclasses/structs ]====================================
        // One property row, right of the tree's indent and expander: the label (name, bold when the
        // value is away from its default, with an optional subtitle), then the editor, or a list
        // element's header text, then a remove button for a removable list element.
        //
        // Selection is marked on the label alone, as WinUI3's row marks it: the grid turns the tree's
        // own row selection off, and the label's frame draws a nub and outline while the row is
        // selected, the same whether the list has focus or not.
        //
        // Every hookup goes through one BindingSet, disposed when the tree drops the row, so a row
        // never outlives its view and never keeps listening to its target. A row is retained natively
        // by the table its grid retains, so it holds the grid weakly (see the grid's class comment).
        private sealed class RowView : NSView, IFlatTreeRowContent
        {
            private const double kSubtitleFontSize = 10.0;
            private const double kLabelFontSize = 12.0;
            private const double kColumnSpacing = 6.0;

            // WinUI3's subtitle and list element header text sit at this, on the label's color
            private const double kSecondaryTextOpacity = 0.85;

            private readonly WeakReference<PropertyGrid> m_owner;
            private readonly FlatTreeItem m_item;
            private readonly PropertyEditTarget m_target;
            private readonly BindingSet m_bindings = new BindingSet();
            private readonly LabelFrameView m_labelFrame;
            private readonly FillStack m_labelColumn;
            private readonly NSTextField m_nameLabel;
            private readonly NSTextField m_subtitle;
            private readonly NSLayoutConstraint m_subtitleCollapse;
            private NSLayoutConstraint m_labelWidthConstraint;
            private bool m_isTornDown;

            public RowView (PropertyGrid owner, FlatTreeItem item)
            {
                m_owner = new WeakReference<PropertyGrid>(owner);
                m_item = item;
                m_target = item.Source as PropertyEditTarget;
                var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: kColumnSpacing)
                {
                    Padding = owner.ElementPadding,
                };
                row.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(row);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                // 1. Label column: name, then subtitle, inside the frame that marks the row selected. The column sits
                //  inside the frame's edges, which are always kept for it so nothing moves when the row is selected.
                m_labelColumn = new FillStack(NSUserInterfaceLayoutOrientation.Vertical);
                m_nameLabel = new RowNameLabel(this);
                m_nameLabel.TextColor = ThemedColors.Get(ThemedColorKeys.PropertyGridRow_LabelForeground);
                m_nameLabel.LineBreakMode = NSLineBreakMode.TruncatingTail;
                m_labelColumn.Add(m_nameLabel);

                m_subtitle = NSTextField.CreateLabel(String.Empty);
                m_subtitle.Font = NSFontManager.SharedFontManager.ConvertFont(NSFont.SystemFontOfSize((nfloat)kSubtitleFontSize), NSFontTraitMask.Italic);
                m_subtitle.TextColor = ThemedColors.Get(ThemedColorKeys.PropertyGridRow_LabelForeground);
                m_subtitle.AlphaValue = (nfloat)kSecondaryTextOpacity;
                m_labelColumn.Add(m_subtitle);

                m_labelFrame = new LabelFrameView();
                m_labelColumn.TranslatesAutoresizingMaskIntoConstraints = false;
                m_labelFrame.AddSubview(m_labelColumn);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    m_labelColumn.LeadingAnchor.ConstraintEqualTo(m_labelFrame.LeadingAnchor, (nfloat)LabelFrameView.kLeadingEdgeThickness),
                    m_labelColumn.TrailingAnchor.ConstraintEqualTo(m_labelFrame.TrailingAnchor, (nfloat)(-LabelFrameView.kEdgeThickness)),
                    m_labelColumn.CenterYAnchor.ConstraintEqualTo(m_labelFrame.CenterYAnchor),
                    m_labelColumn.TopAnchor.ConstraintGreaterThanOrEqualTo(m_labelFrame.TopAnchor, (nfloat)LabelFrameView.kEdgeThickness),
                    m_labelColumn.BottomAnchor.ConstraintLessThanOrEqualTo(m_labelFrame.BottomAnchor, (nfloat)(-LabelFrameView.kEdgeThickness)),
                });

                // A hidden view still takes its space in the stack, so an empty subtitle would leave a blank line under
                // the name and sit the name above the row's middle, now that rows size to their content. With no
                // subtitle the line collapses to nothing, so a name alone centers in its row.
                m_subtitleCollapse = m_subtitle.HeightAnchor.ConstraintEqualTo(0);
                m_subtitleCollapse.Active = true;

                // The grid sets the width of every live row when LabelColumnWidth changes (the label
                // splitter changes it), so it knows this row from here until Teardown
                this.ApplyLabelColumnWidth(owner.LabelColumnWidth);
                owner.m_liveRows.Add(this);

                // The frame takes the row's height, as WinUI3's label border does, so the outline runs top to bottom
                row.Add(m_labelFrame, alignment: eFillStackCrossAlignment.Stretch);
                m_bindings.Watch(m_item, nameof(FlatTreeItem.IsSelected), this.RefreshSelection);
                if (m_target == null)
                {
                    return;
                }

                m_bindings.BindLabel(m_nameLabel, m_target, nameof(PropertyEditTarget.DisplayName), this.ReadDisplayName);
                m_bindings.Watch(m_target, nameof(PropertyEditTarget.IsAtDefaultValue), this.RefreshLabelWeight);
                m_bindings.BindLabel(m_subtitle, m_target, nameof(PropertyEditTarget.Subtitle), this.ReadSubtitle);
                m_bindings.BindHidden(m_subtitle, m_target, nameof(PropertyEditTarget.Subtitle), this.IsSubtitleEmpty);
                m_bindings.Watch(m_target, nameof(PropertyEditTarget.Subtitle), this.RefreshSubtitleCollapse);

                // 2. Editor, or a list element's header
                NSView editor = null;
                if (m_target.HasInlineEditor)
                {
                    editor = owner.CreateEditor(m_target.EffectiveEditorTarget, m_bindings);
                    if (editor != null)
                    {
                        row.Add(editor, grows: true, alignment: eFillStackCrossAlignment.Center);
                    }
                }
                else
                {
                    NSTextField header = NSTextField.CreateLabel(String.Empty);
                    header.TextColor = ThemedColors.Get(ThemedColorKeys.PropertyGridRow_LabelForeground);
                    header.AlphaValue = (nfloat)kSecondaryTextOpacity;
                    m_bindings.BindLabel(header, m_target, nameof(PropertyEditTarget.ListElementHeaderText), this.ReadListElementHeader);
                    m_bindings.BindHidden(header, m_target, nameof(PropertyEditTarget.IsListElementHeaderVisible), this.IsListElementHeaderHidden);
                    row.Add(header, grows: true, alignment: eFillStackCrossAlignment.Center);
                }

                // 3. Remove, for a removable list element: a small AJut button, faded, as WinUI3's is
                if (PropertyGridRowRules.CanRemoveFromList(m_target, owner.IsReadOnly) && m_target.ListElementRemoveCommand != null)
                {
                    const double kRemoveButtonOpacity = 0.7;
                    const double kRemoveButtonMinSize = 20.0;

                    BrushButton remove = CreateGlyphButton("-", "Remove", kRemoveButtonMinSize);
                    remove.AlphaValue = (nfloat)kRemoveButtonOpacity;
                    m_bindings.BindCommand(remove, m_target.ListElementRemoveCommand);
                    row.Add(remove, alignment: eFillStackCrossAlignment.Center);
                }

                // 4. Tooltip placement
                string toolTip = m_target.EffectiveToolTip;
                if (PropertyGridRowRules.ShowsToolTipOnName(owner.PropertyToolTipPlacement))
                {
                    m_labelColumn.ToolTip = toolTip;
                }

                if (editor != null && PropertyGridRowRules.ShowsToolTipOnValue(owner.PropertyToolTipPlacement))
                {
                    editor.ToolTip = toolTip;
                }

                // 5. "Set to default" on a right click anywhere on the row, where it applies (see HitTest)
                if (PropertyGridRowRules.CanResetToDefault(m_target, owner.IsReadOnly))
                {
                    var menu = new NSMenu { AutoEnablesItems = false };
                    var setToDefault = new NSMenuItem("Set to default");
                    m_bindings.BindCommand(setToDefault, new ActionCommand(m_target.ResetToDefault));
                    menu.AddItem(setToDefault);
                    this.Menu = menu;
                }
            }

            // For a peer the runtime makes over an existing native row, which has no owner or item to tell
            private RowView (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            // A right click goes to the view under the pointer, which is nearly always the label or the editor rather than
            // the row, so the row's menu never came up. While a right click (or a Control click) is being delivered, a row
            // with a menu takes it for itself, so "Set to default" comes up anywhere on the row. Only those clicks are
            // taken: left clicks, editing, tooltips, and the cursor still go to whatever is under the pointer.
            public override NSView HitTest (CGPoint point)
            {
                NSView hit = base.HitTest(point);
                if ((hit == null) || (this.Menu == null))
                {
                    return hit;
                }

                NSEvent current = NSApplication.SharedApplication.CurrentEvent;
                bool isRightClick = (current != null)
                    && ((current.Type == NSEventType.RightMouseDown)
                        || ((current.Type == NSEventType.LeftMouseDown) && current.ModifierFlags.HasFlag(NSEventModifierMask.ControlKeyMask)));

                return isRightClick ? this : hit;
            }

            /// <summary>Whether any of the row is in view; the table keeps a few rows ready just off screen</summary>
            public bool IsShown => !this.VisibleRect().IsEmpty;

            public void Teardown ()
            {
                if (m_isTornDown)
                {
                    return;
                }

                m_isTornDown = true;
                if (m_owner.TryGetTarget(out PropertyGrid owner))
                {
                    owner.m_liveRows.Remove(this);
                }

                m_bindings.Dispose();
            }

            /// <summary>Sizes the label column for the grid's label column width at this row's depth; NaN sizes it to its text</summary>
            public void ApplyLabelColumnWidth (double labelColumnWidth)
            {
                double labelWidth = PropertyGridRowRules.ComputeLabelWidth(labelColumnWidth, m_item.IndentWidth, kExpanderColumnWidth);
                if (double.IsNaN(labelWidth))
                {
                    if (m_labelWidthConstraint != null)
                    {
                        m_labelWidthConstraint.Active = false;
                    }

                    return;
                }

                // The width is the frame's, edges included, as WinUI3 sizes its label border
                if (m_labelWidthConstraint == null)
                {
                    m_labelFrame.TranslatesAutoresizingMaskIntoConstraints = false;
                    m_labelWidthConstraint = m_labelFrame.WidthAnchor.ConstraintEqualTo((nfloat)labelWidth);
                }

                m_labelWidthConstraint.Constant = (nfloat)labelWidth;
                m_labelWidthConstraint.Active = true;
            }

            /// <summary>
            /// Whether a point in <paramref name="relativeTo"/> is on this row; if so, the boundary between the label and
            /// the editor as an x in <paramref name="relativeTo"/> (the middle of the space between them), and the label
            /// column width this row shows
            /// </summary>
            public bool TryGetLabelBoundaryAt (CGPoint point, NSView relativeTo, out double boundaryX, out double shownLabelColumnWidth)
            {
                boundaryX = 0.0;
                shownLabelColumnWidth = double.NaN;
                if (!this.Bounds.Contains(this.ConvertPointFromView(point, relativeTo)))
                {
                    return false;
                }

                var gapMiddle = new CGPoint(m_labelFrame.Bounds.Width + (nfloat)(kColumnSpacing / 2), 0);
                boundaryX = (double)m_labelFrame.ConvertPointToView(gapMiddle, relativeTo).X;
                shownLabelColumnWidth = PropertyGridRowRules.ToLabelColumnWidth((double)m_labelFrame.Bounds.Width, m_item.IndentWidth, kExpanderColumnWidth);
                return true;
            }

            /// <summary>The label column width that would show this row's whole name and subtitle</summary>
            public double MeasureFittingLabelColumnWidth ()
            {
                // A label's intrinsic width is its whole text on one line, whatever width it is given now; the frame's
                //  edges come on top
                double labelWidth = (double)m_nameLabel.IntrinsicContentSize.Width;
                if (!m_subtitle.Hidden)
                {
                    labelWidth = Math.Max(labelWidth, (double)m_subtitle.IntrinsicContentSize.Width);
                }

                labelWidth += LabelFrameView.kLeadingEdgeThickness + LabelFrameView.kEdgeThickness;
                return PropertyGridRowRules.ToLabelColumnWidth(labelWidth, m_item.IndentWidth, kExpanderColumnWidth);
            }

            private void RefreshSelection () => m_labelFrame.IsSelected = m_item.IsSelected;
            private string ReadDisplayName () => m_target.DisplayName ?? String.Empty;
            private string ReadSubtitle () => m_target.Subtitle ?? String.Empty;
            private bool IsSubtitleEmpty () => String.IsNullOrEmpty(m_target.Subtitle);
            private void RefreshSubtitleCollapse () => m_subtitleCollapse.Active = this.IsSubtitleEmpty();
            private string ReadListElementHeader () => m_target.ListElementHeaderText ?? String.Empty;
            private bool IsListElementHeaderHidden () => !m_target.IsListElementHeaderVisible;

            private void RefreshLabelWeight ()
            {
                m_nameLabel.Font = PropertyGridRowRules.UsesDefaultValueLabel(m_target)
                    ? NSFont.SystemFontOfSize((nfloat)kLabelFontSize)
                    : NSFont.BoldSystemFontOfSize((nfloat)kLabelFontSize);
            }
        }

        // A row's name, as a plain label (what NSTextField.CreateLabel makes), that also gives VoiceOver the row's menu.
        // VoiceOver reads a row by its name, and its show-menu command (VO-Shift-M, or AXShowMenu) asks the element it is
        // on; a label has no menu of its own, so "Set to default" could only be reached with a mouse. This pops up the
        // row's menu, where the row has one, under the name. The row retains this label natively, so the label holds the
        // row weakly; a strong hold would keep a row the table had dropped alive through its own label.
        private sealed class RowNameLabel : NSTextField
        {
            private readonly WeakReference<NSView> m_row;

            public RowNameLabel (NSView row)
            {
                m_row = new WeakReference<NSView>(row);
                this.StringValue = String.Empty;
                this.Editable = false;
                this.Selectable = false;
                this.Bezeled = false;
                this.Bordered = false;
                this.DrawsBackground = false;
                this.Font = NSFont.SystemFontOfSize(NSFont.SystemFontSize);
                this.Cell.Wraps = false;
            }

            // For a peer the runtime makes over an existing native label, which has no row to tell
            private RowNameLabel (NativeHandle handle) : base(handle)
            {
            }

            public override bool AccessibilityPerformShowMenu ()
            {
                NSMenu menu = m_row.TryGetTarget(out NSView row) ? row.Menu : null;
                if (menu == null)
                {
                    return false;
                }

                var underName = new CGPoint(0, this.IsFlipped ? this.Bounds.Height : 0);
                menu.PopUpMenu(null, underName, this);
                return true;
            }
        }

        // A row's label area, which marks the row selected the way WinUI3's PART_LabelBorder does: a nub
        // down its leading edge and an outline around the rest, in the selection border color, and no
        // fill. The label sits inside those edges whether or not they show.
        private sealed class LabelFrameView : NSView
        {
            /// <summary>The nub's width, down the leading edge</summary>
            public const double kLeadingEdgeThickness = 4.0;

            /// <summary>The outline's width on the other three edges</summary>
            public const double kEdgeThickness = 1.0;

            private bool m_isSelected;

            public LabelFrameView ()
            {
            }

            // For a peer the runtime makes over an existing native frame
            private LabelFrameView (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            /// <summary>Shows the nub and outline</summary>
            public bool IsSelected
            {
                get => m_isSelected;
                set
                {
                    if (m_isSelected != value)
                    {
                        m_isSelected = value;
                        this.NeedsDisplay = true;
                    }
                }
            }

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if (!m_isSelected)
                {
                    return;
                }

                CGRect bounds = this.Bounds;
                var edge = (nfloat)kEdgeThickness;
                ThemedColors.Get(ThemedColorKeys.PropertyGridRow_SelectionBorder).SetFill();
                NSBezierPath.FillRect(new CGRect(bounds.X, bounds.Y, (nfloat)kLeadingEdgeThickness, bounds.Height));
                NSBezierPath.FillRect(new CGRect(bounds.X, bounds.Y, bounds.Width, edge));
                NSBezierPath.FillRect(new CGRect(bounds.X, bounds.GetMaxY() - edge, bounds.Width, edge));
                NSBezierPath.FillRect(new CGRect(bounds.GetMaxX() - edge, bounds.Y, edge, bounds.Height));
            }
        }

        // The built-in editor for a Nullable<T> property: the inner type's own editor, through a
        // proxy target (AJut.UX's NullableEditorContext.BuildProxyTarget), plus Set and Unset, laid out
        // as WinUI3's NullableEditor is. While the value is null the inner editor is dimmed and
        // disabled under a shade that takes its clicks, with "Not set" and the Set button on the shade.
        // While it has a value, Unset sits beside the editor.
        private sealed class NullableEditorView : NSView
        {
            private const double kDimmedAlpha = 0.4;

            // WinUI3's: "Not set" at 0.9, starting 8 in (its panel's margin and its own), and 8 from the Set button
            private const double kNotSetOpacity = 0.9;
            private const double kShadeInset = 8.0;
            private const double kShadeSpacing = 8.0;

            private readonly PropertyEditTarget m_outer;
            private readonly bool m_isReadOnly;
            private readonly NullableEditorContext m_context;
            private readonly PropertyEditTarget m_proxy;
            private readonly NSView m_innerEditor;
            private readonly ThemedView m_nullShade;
            private readonly BrushButton m_set;
            private readonly BrushButton m_unset;

            [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            public NullableEditorView (PropertyGrid owner, PropertyEditTarget outer, BindingSet bindings, bool isReadOnly)
            {
                m_outer = outer;
                m_isReadOnly = isReadOnly;
                m_context = outer.EditContext as NullableEditorContext;
                m_proxy = NullableEditorContext.BuildProxyTarget(outer, m_context);

                var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 6.0);
                row.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(row);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                // 1. The inner editor, with the null shade over it: a view over another takes the clicks first, so the
                //  shade keeps them from the dimmed editor, as WinUI3's hit-testable overlay does
                var editorHost = new NSView();
                m_innerEditor = owner.CreateEditor(m_proxy, bindings) ?? new NSView();
                m_innerEditor.TranslatesAutoresizingMaskIntoConstraints = false;
                editorHost.AddSubview(m_innerEditor);

                m_nullShade = new ThemedView
                {
                    BackgroundColorKey = ThemedColorKeys.NullableEditor_OverlayBackground,
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };
                editorHost.AddSubview(m_nullShade);

                var shadeRow = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: kShadeSpacing)
                {
                    Padding = new NSEdgeInsets(0, (nfloat)kShadeInset, 0, 0),
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };
                m_nullShade.AddSubview(shadeRow);

                NSTextField notSet = NSTextField.CreateLabel("Not set");
                notSet.TextColor = ThemedColors.Get(ThemedColorKeys.NullableEditor_NotSetForeground);
                notSet.AlphaValue = (nfloat)kNotSetOpacity;
                shadeRow.Add(notSet, alignment: eFillStackCrossAlignment.Center);

                m_set = CreateTextButton("Set");
                bindings.BindCommand(m_set, new ActionCommand(this.SetToInnerDefault));
                shadeRow.Add(m_set, alignment: eFillStackCrossAlignment.Center);

                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    m_innerEditor.TopAnchor.ConstraintEqualTo(editorHost.TopAnchor),
                    m_innerEditor.BottomAnchor.ConstraintEqualTo(editorHost.BottomAnchor),
                    m_innerEditor.LeadingAnchor.ConstraintEqualTo(editorHost.LeadingAnchor),
                    m_innerEditor.TrailingAnchor.ConstraintEqualTo(editorHost.TrailingAnchor),
                    m_nullShade.TopAnchor.ConstraintEqualTo(editorHost.TopAnchor),
                    m_nullShade.BottomAnchor.ConstraintEqualTo(editorHost.BottomAnchor),
                    m_nullShade.LeadingAnchor.ConstraintEqualTo(editorHost.LeadingAnchor),
                    m_nullShade.TrailingAnchor.ConstraintEqualTo(editorHost.TrailingAnchor),
                    shadeRow.LeadingAnchor.ConstraintEqualTo(m_nullShade.LeadingAnchor),
                    shadeRow.TrailingAnchor.ConstraintLessThanOrEqualTo(m_nullShade.TrailingAnchor),
                    shadeRow.CenterYAnchor.ConstraintEqualTo(m_nullShade.CenterYAnchor),
                    shadeRow.TopAnchor.ConstraintGreaterThanOrEqualTo(m_nullShade.TopAnchor),
                    shadeRow.BottomAnchor.ConstraintLessThanOrEqualTo(m_nullShade.BottomAnchor),
                });

                row.Add(editorHost, grows: true, alignment: eFillStackCrossAlignment.Center);

                // 2. Unset, beside the editor while it has a value
                m_unset = CreateTextButton("Unset");
                bindings.BindCommand(m_unset, new ActionCommand(this.Unset));
                row.Add(m_unset, alignment: eFillStackCrossAlignment.Center);

                bindings.Watch(outer, nameof(PropertyEditTarget.EditValue), this.OnOuterValueChanged);
            }

            // For a peer the runtime makes over an existing native editor, which has no owner to tell
            private NullableEditorView (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            // The proxy reads the inner type's default while the outer value is null, so going null to 0
            // and back leaves its cached value at 0 both times; force the raise so the inner editor
            // always refreshes
            private void OnOuterValueChanged ()
            {
                m_proxy.ForceRaiseEditValueChanged();

                bool isNull = m_outer.EditValue == null;
                m_innerEditor.AlphaValue = (nfloat)(isNull ? kDimmedAlpha : 1.0);
                if (m_innerEditor is NSControl control)
                {
                    control.Enabled = !isNull && !m_isReadOnly;
                }

                m_nullShade.Hidden = !isNull;
                m_set.Hidden = m_isReadOnly;
                m_unset.Hidden = isNull || m_isReadOnly;
            }

            // The trimmer can't see that InnerType keeps a parameterless constructor. It only creates one for a
            // value type, and every value type has its default value with nothing for the trimmer to remove.
            [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Only value types are created, and a value type's default needs no constructor metadata")]
            private void SetToInnerDefault ()
            {
                Type innerType = m_context?.InnerType;
                m_outer.EditValue = innerType != null && innerType.IsValueType ? Activator.CreateInstance(innerType) : null;
            }

            private void Unset ()
            {
                m_outer.EditValue = null;
            }
        }
    }
}
