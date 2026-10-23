namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Theming;
    using CoreGraphics;
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

    public class EnumComboBox : UIControl
    {
        // ===========[ Instance Fields ]===================================
        private readonly UIButton m_button = new UIButton(CGRect.Empty) { TranslatesAutoresizingMaskIntoConstraints = false };
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
            const float kCornerRadius = 6f;
            const float kVerticalPadding = 7f;
            const float kHorizontalPadding = 12f;

            UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
            configuration.Image = UIImage.GetSystemImage("chevron.up.chevron.down");
            configuration.ImagePlacement = NSDirectionalRectEdge.Trailing;
            configuration.ImagePadding = kHorizontalPadding / 2f;
            configuration.ContentInsets = new NSDirectionalEdgeInsets(kVerticalPadding, kHorizontalPadding, kVerticalPadding, kHorizontalPadding);
            configuration.BaseForegroundColor = ThemedColors.Get(ThemedColorKeys.ComboBoxForeground);
            UIBackgroundConfiguration background = configuration.Background;
            background.BackgroundColor = ThemedColors.Get(ThemedColorKeys.ComboBoxBackground);
            background.CornerRadius = kCornerRadius;
            configuration.Background = background;
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

        /// <summary>The button the menu hangs off, for anything this control does not surface (its configuration, font).</summary>
        public UIButton Button => m_button;

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
    }
}
