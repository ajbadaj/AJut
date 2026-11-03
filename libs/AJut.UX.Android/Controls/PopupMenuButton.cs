namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Graphics.Drawables;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// A button that opens a menu of <see cref="MenuItems"/> when tapped, and shows itself checked while the menu is open,
    /// as WinUI3's PopupMenuButton does. The menu is the SDK's PopupMenu, built from the items each time it opens: a
    /// separator divides groups, an item with Items of its own opens a submenu, and a checkable item shows its check.
    /// With the AJut theme loaded the button takes MobileLook.PopupMenuButton's look, which UIKit's PopupMenuButton draws
    /// too; unthemed it is the app's own button. The menu itself is the platform's in both.
    /// </summary>
    [Register("ajut.ux.controls.PopupMenuButton")]
    public class PopupMenuButton : Button
    {
        // Only painted with the AJut theme loaded, so the literals are what a key the palette lacks falls back to
        private static readonly ThemedColor kFill = ThemedColor.FromArgb(MobileLook.PopupMenuButton.Fill, 0xFFFBFBFB, 0xFF2D2D2D);
        private static readonly ThemedColor kPointerOverFill = ThemedColor.FromArgb(MobileLook.PopupMenuButton.PointerOverFill, 0xFFF6F6F6, 0xFF323232);
        private static readonly ThemedColor kOpenPointerOverFill = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenPointerOverFill, 0xFF1975C5, 0xFF47B1E8);
        private static readonly ThemedColor kPointerOverText = ThemedColor.FromArgb(MobileLook.PopupMenuButton.PointerOverText, 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kOpenPointerOverText = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenPointerOverText, 0xFFFFFFFF, 0xFF000000);
        private static readonly ThemedColor kPointerOverBorder = ThemedColor.FromArgb(MobileLook.PopupMenuButton.PointerOverBorder, 0x0F000000, 0x17FFFFFF);
        private static readonly ThemedColor kOpenPointerOverBorder = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenPointerOverBorder, 0x00000000, 0x00000000);
        private static readonly ThemedColor kPressedFill = ThemedColor.FromArgb(MobileLook.PopupMenuButton.PressedFill, 0xFFF0F0F0, 0xFF242424);
        private static readonly ThemedColor kDisabledFill = ThemedColor.FromArgb(MobileLook.PopupMenuButton.DisabledFill, 0xFFF5F5F5, 0xFF2A2A2A);
        private static readonly ThemedColor kOpenFill = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenFill, 0xFF0067C0, 0xFF4CC2FF);
        private static readonly ThemedColor kOpenPressedFill = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenPressedFill, 0xFF1975C5, 0xFF42A7DC);
        private static readonly ThemedColor kText = ThemedColor.FromArgb(MobileLook.PopupMenuButton.Text, 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kPressedText = ThemedColor.FromArgb(MobileLook.PopupMenuButton.PressedText, 0x9E000000, 0xC5FFFFFF);
        private static readonly ThemedColor kDisabledText = ThemedColor.FromArgb(MobileLook.PopupMenuButton.DisabledText, 0x5C000000, 0x5DFFFFFF);
        private static readonly ThemedColor kOpenText = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenText, 0xFFFFFFFF, 0xFF000000);
        private static readonly ThemedColor kOpenPressedText = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenPressedText, 0xB3FFFFFF, 0x80000000);
        private static readonly ThemedColor kBorder = ThemedColor.FromArgb(MobileLook.PopupMenuButton.Border, 0x0F000000, 0x17FFFFFF);
        private static readonly ThemedColor kPressedBorder = ThemedColor.FromArgb(MobileLook.PopupMenuButton.PressedBorder, 0x0F000000, 0x17FFFFFF);
        private static readonly ThemedColor kDisabledBorder = ThemedColor.FromArgb(MobileLook.PopupMenuButton.DisabledBorder, 0x0F000000, 0x17FFFFFF);
        private static readonly ThemedColor kOpenBorder = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenBorder, 0x00000000, 0x00000000);
        private static readonly ThemedColor kOpenPressedBorder = ThemedColor.FromArgb(MobileLook.PopupMenuButton.OpenPressedBorder, 0x00000000, 0x00000000);
        private static readonly ThemedColor kChevron = ThemedColor.FromArgb(MobileLook.PopupMenuButton.Chevron, 0xE4000000, 0xFFFFFFFF);

        // The states each look is for, most specific first, as StateListDrawable and ColorStateList take the first match.
        //  Activated is the menu being open; hovered only comes with a mouse or a stylus.
        private static readonly int[][] kStates =
        {
            new[] { -Android.Resource.Attribute.StateEnabled },
            new[] { Android.Resource.Attribute.StateActivated, Android.Resource.Attribute.StatePressed },
            new[] { Android.Resource.Attribute.StateActivated, Android.Resource.Attribute.StateHovered },
            new[] { Android.Resource.Attribute.StateActivated },
            new[] { Android.Resource.Attribute.StatePressed },
            new[] { Android.Resource.Attribute.StateHovered },
            Array.Empty<int>(),
        };

        private readonly List<PopupMenuItem> m_shownItems = new List<PopupMenuItem>();
        private PopupMenu? m_shownMenu;
        private int m_nextGroupId;

        // ===========[ Setup/Construction/Teardown ]===================================
        public PopupMenuButton (Context context) : base(context)
        {
            this.Initialize(context);
        }

        public PopupMenuButton (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context);
        }

        public PopupMenuButton (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context);
        }

        protected PopupMenuButton (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();
            this.Click -= this.OnClick;
            this.Click += this.OnClick;
        }

        protected override void OnDetachedFromWindow ()
        {
            // A menu left showing from a view that has left its window would hold that window
            this.Click -= this.OnClick;
            m_shownMenu?.Dismiss();
            base.OnDetachedFromWindow();
        }

        // ===========[ Events ]===================================
        /// <summary>Raised when the menu opens, by a tap or by <see cref="IsOpen"/></summary>
        public event EventHandler<EventArgs>? FlyoutOpened;

        /// <summary>Raised when the menu closes, whether an item was picked or not</summary>
        public event EventHandler<EventArgs>? FlyoutClosed;

        // ===========[ Properties ]===================================
        /// <summary>The button's text, as WinUI3's ButtonContent shows it. The same as Text.</summary>
        public string? ButtonContent
        {
            get => this.Text;
            set => this.Text = value;
        }

        /// <summary>The menu's entries, read each time it opens, so a change while it is closed shows the next time</summary>
        public ObservableCollection<PopupMenuItem> MenuItems { get; } = new ObservableCollection<PopupMenuItem>();

        /// <summary>
        /// Whether the menu is open. Setting it opens or closes the menu; opening needs the button to be in a window, and
        /// does nothing until it is.
        /// </summary>
        public bool IsOpen
        {
            get => m_shownMenu != null;
            set
            {
                if (value)
                {
                    this.ShowMenu();
                }
                else
                {
                    m_shownMenu?.Dismiss();
                }
            }
        }

        /// <summary>The menu while it is open, for anything this button does not surface, or null when it is closed</summary>
        public PopupMenu? ShownMenu => m_shownMenu;

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context)
        {
            float density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            bool isThemed = ThemeColors.IsThemeLoaded;
            if (isThemed)
            {
                this.PaintThemedFace(context, density);
            }

            // Unthemed the chevron follows the button's own text color, so it sits with the app's buttons
            Color chevronColor = isThemed ? ThemeColors.Resolve(context, kChevron).ToAndroidColor() : new Color(this.CurrentTextColor);
            int chevronWidth = (int)(MobileLook.PopupMenuButton.ChevronWidth * density);
            var chevron = new ChevronDrawable(chevronColor, MobileLook.PopupMenuButton.ChevronThickness * density);
            chevron.SetBounds(0, 0, chevronWidth, chevronWidth / 2);
            this.SetCompoundDrawablesRelative(null, null, chevron, null);
            this.CompoundDrawablePadding = (int)(MobileLook.PopupMenuButton.ChevronSpacing * density);
        }

        // The face: the fill, outlined with the border, for each state, and the text color for each
        private void PaintThemedFace (Context context, float density)
        {
            float radius = MobileLook.PopupMenuButton.CornerRadius * density;
            int border = (int)Math.Max(1f, MobileLook.PopupMenuButton.BorderThickness * density);

            var faces = new StateListDrawable();
            faces.AddState(kStates[0], _Face(kDisabledFill, kDisabledBorder));
            faces.AddState(kStates[1], _Face(kOpenPressedFill, kOpenPressedBorder));
            faces.AddState(kStates[2], _Face(kOpenPointerOverFill, kOpenPointerOverBorder));
            faces.AddState(kStates[3], _Face(kOpenFill, kOpenBorder));
            faces.AddState(kStates[4], _Face(kPressedFill, kPressedBorder));
            faces.AddState(kStates[5], _Face(kPointerOverFill, kPointerOverBorder));
            faces.AddState(kStates[6], _Face(kFill, kBorder));
            this.Background = faces;

            this.SetTextColor(new ColorStateList(kStates, new int[]
            {
                _Argb(kDisabledText), _Argb(kOpenPressedText), _Argb(kOpenPointerOverText), _Argb(kOpenText), _Argb(kPressedText), _Argb(kPointerOverText), _Argb(kText),
            }));

            // WinUI3's button does not shout its text, and the touch target keeps the platform's minimum height
            this.SetAllCaps(false);
            int paddingHorizontal = (int)(MobileLook.PopupMenuButton.PaddingHorizontal * density);
            int paddingVertical = (int)(MobileLook.PopupMenuButton.PaddingVertical * density);
            this.SetPaddingRelative(paddingHorizontal, paddingVertical, paddingHorizontal, paddingVertical);

            GradientDrawable _Face (ThemedColor fill, ThemedColor stroke)
            {
                var face = new GradientDrawable();
                face.SetColor(ThemeColors.Resolve(context, fill).ToAndroidColor());
                face.SetCornerRadius(radius);
                face.SetStroke(border, ThemeColors.Resolve(context, stroke).ToAndroidColor());
                return face;
            }

            int _Argb (ThemedColor color) => ThemeColors.Resolve(context, color).ToAndroidColor().ToArgb();
        }

        private void ShowMenu ()
        {
            if ((m_shownMenu != null) || !this.IsAttachedToWindow)
            {
                return;
            }

            var menu = new PopupMenu(this.Context!, this);
            m_shownItems.Clear();
            m_nextGroupId = 0;
            this.AddItems(menu.Menu!, this.MenuItems, isInSubmenu: false);

            menu.MenuItemClick += this.OnMenuItemClick;
            menu.DismissEvent += this.OnMenuDismissed;
            m_shownMenu = menu;
            this.Activated = true;
            menu.Show();
            this.FlyoutOpened?.Invoke(this, EventArgs.Empty);
        }

        // Each run of items between separators is a group, with the platform's divider between groups (Android 9 and up
        //  draws them). An item's id is its place in the shown list, plus one, since 0 is no id.
        private void AddItems (IMenu menu, IEnumerable<PopupMenuItem> items, bool isInSubmenu)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
            {
                menu.SetGroupDividerEnabled(true);
            }

            foreach (IReadOnlyList<PopupMenuItem> group in PopupMenuItem.GroupBySeparators(items))
            {
                int groupId = ++m_nextGroupId;
                foreach (PopupMenuItem item in group)
                {
                    m_shownItems.Add(item);
                    int id = m_shownItems.Count;
                    if (item.HasSubmenu && !isInSubmenu)
                    {
                        ISubMenu submenu = menu.AddSubMenu(groupId, id, id, item.Text)!;
                        submenu.Item!.SetEnabled(item.IsEnabled);
                        this.AddItems(submenu, item.Items, isInSubmenu: true);
                    }
                    else if (item.HasSubmenu)
                    {
                        // Android menus go one submenu deep, so a submenu inside a submenu shows its title, then its items
                        //  as a group of their own
                        menu.Add(groupId, id, id, item.Text)!.SetEnabled(false);
                        this.AddItems(menu, item.Items, isInSubmenu: true);
                    }
                    else
                    {
                        IMenuItem entry = menu.Add(groupId, id, id, item.Text)!;
                        entry.SetEnabled(item.CanInvoke);
                        if (item.IsCheckable)
                        {
                            entry.SetCheckable(true);
                            entry.SetChecked(item.IsChecked);
                        }
                    }
                }
            }
        }

        private void OnClick (object? sender, EventArgs e) => this.ShowMenu();

        private void OnMenuItemClick (object? sender, PopupMenu.MenuItemClickEventArgs e)
        {
            int index = (e.Item?.ItemId ?? 0) - 1;
            if ((index >= 0) && (index < m_shownItems.Count))
            {
                m_shownItems[index].Invoke();
            }
        }

        private void OnMenuDismissed (object? sender, PopupMenu.DismissEventArgs e)
        {
            if (sender is PopupMenu menu)
            {
                menu.MenuItemClick -= this.OnMenuItemClick;
                menu.DismissEvent -= this.OnMenuDismissed;
            }

            m_shownMenu = null;
            m_shownItems.Clear();
            this.Activated = false;
            this.FlyoutClosed?.Invoke(this, EventArgs.Empty);
        }

        // ===========[ Subclasses/structs ]===================================
        // A down chevron filling its bounds: two strokes meeting at the bottom middle, rounded at their ends
        private sealed class ChevronDrawable : Drawable
        {
            private readonly Android.Graphics.Paint m_paint = new Android.Graphics.Paint(PaintFlags.AntiAlias);
            private readonly Android.Graphics.Path m_path = new Android.Graphics.Path();

            public ChevronDrawable (Color color, float thickness)
            {
                m_paint.Color = color;
                m_paint.StrokeWidth = thickness;
                m_paint.SetStyle(Android.Graphics.Paint.Style.Stroke);
                m_paint.StrokeCap = Android.Graphics.Paint.Cap.Round;
                m_paint.StrokeJoin = Android.Graphics.Paint.Join.Round;
            }

            public override int Opacity => (int)Format.Translucent;

            public override void Draw (Canvas canvas)
            {
                // Inset by half the stroke so the rounded ends stay inside the bounds
                Rect bounds = this.Bounds;
                float inset = m_paint.StrokeWidth / 2f;
                m_path.Reset();
                m_path.MoveTo(bounds.Left + inset, bounds.Top + inset);
                m_path.LineTo(bounds.ExactCenterX(), bounds.Bottom - inset);
                m_path.LineTo(bounds.Right - inset, bounds.Top + inset);
                canvas.DrawPath(m_path, m_paint);
            }

            public override void SetAlpha (int alpha)
            {
                m_paint.Alpha = alpha;
                this.InvalidateSelf();
            }

            public override void SetColorFilter (ColorFilter? colorFilter)
            {
                m_paint.SetColorFilter(colorFilter);
                this.InvalidateSelf();
            }
        }
    }
}
