namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    // ===========[ PopupMenuButton ]============================================
    // A button that drops a menu of MenuItems when pressed, and shows itself checked while the menu
    // is open, as WinUI3's PopupMenuButton does. The menu is AppKit's own NSMenu, built from the items
    // each time it opens: a separator divides groups, an item with Items of its own opens a submenu
    // (as deep as they go), and a checkable item shows its check. So it is a menu like every other
    // one on the Mac, and only the button takes the AJut look: the ToggleButton keys WinUI3's button
    // takes, through ThemedColorKeys, with macOS control colors when no theme is loaded.
    //
    // The menu opens on the press, as a Mac pull-down button's does, and from there the press is the
    // menu's: holding, dragging to an item and letting go picks it. NSMenu.PopUpMenu runs the menu's
    // own tracking loop and only returns once the menu has closed, so the open look, FlyoutOpened and
    // FlyoutClosed all sit around that one call. Space or Return opens it from the keyboard, and so
    // does VoiceOver's press. Setting IsOpen opens it a turn of the run loop later, so the setter
    // does not wait out the menu.
    //
    // Nothing a menu holds leads back to the button: every item's action goes to one static target,
    // and each item finds its PopupMenuItem through a weak reference it carries. Keep the items, as
    // MenuItems does.
    public class PopupMenuButton : ManagedButton
    {
        // WinUI3's PART_Button and chevron glyph, as are the corner and padding where they are used
        private const double kBorderThickness = 1.0;
        private const double kChevronSpacing = 4.0;

        /// <summary>The chevron's width; it is half as tall</summary>
        private const double kChevronWidth = 8.0;

        private static readonly MenuPickTarget g_pickTarget = new MenuPickTarget();

        private string m_buttonContent = String.Empty;
        private NSFont m_font = NSFont.SystemFontOfSize(NSFont.SystemFontSize);
        private NSMenu m_openMenu;
        private bool m_isOpen;
        private int m_openRequestRevision;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PopupMenuButton ()
        {
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native button whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls; its MenuItems start empty. None of its managed state
        /// survives a dispose, so a call that needs that state can still fail: dispose it only once AppKit is done with it.
        /// </summary>
        protected PopupMenuButton (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when the menu opens</summary>
        public event EventHandler<EventArgs> FlyoutOpened;

        /// <summary>Raised when the menu closes, whether an item was picked or not</summary>
        public event EventHandler<EventArgs> FlyoutClosed;

        // ===========[ Properties ]============================================
        /// <summary>The button's text, as WinUI3's ButtonContent shows it</summary>
        public string ButtonContent
        {
            get => m_buttonContent;
            set
            {
                m_buttonContent = value ?? String.Empty;
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
            }
        }

        /// <summary>The text's font, and the menu's, the system font at its regular size by default</summary>
        public override NSFont Font
        {
            get => m_font;
            set
            {
                m_font = value ?? NSFont.SystemFontOfSize(NSFont.SystemFontSize);
                this.InvalidateIntrinsicContentSize();
                this.NeedsDisplay = true;
            }
        }

        /// <summary>The menu's entries, read each time it opens, so a change while it is closed shows the next time</summary>
        public ObservableCollection<PopupMenuItem> MenuItems { get; } = new ObservableCollection<PopupMenuItem>();

        /// <summary>
        /// Whether the menu is open. Setting it false closes the menu. Setting it true opens the menu a turn of the run loop
        /// later, if the button is enabled and in a window then.
        /// </summary>
        public bool IsOpen
        {
            get => m_isOpen;
            set
            {
                // A new request replaces one still waiting, so false right after true opens nothing
                ++m_openRequestRevision;
                if (!value)
                {
                    m_openMenu?.CancelTracking();
                    return;
                }

                if (m_isOpen)
                {
                    return;
                }

                // The menu's tracking loop holds whatever opens it until the menu closes, so code that sets IsOpen gets on
                // with its own work first, and the menu opens on the next turn. A weak reference and the revision, not the
                // button, ride along, so a button dropped in the meantime is not kept for it.
                int revision = m_openRequestRevision;
                var owner = new WeakReference<PopupMenuButton>(this);
                NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
                {
                    if (owner.TryGetTarget(out PopupMenuButton button) && (button.m_openRequestRevision == revision))
                    {
                        button.OpenMenu();
                    }
                });
            }
        }

        public override string AccessibilityRole => NSAccessibilityRoles.MenuButtonRole;

        protected override string DefaultAccessibilityLabel => m_buttonContent;

        public override CGSize IntrinsicContentSize
        {
            get
            {
                const double kPaddingHorizontal = 6.0;
                const double kPaddingVertical = 4.0;

                double titleWidth = (m_buttonContent.Length > 0) ? Math.Ceiling((double)this.MakeTitle(NSColor.Black).Size.Width) + kChevronSpacing : 0.0;
                return new CGSize(
                    (nfloat)(titleWidth + kChevronWidth + (2.0 * (kPaddingHorizontal + kBorderThickness))),
                    (nfloat)(this.LineHeight() + (2.0 * (kPaddingVertical + kBorderThickness)))
                );
            }
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// An NSMenu for <paramref name="items"/>, the way this button's menu shows them: each group between separators with
        /// a separator line between it and the next, an item with Items of its own as a submenu, a checkable item with its
        /// check, and an item that cannot be picked disabled. Picking one calls its <see cref="PopupMenuItem.Invoke"/>. For
        /// building any other AppKit menu (a toolbar item's, a context menu) from the same items. The menu items hold their
        /// PopupMenuItems weakly, so a menu kept somewhere ties nothing into a cycle: keep the items yourself.
        /// </summary>
        public static NSMenu CreateMenu (IEnumerable<PopupMenuItem> items)
        {
            // AutoEnablesItems would enable each item by asking its target, and every item here shares one target, so
            // each item's own enabled state is set instead
            var menu = new NSMenu { AutoEnablesItems = false };
            IReadOnlyList<IReadOnlyList<PopupMenuItem>> groups = PopupMenuItem.GroupBySeparators(items);
            for (int groupIndex = 0; groupIndex < groups.Count; ++groupIndex)
            {
                if (groupIndex > 0)
                {
                    menu.AddItem(NSMenuItem.SeparatorItem);
                }

                foreach (PopupMenuItem item in groups[groupIndex])
                {
                    menu.AddItem(CreateMenuItem(item));
                }
            }

            return menu;
        }

        /// <summary>Opens the menu, as a click does</summary>
        public override void PerformClick (NSObject sender)
        {
            this.OpenMenu();
        }

        /// <summary>VoiceOver's press opens the menu a turn later, so VoiceOver is not kept waiting while the menu is open</summary>
        public override bool AccessibilityPerformPress () => this.OpenMenuFromAccessibility();

        /// <summary>VoiceOver's show menu, the action a menu button is expected to have, opens it the same way</summary>
        public override bool AccessibilityPerformShowMenu () => this.OpenMenuFromAccessibility();

        // The menu opens on the press and takes the mouse from there, so the release and any drag between belong to it.
        //  Nothing is passed to ManagedButton, which would track the press as a click.
        public override void MouseDown (NSEvent theEvent)
        {
            this.OpenMenu();
        }

        public override void MouseDragged (NSEvent theEvent)
        {
        }

        public override void MouseUp (NSEvent theEvent)
        {
        }

        public override void DrawRect (CGRect dirtyRect)
        {
            const double kCornerRadius = 2.0;
            const double kChevronThickness = 1.5;

            Look look = this.GetLook();
            CGRect bounds = this.Bounds;

            // 1. The surface. The border is stroked on the rect's edge, half in and half out, so the rect is inset by half
            //  the thickness to keep all of it inside the bounds.
            nfloat halfBorder = (nfloat)(kBorderThickness / 2.0);
            NSBezierPath surface = NSBezierPath.FromRoundedRect(bounds.Inset(halfBorder, halfBorder), (nfloat)kCornerRadius, (nfloat)kCornerRadius);
            look.Background.SetFill();
            surface.Fill();
            surface.LineWidth = (nfloat)kBorderThickness;
            look.Border.SetStroke();
            surface.Stroke();

            // 2. The text and the chevron after it, centered together, as WinUI3's button centers its content
            NSAttributedString title = (m_buttonContent.Length > 0) ? this.MakeTitle(look.Foreground) : null;
            double titleWidth = (title != null) ? Math.Ceiling((double)title.Size.Width) : 0.0;
            double contentWidth = titleWidth + ((title != null) ? kChevronSpacing : 0.0) + kChevronWidth;
            double left = ((double)bounds.Width - contentWidth) / 2.0;
            double centerY = (double)bounds.Height / 2.0;
            if (title != null)
            {
                title.DrawAtPoint(new CGPoint(left, centerY - ((double)title.Size.Height / 2.0)));
            }

            // 3. A downward chevron. This view is flipped, so a smaller y is higher up.
            double chevronLeft = left + contentWidth - kChevronWidth;
            double chevronHalfHeight = kChevronWidth / 4.0;
            var chevron = new NSBezierPath
            {
                LineWidth = (nfloat)kChevronThickness,
                LineCapStyle = NSLineCapStyle.Round,
                LineJoinStyle = NSLineJoinStyle.Round,
            };
            chevron.MoveTo(new CGPoint(chevronLeft, centerY - chevronHalfHeight));
            chevron.LineTo(new CGPoint(chevronLeft + (kChevronWidth / 2.0), centerY + chevronHalfHeight));
            chevron.LineTo(new CGPoint(chevronLeft + kChevronWidth, centerY - chevronHalfHeight));
            ThemedColors.Get(ThemedColorKeys.PopupMenuButton_Chevron).SetStroke();
            chevron.Stroke();
        }

        // ===========[ Helper Methods ]========================================
        private static NSMenuItem CreateMenuItem (PopupMenuItem item)
        {
            var menuItem = new NSMenuItem(item.Text ?? String.Empty);

            // A Mac menu can show a disabled submenu, which then does not open
            if (item.HasSubmenu)
            {
                menuItem.Submenu = CreateMenu(item.Items);
                menuItem.Enabled = item.IsEnabled;
                return menuItem;
            }

            menuItem.Target = g_pickTarget;
            menuItem.Action = MenuPickTarget.kPickSelector;
            menuItem.RepresentedObject = new ItemReference(item);
            menuItem.Enabled = item.CanInvoke;
            if (item.IsCheckable)
            {
                menuItem.State = item.IsChecked ? NSCellStateValue.On : NSCellStateValue.Off;
            }

            return menuItem;
        }

        private void OpenMenu ()
        {
            const double kMenuGap = 2.0;

            if (m_isOpen || !this.Enabled || (this.Window == null))
            {
                return;
            }

            // At least as wide as the button, as a Mac pull-down menu is
            NSMenu menu = CreateMenu(this.MenuItems);
            menu.MinimumWidth = this.Bounds.Width;
            menu.Font = m_font;

            m_openMenu = menu;
            this.SetOpen(true);
            try
            {
                this.FlyoutOpened?.Invoke(this, EventArgs.Empty);

                // With no item to line up, the menu's top left corner goes at the point, which is just under the button's
                //  bottom left in this flipped view. It returns once the menu has closed.
                menu.PopUpMenu(null, new CGPoint(0, (double)this.Bounds.Height + kMenuGap), this);
            }
            finally
            {
                // Closed whatever happened in between, so a FlyoutOpened handler that throws cannot leave it drawn open
                m_openMenu = null;
                this.SetOpen(false);
            }

            // The menu had the mouse, so the button heard nothing of the pointer coming or going while it was open
            this.RefreshPointerOver();
            this.FlyoutClosed?.Invoke(this, EventArgs.Empty);
        }

        private bool OpenMenuFromAccessibility ()
        {
            if (!this.Enabled)
            {
                return false;
            }

            this.IsOpen = true;
            return true;
        }

        private void SetOpen (bool isOpen)
        {
            m_isOpen = isOpen;
            this.NeedsDisplay = true;

            // The menu's tracking loop is about to run, so the open look is drawn now rather than whenever AppKit next
            //  gets to it
            if (isOpen)
            {
                this.DisplayIfNeeded();
            }
        }

        private double LineHeight () => Math.Ceiling((double)(m_font.Ascender - m_font.Descender + m_font.Leading));

        private NSAttributedString MakeTitle (NSColor foreground)
        {
            return new NSAttributedString(m_buttonContent, new NSStringAttributes { Font = m_font, ForegroundColor = foreground });
        }

        // The keys for the state the button is in. Each is a dynamic color, so a light or dark switch, or the theme coming
        //  or going, repaints it with nothing more done here.
        private Look GetLook ()
        {
            if (!this.Enabled)
            {
                return new Look(ThemedColorKeys.PopupMenuButton_DisabledBackground, ThemedColorKeys.PopupMenuButton_DisabledForeground, ThemedColorKeys.PopupMenuButton_DisabledBorder);
            }

            if (m_isOpen)
            {
                return new Look(ThemedColorKeys.PopupMenuButton_OpenBackground, ThemedColorKeys.PopupMenuButton_OpenForeground, ThemedColorKeys.PopupMenuButton_OpenBorder);
            }

            return (this.CurrentState == eManagedButtonState.PointerOver)
                ? new Look(ThemedColorKeys.PopupMenuButton_PointerOverBackground, ThemedColorKeys.PopupMenuButton_PointerOverForeground, ThemedColorKeys.PopupMenuButton_PointerOverBorder)
                : new Look(ThemedColorKeys.PopupMenuButton_NormalBackground, ThemedColorKeys.PopupMenuButton_NormalForeground, ThemedColorKeys.PopupMenuButton_NormalBorder);
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>The three colors the button draws a state in</summary>
        private readonly struct Look
        {
            public Look (string backgroundKey, string foregroundKey, string borderKey)
            {
                this.Background = ThemedColors.Get(backgroundKey);
                this.Foreground = ThemedColors.Get(foregroundKey);
                this.Border = ThemedColors.Get(borderKey);
            }

            public NSColor Background { get; }
            public NSColor Foreground { get; }
            public NSColor Border { get; }
        }

        // The one target every menu item's action goes to. It holds nothing, and lives as long as the app, so a menu item,
        //  which holds its target weakly, always finds it.
        private sealed class MenuPickTarget : NSObject
        {
            public static readonly Selector kPickSelector = new Selector("ajutPickPopupMenuItem:");

            public MenuPickTarget ()
            {
            }

            // For a peer the runtime makes over an existing native target
            private MenuPickTarget (NativeHandle handle) : base(handle)
            {
            }

            // AppKit sends a picked item's action from inside the menu's tracking loop, before the menu has closed. The pick
            //  waits a turn, so it runs once the menu is gone: a pick that opens a modal panel or alert then does not leave
            //  the button drawn open under it, and FlyoutClosed comes first. Only a weak reference rides along.
            [Export("ajutPickPopupMenuItem:")]
            public void Pick (NSMenuItem sender)
            {
                if (!(sender.RepresentedObject is ItemReference reference) || !reference.TryGetItem(out PopupMenuItem item))
                {
                    return;
                }

                var picked = new WeakReference<PopupMenuItem>(item);
                NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
                {
                    if (picked.TryGetTarget(out PopupMenuItem pickedItem))
                    {
                        pickedItem.Invoke();
                    }
                });
            }
        }

        // What a menu item carries to find its PopupMenuItem. The menu item retains it, so it holds the item weakly: the
        //  button's MenuItems keeps the item, and nothing native should.
        private sealed class ItemReference : NSObject
        {
            private readonly WeakReference<PopupMenuItem> m_item;

            public ItemReference (PopupMenuItem item)
            {
                m_item = new WeakReference<PopupMenuItem>(item);
            }

            // For a peer the runtime makes over an existing native reference, which then leads to no item
            private ItemReference (NativeHandle handle) : base(handle)
            {
                m_item = new WeakReference<PopupMenuItem>(null);
            }

            public bool TryGetItem (out PopupMenuItem item) => m_item.TryGetTarget(out item);
        }
    }
}
