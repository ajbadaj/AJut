namespace AJutShowRoomAndroid
{
    using System;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Text;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// Every control the AJut theme paints on Android, once per state, side by side, so a theme can be judged at a
    /// glance in each look the theme bar picks. The layout places the samples and tags each with the state it shows;
    /// this sets the states a layout can't.
    /// </summary>
    public sealed class ThemeTesterPage : ShowroomPage
    {
        private const string kPlusButtonDescription = "Increase";
        private const int kRestValue = 5;
        private const string kOutOfBoundsText = "50";

        private View? m_root;

        // ===========[ Properties ]===================================
        public override string Title => "Theme Tester";

        public override int LayoutId => Resource.Layout.page_theme_tester;

        // ===========[ Public Interface Methods ]===================================
        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_root = root;
            _SetUpValues(root);
            foreach (ToggleStripItem item in root.FindViewById<ToggleStrip>(Resource.Id.theme_tester_strip_checked)!.Items)
            {
                item.IsSelected = true;
            }

            // Hover, press and focus are view states Android keeps on the view itself, a strip makes its item views as it
            // is attached, and a NumericEditor only hears typing while attached, so those are set once the page is
            // attached and laid out.
            root.Post(this.PinViewStates);
        }

        public override void Unbind ()
        {
            m_root = null;
        }

        // ===========[ Helper Methods ]===================================
        private void PinViewStates ()
        {
            if (m_root == null)
            {
                return;
            }

            _PinTree(m_root);

            // Focus scrolls the page to the focused field; the page should still open at its top.
            m_root.ScrollTo(0, 0);
        }

        // The states that are the controls' own data: read-only, a disabled Spinner
        private static void _SetUpValues (View view)
        {
            switch (view)
            {
                case NumericEditor editor:
                    editor.Value = kRestValue;
                    editor.IsReadOnly = _TagOf(editor) == "read-only";
                    return;

                case EnumComboBox comboBox:
                    comboBox.SelectedItem = eSample.Second;
                    comboBox.Spinner.Enabled = _TagOf(comboBox) != "disabled";
                    return;

                case ViewGroup group:
                    for (int index = 0; index < group.ChildCount; ++index)
                    {
                        _SetUpValues(group.GetChildAt(index)!);
                    }

                    return;
            }
        }

        private static void _PinTree (View view)
        {
            switch (_TagOf(view))
            {
                case "hovered":
                    _Hold(view);
                    view.Hovered = true;
                    return;

                case "pressed":
                    _Hold(view);
                    view.Pressed = true;
                    return;

                case "focused":
                    // The one real focus on the page, since a view only shows focus while it has it. The field takes no
                    // typing, so it is not a text editor to Android and no keyboard (or a keyboard's floating toolbar)
                    // comes up over the page.
                    EditText field = ((NumericEditor)view).Field;
                    field.InputType = InputTypes.Null;
                    field.RequestFocus();
                    return;

                case "error":
                    // A value set in code is capped to the bounds, so it is typed text that goes out of them, and the
                    // field only hears typing once it is attached.
                    ((NumericEditor)view).Field.Text = kOutOfBoundsText;
                    return;

                case "step-pressed":
                    TextView plus = _FindByDescription(view, kPlusButtonDescription)!;
                    _Hold(plus);
                    plus.Pressed = true;
                    return;

                case "pin-items":
                    _PinItemsByName(view);
                    return;

                case "scroll-middle":
                    // The strip's scroller is its own, the one HorizontalScrollView inside it
                    HorizontalScrollView scroller = _FindScroller(view)!;
                    scroller.ScrollTo((scroller.GetChildAt(0)!.Width - scroller.Width) / 2, 0);
                    return;
            }

            if (view is ViewGroup group)
            {
                for (int index = 0; index < group.ChildCount; ++index)
                {
                    _PinTree(group.GetChildAt(index)!);
                }
            }
        }

        // A strip's item views are its own, so they are found by the item names, each named for the state it shows
        private static void _PinItemsByName (View view)
        {
            if (view is TextView item)
            {
                string name = item.Text ?? string.Empty;
                if (name.Equals("Hovered", StringComparison.OrdinalIgnoreCase))
                {
                    _Hold(item);
                    item.Hovered = true;
                }
                else if (name.EndsWith("pressed", StringComparison.OrdinalIgnoreCase))
                {
                    _Hold(item);
                    item.Pressed = true;
                }

                return;
            }

            if (view is ViewGroup group)
            {
                for (int index = 0; index < group.ChildCount; ++index)
                {
                    _PinItemsByName(group.GetChildAt(index)!);
                }
            }
        }

        // A view that takes no taps keeps a pinned hover or press: a tap would otherwise end it
        private static void _Hold (View view)
        {
            view.Clickable = false;
            view.LongClickable = false;
        }

        private static TextView? _FindByDescription (View view, string description)
        {
            if ((view is TextView text) && (text.ContentDescription == description))
            {
                return text;
            }

            if (view is ViewGroup group)
            {
                for (int index = 0; index < group.ChildCount; ++index)
                {
                    TextView? found = _FindByDescription(group.GetChildAt(index)!, description);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static HorizontalScrollView? _FindScroller (View view)
        {
            if (view is HorizontalScrollView scroller)
            {
                return scroller;
            }

            if (view is ViewGroup group)
            {
                for (int index = 0; index < group.ChildCount; ++index)
                {
                    HorizontalScrollView? found = _FindScroller(group.GetChildAt(index)!);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static string? _TagOf (View view) => view.Tag?.ToString();

        // ===========[ Subclasses/structs ]===================================
        private enum eSample
        {
            First,
            Second,
            Third,
        }
    }
}
