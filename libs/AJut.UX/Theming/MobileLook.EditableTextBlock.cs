namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// EditableTextBlock: a label that turns into a text field to edit. WinUI3 has no EditableTextBlock, so the keys
        /// are WPF's: the label takes the normal text color, and while editing the text sits in a text box well with the
        /// focused text box's text color and the primary highlight as its selection. WPF's has no disabled look, so a
        /// disabled label takes the stock disabled text key every other text control uses. The empty message is the
        /// label's color, faded, and italic.
        /// </summary>
        public static class EditableTextBlock
        {
            // ===========[ Keys ]===================================
            /// <summary>The text while it shows as a label.</summary>
            public const string LabelText = "AJut_Brush_NormalText";

            /// <summary>The label's text while the control is disabled.</summary>
            public const string LabelTextDisabled = "TextControlForegroundDisabled";

            /// <summary>The well the text sits in while it is edited.</summary>
            public const string FieldBackground = "AJut_Brush_TextBoxBackground";

            /// <summary>The text while it is edited, and the caret.</summary>
            public const string FieldText = "AJut_Brush_InteractiveText";

            /// <summary>Selected text while editing, laid under the text at <see cref="SelectionOpacity"/> where the platform needs it see through.</summary>
            public const string FieldSelection = "AJut_Brush_PrimaryHighlight";

            // ===========[ Sizes ]===================================
            /// <summary>The empty message's opacity as a label, as on WPF.</summary>
            public const float EmptyMessageOpacity = 0.8f;

            /// <summary>The empty message's opacity behind an edit that started empty, as on WPF.</summary>
            public const float EditingEmptyMessageOpacity = 0.4f;

            /// <summary>The selection fill's opacity, WPF's text box selection opacity.</summary>
            public const float SelectionOpacity = 0.4f;

            /// <summary>
            /// The space around the text on either side, the same for the label and the field, so the text stays where it
            /// is when editing starts.
            /// </summary>
            public const float ContentHorizontalInset = 4f;

            /// <summary>The space above and below the text, the same for the label and the field.</summary>
            public const float ContentVerticalInset = 2f;

            /// <summary>The corners of the well while editing.</summary>
            public const float FieldCornerRadius = 4f;

            /// <summary>The control is never shorter than this, Apple's smallest comfortable touch target.</summary>
            public const float MinTouchHeight = 44f;
        }
    }
}
