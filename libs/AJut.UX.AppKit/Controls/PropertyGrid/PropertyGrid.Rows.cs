namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.CodeBinding;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;

    public partial class PropertyGrid
    {
        // ===========[ Subclasses/structs ]====================================
        // One property row, right of the tree's indent and expander: the label (name, bold when the
        // value is away from its default, with an optional subtitle), then the editor, or a list
        // element's header text, then a remove button for a removable list element.
        //
        // Every hookup goes through one BindingSet, disposed when the tree drops the row, so a row
        // never outlives its view and never keeps listening to its target.
        [RequiresUnreferencedCode(PropertyGridTrimming.kMessage)]
        [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
        private sealed class RowView : NSView, IFlatTreeRowContent
        {
            private const double kSubtitleFontSize = 10.0;
            private const double kLabelFontSize = 12.0;
            private const double kColumnSpacing = 6.0;

            private readonly PropertyGrid m_owner;
            private readonly FlatTreeItem m_item;
            private readonly PropertyEditTarget m_target;
            private readonly BindingSet m_bindings = new BindingSet();
            private readonly FillStack m_labelColumn;
            private readonly NSTextField m_nameLabel;
            private readonly NSTextField m_subtitle;
            private readonly NSLayoutConstraint m_subtitleCollapse;
            private NSLayoutConstraint m_labelWidthConstraint;
            private bool m_isTornDown;

            public RowView (PropertyGrid owner, FlatTreeItem item)
            {
                m_owner = owner;
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

                // 1. Label column: name, then subtitle
                m_labelColumn = new FillStack(NSUserInterfaceLayoutOrientation.Vertical);
                m_nameLabel = NSTextField.CreateLabel(String.Empty);
                m_nameLabel.TextColor = ThemedColors.Get(ThemedColorKeys.PropertyGridRow_LabelForeground);
                m_nameLabel.LineBreakMode = NSLineBreakMode.TruncatingTail;
                m_labelColumn.Add(m_nameLabel);

                m_subtitle = NSTextField.CreateLabel(String.Empty);
                m_subtitle.Font = NSFontManager.SharedFontManager.ConvertFont(NSFont.SystemFontOfSize((nfloat)kSubtitleFontSize), NSFontTraitMask.Italic);
                m_subtitle.TextColor = ThemedColors.Get(ThemedColorKeys.PropertyGridRow_SubtitleForeground);
                m_labelColumn.Add(m_subtitle);

                // A hidden view still takes its space in the stack, so an empty subtitle would leave a blank line under
                // the name and sit the name above the row's middle, now that rows size to their content. With no
                // subtitle the line collapses to nothing, so a name alone centers in its row.
                m_subtitleCollapse = m_subtitle.HeightAnchor.ConstraintEqualTo(0);
                m_subtitleCollapse.Active = true;

                // The grid sets the width of every live row when LabelColumnWidth changes (the label
                // splitter changes it), so it knows this row from here until Teardown
                this.ApplyLabelColumnWidth(owner.LabelColumnWidth);
                owner.m_liveRows.Add(this);

                row.Add(m_labelColumn, alignment: eFillStackCrossAlignment.Center);
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
                    header.TextColor = ThemedColors.Get(ThemedColorKeys.PropertyGridRow_SubtitleForeground);
                    m_bindings.BindLabel(header, m_target, nameof(PropertyEditTarget.ListElementHeaderText), this.ReadListElementHeader);
                    m_bindings.BindHidden(header, m_target, nameof(PropertyEditTarget.IsListElementHeaderVisible), this.IsListElementHeaderHidden);
                    row.Add(header, grows: true, alignment: eFillStackCrossAlignment.Center);
                }

                // 3. Remove, for a removable list element
                if (PropertyGridRowRules.CanRemoveFromList(m_target, owner.IsReadOnly) && m_target.ListElementRemoveCommand != null)
                {
                    var remove = new NSButton { Title = "-", ToolTip = "Remove" };
                    remove.BezelStyle = NSBezelStyle.Recessed;
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

                // 5. "Set to default" on a right click anywhere on the row, where it applies; HitTest sends the click to
                //  the row. The label and its column carry the same menu.
                if (PropertyGridRowRules.CanResetToDefault(m_target, owner.IsReadOnly))
                {
                    var menu = new NSMenu { AutoEnablesItems = false };
                    var setToDefault = new NSMenuItem("Set to default");
                    m_bindings.BindCommand(setToDefault, new ActionCommand(m_target.ResetToDefault));
                    menu.AddItem(setToDefault);
                    this.Menu = menu;
                    m_labelColumn.Menu = menu;
                    m_nameLabel.Menu = menu;
                    m_subtitle.Menu = menu;
                }
            }

            public override bool IsFlipped => true;

            // A right click (or Control click) anywhere on a row with "Set to default" goes to the row, so the whole row
            // offers it: a right click goes to the view under the pointer, and the label and the editors would otherwise
            // take it first. Only right clicks are taken, so left clicks, editing and tooltips are untouched.
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
                m_owner.m_liveRows.Remove(this);
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

                if (m_labelWidthConstraint == null)
                {
                    m_labelColumn.TranslatesAutoresizingMaskIntoConstraints = false;
                    m_labelWidthConstraint = m_labelColumn.WidthAnchor.ConstraintEqualTo((nfloat)labelWidth);
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

                var gapMiddle = new CGPoint(m_labelColumn.Bounds.Width + (nfloat)(kColumnSpacing / 2), 0);
                boundaryX = (double)m_labelColumn.ConvertPointToView(gapMiddle, relativeTo).X;
                shownLabelColumnWidth = PropertyGridRowRules.ToLabelColumnWidth((double)m_labelColumn.Bounds.Width, m_item.IndentWidth, kExpanderColumnWidth);
                return true;
            }

            /// <summary>The label column width that would show this row's whole name and subtitle</summary>
            public double MeasureFittingLabelColumnWidth ()
            {
                // A label's intrinsic width is its whole text on one line, whatever width it is given now
                double labelWidth = (double)m_nameLabel.IntrinsicContentSize.Width;
                if (!m_subtitle.Hidden)
                {
                    labelWidth = Math.Max(labelWidth, (double)m_subtitle.IntrinsicContentSize.Width);
                }

                return PropertyGridRowRules.ToLabelColumnWidth(labelWidth, m_item.IndentWidth, kExpanderColumnWidth);
            }

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

        // The built-in editor for a Nullable<T> property: the inner type's own editor, through a
        // proxy target (AJut.UX's NullableEditorContext.BuildProxyTarget), plus Set and Unset. While
        // the value is null the inner editor is dimmed and disabled and "Not set" shows beside it.
        [RequiresUnreferencedCode(PropertyGridTrimming.kMessage)]
        [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
        private sealed class NullableEditorView : NSView
        {
            private const double kDimmedAlpha = 0.4;

            private readonly PropertyEditTarget m_outer;
            private readonly bool m_isReadOnly;
            private readonly NullableEditorContext m_context;
            private readonly PropertyEditTarget m_proxy;
            private readonly NSView m_innerEditor;
            private readonly NSTextField m_notSet;
            private readonly NSButton m_set;
            private readonly NSButton m_unset;

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

                m_innerEditor = owner.CreateEditor(m_proxy, bindings) ?? new NSView();
                row.Add(m_innerEditor, grows: true, alignment: eFillStackCrossAlignment.Center);

                m_notSet = NSTextField.CreateLabel("Not set");
                m_notSet.TextColor = ThemedColors.Get(ThemedColorKeys.NullableEditor_NotSetForeground);
                row.Add(m_notSet, alignment: eFillStackCrossAlignment.Center);

                m_set = new NSButton { Title = "Set" };
                m_set.BezelStyle = NSBezelStyle.Recessed;
                bindings.BindCommand(m_set, new ActionCommand(this.SetToInnerDefault));
                row.Add(m_set, alignment: eFillStackCrossAlignment.Center);

                m_unset = new NSButton { Title = "Unset" };
                m_unset.BezelStyle = NSBezelStyle.Recessed;
                bindings.BindCommand(m_unset, new ActionCommand(this.Unset));
                row.Add(m_unset, alignment: eFillStackCrossAlignment.Center);

                bindings.Watch(outer, nameof(PropertyEditTarget.EditValue), this.OnOuterValueChanged);
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

                m_notSet.Hidden = !isNull;
                m_set.Hidden = !isNull || m_isReadOnly;
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
