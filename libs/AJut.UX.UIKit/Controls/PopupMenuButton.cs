namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using AJut.UX.Theming;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// A button that opens a menu of <see cref="MenuItems"/> when tapped, and shows itself checked while the menu is open,
    /// as WinUI3's PopupMenuButton does. The menu is UIKit's own pull-down menu, built from the items each time it opens:
    /// a separator divides groups, an item with Items of its own opens a submenu, and a checkable item shows its check.
    /// With the AJut theme loaded the button takes MobileLook.PopupMenuButton's look, which Android's PopupMenuButton
    /// draws too; unthemed it is a system bordered button. The menu itself is the system's in both.
    /// </summary>
    public class PopupMenuButton : UIButton
    {
        private bool m_isOpen;

        // ===========[ Setup/Construction/Teardown ]===================================
        public PopupMenuButton () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native button.</summary>
        protected PopupMenuButton (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        private void Setup ()
        {
            bool isThemed = ThemedColors.IsThemeLoaded;
            UIButtonConfiguration configuration = isThemed ? UIButtonConfiguration.PlainButtonConfiguration : UIButtonConfiguration.BorderedButtonConfiguration;
            configuration.Image = UIImage.GetSystemImage("chevron.down");
            configuration.ImagePlacement = NSDirectionalRectEdge.Trailing;
            configuration.ImagePadding = MobileLook.PopupMenuButton.ChevronSpacing;
            configuration.PreferredSymbolConfigurationForImage = UIImageSymbolConfiguration.Create(MobileLook.PopupMenuButton.ChevronWidth, UIImageSymbolWeight.Semibold);
            if (isThemed)
            {
                float paddingHorizontal = MobileLook.PopupMenuButton.PaddingHorizontal;
                float paddingVertical = MobileLook.PopupMenuButton.PaddingVertical;
                configuration.ContentInsets = new NSDirectionalEdgeInsets(paddingVertical, paddingHorizontal, paddingVertical, paddingHorizontal);

                // The chevron takes its own key in every state, as WinUI3's glyph does. Static, so the native
                //  configuration holds nothing that leads back here.
                configuration.ImageColorTransformer = static _ => ThemedColors.Get(MobileLook.PopupMenuButton.Chevron) ?? UIColor.Label;
            }

            this.Configuration = configuration;
            this.ShowsMenuAsPrimaryAction = true;
            this.Menu = UIMenu.Create(new UIMenuElement[] { UIDeferredMenuElement.CreateUncached(new MenuProvider(this).Provide) });
        }

        // ===========[ Events ]===================================
        /// <summary>Raised when the menu opens</summary>
        public event EventHandler<EventArgs> FlyoutOpened;

        /// <summary>Raised when the menu closes, whether an item was picked or not</summary>
        public event EventHandler<EventArgs> FlyoutClosed;

        // ===========[ Properties ]===================================
        /// <summary>The button's text, as WinUI3's ButtonContent shows it</summary>
        public string ButtonContent
        {
            get => this.Configuration?.Title;
            set
            {
                UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
                configuration.Title = value;
                this.Configuration = configuration;
            }
        }

        /// <summary>The menu's entries, read each time it opens, so a change while it is closed shows the next time</summary>
        public ObservableCollection<PopupMenuItem> MenuItems { get; } = new ObservableCollection<PopupMenuItem>();

        /// <summary>
        /// Whether the menu is open. Setting it false closes the menu. Setting it true opens the menu on iOS 17.4 and up,
        /// with the button in a window; before 17.4 UIKit has no way to open a button's menu from code, so only a tap does.
        /// </summary>
        public bool IsOpen
        {
            get => m_isOpen;
            set
            {
                if (value == m_isOpen)
                {
                    return;
                }

                if (!value)
                {
                    this.ContextMenuInteraction?.DismissMenu();
                }
                else if ((this.Window != null) && OperatingSystem.IsIOSVersionAtLeast(17, 4))
                {
                    this.PerformPrimaryAction();
                }
            }
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// UIKit menu elements for <paramref name="items"/>, the way this button's menu shows them: each group between
        /// separators drawn inline with a divider, an item with Items of its own as a submenu, a checkable item with its
        /// check, and an item that cannot be picked disabled. Picking one calls its <see cref="PopupMenuItem.Invoke"/>. For
        /// building any other UIKit menu (a bar button item's, say) from the same items. The actions hold their items
        /// weakly, so that a menu kept natively ties nothing into a cycle: keep the items, as this button's MenuItems does.
        /// </summary>
        public static UIMenuElement[] CreateMenuElements (IEnumerable<PopupMenuItem> items)
        {
            IReadOnlyList<IReadOnlyList<PopupMenuItem>> groups = PopupMenuItem.GroupBySeparators(items);
            if (groups.Count == 1)
            {
                return _ElementsOf(groups[0]);
            }

            var elements = new UIMenuElement[groups.Count];
            for (int index = 0; index < groups.Count; ++index)
            {
                elements[index] = UIMenu.Create(string.Empty, null, UIMenuIdentifier.None, UIMenuOptions.DisplayInline, _ElementsOf(groups[index]));
            }

            return elements;

            static UIMenuElement[] _ElementsOf (IReadOnlyList<PopupMenuItem> group)
            {
                var elements = new UIMenuElement[group.Count];
                for (int index = 0; index < group.Count; ++index)
                {
                    elements[index] = _ElementFor(group[index]);
                }

                return elements;
            }

            // A UIMenu has no disabled state, so a disabled item with a submenu shows as a disabled action instead
            static UIMenuElement _ElementFor (PopupMenuItem item)
            {
                if (item.HasSubmenu && item.IsEnabled)
                {
                    return UIMenu.Create(item.Text, CreateMenuElements(item.Items));
                }

                UIAction action = UIAction.Create(item.Text, null, null, new PickTarget(item).Run);
                if (!item.CanInvoke || item.HasSubmenu)
                {
                    action.Attributes = UIMenuElementAttributes.Disabled;
                }

                if (item.IsCheckable)
                {
                    action.State = item.IsChecked ? UIMenuElementState.On : UIMenuElementState.Off;
                }

                return action;
            }
        }

        public override void UpdateConfiguration ()
        {
            if (!ThemedColors.IsThemeLoaded)
            {
                base.UpdateConfiguration();
                return;
            }

            // base.UpdateConfiguration is skipped on purpose, as GeometryButton skips it: it would lay the system's own
            //  dimming and highlight over the state colors here
            Look look = this.GetLook();
            UIButtonConfiguration configuration = this.Configuration ?? UIButtonConfiguration.PlainButtonConfiguration;
            configuration.BaseForegroundColor = look.Text;
            UIBackgroundConfiguration background = configuration.Background;
            background.BackgroundColor = look.Fill;
            background.StrokeColor = look.Border;
            background.StrokeWidth = MobileLook.PopupMenuButton.BorderThickness;
            background.CornerRadius = MobileLook.PopupMenuButton.CornerRadius;
            configuration.Background = background;
            this.Configuration = configuration;
        }

        // The menu's interaction tells the button, its delegate, as the menu opens and closes; the checked look and
        //  IsOpen follow it
        public override void WillDisplayMenu (UIContextMenuInteraction interaction, UIContextMenuConfiguration configuration, IUIContextMenuInteractionAnimating animator)
        {
            base.WillDisplayMenu(interaction, configuration, animator);
            m_isOpen = true;
            this.SetNeedsUpdateConfiguration();
            this.FlyoutOpened?.Invoke(this, EventArgs.Empty);
        }

        public override void WillEnd (UIContextMenuInteraction interaction, UIContextMenuConfiguration configuration, IUIContextMenuInteractionAnimating animator)
        {
            base.WillEnd(interaction, configuration, animator);
            m_isOpen = false;
            this.SetNeedsUpdateConfiguration();
            this.FlyoutClosed?.Invoke(this, EventArgs.Empty);
        }

        // ===========[ Helper Methods ]===================================
        // MobileLook.PopupMenuButton's keys, which Android's PopupMenuButton draws too. Each resolves to a dynamic color, so
        //  a light or dark switch repaints it with nothing more done here.
        private Look GetLook ()
        {
            if (!this.Enabled)
            {
                return _Look(MobileLook.PopupMenuButton.DisabledFill, MobileLook.PopupMenuButton.DisabledText, MobileLook.PopupMenuButton.DisabledBorder);
            }

            if (m_isOpen)
            {
                if (this.Highlighted)
                {
                    return _Look(MobileLook.PopupMenuButton.OpenPressedFill, MobileLook.PopupMenuButton.OpenPressedText, MobileLook.PopupMenuButton.OpenPressedBorder);
                }

                return this.Hovered
                    ? _Look(MobileLook.PopupMenuButton.OpenPointerOverFill, MobileLook.PopupMenuButton.OpenPointerOverText, MobileLook.PopupMenuButton.OpenPointerOverBorder)
                    : _Look(MobileLook.PopupMenuButton.OpenFill, MobileLook.PopupMenuButton.OpenText, MobileLook.PopupMenuButton.OpenBorder);
            }

            if (this.Highlighted)
            {
                return _Look(MobileLook.PopupMenuButton.PressedFill, MobileLook.PopupMenuButton.PressedText, MobileLook.PopupMenuButton.PressedBorder);
            }

            return this.Hovered
                ? _Look(MobileLook.PopupMenuButton.PointerOverFill, MobileLook.PopupMenuButton.PointerOverText, MobileLook.PopupMenuButton.PointerOverBorder)
                : _Look(MobileLook.PopupMenuButton.Fill, MobileLook.PopupMenuButton.Text, MobileLook.PopupMenuButton.Border);

            static Look _Look (string fill, string text, string border)
            {
                return new Look(
                    ThemedColors.Get(fill) ?? UIColor.SystemFill,
                    ThemedColors.Get(text) ?? UIColor.Label,
                    ThemedColors.Get(border) ?? UIColor.Separator
                );
            }
        }

        // ===========[ Subclasses/structs ]===================================
        private readonly record struct Look (UIColor Fill, UIColor Text, UIColor Border);

        // The deferred element is held by the menu, which the button holds natively, so the provider holds the button
        //  weakly; a strong reference back would tie them into a cycle the GC never breaks
        private sealed class MenuProvider
        {
            private readonly WeakReference<PopupMenuButton> m_owner;

            public MenuProvider (PopupMenuButton owner)
            {
                m_owner = new WeakReference<PopupMenuButton>(owner);
            }

            public void Provide (UIDeferredMenuElementCompletionHandler completion)
            {
                completion(m_owner.TryGetTarget(out PopupMenuButton owner) ? CreateMenuElements(owner.MenuItems) : Array.Empty<UIMenuElement>());
            }
        }

        // An action's handler holds its item weakly: the button's MenuItems keeps the item, and nothing native should
        private sealed class PickTarget
        {
            private readonly WeakReference<PopupMenuItem> m_item;

            public PickTarget (PopupMenuItem item)
            {
                m_item = new WeakReference<PopupMenuItem>(item);
            }

            public void Run (UIAction action)
            {
                if (m_item.TryGetTarget(out PopupMenuItem item))
                {
                    item.Invoke();
                }
            }
        }
    }
}
