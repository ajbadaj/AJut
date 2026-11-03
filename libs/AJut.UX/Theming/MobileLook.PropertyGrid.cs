namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// PropertyGrid: a settings form of property rows, set out for a phone with each row's name above its editor
        /// rather than in a column beside it, so an editor takes the row's whole width. The name reads plain while the
        /// value is at its default and bold once it has been changed, with an optional subtitle under it. A selected row
        /// takes an outline and a strip down its leading edge with no fill, and a pressed row the hover tint, as WinUI3's
        /// grid row draws them. A removable list element has a remove button at the end of its name's line.
        /// </summary>
        public static class PropertyGrid
        {
            // ===========[ Keys ]===================================
            /// <summary>The property's name, at <see cref="LabelOpacity"/>.</summary>
            public const string Label = AJutThemeKeys.PropertyGridItemRow_LabelForeground;

            /// <summary>The subtitle under the name, at <see cref="SubtitleOpacity"/>.</summary>
            public const string Subtitle = AJutThemeKeys.PropertyGridItemRow_LabelForeground;

            /// <summary>A list element's header text (its [PGList] display member), at <see cref="ListElementHeaderOpacity"/>.</summary>
            public const string ListElementHeader = AJutThemeKeys.PropertyGridItemRow_LabelForeground;

            /// <summary>A selected row's outline, and the strip down its leading edge.</summary>
            public const string SelectionAccent = AJutThemeKeys.PropertyGridItemRow_SelectionBorderBrush;

            /// <summary>A pressed row's fill, WinUI3's hover tint.</summary>
            public const string PressedFill = AJutThemeKeys.PropertyGridItemRow_HoverBackground;

            /// <summary>A pressed row's outline, WinUI3's hover border.</summary>
            public const string PressedBorder = AJutThemeKeys.PropertyGridItemRow_HoverBorderBrush;

            /// <summary>A list element's remove glyph, at <see cref="RemoveGlyphOpacity"/>; it is a plain button, as on WinUI3.</summary>
            public const string RemoveGlyph = "ButtonForeground";

            /// <summary>A nullable property's "Not set", beside its dimmed editor while it has no value.</summary>
            public const string NotSet = AJutThemeKeys.NullableEditor_NotSetForeground;

            // ===========[ Sizes ]===================================
            /// <summary>The indent per tree depth, as on WinUI3.</summary>
            public const float TreeDepthIndentSize = 16f;

            public const float LabelFontSize = 15f;
            public const float SubtitleFontSize = 12f;
            public const float ListElementHeaderFontSize = 15f;

            /// <summary>The name's opacity, as WinUI3's label template has it.</summary>
            public const float LabelOpacity = 0.8f;

            /// <summary>The subtitle's opacity, as WinUI3's subtitle style has it.</summary>
            public const float SubtitleOpacity = 0.85f;

            /// <summary>A list element header's opacity, as WinUI3's has it.</summary>
            public const float ListElementHeaderOpacity = 0.85f;

            /// <summary>The remove glyph's opacity, as WinUI3's delete button has it.</summary>
            public const float RemoveGlyphOpacity = 0.7f;

            /// <summary>The space above and below a row's name and editor.</summary>
            public const float RowVerticalPadding = 8f;

            /// <summary>The space between a row's name (and subtitle) and its editor under it.</summary>
            public const float LabelToEditorSpacing = 6f;

            /// <summary>The remove button's width and height, a comfortable touch target.</summary>
            public const float RemoveButtonSize = 44f;

            /// <summary>The space between a nullable editor's editor and its "Not set" and set or unset buttons.</summary>
            public const float NullableEditorSpacing = 8f;

            /// <summary>A nullable editor's editor opacity while there is no value.</summary>
            public const float NullableEditorDimmedOpacity = 0.4f;
        }
    }
}
