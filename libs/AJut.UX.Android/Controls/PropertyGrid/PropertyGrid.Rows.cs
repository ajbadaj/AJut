namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.CodeBinding;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Graphics;
    using Android.Util;
    using Android.Views;
    using Android.Views.Accessibility;
    using Android.Widget;

    public partial class PropertyGrid
    {
        // ===========[ Subclasses/structs ]====================================
        // Every color a row paints: the AJut theme's for the look key when the theme is loaded, otherwise the literal for a
        // light or a dark UI. The selection and press take the same blue and tints as the tree's own rows.
        internal static class RowColors
        {
            public static readonly ThemedColor Label = ThemedColor.FromArgb(MobileLook.PropertyGrid.Label, 0xE4000000, 0xFFFFFFFF);
            public static readonly ThemedColor Subtitle = ThemedColor.FromArgb(MobileLook.PropertyGrid.Subtitle, 0xE4000000, 0xFFFFFFFF);
            public static readonly ThemedColor ListElementHeader = ThemedColor.FromArgb(MobileLook.PropertyGrid.ListElementHeader, 0xE4000000, 0xFFFFFFFF);
            public static readonly ThemedColor RemoveGlyph = ThemedColor.FromArgb(MobileLook.PropertyGrid.RemoveGlyph, 0xE4000000, 0xFFFFFFFF);
            public static readonly ThemedColor NotSet = ThemedColor.FromArgb(MobileLook.PropertyGrid.NotSet, 0x9E000000, 0xC5FFFFFF);
            public static readonly ThemedColor SelectionAccent = ThemedColor.FromArgb(MobileLook.PropertyGrid.SelectionAccent, 0xFF186DBA, 0xFF4CC2FF);
            public static readonly ThemedColor PressedFill = ThemedColor.FromArgb(MobileLook.PropertyGrid.PressedFill, 0x0F000000, 0x14FFFFFF);
            public static readonly ThemedColor PressedBorder = ThemedColor.FromArgb(MobileLook.PropertyGrid.PressedBorder, 0x1F000000, 0x24FFFFFF);
        }

        // One property row, right of the tree's indent and chevron: the name (bold when the value is away from its
        // default) with an optional subtitle under it, then the editor under those, or at the end of the name's line for
        // a compact editor, a list header, or a list element's header text; then a remove button for a removable list
        // element. A built-in button row is the button alone: the button carries the name.
        //
        // Every hookup goes through one BindingSet, disposed when the tree drops the row, so a row never outlives its
        // view and never keeps listening to its target. The row needs its grid only while it is built.
        private sealed class RowView : LinearLayout, IFlatTreeRowContent
        {
            private readonly PropertyEditTarget? m_target;
            private readonly BindingSet m_bindings = new BindingSet();
            private readonly TextView m_name;
            private readonly TextView m_subtitle;
            private bool m_isTornDown;

            public RowView (PropertyGrid owner, FlatTreeItem item) : base(owner.Context)
            {
                const float kHeaderSpacing = 8f;

                Context context = owner.Context!;
                m_target = item.Source as PropertyEditTarget;
                this.Orientation = Orientation.Vertical;
                int verticalPadding = ToPixels(context, MobileLook.PropertyGrid.RowVerticalPadding);
                this.SetPadding(0, verticalPadding, 0, verticalPadding);

                // 1. The header line: the name and subtitle, growing to fill what the line's other parts leave
                var header = new LinearLayout(context) { Orientation = Orientation.Horizontal };
                header.SetGravity(GravityFlags.CenterVertical);

                var labelColumn = new LinearLayout(context) { Orientation = Orientation.Vertical };
                m_name = CreateText(context, MobileLook.PropertyGrid.LabelFontSize, RowColors.Label, MobileLook.PropertyGrid.LabelOpacity);
                labelColumn.AddView(m_name);

                // A view that is Gone gives up its space, so an empty subtitle leaves no blank line under the name
                m_subtitle = CreateText(context, MobileLook.PropertyGrid.SubtitleFontSize, RowColors.Subtitle, MobileLook.PropertyGrid.SubtitleOpacity);
                m_subtitle.SetTypeface(null, TypefaceStyle.Italic);
                m_subtitle.Visibility = ViewStates.Gone;
                labelColumn.AddView(m_subtitle);

                header.AddView(labelColumn, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
                this.AddView(header, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
                if (m_target == null)
                {
                    return;
                }

                m_bindings.BindLabel(m_name, m_target, nameof(PropertyEditTarget.DisplayName), this.ReadDisplayName);
                m_bindings.Watch(m_target, nameof(PropertyEditTarget.IsAtDefaultValue), this.RefreshNameWeight);
                m_bindings.BindLabel(m_subtitle, m_target, nameof(PropertyEditTarget.Subtitle), this.ReadSubtitle);
                m_bindings.BindVisible(m_subtitle, m_target, nameof(PropertyEditTarget.Subtitle), this.HasSubtitle);

                // 2. The editor, or a list element's header text
                View? editor = null;
                int headerSpacing = ToPixels(context, kHeaderSpacing);
                if (m_target.HasInlineEditor)
                {
                    editor = owner.CreateEditor(m_target.EffectiveEditorTarget, m_bindings, out ePropertyEditorPlacement placement);
                    if (editor != null)
                    {
                        eBuiltInPropertyEditor builtIn = PropertyGridRowRules.GetBuiltInEditor(m_target.EffectiveEditorTarget, owner.Editors.IsRegistered);
                        if (builtIn == eBuiltInPropertyEditor.Button)
                        {
                            header.Visibility = ViewStates.Gone;
                        }

                        if (placement == ePropertyEditorPlacement.BesideLabel)
                        {
                            var beside = new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { LeftMargin = headerSpacing };
                            header.AddView(editor, beside);
                        }
                        else
                        {
                            var below = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                            {
                                TopMargin = (builtIn == eBuiltInPropertyEditor.Button) ? 0 : ToPixels(context, MobileLook.PropertyGrid.LabelToEditorSpacing),
                            };

                            this.AddView(editor, below);
                        }
                    }
                }
                else
                {
                    TextView listElementHeader = CreateListText(context);
                    listElementHeader.Gravity = GravityFlags.End | GravityFlags.CenterVertical;
                    m_bindings.BindLabel(listElementHeader, m_target, nameof(PropertyEditTarget.ListElementHeaderText), this.ReadListElementHeader);
                    m_bindings.BindVisible(listElementHeader, m_target, nameof(PropertyEditTarget.IsListElementHeaderVisible), this.IsListElementHeaderVisible);
                    header.AddView(listElementHeader, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { LeftMargin = headerSpacing });
                }

                // 3. Remove, for a removable list element
                if (PropertyGridRowRules.CanRemoveFromList(m_target, owner.IsLocked) && (m_target.ListElementRemoveCommand != null))
                {
                    int removeSize = ToPixels(context, MobileLook.PropertyGrid.RemoveButtonSize);
                    var remove = new ImageButton(context)
                    {
                        ContentDescription = "Remove",
                        Alpha = MobileLook.PropertyGrid.RemoveGlyphOpacity,
                        Background = null,
                    };

                    remove.SetImageResource(Android.Resource.Drawable.IcMenuCloseClearCancel);
                    remove.SetColorFilter(ThemeColors.Resolve(context, RowColors.RemoveGlyph).ToAndroidColor(), PorterDuff.Mode.SrcIn!);
                    m_bindings.BindCommand(remove, m_target.ListElementRemoveCommand);
                    header.AddView(remove, new LayoutParams(removeSize, removeSize));
                }

                // 4. A phone has no hover tooltips, so the tooltip text is TalkBack's hint where the placement puts it
                string? toolTip = m_target.EffectiveToolTip;
                if (!string.IsNullOrEmpty(toolTip))
                {
                    if (PropertyGridRowRules.ShowsToolTipOnName(owner.PropertyToolTipPlacement))
                    {
                        m_name.SetAccessibilityDelegate(new HintDelegate(toolTip));
                    }

                    if ((editor != null) && PropertyGridRowRules.ShowsToolTipOnValue(owner.PropertyToolTipPlacement))
                    {
                        editor.SetAccessibilityDelegate(new HintDelegate(toolTip));
                    }
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

            /// <summary>dp to pixels for this context's screen</summary>
            public static int ToPixels (Context context, double dp) => (int)Math.Round(dp * (context.Resources?.DisplayMetrics?.Density ?? 1f));

            /// <summary>A list header's text: a list row's element count, or a list element's header</summary>
            public static TextView CreateListText (Context context)
            {
                return CreateText(context, MobileLook.PropertyGrid.ListElementHeaderFontSize, RowColors.ListElementHeader, MobileLook.PropertyGrid.ListElementHeaderOpacity);
            }

            // Text sizes are in sp, which is MobileLook's dp at the default font scale and grows with the user's font size
            private static TextView CreateText (Context context, float size, ThemedColor color, float opacity)
            {
                var text = new TextView(context)
                {
                    Ellipsize = Android.Text.TextUtils.TruncateAt.End,
                    Alpha = opacity,
                };

                text.SetSingleLine(true);
                text.SetTextSize(ComplexUnitType.Sp, size);
                text.SetTextColor(ThemeColors.Resolve(context, color).ToAndroidColor());
                return text;
            }

            private string ReadDisplayName () => m_target!.DisplayName ?? string.Empty;
            private string ReadSubtitle () => m_target!.Subtitle ?? string.Empty;
            private bool HasSubtitle () => !string.IsNullOrEmpty(m_target!.Subtitle);
            private string ReadListElementHeader () => m_target!.ListElementHeaderText ?? string.Empty;
            private bool IsListElementHeaderVisible () => m_target!.IsListElementHeaderVisible;

            private void RefreshNameWeight ()
            {
                m_name.SetTypeface(null, PropertyGridRowRules.UsesDefaultValueLabel(m_target!) ? TypefaceStyle.Normal : TypefaceStyle.Bold);
            }
        }

        // The built-in editor for a Nullable<T> property: the inner type's own editor, through a proxy target (AJut.UX's
        // NullableEditorContext.BuildProxyTarget), plus Set and Unset. While the value is null the inner editor is dimmed
        // and disabled and "Not set" shows beside it.
        private sealed class NullableEditorView : LinearLayout
        {
            private readonly PropertyEditTarget m_outer;
            private readonly bool m_isReadOnly;
            private readonly NullableEditorContext? m_context;
            private readonly PropertyEditTarget m_proxy;
            private readonly TouchGate m_innerEditor;
            private readonly TextView m_notSet;
            private readonly Button m_set;
            private readonly Button m_unset;

            [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            [UnconditionalSuppressMessage("AOT", "IL3050", Justification = PropertyGridTrimming.kSourceCarriesTheWarning)]
            public NullableEditorView (PropertyGrid owner, PropertyEditTarget outer, BindingSet bindings, bool isReadOnly) : base(owner.Context)
            {
                Context context = owner.Context!;
                m_outer = outer;
                m_isReadOnly = isReadOnly;
                m_context = outer.EditContext as NullableEditorContext;
                m_proxy = NullableEditorContext.BuildProxyTarget(outer, m_context!);
                this.Orientation = Orientation.Horizontal;
                this.SetGravity(GravityFlags.CenterVertical);

                int spacing = RowView.ToPixels(context, MobileLook.PropertyGrid.NullableEditorSpacing);
                m_innerEditor = new TouchGate(context);
                m_innerEditor.AddView(owner.CreateEditor(m_proxy, bindings, out _) ?? new View(context));
                this.AddView(m_innerEditor, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

                m_notSet = new TextView(context) { Text = "Not set" };
                m_notSet.SetTextColor(ThemeColors.Resolve(context, RowColors.NotSet).ToAndroidColor());
                this.AddView(m_notSet, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { LeftMargin = spacing });

                m_set = CreateTextButton(context, "Set");
                bindings.OnClick(m_set, this.SetToInnerDefault);
                this.AddView(m_set, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { LeftMargin = spacing });

                m_unset = CreateTextButton(context, "Unset");
                bindings.OnClick(m_unset, this.Unset);
                this.AddView(m_unset, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { LeftMargin = spacing });

                bindings.Watch(outer, nameof(PropertyEditTarget.EditValue), this.OnOuterValueChanged);
            }

            private static Button CreateTextButton (Context context, string title)
            {
                var button = new Button(context, null, Android.Resource.Attribute.BorderlessButtonStyle) { Text = title };
                button.SetMinWidth(0);
                button.SetMinimumWidth(0);
                return button;
            }

            // The proxy reads the inner type's default while the outer value is null, so going null to 0 and back leaves
            // its cached value at 0 both times; force the raise so the inner editor always refreshes
            private void OnOuterValueChanged ()
            {
                m_proxy.ForceRaiseEditValueChanged();

                bool isNull = m_outer.EditValue == null;
                m_innerEditor.Alpha = isNull ? MobileLook.PropertyGrid.NullableEditorDimmedOpacity : 1f;
                m_innerEditor.IsBlocking = isNull || m_isReadOnly;
                m_notSet.Visibility = isNull ? ViewStates.Visible : ViewStates.Gone;
                m_set.Visibility = (isNull && !m_isReadOnly) ? ViewStates.Visible : ViewStates.Gone;
                m_unset.Visibility = (!isNull && !m_isReadOnly) ? ViewStates.Visible : ViewStates.Gone;
            }

            // The trimmer can't see that InnerType keeps a parameterless constructor. It only creates one for a value
            // type, and every value type has its default value with nothing for the trimmer to remove.
            [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Only value types are created, and a value type's default needs no constructor metadata")]
            private void SetToInnerDefault ()
            {
                Type? innerType = m_context?.InnerType;
                m_outer.EditValue = (((innerType != null) && innerType.IsValueType) ? Activator.CreateInstance(innerType) : null)!;
            }

            private void Unset ()
            {
                m_outer.EditValue = null!;
            }

            // Holds the inner editor, and keeps every touch and focus from it while blocking: a ViewGroup's Enabled never
            // reaches its children, and an editor may be one
            private sealed class TouchGate : FrameLayout
            {
                private bool m_isBlocking;

                public TouchGate (Context context) : base(context)
                {
                }

                public bool IsBlocking
                {
                    get => m_isBlocking;
                    set
                    {
                        m_isBlocking = value;
                        this.DescendantFocusability = value ? DescendantFocusability.BlockDescendants : DescendantFocusability.AfterDescendants;
                        if (value)
                        {
                            this.FindFocus()?.ClearFocus();
                        }
                    }
                }

                // Blocking, a touch never reaches the editor and is left for the row, so a tap still selects the row
                public override bool OnInterceptTouchEvent (MotionEvent? e) => m_isBlocking || base.OnInterceptTouchEvent(e);

                public override bool OnTouchEvent (MotionEvent? e) => !m_isBlocking && base.OnTouchEvent(e);
            }
        }

        // Gives a view's accessibility node the property's tooltip text as its hint, which TalkBack reads after the view
        private sealed class HintDelegate : View.AccessibilityDelegate
        {
            private readonly string m_hint;

            public HintDelegate (string hint)
            {
                m_hint = hint;
            }

            public override void OnInitializeAccessibilityNodeInfo (View host, AccessibilityNodeInfo info)
            {
                base.OnInitializeAccessibilityNodeInfo(host, info);
                info.HintText = m_hint;
            }
        }
    }
}
