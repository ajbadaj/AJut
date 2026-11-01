namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Theming;
    using Android.Content;
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
    /// behaves like any other drop-down in the app.
    /// </summary>
    [Register("ajut.ux.controls.EnumComboBox")]
    public class EnumComboBox : FrameLayout
    {
        // Only painted with the AJut theme loaded, so the literals are what a key the palette lacks falls back to
        private static readonly ThemedGradient kSurface = new ThemedGradient(
            MobileLook.EnumComboBox.Surface,
            new[] { ThemedGradient.Stop(0.0, 0xFFFDFDFD) },
            new[] { ThemedGradient.Stop(0.0, 0xFF2B2B2B) }
        );
        private static readonly ThemedColor kSurfacePressed = ThemedColor.FromArgb(MobileLook.EnumComboBox.SurfacePressed, 0xFFDADADA, 0xFF242424);
        private static readonly ThemedColor kBorder = ThemedColor.FromArgb(MobileLook.EnumComboBox.Border, 0xFF888888, 0xFF888888);
        private static readonly ThemedColor kText = ThemedColor.FromArgb(MobileLook.EnumComboBox.Text, 0xE4000000, 0xFFFFFFFF);
        private static readonly ThemedColor kDropDownSurface = ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownSurface, 0xFFFFFFFF, 0xFF2B2B2B);
        private static readonly ThemedColor kDropDownText = ThemedColor.FromArgb(MobileLook.EnumComboBox.DropDownText, 0xE4000000, 0xFFFFFFFF);

        private Spinner m_spinner = null!;
        private Type? m_enumType;
        private Enum[] m_values = Array.Empty<Enum>();
        private object? m_selectedItem;

        // ===========[ Setup/Construction/Teardown ]===================================
        public EnumComboBox (Context context) : base(context)
        {
            this.Initialize(context);
        }

        public EnumComboBox (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context);
        }

        public EnumComboBox (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context);
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
                }

                this.SelectedItemChanged?.Invoke(this, new EventArgs<object?>(value));
            }
        }

        /// <summary>The values offered, in order.</summary>
        public IReadOnlyList<Enum> Options => m_values;

        /// <summary>The Spinner underneath, for styling it the way the app styles its other drop-downs.</summary>
        public Spinner Spinner => m_spinner;

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context)
        {
            m_spinner = new Spinner(context);
            this.AddView(m_spinner, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
            if (ThemeColors.IsThemeLoaded)
            {
                this.PaintThemedFace(context);
            }
        }

        // The face replaces the Spinner's own background, which is also what drew its arrow, so the chevron is drawn here
        //  as a layer at the face's trailing end, with the text padded clear of it
        private void PaintThemedFace (Context context)
        {
            float density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            float radius = MobileLook.EnumComboBox.CornerRadius * density;
            int border = (int)Math.Max(1f, MobileLook.EnumComboBox.BorderThickness * density);
            Android.Graphics.Color borderColor = ThemeColors.Resolve(context, kBorder).ToAndroidColor();

            GradientDrawable face = ThemeColors.ToDrawable(ThemeColors.Resolve(context, kSurface));
            face.SetCornerRadius(radius);
            face.SetStroke(border, borderColor);
            var pressed = new GradientDrawable();
            pressed.SetColor(ThemeColors.Resolve(context, kSurfacePressed).ToAndroidColor());
            pressed.SetCornerRadius(radius);
            pressed.SetStroke(border, borderColor);
            var states = new StateListDrawable();
            states.AddState(new[] { Android.Resource.Attribute.StatePressed }, pressed);
            states.AddState(Array.Empty<int>(), face);

            int paddingHorizontal = (int)(MobileLook.EnumComboBox.PaddingHorizontal * density);
            int paddingVertical = (int)(MobileLook.EnumComboBox.PaddingVertical * density);
            int chevronWidth = (int)(MobileLook.EnumComboBox.ChevronWidth * density);
            var chevron = new ChevronDrawable(ThemeColors.Resolve(context, kText).ToAndroidColor(), MobileLook.EnumComboBox.ChevronThickness * density);
            const int kChevronLayer = 1;
            var layers = new LayerDrawable(new Drawable[] { states, chevron });
            layers.SetLayerSize(kChevronLayer, chevronWidth, chevronWidth / 2);
            layers.SetLayerGravity(kChevronLayer, GravityFlags.CenterVertical | GravityFlags.End);
            layers.SetLayerInsetEnd(kChevronLayer, paddingHorizontal);

            m_spinner.Background = layers;
            m_spinner.SetPaddingRelative(paddingHorizontal, paddingVertical, (paddingHorizontal * 2) + chevronWidth, paddingVertical);
            m_spinner.SetPopupBackgroundDrawable(new ColorDrawable(ThemeColors.Resolve(context, kDropDownSurface).ToAndroidColor()));
        }

        // Takes a value rather than its Type so it stays clean under trimming without a suppression: the trim analyzer
        //  treats GetType() on a value typed Enum as keeping the public fields GetSelectableValues reads, which holds
        //  because the trimmer keeps every field of an enum type it keeps. EnumToggleStrip.RebuildItems has the longer story.
        private void RebuildOptions (Enum valueOfTheNewType)
        {
            m_enumType = valueOfTheNewType.GetType();
            m_values = EnumSelectionOptions.GetSelectableValues(valueOfTheNewType.GetType()).ToArray();
            List<string> names = m_values.Select(EnumSelectionOptions.GetDisplayName).ToList();
            ArrayAdapter<string> adapter = ThemeColors.IsThemeLoaded
                ? new ThemedAdapter(this.Context!, names)
                : new ArrayAdapter<string>(this.Context!, Android.Resource.Layout.SimpleSpinnerItem, names);
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
        // The stock spinner item layouts, with AJut's text: the selected value in the interactive text on the face, and
        //  each option in the normal text over the themed list
        private sealed class ThemedAdapter : ArrayAdapter<string>
        {
            private readonly Android.Graphics.Color m_faceText;
            private readonly Android.Graphics.Color m_dropDownText;

            public ThemedAdapter (Context context, IList<string> names) : base(context, Android.Resource.Layout.SimpleSpinnerItem, names)
            {
                m_faceText = ThemeColors.Resolve(context, kText).ToAndroidColor();
                m_dropDownText = ThemeColors.Resolve(context, kDropDownText).ToAndroidColor();
            }

            public override View GetView (int position, View? convertView, ViewGroup parent)
            {
                View view = base.GetView(position, convertView, parent);
                (view as TextView)?.SetTextColor(m_faceText);
                return view;
            }

            public override View? GetDropDownView (int position, View? convertView, ViewGroup? parent)
            {
                View? view = base.GetDropDownView(position, convertView, parent);
                (view as TextView)?.SetTextColor(m_dropDownText);
                return view;
            }
        }

        // A down chevron filling its bounds: two strokes meeting at the bottom middle, rounded at their ends
        private sealed class ChevronDrawable : Drawable
        {
            private readonly Paint m_paint = new Paint(PaintFlags.AntiAlias);
            private readonly Android.Graphics.Path m_path = new Android.Graphics.Path();

            public ChevronDrawable (Android.Graphics.Color color, float thickness)
            {
                m_paint.Color = color;
                m_paint.StrokeWidth = thickness;
                m_paint.SetStyle(Paint.Style.Stroke);
                m_paint.StrokeCap = Paint.Cap.Round;
                m_paint.StrokeJoin = Paint.Join.Round;
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
