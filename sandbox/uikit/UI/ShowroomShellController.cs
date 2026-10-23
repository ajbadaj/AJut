namespace AJutShowRoomUIKit.UI
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using UIKit;
    using eThemeMode = ShowroomSettings.eThemeMode;

    /// <summary>
    /// The showroom window's content: the theme bar across the top, on every page, so any page can be judged in any look,
    /// and the navigation stack of areas and pages under it. Dark, Light, and System set the window's light or dark,
    /// which every control follows as it is. The AJut theme and touch switches change the colors controls are made with,
    /// so they rebuild the showroom, back on the page that was showing.
    /// </summary>
    public sealed class ShowroomShellController : UIViewController
    {
        private readonly UINavigationController m_navigation;
        private readonly EnumToggleStrip m_modeStrip = new EnumToggleStrip();
        private readonly UISwitch m_themeSwitch = new UISwitch();
        private readonly UISwitch m_touchSwitch = new UISwitch();
        private readonly UIStackView m_barLayout = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false,
        };

        // ===========[ Setup/Construction/Teardown ]===================================
        public ShowroomShellController (UINavigationController navigation)
        {
            m_navigation = navigation;
        }

        public override void LoadView ()
        {
            const float kBarMargin = 16f;
            const float kBarPadding = 8f;
            const float kGroupSpacing = 8f;
            const float kSwitchSpacing = 6f;
            const float kPairSpacing = 14f;
            const float kSeparatorThickness = 1f;

            this.View = new UIView { BackgroundColor = ShowroomSettings.PageBackground };

            m_modeStrip.SelectedItem = ShowroomSettings.ThemeMode;
            m_modeStrip.Strip.EnsureUniformSize = true;
            m_modeStrip.AccessibilityLabel = "Light or dark";

            var switches = new UIStackView(new UIView[] { _Label("AJut"), m_themeSwitch, _Label("Touch"), m_touchSwitch })
            {
                Axis = UILayoutConstraintAxis.Horizontal,
                Alignment = UIStackViewAlignment.Center,
                Spacing = kSwitchSpacing,
            };
            switches.SetCustomSpacing(kPairSpacing, m_themeSwitch);

            m_barLayout.Spacing = kGroupSpacing;
            m_barLayout.AddArrangedSubview(m_modeStrip);
            m_barLayout.AddArrangedSubview(new UIView());
            m_barLayout.AddArrangedSubview(switches);
            this.View.AddSubview(m_barLayout);

            var separator = new UIView { BackgroundColor = UIColor.Separator, TranslatesAutoresizingMaskIntoConstraints = false };
            this.View.AddSubview(separator);

            this.AddChildViewController(m_navigation);
            UIView pages = m_navigation.View;
            pages.TranslatesAutoresizingMaskIntoConstraints = false;
            this.View.AddSubview(pages);
            m_navigation.DidMoveToParentViewController(this);

            UILayoutGuide safeArea = this.View.SafeAreaLayoutGuide;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_barLayout.TopAnchor.ConstraintEqualTo(safeArea.TopAnchor, kBarPadding),
                m_barLayout.LeadingAnchor.ConstraintEqualTo(safeArea.LeadingAnchor, kBarMargin),
                m_barLayout.TrailingAnchor.ConstraintEqualTo(safeArea.TrailingAnchor, -kBarMargin),
                separator.TopAnchor.ConstraintEqualTo(m_barLayout.BottomAnchor, kBarPadding),
                separator.LeadingAnchor.ConstraintEqualTo(this.View.LeadingAnchor),
                separator.TrailingAnchor.ConstraintEqualTo(this.View.TrailingAnchor),
                separator.HeightAnchor.ConstraintEqualTo(kSeparatorThickness),
                pages.TopAnchor.ConstraintEqualTo(separator.BottomAnchor),
                pages.LeadingAnchor.ConstraintEqualTo(this.View.LeadingAnchor),
                pages.TrailingAnchor.ConstraintEqualTo(this.View.TrailingAnchor),
                pages.BottomAnchor.ConstraintEqualTo(this.View.BottomAnchor),
            });

            this.RefreshBar();

            static UILabel _Label (string text)
            {
                return new UILabel
                {
                    Text = text,
                    Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Subheadline),
                    TextColor = UIColor.Label,
                };
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);

            // The bar's controls are this controller's views, so a handler of this controller held by them is a cycle
            // across native objects; they are only followed while the shell is showing, the way the pages do it
            m_modeStrip.ValueChanged -= this.ModeStrip_OnValueChanged;
            m_modeStrip.ValueChanged += this.ModeStrip_OnValueChanged;
            m_themeSwitch.ValueChanged -= this.ThemeSwitch_OnValueChanged;
            m_themeSwitch.ValueChanged += this.ThemeSwitch_OnValueChanged;
            m_touchSwitch.ValueChanged -= this.TouchSwitch_OnValueChanged;
            m_touchSwitch.ValueChanged += this.TouchSwitch_OnValueChanged;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_modeStrip.ValueChanged -= this.ModeStrip_OnValueChanged;
            m_themeSwitch.ValueChanged -= this.ThemeSwitch_OnValueChanged;
            m_touchSwitch.ValueChanged -= this.TouchSwitch_OnValueChanged;
        }

        // ===========[ Events ]===================================
        private void ModeStrip_OnValueChanged (object sender, EventArgs e)
        {
            if (m_modeStrip.SelectedItem is not eThemeMode mode)
            {
                return;
            }

            bool wasTouchFocused = ThemedColors.IsTouchFocused;
            ShowroomSettings.ThemeMode = mode;
            UIWindow window = this.View.Window;
            if (window != null)
            {
                window.OverrideUserInterfaceStyle = ShowroomSettings.InterfaceStyle;
            }

            // Light and dark need nothing rebuilt, but System takes iOS's touch answer, which can change the palette
            if (ThemedColors.IsThemeLoaded && (ThemedColors.IsTouchFocused != wasTouchFocused))
            {
                this.RebuildShowroom();
                return;
            }

            this.RefreshBar();
        }

        private void ThemeSwitch_OnValueChanged (object sender, EventArgs e)
        {
            ThemedColors.IsThemeLoaded = m_themeSwitch.On;
            this.RebuildShowroom();
        }

        private void TouchSwitch_OnValueChanged (object sender, EventArgs e)
        {
            bool wasTouchFocused = ThemedColors.IsTouchFocused;
            ShowroomSettings.IsTouchFocused = m_touchSwitch.On;
            if (ThemedColors.IsTouchFocused != wasTouchFocused)
            {
                this.RebuildShowroom();
                return;
            }

            this.RefreshBar();
        }

        // ===========[ Public Interface Methods ]===================================
        public override void ViewDidLayoutSubviews ()
        {
            const float kOneRowWidth = 480f;

            base.ViewDidLayoutSubviews();

            // A phone held upright has no room for the strip and both switches on one line, so they take two there
            bool isOneRow = this.View.Bounds.Width >= kOneRowWidth;
            UILayoutConstraintAxis axis = isOneRow ? UILayoutConstraintAxis.Horizontal : UILayoutConstraintAxis.Vertical;
            if (m_barLayout.Axis != axis)
            {
                m_barLayout.Axis = axis;
                m_barLayout.Alignment = isOneRow ? UIStackViewAlignment.Center : UIStackViewAlignment.Leading;

                // This pass has already placed the bar at its old height, and a new axis changes only the stack's own
                // constraints, so nothing else asks for the pass that moves the separator and pages to fit it
                this.View.SetNeedsLayout();
            }
        }

        // ===========[ Helper Methods ]===================================
        private void RefreshBar ()
        {
            m_modeStrip.SelectedItem = ShowroomSettings.ThemeMode;
            m_themeSwitch.On = ThemedColors.IsThemeLoaded;
            m_touchSwitch.On = ShowroomSettings.IsTouchFocused;
            m_touchSwitch.Enabled = ShowroomSettings.CanChooseTouch;
        }

        // Controls read their colors when they are made, so a theme or touch switch makes the showroom again, the same as
        // restarting an app with the theme merged in or not, and opens the same pages on top
        private void RebuildShowroom ()
        {
            UIWindow window = this.View.Window;
            if (window != null)
            {
                window.RootViewController = ShowroomRoot.Create(ShowroomRoot.PathOf(m_navigation));
            }
        }
    }
}
