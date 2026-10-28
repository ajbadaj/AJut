namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Android.Content;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// A drop-down of an enum's values, inferred from the type of <see cref="SelectedItem"/>. The options come from
    /// AJut.UX's <see cref="EnumSelectionOptions"/>, so values marked [Browsable(false)] or [ExcludeFromSelection] are
    /// left out and every value is labeled the same way on every AJut toolkit. It is a Spinner underneath, so it looks
    /// and behaves like any other drop-down in the app.
    /// </summary>
    [Register("ajut.ux.controls.EnumComboBox")]
    public class EnumComboBox : FrameLayout
    {
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
        }

        // Takes a value rather than its Type so it stays clean under trimming without a suppression: the trim analyzer
        //  treats GetType() on a value typed Enum as keeping the public fields GetSelectableValues reads, which holds
        //  because the trimmer keeps every field of an enum type it keeps. EnumToggleStrip.RebuildItems has the longer story.
        private void RebuildOptions (Enum valueOfTheNewType)
        {
            m_enumType = valueOfTheNewType.GetType();
            m_values = EnumSelectionOptions.GetSelectableValues(valueOfTheNewType.GetType()).ToArray();
            var adapter = new ArrayAdapter<string>(this.Context!, Android.Resource.Layout.SimpleSpinnerItem, m_values.Select(EnumSelectionOptions.GetDisplayName).ToList());
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
    }
}
