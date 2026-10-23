namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Linq;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    // ===========[ EnumToggleStrip ]============================================
    // A ToggleStrip of an enum's values, inferred from SelectedItem like WinUI3's: setting a value of a new enum type
    // rebuilds the options (AJut.UX's EnumSelectionOptions, so [Browsable(false)] and [ExcludeFromSelection] values are
    // left out). The sync between the value and the strip's selection is AJut.UX's too:
    //  - Single select: SelectedItem is the selected value.
    //  - Multi-select on a [Flags] enum: SelectedItem is the OR of every selected value, and SelectedItems the values.
    //    Picking the enum's zero value (a None) makes SelectedItem zero and turns the other flags off.
    //  - Multi-select on any other enum: SelectedItem is the most recently selected value, and SelectedItems all of them.
    // A user change sends ValueChanged; a SelectedItem set from code does not. The inner strip is exposed as Strip, for
    // its look (ItemPadding, SeparatorThickness, OverflowBehavior and the rest).

    public class EnumToggleStrip : UIControl
    {
        // ===========[ Instance Fields ]===================================
        private readonly ToggleStrip m_strip = new ToggleStrip { TranslatesAutoresizingMaskIntoConstraints = false };
        private Type m_itemType;
        private object m_selectedItem;
        private IList m_selectedItems = Array.Empty<object>();
        private bool m_isSyncing;
        private bool m_isStripHooked;

        // ===========[ Setup/Construction/Teardown ]===================================
        public EnumToggleStrip () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native control.</summary>
        protected EnumToggleStrip (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        private void Setup ()
        {
            this.AddSubview(m_strip);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_strip.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_strip.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                m_strip.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_strip.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
            });
        }

        public override void MovedToWindow ()
        {
            base.MovedToWindow();

            // The strip is a subview, so this control keeps it alive natively, and this control's handler held by the
            // strip would keep this control alive from the managed side, a cycle across native objects. So the strip's
            // event is hooked only while this control is on screen, which is the only time the user can change it.
            if (this.Window != null)
            {
                if (!m_isStripHooked)
                {
                    m_isStripHooked = true;
                    m_strip.SelectionChanged += this.Strip_OnSelectionChanged;
                }
            }
            else
            {
                this.UnhookStrip();
            }
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.UnhookStrip();
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]===================================
        private void Strip_OnSelectionChanged (object sender, ToggleStrip.ToggleStripSelectionChangedEventArgs e)
        {
            if (m_isSyncing || m_itemType == null)
            {
                return;
            }

            object value = EnumSelectionOptions.ValueFromSelection(m_itemType, this.AllowMultiSelect, e.CurrentSelection, m_strip.SelectedItem);

            // The items follow the value they made, so picking a [Flags] None turns the other flags off
            if (value is Enum enumValue)
            {
                this.PushSelectionFromValue(enumValue);
            }
            else
            {
                m_selectedItems = e.CurrentSelection;
            }

            m_selectedItem = value;
            this.SendActionForControlEvents(UIControlEvent.ValueChanged);
        }

        // ===========[ Properties ]===================================
        /// <summary>The selected enum value; see the class notes for what it holds under multi-select. Setting it from code does not send ValueChanged.</summary>
        public object SelectedItem
        {
            get => m_selectedItem;
            set
            {
                m_selectedItem = value;
                if (value is not Enum enumValue)
                {
                    return;
                }

                if (enumValue.GetType() != m_itemType)
                {
                    this.RebuildItems(enumValue);
                }

                this.PushSelectionFromValue(enumValue);
            }
        }

        /// <summary>Every selected enum value, in strip order.</summary>
        public IList SelectedItems => m_selectedItems;

        public bool AllowMultiSelect
        {
            get => m_strip.AllowMultiSelect;
            set => m_strip.AllowMultiSelect = value;
        }

        public bool AllowNoSelection
        {
            get => m_strip.AllowNoSelection;
            set => m_strip.AllowNoSelection = value;
        }

        /// <summary>Whether the options run left to right (Horizontal, the default) or top to bottom; passed on to the inner strip.</summary>
        public UILayoutConstraintAxis Orientation
        {
            get => m_strip.Orientation;
            set => m_strip.Orientation = value;
        }

        /// <summary>The inner strip, for its look; drive the selection through SelectedItem rather than through it.</summary>
        public ToggleStrip Strip => m_strip;

        // ===========[ Helper Methods ]===================================
        private void UnhookStrip ()
        {
            if (m_isStripHooked)
            {
                m_isStripHooked = false;
                m_strip.SelectionChanged -= this.Strip_OnSelectionChanged;
            }
        }

        private void RebuildItems (Enum valueOfTheNewType)
        {
            // This takes a value rather than its Type so it stays clean under trimming without a suppression.
            // GetSelectableValues reads the enum's public static fields (one per value, with their attributes), so its
            // parameter asks for PublicFields to be kept. The trim analyzer treats GetType() on a value typed Enum as
            // meeting that, which holds because the trimmer keeps every field of an enum type it keeps
            // (MarkStep.MarkImplicitlyUsedFields), and a live value's type is kept.
            // A Type handed in from elsewhere is just "some type" to the analyzer, and warns (IL2067). If this ever has to
            // take a Type from outside, that Type needs [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)].
            // The analyzer's rule is the Object_GetType case of:
            //  https://github.com/dotnet/runtime/blob/main/src/tools/illink/src/ILLink.RoslynAnalyzer/TrimAnalysis/HandleCallAction.cs
            Type enumType = valueOfTheNewType.GetType();
            m_itemType = enumType;
            m_isSyncing = true;
            try
            {
                m_strip.ItemsSource = EnumSelectionOptions.GetSelectableValues(enumType).ToArray();
            }
            finally
            {
                m_isSyncing = false;
            }
        }

        private void PushSelectionFromValue (Enum value)
        {
            m_isSyncing = true;
            try
            {
                EnumSelectionOptions.ApplyValueToItems(m_strip.Items, value, this.AllowMultiSelect);
                m_selectedItems = m_strip.Items.Where(_IsSelected).Select(_Data).ToList();
            }
            finally
            {
                m_isSyncing = false;
            }

            static bool _IsSelected (ToggleStripItem item) => item.IsSelected;
            static object _Data (ToggleStripItem item) => item.Data;
        }
    }
}
