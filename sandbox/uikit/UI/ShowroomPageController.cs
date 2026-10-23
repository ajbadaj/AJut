namespace AJutShowRoomUIKit.UI
{
    using UIKit;

    /// <summary>
    /// The base for every showroom page: a scrolling column that a page fills top to bottom with section headings,
    /// a line of explanation, the live demo, and the code that built it, so each demo carries its own usage the
    /// way the XAML showrooms show markup beside theirs.
    /// </summary>
    public abstract class ShowroomPageController : UIViewController
    {
        private const float kSectionSpacing = 12f;
        private readonly UIStackView m_column = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = kSectionSpacing,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };

        protected ShowroomPageController (string title)
        {
            this.Title = title;
        }

        public override void LoadView ()
        {
            const float kMargin = 16f;

            var scroller = new UIScrollView
            {
                BackgroundColor = ShowroomSettings.PageBackground,
                AlwaysBounceVertical = true,
            };
            scroller.AddSubview(m_column);
            this.View = scroller;

            UILayoutGuide content = scroller.ContentLayoutGuide;
            UILayoutGuide frame = scroller.FrameLayoutGuide;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_column.TopAnchor.ConstraintEqualTo(content.TopAnchor, kMargin),
                m_column.BottomAnchor.ConstraintEqualTo(content.BottomAnchor, -kMargin),
                m_column.LeadingAnchor.ConstraintEqualTo(frame.LeadingAnchor, kMargin),
                m_column.TrailingAnchor.ConstraintEqualTo(frame.TrailingAnchor, -kMargin),
            });

            this.BuildPage();
        }

        /// <summary>Fill the page with <see cref="AddHeading"/>, <see cref="AddText"/>, <see cref="AddDemo"/> and <see cref="AddCode"/>, in reading order.</summary>
        protected abstract void BuildPage ();

        protected void AddHeading (string text)
        {
            m_column.AddArrangedSubview(new UILabel
            {
                Text = text,
                Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Title3),
                Lines = 0,
            });
        }

        protected void AddText (string text)
        {
            m_column.AddArrangedSubview(new UILabel
            {
                Text = text,
                Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body),
                TextColor = UIColor.SecondaryLabel,
                Lines = 0,
            });
        }

        /// <summary>Adds a demo view at its natural size, leading aligned, so a button is not stretched across the page.</summary>
        protected void AddDemo (UIView demo)
        {
            var row = new UIStackView(new[] { demo, new UIView() })
            {
                Axis = UILayoutConstraintAxis.Horizontal,
                Alignment = UIStackViewAlignment.Center,
            };
            m_column.AddArrangedSubview(row);
        }

        /// <summary>Adds a row of demo views side by side.</summary>
        protected void AddDemoRow (params UIView[] demos)
        {
            const float kRowSpacing = 8f;

            var row = new UIStackView(demos)
            {
                Axis = UILayoutConstraintAxis.Horizontal,
                Alignment = UIStackViewAlignment.Center,
                Spacing = kRowSpacing,
            };
            row.AddArrangedSubview(new UIView());
            m_column.AddArrangedSubview(row);
        }

        /// <summary>Adds a view across the page's full width, such as a row of samples that share it evenly.</summary>
        protected void AddWide (UIView view)
        {
            m_column.AddArrangedSubview(view);
        }

        protected void AddCode (string code)
        {
            m_column.AddArrangedSubview(new ShowroomCodeDisplay(code));
        }
    }
}
