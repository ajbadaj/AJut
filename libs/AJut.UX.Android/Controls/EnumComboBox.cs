namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    /// A drop-down of an enum's values, inferred from the type of <see cref="SelectedItem"/>. The options come from
    /// AJut.UX's <see cref="EnumSelectionOptions"/>, so values marked [Browsable(false)] or [ExcludeFromSelection] are
    /// left out and every value is labeled the same way on every AJut toolkit. It is a Spinner underneath. With the AJut
    /// theme loaded it takes MobileLook.EnumComboBox's look, which UIKit's EnumComboBox draws too; unthemed it looks and
    /// behaves like any other drop-down in the app. Its Enabled, and android:enabled in a layout, pass to the Spinner.
    /// </summary>
    [Register("ajut.ux.controls.EnumComboBox")]
    public class EnumComboBox : FrameLayout
    {
        // Only painted with the AJut theme loaded, so the literals are what a key the palette lacks falls back to. Each
        //  state's key comes from MobileLook.EnumComboBox's state methods, so the solid colors are looked up by key.
        private static readonly ThemedGradient kSurface = new ThemedGradient(
            MobileLook.EnumComboBox.Surface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B) }
        );
        private static readonly Dictionary<string, ThemedColor> kThemedColors = new[]
        {
            ThemedColor.FromArgb(MobileLook.EnumComboBox.SurfacePointerOver, 0xFFF0F0F0, 0xFF333333),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.SurfacePressed, 0xFFDADADA, 0xFF242424),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.SurfaceDisabled, 0xFFEEEEEE, 0xFF2B2B2E),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.Border, 0xFF888888, 0xFF888888),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.BorderPointerOver, 0xFF3B9CF0, 0xFF3B9CF0),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.BorderPressed, 0xFF0273D1, 0xFF0273D1),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.BorderDisabled, 0xFFCCCCCC, 0xFF3F3F3F),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.Text, 0xE4000000, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.TextPointerOver, 0xE4000000, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.TextPressed, 0xE4000000, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.TextDisabled, 0x5C000000, 0x5DFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.Chevron, 0x9E000000, 0xC5FFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.ChevronDisabled, 0x5C000000, 0x5DFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownSurface, 0xFFFFFFFF, 0xFF2B2B2B),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownBorder, 0xFF3B9CF0, 0xFF3B9CF0),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownText, 0xE4000000, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownTextPointerOver, 0xE4000000, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownTextPressed, 0xFFFFFFFF, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownTextSelected, 0xFFFFFFFF, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownTextSelectedPointerOver, 0xFFFFFFFF, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownTextSelectedPressed, 0xFFFFFFFF, 0xFFFFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownItemPointerOverFill, 0x19000000, 0x19FFFFFF),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownItemPressedFill, 0xFF01417C, 0xFF01417C),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownItemSelectedFill, 0xFF0273D1, 0xFF0273D1),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownItemSelectedPointerOverFill, 0xFF3B9CF0, 0xFF3B9CF0),
            ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownItemSelectedPressedFill, 0xFF01417C, 0xFF01417C),
        }.ToDictionary(color => color.Key);

        // A StateListDrawable or ColorStateList takes the first state that matches, so they are listed in this order: a
        //  disabled Spinner is never pressed or hovered, and a press outranks a pointer resting over it
        private static readonly int[] kDisabledState = { -Android.Resource.Attribute.StateEnabled };
        private static readonly int[] kPressedState = { Android.Resource.Attribute.StatePressed };
        private static readonly int[] kHoveredState = { Android.Resource.Attribute.StateHovered };
        private static readonly int[] kListSelectedState = { Android.Resource.Attribute.StateSelected };
        private static readonly int[] kAnyState = Array.Empty<int>();

        private Spinner m_spinner = null!;
        private ThemedAdapter? m_themedAdapter;
        private Type? m_enumType;
        private Enum[] m_values = Array.Empty<Enum>();
        private object? m_selectedItem;

        // ===========[ Setup/Construction/Teardown ]===================================
        public EnumComboBox (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public EnumComboBox (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public EnumComboBox (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected EnumComboBox (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();
            m_spinner.ItemSelected -= this.OnSpinnerItemSelected;
            m_spinner.ItemSelected += this.OnSpinnerItemSelected;
        }

        protected override void OnDetachedFromWindow ()
        {
            m_spinner.ItemSelected -= this.OnSpinnerItemSelected;
            base.OnDetachedFromWindow();
        }

        // ===========[ Events ]===================================
        /// <summary>Raised when <see cref="SelectedItem"/> changes, from a pick or from code. Bind two-way through this.</summary>
        public event EventHandler<EventArgs<object?>>? SelectedItemChanged;

        // ===========[ Properties ]===================================
        /// <summary>The selected enum value. Its type picks the options; a value the options leave out shows no selection.</summary>
        public object? SelectedItem
        {
            get => m_selectedItem;
            set
            {
                if (Equals(m_selectedItem, value))
                {
                    return;
                }

                m_selectedItem = value;
                if (value is Enum enumValue)
                {
                    if (enumValue.GetType() != m_enumType)
                    {
                        this.RebuildOptions(enumValue);
                    }

                    int index = Array.IndexOf(m_values, enumValue);
                    if ((index >= 0) && (m_spinner.SelectedItemPosition != index))
                    {
                        m_spinner.SetSelection(index, false);
                    }

                    if (m_themedAdapter != null)
                    {
                        m_themedAdapter.SelectedPosition = index;
                    }
                }

                this.SelectedItemChanged?.Invoke(this, new EventArgs<object?>(value));
            }
        }

        /// <summary>The values offered, in order.</summary>
        public IReadOnlyList<Enum> Options => m_values;

        /// <summary>Whether it takes taps; passed on to the Spinner, which a ViewGroup's own Enabled does not reach.</summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;

                // The Spinner is made in Initialize, so it is not there yet when the base constructor runs
                if (m_spinner != null)
                {
                    m_spinner.Enabled = value;
                }
            }
        }

        /// <summary>The Spinner underneath, for styling it the way the app styles its other drop-downs.</summary>
        public Spinner Spinner => m_spinner;

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_spinner = new Spinner(context);
            this.AddView(m_spinner, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutEnumComboBox);
                this.Enabled = values.GetBoolean(Resource.Styleable.AJutEnumComboBox_android_enabled, true);
                values.Recycle();
            }

            if (ThemeColors.IsThemeLoaded)
            {
                this.PaintThemedFace(context);
            }
        }

        // The face replaces the Spinner's own background, which is also what drew its arrow, so the chevron is drawn here
        //  as a layer at the face's trailing end, with the text padded clear of it. The Spinner's own state (enabled,
        //  pressed, hovered by a stylus or a mouse) picks the face, and the chevron follows it as a layer of the same
        //  drawable.
        private void PaintThemedFace (Context context)
        {
            float density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            float radius = MobileLook.EnumComboBox.CornerRadius * density;
            int border = (int)Math.Max(1f, MobileLook.EnumComboBox.BorderThickness * density);

            var states = new StateListDrawable();
            states.AddState(kDisabledState, MakeFace(context, false, false, false, radius, border));
            states.AddState(kPressedState, MakeFace(context, true, true, false, radius, border));
            states.AddState(kHoveredState, MakeFace(context, true, false, true, radius, border));
            states.AddState(kAnyState, MakeFace(context, true, false, false, radius, border));

            int paddingHorizontal = (int)(MobileLook.EnumComboBox.PaddingHorizontal * density);
            int paddingVertical = (int)(MobileLook.EnumComboBox.PaddingVertical * density);
            int chevronWidth = (int)(MobileLook.EnumComboBox.ChevronWidth * density);
            var chevronColors = new ColorStateList(
                new[] { kDisabledState, kAnyState },
                new[]
                {
                    ResolveColor(context, MobileLook.EnumComboBox.ChevronFor(false)).ToArgb(),
                    ResolveColor(context, MobileLook.EnumComboBox.ChevronFor(true)).ToArgb(),
                }
            );
            var chevron = new ChevronDrawable(chevronColors, MobileLook.EnumComboBox.ChevronThickness * density);
            const int kChevronLayer = 1;
            var layers = new LayerDrawable(new Drawable[] { states, chevron });
            layers.SetLayerSize(kChevronLayer, chevronWidth, chevronWidth / 2);
            layers.SetLayerGravity(kChevronLayer, GravityFlags.CenterVertical | GravityFlags.End);
            layers.SetLayerInsetEnd(kChevronLayer, paddingHorizontal);

            m_spinner.Background = layers;
            m_spinner.SetPaddingRelative(paddingHorizontal, paddingVertical, (paddingHorizontal * 2) + chevronWidth, paddingVertical);

            // The stock background was also what showed the Spinner's focus, so the AJut focus look takes its place
            FocusVisual.Apply(m_spinner);

            var dropDown = new GradientDrawable();
            dropDown.SetColor(ResolveColor(context, MobileLook.EnumComboBox.DropDownSurface));
            dropDown.SetCornerRadius(radius);
            dropDown.SetStroke(border, ResolveColor(context, MobileLook.EnumComboBox.DropDownBorder));
            m_spinner.SetPopupBackgroundDrawable(dropDown);
        }

        private static GradientDrawable MakeFace (Context context, bool isEnabled, bool isPressed, bool isPointerOver, float radius, int border)
        {
            // The face at rest is the pop-out surface, a gradient; every other state is one color
            string surfaceKey = MobileLook.EnumComboBox.SurfaceFor(isEnabled, isPressed, isPointerOver);
            GradientDrawable face;
            if (surfaceKey == kSurface.Key)
            {
                face = ThemeColors.ToDrawable(ThemeColors.Resolve(context, kSurface));
            }
            else
            {
                face = new GradientDrawable();
                face.SetColor(ResolveColor(context, surfaceKey));
            }

            face.SetCornerRadius(radius);
            face.SetStroke(border, ResolveColor(context, MobileLook.EnumComboBox.BorderFor(isEnabled, isPressed, isPointerOver)));
            return face;
        }

        // The face's text in each of the face's states, in the same order as the face itself
        private static ColorStateList MakeFaceTextColors (Context context)
        {
            return new ColorStateList(
                new[] { kDisabledState, kPressedState, kHoveredState, kAnyState },
                new[]
                {
                    ResolveColor(context, MobileLook.EnumComboBox.TextFor(false, false, false)).ToArgb(),
                    ResolveColor(context, MobileLook.EnumComboBox.TextFor(true, true, false)).ToArgb(),
                    ResolveColor(context, MobileLook.EnumComboBox.TextFor(true, false, true)).ToArgb(),
                    ResolveColor(context, MobileLook.EnumComboBox.TextFor(true, false, false)).ToArgb(),
                }
            );
        }

        // An option in the open list. The list marks the row a pointer or the keyboard is on as selected (its own sense of
        //  the word, not the chosen option), so that state takes the pointer-over look; the chosen option is filled at rest.
        private static ColorStateList MakeOptionTextColors (Context context, bool isChosen)
        {
            return new ColorStateList(
                new[] { kPressedState, kListSelectedState, kAnyState },
                new[]
                {
                    ResolveColor(context, MobileLook.EnumComboBox.DropDownTextFor(isChosen, true, false)).ToArgb(),
                    ResolveColor(context, MobileLook.EnumComboBox.DropDownTextFor(isChosen, false, true)).ToArgb(),
                    ResolveColor(context, MobileLook.EnumComboBox.DropDownTextFor(isChosen, false, false)).ToArgb(),
                }
            );
        }

        private static StateListDrawable MakeOptionFill (Context context, bool isChosen)
        {
            var fills = new StateListDrawable();
            fills.AddState(kPressedState, MakeOptionFillFor(context, MobileLook.EnumComboBox.DropDownItemFill(isChosen, true, false)));
            fills.AddState(kListSelectedState, MakeOptionFillFor(context, MobileLook.EnumComboBox.DropDownItemFill(isChosen, false, true)));
            fills.AddState(kAnyState, MakeOptionFillFor(context, MobileLook.EnumComboBox.DropDownItemFill(isChosen, false, false)));
            return fills;
        }

        private static ColorDrawable MakeOptionFillFor (Context context, string? key)
        {
            return new ColorDrawable(key == null ? Android.Graphics.Color.Transparent : ResolveColor(context, key));
        }

        private static Android.Graphics.Color ResolveColor (Context context, string key) => ThemeColors.Resolve(context, kThemedColors[key]).ToAndroidColor();

        // Takes a value rather than its Type so it stays clean under trimming without a suppression: the trim analyzer
        //  treats GetType() on a value typed Enum as keeping the public fields GetSelectableValues reads, which holds
        //  because the trimmer keeps every field of an enum type it keeps. EnumToggleStrip.RebuildItems has the longer story.
        private void RebuildOptions (Enum valueOfTheNewType)
        {
            m_enumType = valueOfTheNewType.GetType();
            m_values = EnumSelectionOptions.GetSelectableValues(valueOfTheNewType.GetType()).ToArray();
            List<string> names = m_values.Select(EnumSelectionOptions.GetDisplayName).ToList();
            m_themedAdapter = ThemeColors.IsThemeLoaded ? new ThemedAdapter(this.Context!, names) : null;
            ArrayAdapter<string> adapter = m_themedAdapter ?? new ArrayAdapter<string>(this.Context!, Android.Resource.Layout.SimpleSpinnerItem, names);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            m_spinner.Adapter = adapter;
        }

        // The Spinner reports a selection whenever it lays out a new one, including the one set from code, so only a
        // value that differs from SelectedItem is a pick.
        private void OnSpinnerItemSelected (object? sender, AdapterView.ItemSelectedEventArgs e)
        {
            if ((e.Position >= 0) && (e.Position < m_values.Length))
            {
                this.SelectedItem = m_values[e.Position];
            }
        }

        // ===========[ Subclasses/structs ]===================================
        // The stock spinner item layouts, with AJut's text and fills: the selected value on the face in the face's text for
        //  its state, and each option in the open list on its own fill, with the chosen option filled with the selection
        private sealed class ThemedAdapter : ArrayAdapter<string>
        {
            private readonly ColorStateList m_faceText;
            private readonly ColorStateList m_optionText;
            private readonly ColorStateList m_chosenOptionText;

            public ThemedAdapter (Context context, IList<string> names) : base(context, Android.Resource.Layout.SimpleSpinnerItem, names)
            {
                m_faceText = MakeFaceTextColors(context);
                m_optionText = MakeOptionTextColors(context, false);
                m_chosenOptionText = MakeOptionTextColors(context, true);
            }

            /// <summary>The chosen option's position, which the open list marks; -1 for none.</summary>
            public int SelectedPosition { get; set; } = -1;

            public override View GetView (int position, View? convertView, ViewGroup parent)
            {
                View view = base.GetView(position, convertView, parent);
                if (view is TextView text)
                {
                    // The face's text is a child of the Spinner, which is what is pressed, hovered or disabled, so it takes
                    //  its state from the Spinner to follow the face. It has to be set before the Spinner adds the view.
                    text.DuplicateParentStateEnabled = true;
                    text.SetTextColor(m_faceText);
                }

                return view;
            }

            public override View? GetDropDownView (int position, View? convertView, ViewGroup? parent)
            {
                View? view = base.GetDropDownView(position, convertView, parent);
                if (view is TextView text)
                {
                    // A row is reused for other options as the list scrolls, so each one is given its fill and text again.
                    //  A pointer resting on a row shows the platform's own list selector under it, since the list marks a
                    //  hovered row with its selector rather than a state on the row.
                    bool isChosen = position == this.SelectedPosition;
                    text.SetTextColor(isChosen ? m_chosenOptionText : m_optionText);
                    text.Background = MakeOptionFill(this.Context, isChosen);
                }

                return view;
            }
        }

        // A down chevron filling its bounds: two strokes meeting at the bottom middle, rounded at their ends, in the color
        //  its state list gives the state of the drawable it is a layer of
        private sealed class ChevronDrawable : Drawable
        {
            private readonly Paint m_paint = new Paint(PaintFlags.AntiAlias);
            private readonly Android.Graphics.Path m_path = new Android.Graphics.Path();
            private readonly ColorStateList m_colors;

            public ChevronDrawable (ColorStateList colors, float thickness)
            {
                m_colors = colors;
                m_paint.Color = new Android.Graphics.Color(colors.DefaultColor);
                m_paint.StrokeWidth = thickness;
                m_paint.SetStyle(Paint.Style.Stroke);
                m_paint.StrokeCap = Paint.Cap.Round;
                m_paint.StrokeJoin = Paint.Join.Round;
            }

            public override int Opacity => (int)Format.Translucent;

            public override bool IsStateful => true;

            protected override bool OnStateChange (int[] state)
            {
                int color = m_colors.GetColorForState(state, new Android.Graphics.Color(m_colors.DefaultColor));
                if (m_paint.Color.ToArgb() == color)
                {
                    return false;
                }

                m_paint.Color = new Android.Graphics.Color(color);
                this.InvalidateSelf();
                return true;
            }

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
