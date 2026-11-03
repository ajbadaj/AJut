namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using CoreAnimation;
    using CoreGraphics;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Foundation;
    using ObjCRuntime;
    using UIKit;

    // ===========[ EnumComboBox ]===============================================
    // UIKit enum picker: a button showing the selected value that opens a menu of the enum's values, with the
    // selected one checked. UIKit has no combo box; a pull-down menu on a button is its equivalent, and the name is
    // kept so code reads the same on every stack.
    //
    // Like WinUI3's, it infers the options from SelectedItem, which must be an enum value: setting a value of a new
    // enum type rebuilds the menu. The options are AJut.UX's EnumSelectionOptions, so a value marked
    // [Browsable(false)] or [ExcludeFromSelection] is left out, the same as on every other stack. Picking a value
    // sends ValueChanged; a SelectedItem set from code does not.
    //
    // With the AJut theme loaded it takes MobileLook.EnumComboBox's look, which Android's EnumComboBox draws too: a face on
    // the pop-out surface outlined with the text box border, and a down chevron in the symbol color; the hover fill and
    // outline under an iPad pointer, the pressed fill and outline while held, and the flat disabled look while disabled.
    // The menu itself is the system's, so it stays the system's look. Unthemed, the button keeps the system fill.

    public class EnumComboBox : UIControl
    {
        // Only painted with the AJut theme loaded, so the literals are what a key the palette lacks falls back to
        private static readonly ThemedGradient kSurface = new ThemedGradient(
            MobileLook.EnumComboBox.Surface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD), ThemedGradient.Stop(1.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B), ThemedGradient.Stop(1.0, 0xFF2B2B2B) }
        );

        // ===========[ Instance Fields ]===================================
        private readonly ComboButton m_button = new ComboButton { TranslatesAutoresizingMaskIntoConstraints = false };
        private readonly CAGradientLayer m_surface = new CAGradientLayer
        {
            StartPoint = new CGPoint(0.5, 0.0),
            EndPoint = new CGPoint(0.5, 1.0),
            ZPosition = -1,
            MasksToBounds = true,
        };
        private Type m_itemType;
        private IReadOnlyList<Enum> m_options = Array.Empty<Enum>();
        private object m_selectedItem;

        // ===========[ Setup/Construction/Teardown ]===================================
        public EnumComboBox () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native control.</summary>
        protected EnumComboBox (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        private void Setup ()
        {
            // Unthemed the button keeps UIKit's own rounding and pop-up chevron, among the system's other pull-downs
            const float kUnthemedCornerRadius = 6f;

            bool isThemed = ThemedColors.IsThemeLoaded;
            float paddingHorizontal = MobileLook.EnumComboBox.PaddingHorizontal;
            float paddingVertical = MobileLook.EnumComboBox.PaddingVertical;
            UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
            configuration.Image = UIImage.GetSystemImage(isThemed ? "chevron.down" : "chevron.up.chevron.down");
            configuration.ImagePlacement = NSDirectionalRectEdge.Trailing;
            configuration.ImagePadding = paddingHorizontal / 2f;
            configuration.ContentInsets = new NSDirectionalEdgeInsets(paddingVertical, paddingHorizontal, paddingVertical, paddingHorizontal);
            configuration.BaseForegroundColor = ThemedColors.Get(MobileLook.EnumComboBox.Text);
            UIBackgroundConfiguration background = configuration.Background;
            background.BackgroundColor = isThemed ? UIColor.Clear : ThemedColors.Get(ThemedColorKeys.ComboBoxBackground);
            background.CornerRadius = isThemed ? MobileLook.EnumComboBox.CornerRadius : kUnthemedCornerRadius;
            configuration.Background = background;
            if (isThemed)
            {
                // The face is drawn behind the button, which is clear over it, and outlined on this control's own layer
                this.Layer.CornerRadius = MobileLook.EnumComboBox.CornerRadius;
                this.Layer.BorderWidth = MobileLook.EnumComboBox.BorderThickness;
                m_surface.CornerRadius = MobileLook.EnumComboBox.CornerRadius;
                this.Layer.InsertSublayer(m_surface, 0);
            }

            m_button.Configuration = configuration;
            m_button.ShowsMenuAsPrimaryAction = true;
            m_button.HorizontalAlignment = UIControlContentHorizontalAlignment.Leading;

            this.AddSubview(m_button);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_button.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_button.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_button.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_button.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
            });
        }

        // ===========[ Properties ]===================================
        /// <summary>The selected enum value. Its type decides the options; setting it from code does not send ValueChanged.</summary>
        public object SelectedItem
        {
            get => m_selectedItem;
            set
            {
                m_selectedItem = value;
                this.Refresh();
            }
        }

        /// <summary>The values offered, in order, as Android's EnumComboBox gives them.</summary>
        public IReadOnlyList<Enum> Options => m_options;

        /// <summary>Whether it takes touches; passed on to the button the menu hangs off, which a UIControl's own Enabled does not reach.</summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                m_button.Enabled = value;
            }
        }

        /// <summary>The button the menu hangs off, for anything this control does not surface (its configuration, font).</summary>
        public UIButton Button => m_button;

        // ===========[ Public Interface Methods ]===================================
        public override void LayoutSubviews ()
        {
            base.LayoutSubviews();
            if (!ThemedColors.IsThemeLoaded)
            {
                return;
            }

            // The border and the face's colors are CGColors, which freeze a dynamic color, so they are resolved here: UIKit
            // calls LayoutSubviews again whenever the appearance (light/dark) changes, and the button asks for it again
            // whenever its state does
            string borderKey = MobileLook.EnumComboBox.BorderFor(this.Enabled, m_button.Highlighted, m_button.Hovered);
            this.Layer.BorderColor = ThemedColors.Get(borderKey).GetResolvedColor(this.TraitCollection).CGColor;
            IReadOnlyList<AJutGradientStop> stops = kSurface.Resolve(this.TraitCollection.UserInterfaceStyle);
            var colors = new CGColor[stops.Count];
            var locations = new NSNumber[stops.Count];
            for (int index = 0; index < stops.Count; ++index)
            {
                colors[index] = stops[index].Color.ToUIColor().CGColor;
                locations[index] = new NSNumber(stops[index].Offset);
            }

            CATransaction.Begin();
            CATransaction.DisableActions = true;
            m_surface.Frame = this.Bounds;
            m_surface.Colors = colors;
            m_surface.Locations = locations;
            CATransaction.Commit();
        }

        // ===========[ Helper Methods ]===================================
        private void Refresh ()
        {
            if (m_selectedItem is not Enum selected)
            {
                return;
            }

            if (selected.GetType() != m_itemType)
            {
                this.RebuildOptions(selected);
            }

            // The menu is rebuilt for every selection so the check mark sits on the selected value
            var actions = new UIMenuElement[m_options.Count];
            for (int index = 0; index < m_options.Count; ++index)
            {
                actions[index] = PickTarget.CreateAction(this, m_options[index], m_options[index].Equals(selected));
            }

            m_button.Menu = UIMenu.Create(actions);

            UIButtonConfiguration configuration = m_button.Configuration;
            configuration.Title = EnumSelectionOptions.GetDisplayName(selected);
            m_button.Configuration = configuration;
        }

        private void RebuildOptions (Enum valueOfTheNewType)
        {
            // This takes a value rather than its Type, and calls GetType() on the parameter, so it stays clean under
            // trimming without a suppression. GetSelectableValues reads the enum's public static fields (one per value,
            // with their attributes), so its parameter asks for PublicFields to be kept, and GetType() on a value typed
            // Enum meets that, since the trimmer keeps every field of an enum type it keeps
            // (MarkStep.MarkImplicitlyUsedFields), and a live value's type is kept.
            // Where GetType() is called matters. The Roslyn analyzer accepts it on an Enum typed local as well (a pattern
            // match, say), but ILLink, which trims the app, flags that form IL2072 and passes this one, on an Enum typed
            // parameter, the form EnumToggleStrip.RebuildItems uses too.
            // https://github.com/dotnet/runtime/blob/main/src/tools/illink/src/ILLink.RoslynAnalyzer/TrimAnalysis/HandleCallAction.cs
            Type enumType = valueOfTheNewType.GetType();
            m_itemType = enumType;
            m_options = new List<Enum>(EnumSelectionOptions.GetSelectableValues(enumType));
        }

        private void OnPicked (Enum value)
        {
            if (value.Equals(m_selectedItem))
            {
                return;
            }

            m_selectedItem = value;
            this.Refresh();
            this.SendActionForControlEvents(UIControlEvent.ValueChanged);
        }

        // ===========[ Subclasses/structs ]===================================
        // The menu's actions are held by the menu, which the button holds natively, so each action's handler holds the
        // combo box weakly; a strong reference back would tie them into a cycle across native objects.
        private sealed class PickTarget
        {
            private readonly WeakReference<EnumComboBox> m_owner;
            private readonly Enum m_value;

            private PickTarget (EnumComboBox owner, Enum value)
            {
                m_owner = new WeakReference<EnumComboBox>(owner);
                m_value = value;
            }

            public static UIAction CreateAction (EnumComboBox owner, Enum value, bool isSelected)
            {
                UIAction action = UIAction.Create(EnumSelectionOptions.GetDisplayName(value), null, null, new PickTarget(owner, value).Run);
                action.State = isSelected ? UIMenuElementState.On : UIMenuElementState.Off;
                return action;
            }

            private void Run (UIAction action)
            {
                if (m_owner.TryGetTarget(out EnumComboBox owner))
                {
                    owner.OnPicked(m_value);
                }
            }
        }

        // The button the menu hangs off. With the AJut theme loaded it is clear over the face at rest, so the face's
        // gradient shows, and takes the one-color fill MobileLook gives any other state (hover, held, disabled), as
        // Android's face does. Its text and chevron follow the state too. base.UpdateConfiguration is skipped then so the
        // system lays no dimmed highlight of its own over it. Unthemed it is a stock button.
        private sealed class ComboButton : UIButton
        {
            public ComboButton () : base(CGRect.Empty)
            {
            }

            public override void UpdateConfiguration ()
            {
                if (!ThemedColors.IsThemeLoaded)
                {
                    base.UpdateConfiguration();
                    return;
                }

                bool isEnabled = this.Enabled;
                bool isPressed = this.Highlighted;
                bool isPointerOver = this.Hovered;
                UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
                UIBackgroundConfiguration background = configuration.Background;
                string surfaceKey = MobileLook.EnumComboBox.SurfaceFor(isEnabled, isPressed, isPointerOver);
                background.BackgroundColor = (surfaceKey == MobileLook.EnumComboBox.Surface) ? UIColor.Clear : ThemedColors.Get(surfaceKey);
                configuration.Background = background;
                configuration.BaseForegroundColor = ThemedColors.Get(MobileLook.EnumComboBox.TextFor(isEnabled, isPressed, isPointerOver));

                // The chevron is the configuration's image, which takes the text's color unless it carries its own
                UIColor chevronColor = ThemedColors.Get(MobileLook.EnumComboBox.ChevronFor(isEnabled));
                configuration.Image = UIImage.GetSystemImage("chevron.down")?.ApplyTintColor(chevronColor, UIImageRenderingMode.AlwaysOriginal);
                this.Configuration = configuration;

                // The outline is on the combo box's own layer, which works it out for this state as it lays out
                this.Superview?.SetNeedsLayout();

                // Themed, keyboard focus shows as AJut's dotted rectangle in place of the system's (see FocusVisual)
                FocusVisual.TakeOver(this);
            }

            public override void DidUpdateFocus (UIFocusUpdateContext context, UIFocusAnimationCoordinator coordinator)
            {
                base.DidUpdateFocus(context, coordinator);
                if (ThemedColors.IsThemeLoaded)
                {
                    FocusVisual.Update(this);
                }
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews();
                if (ThemedColors.IsThemeLoaded)
                {
                    FocusVisual.Update(this);
                }
            }
        }
    }
}
