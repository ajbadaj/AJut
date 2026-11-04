namespace AJutShowRoomAppKit.UI
{
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;

    // The layout every control page shares, the same as the WinUI3 and WPF showrooms: a title, a
    // description, the live demo, then the code that built the demo. The page scrolls vertically.
    public class ShowroomPage : ThemedView, IShowroomPage
    {
        private const double kMaxContentWidth = 1000.0;
        private const double kPagePadding = 20.0;

        private readonly FillStack m_document;
        private readonly List<ShowroomCodeDisplay> m_codeDisplays = new List<ShowroomCodeDisplay>();

        // ===========[ Setup/Construction/Teardown ]===========================
        public ShowroomPage (string title, string description)
        {
            ShowroomStyle.EnsureRegistered();
            this.BackgroundColorKey = ShowroomStyle.PageBackground;

            var scroll = new NSScrollView
            {
                HasVerticalScroller = true,
                DrawsBackground = false,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            this.AddSubview(scroll);

            m_document = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 12.0)
            {
                Padding = new NSEdgeInsets((nfloat)kPagePadding, (nfloat)kPagePadding, (nfloat)kPagePadding, (nfloat)kPagePadding),
            };
            m_document.TranslatesAutoresizingMaskIntoConstraints = false;
            scroll.DocumentView = m_document;

            // The document is flipped (FillStack is), so it starts at the top of the scroll view
            // rather than the bottom. Its width follows the visible area, which is what makes the
            // scrolling vertical only.
            NSClipView clip = scroll.ContentView;
            NSLayoutConstraint maxWidth = m_document.WidthAnchor.ConstraintLessThanOrEqualTo((nfloat)kMaxContentWidth);
            NSLayoutConstraint fillWidth = m_document.WidthAnchor.ConstraintEqualTo(clip.WidthAnchor);
            fillWidth.Priority = 500;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                scroll.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                scroll.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                scroll.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                scroll.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_document.TopAnchor.ConstraintEqualTo(clip.TopAnchor),
                m_document.LeadingAnchor.ConstraintEqualTo(clip.LeadingAnchor),
                maxWidth,
                fillWidth,
            });

            m_document.Add(ShowroomStyle.Title(title));
            if (description != null)
            {
                m_document.Add(ShowroomStyle.Body(description));
            }
        }

        /// <summary>Lets go of everything the page hooked up: its code displays, then whatever the page itself added</summary>
        public void Teardown ()
        {
            foreach (ShowroomCodeDisplay codeDisplay in m_codeDisplays)
            {
                codeDisplay.Teardown();
            }

            m_codeDisplays.Clear();
            this.OnTeardown();
        }

        /// <summary>Unhooks what a page added on top of the shared layout</summary>
        protected virtual void OnTeardown ()
        {
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Adds a section heading</summary>
        protected void AddHeading (string text)
        {
            m_document.Add(ShowroomStyle.Heading(text));
        }

        /// <summary>Adds a paragraph of explanation</summary>
        protected void AddText (string text)
        {
            m_document.Add(ShowroomStyle.Body(text));
        }

        /// <summary>Adds a demo view, stretched to the page width unless an alignment says otherwise</summary>
        protected void AddDemo (NSView demo, eFillStackCrossAlignment alignment = eFillStackCrossAlignment.Stretch)
        {
            m_document.Add(demo, alignment: alignment);
        }

        /// <summary>A labeled row, label on the left, for laying variants of a control side by side</summary>
        protected void AddLabeledDemo (string label, NSView demo)
        {
            var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 12.0);
            NSTextField labelField = ShowroomStyle.Body(label);
            labelField.TranslatesAutoresizingMaskIntoConstraints = false;
            labelField.WidthAnchor.ConstraintEqualTo((nfloat)180).Active = true;
            row.Add(labelField, alignment: eFillStackCrossAlignment.Center);
            // Not a grower: most demos have a size of their own, and a grower is stretched to the row's end
            row.Add(demo, alignment: eFillStackCrossAlignment.Center);
            m_document.Add(row);
        }

        /// <summary>Adds the code display, showing the code that built the demo above it</summary>
        protected void AddCode (string code)
        {
            var codeDisplay = new ShowroomCodeDisplay(code);
            m_codeDisplays.Add(codeDisplay);
            m_document.Add(codeDisplay);
        }
    }
}
