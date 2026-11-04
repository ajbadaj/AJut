namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Theming;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// A busy indicator: the platform's own spinner beside a line of text, the way WPF's BusyWait puts its spinning glyph
    /// beside its content. <see cref="IsSpinning"/> turns it on and off, and <see cref="SpinnerDock"/> puts the spinner on
    /// any side of the text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As an overlay (<see cref="IsOverlay"/>) it is laid over what it covers, filling the same space. While spinning it
    /// fills that space with the blockout fill and takes every touch, so nothing under it can be used, with the spinner and
    /// text in its middle; stopped, it draws nothing and lets touches through. So an overlay can sit over a page for good
    /// and be switched on while the page is busy, the way WPF's stack navigation shows its busy overlay.
    /// </para>
    /// <para>
    /// Stopped inline, the spinner is hidden but keeps its room, so the text does not move when it starts again. The
    /// spinner is UIActivityIndicatorView's large style, scaled to <see cref="SpinnerSize"/>.
    /// </para>
    /// </remarks>
    public class BusyWait : UIView
    {
        // The large spinner's own width and height, which the scale to SpinnerSize is taken from
        private const float kLargeSpinnerSize = 37f;

        // The unthemed overlay, WPF's stack navigation overlay unthemed: a dark fill, and light gray text over it
        private const float kUnthemedOverlayFillOpacity = 0.75f;
        private const float kUnthemedOverlayTextWhite = 0.867f;

        private readonly UIActivityIndicatorView m_spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { HidesWhenStopped = false, TranslatesAutoresizingMaskIntoConstraints = false };
        private readonly UIView m_spinnerBox = new UIView { TranslatesAutoresizingMaskIntoConstraints = false, UserInteractionEnabled = false };
        private readonly UILabel m_label = new UILabel { Lines = 0, TextAlignment = UITextAlignment.Center };
        private readonly UIStackView m_stack = new UIStackView { Alignment = UIStackViewAlignment.Center, TranslatesAutoresizingMaskIntoConstraints = false };
        private UIColor m_unthemedSpinnerColor;
        private UIColor m_backgroundBeforeOverlay;
        private NSLayoutConstraint m_spinnerWidth;
        private NSLayoutConstraint m_spinnerHeight;
        private NSLayoutConstraint[] m_insetConstraints;
        private bool m_isSpinning = true;
        private bool m_isOverlay;
        private bool m_isShowingOverlay;
        private float m_spinnerSize = MobileLook.BusyWait.SpinnerSize;
        private eBusyWaitSpinnerDock m_spinnerDock = eBusyWaitSpinnerDock.Right;

        // ===========[ Setup/Construction/Teardown ]===================================
        public BusyWait () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native view.</summary>
        protected BusyWait (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        private void Setup ()
        {
            m_unthemedSpinnerColor = m_spinner.Color;
            this.UserInteractionEnabled = false;

            // The spinner is centered in a box of SpinnerSize, and scaled to fill it, so the stack lays out the box
            m_spinnerBox.AddSubview(m_spinner);
            m_spinnerWidth = m_spinnerBox.WidthAnchor.ConstraintEqualTo(m_spinnerSize);
            m_spinnerHeight = m_spinnerBox.HeightAnchor.ConstraintEqualTo(m_spinnerSize);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_spinnerWidth,
                m_spinnerHeight,
                m_spinner.CenterXAnchor.ConstraintEqualTo(m_spinnerBox.CenterXAnchor),
                m_spinner.CenterYAnchor.ConstraintEqualTo(m_spinnerBox.CenterYAnchor),
            });

            this.AddSubview(m_stack);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_stack.CenterXAnchor.ConstraintEqualTo(this.CenterXAnchor),
                m_stack.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
            });

            this.ArrangeStack();
            this.ApplyLook();
            this.ApplySpinning();
        }

        // ===========[ Properties ]===================================
        /// <summary>Whether the spinner is spinning. On by default, as on WPF.</summary>
        public bool IsSpinning
        {
            get => m_isSpinning;
            set
            {
                if (m_isSpinning != value)
                {
                    m_isSpinning = value;
                    this.ApplySpinning();
                }
            }
        }

        /// <summary>
        /// Whether this covers what is under it while spinning: the blockout fill, every touch taken, and the spinner and
        /// text in the middle. Stopped, an overlay draws nothing and lets touches through. Off by default.
        /// </summary>
        public bool IsOverlay
        {
            get => m_isOverlay;
            set
            {
                if (m_isOverlay != value)
                {
                    m_isOverlay = value;
                    this.ArrangeStack();
                    this.ApplyLook();
                    this.ApplySpinning();
                }
            }
        }

        /// <summary>Which side of the text the spinner sits on. Right by default, as on WPF.</summary>
        public eBusyWaitSpinnerDock SpinnerDock
        {
            get => m_spinnerDock;
            set
            {
                if (m_spinnerDock != value)
                {
                    m_spinnerDock = value;
                    this.ArrangeStack();
                }
            }
        }

        /// <summary>The spinner's width and height, in points</summary>
        public float SpinnerSize
        {
            get => m_spinnerSize;
            set
            {
                if (m_spinnerSize != value)
                {
                    m_spinnerSize = value;
                    this.ArrangeStack();
                }
            }
        }

        /// <summary>The text beside the spinner. Null or empty shows the spinner alone.</summary>
        public string Text
        {
            get => m_label.Text;
            set
            {
                m_label.Text = value;
                this.ArrangeStack();
            }
        }

        /// <summary>The label, for styling the text the way the app styles its other text</summary>
        public UILabel TextLabel => m_label;

        // ===========[ Helper Methods ]===================================
        // Left and Right lay the spinner and text out in a row, Top and Bottom in a column. The stack sits in the middle,
        // and hugs it, kept clear of the edges by the overlay's padding (none inline). The hugging is low priority, so a
        // BusyWait given more room than it needs keeps the stack in its middle.
        private void ArrangeStack ()
        {
            bool isRow = (m_spinnerDock == eBusyWaitSpinnerDock.Left) || (m_spinnerDock == eBusyWaitSpinnerDock.Right);
            bool isSpinnerFirst = (m_spinnerDock == eBusyWaitSpinnerDock.Left) || (m_spinnerDock == eBusyWaitSpinnerDock.Top);
            bool hasText = !string.IsNullOrEmpty(m_label.Text);

            foreach (UIView arranged in m_stack.ArrangedSubviews)
            {
                m_stack.RemoveArrangedSubview(arranged);
                arranged.RemoveFromSuperview();
            }

            m_stack.Axis = isRow ? UILayoutConstraintAxis.Horizontal : UILayoutConstraintAxis.Vertical;
            m_stack.Spacing = hasText ? MobileLook.BusyWait.SpinnerSpacing : 0f;
            m_label.Hidden = !hasText;
            m_stack.AddArrangedSubview(isSpinnerFirst ? m_spinnerBox : m_label);
            m_stack.AddArrangedSubview(isSpinnerFirst ? m_label : m_spinnerBox);

            m_spinnerWidth.Constant = m_spinnerSize;
            m_spinnerHeight.Constant = m_spinnerSize;
            nfloat scale = m_spinnerSize / kLargeSpinnerSize;
            m_spinner.Transform = CGAffineTransform.MakeScale(scale, scale);

            if (m_insetConstraints != null)
            {
                NSLayoutConstraint.DeactivateConstraints(m_insetConstraints);
            }

            float padding = m_isOverlay ? MobileLook.BusyWait.OverlayPadding : 0f;
            m_insetConstraints = new[]
            {
                m_stack.LeadingAnchor.ConstraintGreaterThanOrEqualTo(this.LeadingAnchor, padding),
                m_stack.TopAnchor.ConstraintGreaterThanOrEqualTo(this.TopAnchor, padding),
                _Hug(m_stack.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, padding)),
                _Hug(m_stack.TopAnchor.ConstraintEqualTo(this.TopAnchor, padding)),
            };

            NSLayoutConstraint.ActivateConstraints(m_insetConstraints);

            static NSLayoutConstraint _Hug (NSLayoutConstraint constraint)
            {
                constraint.Priority = (float)UILayoutPriority.DefaultLow;
                return constraint;
            }
        }

        // Unthemed and inline, the spinner keeps the platform's own color, as a stock spinner beside it would. Themed, it
        // takes the text color, as WPF's spinner takes its foreground, and as an overlay it always takes the overlay's.
        private void ApplyLook ()
        {
            UIColor foreground = m_isOverlay
                ? (ThemedColors.Get(MobileLook.BusyWait.OverlayForeground) ?? UIColor.FromWhiteAlpha(kUnthemedOverlayTextWhite, 1f))
                : (ThemedColors.Get(MobileLook.BusyWait.Foreground) ?? UIColor.Label);

            m_label.TextColor = foreground;
            m_spinner.Color = (m_isOverlay || ThemedColors.IsThemeLoaded) ? foreground : m_unthemedSpinnerColor;
        }

        // A spinning overlay fills itself and takes touches (a view that takes touches keeps every one that lands on it from
        // what is under it). The background it replaced is put back when it stops, so a background the app gave an inline
        // BusyWait stays.
        private void ApplySpinning ()
        {
            bool showsOverlay = m_isOverlay && m_isSpinning;
            if (showsOverlay && !m_isShowingOverlay)
            {
                m_backgroundBeforeOverlay = this.BackgroundColor;
                this.UserInteractionEnabled = true;
            }
            else if (!showsOverlay && m_isShowingOverlay)
            {
                this.BackgroundColor = m_backgroundBeforeOverlay;
                this.UserInteractionEnabled = false;
                m_backgroundBeforeOverlay = null;
            }

            if (showsOverlay)
            {
                this.BackgroundColor = ThemedColors.Get(MobileLook.BusyWait.OverlayBackground) ?? UIColor.Black.ColorWithAlpha(kUnthemedOverlayFillOpacity);
            }

            m_isShowingOverlay = showsOverlay;

            m_stack.Hidden = m_isOverlay && !m_isSpinning;
            m_spinner.Hidden = !m_isOverlay && !m_isSpinning;
            if (m_isSpinning)
            {
                m_spinner.StartAnimating();
            }
            else
            {
                m_spinner.StopAnimating();
            }
        }
    }
}
