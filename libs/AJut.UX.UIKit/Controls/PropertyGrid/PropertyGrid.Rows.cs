namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.CodeBinding;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using CoreGraphics;
    using UIKit;

    public partial class PropertyGrid
    {
        // ===========[ Subclasses/structs ]====================================
        // Every color a row paints: the AJut theme's for the look key when the theme is loaded, a consumer's unthemed
        // default when one has been set for the key, otherwise the system color a stock form uses there
        private static class RowColors
        {
            public static UIColor Label => Get(MobileLook.PropertyGrid.Label, UIColor.Label);
            public static UIColor Subtitle => Get(MobileLook.PropertyGrid.Subtitle, UIColor.Label);
            public static UIColor ListElementHeader => Get(MobileLook.PropertyGrid.ListElementHeader, UIColor.Label);
            public static UIColor RemoveGlyph => Get(MobileLook.PropertyGrid.RemoveGlyph, UIColor.Label);
            public static UIColor NotSet => Get(MobileLook.PropertyGrid.NotSet, UIColor.SecondaryLabel);

            private static UIColor Get (string key, UIColor fallback) => ThemedColors.Get(key) ?? fallback;
        }

        // One property row, right of the tree's indent and chevron: the name (bold when the value is away from its
        // default) with an optional subtitle under it, then the editor under those, or at the end of the name's line for
        // a compact editor, a list header, or a list element's header text; then a remove button for a removable list
        // element. A built-in button row is the button alone: the button carries the name.
        //
        // Every hookup goes through one BindingSet, disposed when the tree drops the row, so a row never outlives its
        // view and never keeps listening to its target. The row needs its grid only while it is built.
        private sealed class RowView : UIView, IFlatTreeRowContent
        {
            private readonly PropertyEditTarget m_target;
            private readonly BindingSet m_bindings = new BindingSet();
            private readonly UILabel m_name = new UILabel();
            private readonly UILabel m_subtitle = new UILabel();
            private bool m_isTornDown;

            public RowView (PropertyGrid owner, FlatTreeItem item) : base(CGRect.Empty)
            {
                const float kHeaderSpacing = 8f;

                m_target = item.Source as PropertyEditTarget;

                var stack = new UIStackView
                {
                    Axis = UILayoutConstraintAxis.Vertical,
                    Alignment = UIStackViewAlignment.Fill,
                    Spacing = MobileLook.PropertyGrid.LabelToEditorSpacing,
                    LayoutMargins = new UIEdgeInsets(MobileLook.PropertyGrid.RowVerticalPadding, 0, MobileLook.PropertyGrid.RowVerticalPadding, 0),
                    LayoutMarginsRelativeArrangement = true,
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };

                this.AddSubview(stack);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    stack.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    stack.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    stack.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    stack.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                // 1. The header line: the name and subtitle, growing to fill what the line's other parts leave
                var header = new UIStackView
                {
                    Axis = UILayoutConstraintAxis.Horizontal,
                    Alignment = UIStackViewAlignment.Center,
                    Spacing = kHeaderSpacing,
                };

                var labelColumn = new UIStackView { Axis = UILayoutConstraintAxis.Vertical, Alignment = UIStackViewAlignment.Leading };
                labelColumn.SetContentHuggingPriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);
                labelColumn.SetContentCompressionResistancePriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);

                m_name.Font = UIFont.SystemFontOfSize(MobileLook.PropertyGrid.LabelFontSize);
                m_name.TextColor = RowColors.Label;
                m_name.Alpha = MobileLook.PropertyGrid.LabelOpacity;
                m_name.LineBreakMode = UILineBreakMode.TailTruncation;
                labelColumn.AddArrangedSubview(m_name);

                // A hidden view in a stack gives up its space, so an empty subtitle leaves no blank line under the name
                m_subtitle.Font = UIFont.ItalicSystemFontOfSize(MobileLook.PropertyGrid.SubtitleFontSize);
                m_subtitle.TextColor = RowColors.Subtitle;
                m_subtitle.Alpha = MobileLook.PropertyGrid.SubtitleOpacity;
                m_subtitle.LineBreakMode = UILineBreakMode.TailTruncation;
                m_subtitle.Hidden = true;
                labelColumn.AddArrangedSubview(m_subtitle);

                header.AddArrangedSubview(labelColumn);
                stack.AddArrangedSubview(header);
                if (m_target == null)
                {
                    return;
                }

                m_bindings.BindLabel(m_name, m_target, nameof(PropertyEditTarget.DisplayName), this.ReadDisplayName);
                m_bindings.Watch(m_target, nameof(PropertyEditTarget.IsAtDefaultValue), this.RefreshNameWeight);
                m_bindings.BindLabel(m_subtitle, m_target, nameof(PropertyEditTarget.Subtitle), this.ReadSubtitle);
                m_bindings.BindHidden(m_subtitle, m_target, nameof(PropertyEditTarget.Subtitle), this.IsSubtitleEmpty);

                // 2. The editor, or a list element's header text
                UIView editor = null;
                if (m_target.HasInlineEditor)
                {
                    editor = owner.CreateEditor(m_target.EffectiveEditorTarget, m_bindings, out ePropertyEditorPlacement placement);
                    if (editor != null)
                    {
                        eBuiltInPropertyEditor builtIn = PropertyGridRowRules.GetBuiltInEditor(m_target.EffectiveEditorTarget, owner.Editors.IsRegistered);
                        if (builtIn == eBuiltInPropertyEditor.Button)
                        {
                            header.Hidden = true;
                        }

                        if (placement == ePropertyEditorPlacement.BesideLabel)
                        {
                            editor.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
                            header.AddArrangedSubview(editor);
                        }
                        else
                        {
                            stack.AddArrangedSubview(editor);
                        }
                    }
                }
                else
                {
                    var listElementHeader = new UILabel
                    {
                        Font = UIFont.SystemFontOfSize(MobileLook.PropertyGrid.ListElementHeaderFontSize),
                        TextColor = RowColors.ListElementHeader,
                        Alpha = MobileLook.PropertyGrid.ListElementHeaderOpacity,
                        LineBreakMode = UILineBreakMode.TailTruncation,
                        TextAlignment = UITextAlignment.Right,
                    };

                    m_bindings.BindLabel(listElementHeader, m_target, nameof(PropertyEditTarget.ListElementHeaderText), this.ReadListElementHeader);
                    m_bindings.BindHidden(listElementHeader, m_target, nameof(PropertyEditTarget.IsListElementHeaderVisible), this.IsListElementHeaderHidden);
                    header.AddArrangedSubview(listElementHeader);
                }

                // 3. Remove, for a removable list element
                if (PropertyGridRowRules.CanRemoveFromList(m_target, owner.IsLocked) && (m_target.ListElementRemoveCommand != null))
                {
                    var remove = new UIButton(UIButtonType.System);
                    remove.SetImage(UIImage.GetSystemImage("xmark"), UIControlState.Normal);
                    remove.TintColor = RowColors.RemoveGlyph;
                    remove.Alpha = MobileLook.PropertyGrid.RemoveGlyphOpacity;
                    remove.AccessibilityLabel = "Remove";
                    remove.TranslatesAutoresizingMaskIntoConstraints = false;
                    remove.WidthAnchor.ConstraintEqualTo(MobileLook.PropertyGrid.RemoveButtonSize).Active = true;
                    remove.HeightAnchor.ConstraintEqualTo(MobileLook.PropertyGrid.RemoveButtonSize).Active = true;
                    m_bindings.BindCommand(remove, m_target.ListElementRemoveCommand);
                    header.AddArrangedSubview(remove);
                }

                // 4. A phone has no tooltips, so the tooltip text is the VoiceOver hint where the placement puts it
                string toolTip = m_target.EffectiveToolTip;
                if (PropertyGridRowRules.ShowsToolTipOnName(owner.PropertyToolTipPlacement))
                {
                    m_name.AccessibilityHint = toolTip;
                }

                if ((editor != null) && PropertyGridRowRules.ShowsToolTipOnValue(owner.PropertyToolTipPlacement))
                {
                    editor.AccessibilityHint = toolTip;
                }
            }

            public void Teardown ()
            {
                if (m_isTornDown)
                {
                    return;
                }

                m_isTornDown = true;
                m_bindings.Dispose();
            }

            private string ReadDisplayName () => m_target.DisplayName ?? string.Empty;
            private string ReadSubtitle () => m_target.Subtitle ?? string.Empty;
            private bool IsSubtitleEmpty () => string.IsNullOrEmpty(m_target.Subtitle);
            private string ReadListElementHeader () => m_target.ListElementHeaderText ?? string.Empty;
            private bool IsListElementHeaderHidden () => !m_target.IsListElementHeaderVisible;

            private void RefreshNameWeight ()
            {
                m_name.Font = PropertyGridRowRules.UsesDefaultValueLabel(m_target)
                    ? UIFont.SystemFontOfSize(MobileLook.PropertyGrid.LabelFontSize)
                    : UIFont.BoldSystemFontOfSize(MobileLook.PropertyGrid.LabelFontSize);
            }
        }

        // The built-in editor for a Nullable<T> property: the inner type's own editor, through a proxy target (AJut.UX's
        // NullableEditorContext.BuildProxyTarget), plus Set and Unset. While the value is null the inner editor is dimmed
        // and disabled and "Not set" shows beside it.
        private sealed class NullableEditorView : UIView
        {
            private readonly PropertyEditTarget m_outer;
            private readonly bool m_isReadOnly;
            private readonly NullableEditorContext m_context;
            private readonly PropertyEditTarget m_proxy;
            private readonly UIView m_innerEditor;
            private readonly UILabel m_notSet;
            private readonly UIButton m_set;
            private readonly UIButton m_unset;

            [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            public NullableEditorView (PropertyGrid owner, PropertyEditTarget outer, BindingSet bindings, bool isReadOnly) : base(CGRect.Empty)
            {
                m_outer = outer;
                m_isReadOnly = isReadOnly;
                m_context = outer.EditContext as NullableEditorContext;
                m_proxy = NullableEditorContext.BuildProxyTarget(outer, m_context);

                var row = new UIStackView
                {
                    Axis = UILayoutConstraintAxis.Horizontal,
                    Alignment = UIStackViewAlignment.Center,
                    Spacing = MobileLook.PropertyGrid.NullableEditorSpacing,
                    TranslatesAutoresizingMaskIntoConstraints = false,
                };

                this.AddSubview(row);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                m_innerEditor = owner.CreateEditor(m_proxy, bindings, out _) ?? new UIView();
                m_innerEditor.SetContentHuggingPriority((float)UILayoutPriority.DefaultLow, UILayoutConstraintAxis.Horizontal);
                row.AddArrangedSubview(m_innerEditor);

                m_notSet = new UILabel { Text = "Not set", TextColor = RowColors.NotSet };
                m_notSet.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
                row.AddArrangedSubview(m_notSet);

                m_set = CreateTextButton("Set");
                bindings.BindCommand(m_set, new ActionCommand(this.SetToInnerDefault));
                row.AddArrangedSubview(m_set);

                m_unset = CreateTextButton("Unset");
                bindings.BindCommand(m_unset, new ActionCommand(this.Unset));
                row.AddArrangedSubview(m_unset);

                bindings.Watch(outer, nameof(PropertyEditTarget.EditValue), this.OnOuterValueChanged);
            }

            private static UIButton CreateTextButton (string title)
            {
                UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
                configuration.Title = title;
                var button = new UIButton(CGRect.Empty) { Configuration = configuration };
                button.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
                return button;
            }

            // The proxy reads the inner type's default while the outer value is null, so going null to 0 and back leaves
            // its cached value at 0 both times; force the raise so the inner editor always refreshes
            private void OnOuterValueChanged ()
            {
                m_proxy.ForceRaiseEditValueChanged();

                bool isNull = m_outer.EditValue == null;
                m_innerEditor.Alpha = isNull ? MobileLook.PropertyGrid.NullableEditorDimmedOpacity : 1f;
                m_innerEditor.UserInteractionEnabled = !isNull && !m_isReadOnly;
                m_notSet.Hidden = !isNull;
                m_set.Hidden = !isNull || m_isReadOnly;
                m_unset.Hidden = isNull || m_isReadOnly;
            }

            // The trimmer can't see that InnerType keeps a parameterless constructor. It only creates one for a value
            // type, and every value type has its default value with nothing for the trimmer to remove.
            [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Only value types are created, and a value type's default needs no constructor metadata")]
            private void SetToInnerDefault ()
            {
                Type innerType = m_context?.InnerType;
                m_outer.EditValue = ((innerType != null) && innerType.IsValueType) ? Activator.CreateInstance(innerType) : null;
            }

            private void Unset ()
            {
                m_outer.EditValue = null;
            }
        }
    }
}
