namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.App;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Graphics.Drawables;
    using Android.Runtime;
    using Android.Text;
    using Android.Util;
    using Android.Views;
    using Android.Widget;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// A color swatch that edits its color. Tapping it opens a picker (hue, saturation and value, alpha, and a hex
    /// field) in a dialog; the swatch follows the picker live, and closing the dialog completes the edit.
    /// </summary>
    /// <remarks>
    /// Edit lifecycle, the same as the WinUI3 and WPF versions:
    /// <list type="bullet">
    /// <item>Tap the swatch: the starting color is remembered and the picker opens on it.</item>
    /// <item>Each change in the picker sets EditColor straight away, so whatever is bound to it previews live.</item>
    /// <item>The dialog closes: UserEditComplete fires once, if the color ended up different from where it started.</item>
    /// </list>
    /// IsReadOnly stops the picker from opening. In a layout, ajutEditColor sets the starting color.
    ///
    /// EditColor is AJut.UX's ColorArgb, the same type on every AJut toolkit, so a consumer maps its own color type to
    /// one PropertyGrid editor key everywhere. ToAndroidColor and ToColorArgb convert to and from Android's Color.
    /// </remarks>
    [Register("ajut.ux.controls.ColorEditIngressControl")]
    public class ColorEditIngressControl : View, IUserEditNotifier
    {
        private const float kMinWidthDp = 60f;
        private const float kMinHeightDp = 28f;
        private const float kCornerRadiusDp = 2f;
        private const float kBorderThicknessDp = 1f;
        private const float kDogEarMaxDp = 24f;

        private static readonly ThemedColor kNormalBorder = ThemedColor.FromArgb(AJutThemeKeys.ColorEditIngressControl_NormalBorderBrush, 0xFF888888, 0xFF888888);
        private static readonly ThemedColor kHighlightBorder = ThemedColor.FromArgb(AJutThemeKeys.ColorEditIngressControl_HighlightBorderBrush, 0xFF186DBA, 0xFF186DBA);

        private readonly Paint m_paint = new Paint(PaintFlags.AntiAlias);
        private readonly Path m_clip = new Path();
        private readonly Path m_dogEar = new Path();
        private RectF m_bounds = new RectF();
        private Shader? m_glossShader;
        private float m_density = 1f;
        private ColorArgb m_editColor = new ColorArgb(0xFF, 0xFF, 0xFF, 0xFF);
        private bool m_isReadOnly;
        private bool m_showEditDisplay;
        private ColorEditor? m_openEditor;

        // ===========[ Setup/Construction/Teardown ]===================================
        public ColorEditIngressControl (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public ColorEditIngressControl (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public ColorEditIngressControl (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected ColorEditIngressControl (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // A dialog left open when its view goes away leaks the window, so the edit ends with the view.
        protected override void OnDetachedFromWindow ()
        {
            this.CloseEditor();
            base.OnDetachedFromWindow();
        }

        // ===========[ Events ]===================================
        /// <inheritdoc/>
        public event EventHandler<UserEditAppliedEventArgs>? UserEditComplete;

        /// <summary>Raised whenever <see cref="EditColor"/> changes, from the picker or from code. Bind two-way through this.</summary>
        public event EventHandler<EventArgs<ColorArgb>>? EditColorChanged;

        // ===========[ Properties ]===================================
        /// <summary>The color being edited.</summary>
        public ColorArgb EditColor
        {
            get => m_editColor;
            set
            {
                if (m_editColor == value)
                {
                    return;
                }

                m_editColor = value;
                m_openEditor?.ShowColor(value.ToAndroidColor());
                this.Invalidate();
                this.EditColorChanged?.Invoke(this, new EventArgs<ColorArgb>(value));
            }
        }

        /// <summary>Stops the picker from opening.</summary>
        public bool IsReadOnly
        {
            get => m_isReadOnly;
            set => m_isReadOnly = value;
        }

        /// <summary>True while the picker is open.</summary>
        public bool ShowEditDisplay => m_showEditDisplay;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>Opens the picker on the current color, unless it is already open or the control is read only.</summary>
        public void OpenEditor ()
        {
            if (m_isReadOnly || (m_openEditor != null))
            {
                return;
            }

            m_openEditor = new ColorEditor(this, m_editColor);
            m_showEditDisplay = true;
            this.Invalidate();
            m_openEditor.Show();
        }

        /// <summary>Closes the picker if it is open, which completes the edit.</summary>
        public void CloseEditor () => m_openEditor?.Close();

        public override bool PerformClick ()
        {
            base.PerformClick();
            this.OpenEditor();
            return true;
        }

        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            int minWidth = (int)(kMinWidthDp * m_density) + this.PaddingLeft + this.PaddingRight;
            int minHeight = (int)(kMinHeightDp * m_density) + this.PaddingTop + this.PaddingBottom;
            this.SetMeasuredDimension(
                ResolveSize(Math.Max(minWidth, this.SuggestedMinimumWidth), widthMeasureSpec),
                ResolveSize(Math.Max(minHeight, this.SuggestedMinimumHeight), heightMeasureSpec)
            );
        }

        protected override void OnSizeChanged (int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            float inset = (kBorderThicknessDp * m_density) / 2f;
            m_bounds = new RectF(
                this.PaddingLeft + inset,
                this.PaddingTop + inset,
                w - this.PaddingRight - inset,
                h - this.PaddingBottom - inset
            );

            float radius = kCornerRadiusDp * m_density;
            m_clip.Reset();
            m_clip.AddRoundRect(m_bounds, radius, radius, Path.Direction.Cw!);

            // The gloss reads as a soft bevel: lighter above the middle, darker below it, with a hard step at the middle.
            m_glossShader = new LinearGradient(
                0, m_bounds.Top, 0, m_bounds.Bottom,
                new int[] { 0x25FFFFFF, 0x0CFFFFFF, 0x0C000000, 0x23000000 },
                new float[] { 0f, 0.5f, 0.5f, 1f },
                Shader.TileMode.Clamp!
            );

            float dogEar = Math.Min(kDogEarMaxDp * m_density, Math.Min(m_bounds.Width(), m_bounds.Height()));
            m_dogEar.Reset();
            m_dogEar.MoveTo(m_bounds.Right - dogEar, m_bounds.Bottom);
            m_dogEar.LineTo(m_bounds.Right, m_bounds.Bottom);
            m_dogEar.LineTo(m_bounds.Right, m_bounds.Bottom - dogEar);
            m_dogEar.Close();
        }

        protected override void OnDraw (Canvas canvas)
        {
            base.OnDraw(canvas);
            Context context = this.Context!;

            canvas.Save();
            canvas.ClipPath(m_clip);

            // 1. White under the swatch, so a translucent color shows how translucent it is
            canvas.DrawColor(AndroidColor.White);
            canvas.DrawColor(m_editColor.ToAndroidColor());

            // 2. Gloss at rest; the edit overlay (a dim and a dog-ear) while pressed or editing
            bool showEditOverlay = m_showEditDisplay || this.Pressed;
            if (showEditOverlay)
            {
                canvas.DrawColor(new AndroidColor(0x60000000));
                m_paint.SetStyle(Paint.Style.Fill!);
                m_paint.Color = new AndroidColor(0x33505050);
                canvas.DrawPath(m_dogEar, m_paint);
            }
            else if (m_glossShader != null)
            {
                m_paint.SetStyle(Paint.Style.Fill!);
                m_paint.SetShader(m_glossShader);
                canvas.DrawRect(m_bounds, m_paint);
                m_paint.SetShader(null);
            }

            canvas.Restore();

            // 3. The border, highlighted while the picker is open
            float radius = kCornerRadiusDp * m_density;
            m_paint.SetStyle(Paint.Style.Stroke!);
            m_paint.StrokeWidth = kBorderThicknessDp * m_density;
            m_paint.Color = ThemeColors.Resolve(context, m_showEditDisplay ? kHighlightBorder : kNormalBorder).ToAndroidColor();
            canvas.DrawRoundRect(m_bounds, radius, radius, m_paint);
        }

        protected override void DrawableStateChanged ()
        {
            base.DrawableStateChanged();
            this.Invalidate();
        }

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            this.Clickable = true;
            this.Focusable = true;
            this.ContentDescription = "Color";

            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutColorEditIngressControl);
                m_editColor = new AndroidColor(values.GetColor(Resource.Styleable.AJutColorEditIngressControl_ajutEditColor, m_editColor.ToAndroidColor().ToArgb())).ToColorArgb();
                m_isReadOnly = values.GetBoolean(Resource.Styleable.AJutColorEditIngressControl_ajutIsReadOnly, false);
                values.Recycle();
            }
        }

        // Called by the open editor once its dialog has gone, however it went.
        private void OnEditorClosed (ColorArgb startColor)
        {
            m_openEditor = null;
            m_showEditDisplay = false;
            this.Invalidate();

            if (startColor != m_editColor)
            {
                this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(startColor, m_editColor));
            }
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// One open edit: the dialog, the picker, the hex field and the preview, with every subscription between them
        /// and the control. It is made when the picker opens and dropped when it closes, so nothing outlives an edit.
        /// </summary>
        private sealed class ColorEditor
        {
            private readonly ColorEditIngressControl m_owner;
            private readonly ColorArgb m_startColor;
            private readonly HsvColorPicker m_picker;
            private readonly EditText m_hex;
            private readonly GradientDrawable m_preview = new GradientDrawable();
            private readonly AlertDialog m_dialog;
            private bool m_isPushing;
            private bool m_isClosed;

            public ColorEditor (ColorEditIngressControl owner, ColorArgb startColor)
            {
                m_owner = owner;
                m_startColor = startColor;
                Context context = owner.Context!;
                float density = owner.m_density;
                int padding = (int)(16 * density);

                // 1. Picker, then a row with the live preview and the hex field
                m_picker = new HsvColorPicker(context) { Color = startColor.ToAndroidColor() };

                var preview = new View(context) { Background = m_preview };
                m_preview.SetCornerRadius(kCornerRadiusDp * density);
                m_preview.SetStroke((int)Math.Max(1f, density), new AndroidColor(unchecked((int)0xFF888888)));
                var previewSize = new LinearLayout.LayoutParams((int)(40 * density), (int)(32 * density));
                previewSize.MarginEnd = (int)(12 * density);
                preview.LayoutParameters = previewSize;

                m_hex = new EditText(context)
                {
                    InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions | InputTypes.TextFlagCapCharacters,
                    Typeface = Typeface.Monospace,
                    Hint = "#AARRGGBB",
                };
                m_hex.SetSingleLine(true);
                m_hex.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);

                var hexRow = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Horizontal };
                hexRow.SetGravity(GravityFlags.CenterVertical);
                hexRow.SetPadding(0, padding / 2, 0, 0);
                hexRow.AddView(preview);
                hexRow.AddView(m_hex);

                var content = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Vertical };
                content.SetPadding(padding, padding, padding, 0);
                content.AddView(m_picker);
                content.AddView(hexRow);

                this.ShowColor(startColor.ToAndroidColor());

                // 2. The dialog. Picker changes write straight through to the control (a live preview), and however
                //    the dialog goes away (Done, back, a tap outside) the dismiss handler ends the edit.
                m_dialog = new AlertDialog.Builder(context)
                    .SetView(content)!
                    .SetPositiveButton("Done", this.OnDoneClicked)!
                    .Create()!;

                m_picker.ColorChanged += this.OnPickerColorChanged;
                m_hex.TextChanged += this.OnHexTextChanged;
                m_dialog.DismissEvent += this.OnDialogDismissed;
            }

            public void Show () => m_dialog.Show();

            public void Close ()
            {
                if (!m_isClosed)
                {
                    m_dialog.Dismiss();
                }
            }

            /// <summary>Brings the picker, hex field and preview to <paramref name="color"/> without writing back.</summary>
            public void ShowColor (AndroidColor color)
            {
                m_isPushing = true;
                try
                {
                    if (m_picker.Color != color)
                    {
                        m_picker.Color = color;
                    }

                    // Leave the field alone when it already says this color in another spelling (lower case, or
                    // #FF alpha written out), so the user's own typing is not rewritten under the caret.
                    if (!_TryParseHex(m_hex.Text, out AndroidColor shown) || (shown != color))
                    {
                        m_hex.Text = _FormatHex(color);
                    }

                    m_preview.SetColor(color);
                }
                finally
                {
                    m_isPushing = false;
                }
            }

            private void OnPickerColorChanged (object? sender, EventArgs<AndroidColor> e)
            {
                if (!m_isPushing)
                {
                    m_owner.EditColor = e.Value.ToColorArgb();
                }
            }

            // Partial or unparseable text is left alone while the user types; it applies once it reads as a color.
            private void OnHexTextChanged (object? sender, TextChangedEventArgs e)
            {
                if (m_isPushing)
                {
                    return;
                }

                if (_TryParseHex(m_hex.Text, out AndroidColor color))
                {
                    m_isPushing = true;
                    try
                    {
                        m_picker.Color = color;
                        m_preview.SetColor(color);
                    }
                    finally
                    {
                        m_isPushing = false;
                    }

                    m_owner.EditColor = color.ToColorArgb();
                }
            }

            private void OnDoneClicked (object? sender, DialogClickEventArgs e) => this.Close();

            private void OnDialogDismissed (object? sender, EventArgs e)
            {
                m_isClosed = true;
                m_picker.ColorChanged -= this.OnPickerColorChanged;
                m_hex.TextChanged -= this.OnHexTextChanged;
                m_dialog.DismissEvent -= this.OnDialogDismissed;
                m_owner.OnEditorClosed(m_startColor);
            }

            // Only full #RRGGBB or #AARRGGBB counts, so a half typed value never applies.
            private static bool _TryParseHex (string? text, out AndroidColor color)
            {
                string hex = (text ?? string.Empty).Trim();
                if (!hex.StartsWith('#'))
                {
                    hex = "#" + hex;
                }

                if (((hex.Length == 7) || (hex.Length == 9))
                    && AJutColorHelper.TryGetColorFromHex(hex, out byte[] argb))
                {
                    color = AndroidColor.Argb(argb[0], argb[1], argb[2], argb[3]);
                    return true;
                }

                color = default;
                return false;
            }

            private static string _FormatHex (AndroidColor color)
            {
                return color.A == 255
                    ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
                    : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }
        }
    }
}
