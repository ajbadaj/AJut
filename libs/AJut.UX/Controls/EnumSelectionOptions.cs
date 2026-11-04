namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    /// <summary>
    /// The options an enum picker (EnumToggleStrip, EnumComboBox) offers, and the [Flags] arithmetic a multi-select one
    /// needs, the same on every UI toolkit.
    /// </summary>
    public static class EnumSelectionOptions
    {
        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// The values of <paramref name="enumType"/> a picker offers, in declaration order. A value is left out when it is
        /// marked [Browsable(false)] (System.ComponentModel) or [ExcludeFromSelection] (AJut).
        /// </summary>
        public static IEnumerable<Enum> GetSelectableValues ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type enumType)
        {
            foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                Enum value = (Enum)field.GetValue(null)!;

                BrowsableAttribute? browsable = field.GetCustomAttribute<BrowsableAttribute>(false);
                if ((browsable != null) && !browsable.Browsable)
                {
                    continue;
                }

                if (field.IsDefined(typeof(ExcludeFromSelectionAttribute), false))
                {
                    continue;
                }

                yield return value;
            }
        }

        /// <summary>The label a picker shows for <paramref name="value"/>.</summary>
        public static string GetDisplayName (object value) => value.ToString() ?? string.Empty;

        /// <summary>True when <paramref name="enumType"/> is a [Flags] enum, whose values a multi-select picker combines.</summary>
        public static bool IsFlags (Type enumType) => enumType.IsTaggedWithAttribute<FlagsAttribute>();

        /// <summary>The [Flags] value with every one of <paramref name="values"/> set, as a value of <paramref name="enumType"/>.</summary>
        public static Enum CombineFlags (Type enumType, IEnumerable values)
        {
            long combined = 0;
            foreach (object value in values)
            {
                combined |= Convert.ToInt64(value);
            }

            return (Enum)Enum.ToObject(enumType, combined);
        }

        /// <summary>
        /// True when <paramref name="flag"/> is fully set in <paramref name="combined"/>. A zero flag (a None value) is
        /// in every value, so it only counts as set when <paramref name="combined"/> is zero too.
        /// </summary>
        public static bool IsFlagSelected (Enum combined, object flag)
        {
            long bits = Convert.ToInt64(flag);
            long combinedBits = Convert.ToInt64(combined);
            return (bits == 0) ? (combinedBits == 0) : ((combinedBits & bits) == bits);
        }

        /// <summary>
        /// Brings an enum strip's items in line with <paramref name="value"/>. On a multi-select strip over a [Flags] enum,
        /// every flag set in the value is selected first and every unset one deselected after, so the veto on deselecting
        /// the last selected item never trips while the selection moves. Otherwise the item whose data equals the value is
        /// turned on: a single-select strip turns the others off itself, and a multi-select strip over a plain enum keeps
        /// whatever else was on, since one value only says what to add.
        /// </summary>
        public static void ApplyValueToItems (IEnumerable<ToggleStripItem> items, Enum value, bool allowMultiSelect)
        {
            if (allowMultiSelect && IsFlags(value.GetType()))
            {
                foreach (ToggleStripItem item in items)
                {
                    if (!item.IsSelected && IsFlagSelected(value, item.Data))
                    {
                        item.IsSelected = true;
                    }
                }

                foreach (ToggleStripItem item in items)
                {
                    if (item.IsSelected && !IsFlagSelected(value, item.Data))
                    {
                        item.IsSelected = false;
                    }
                }

                return;
            }

            foreach (ToggleStripItem item in items)
            {
                if (value.Equals(item.Data))
                {
                    if (!item.IsSelected)
                    {
                        item.IsSelected = true;
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// The enum value an enum strip's selection stands for: the [Flags] value with every selected flag set, on a
        /// multi-select strip over a [Flags] enum; otherwise <paramref name="selectedItem"/>, the strip's most recently
        /// selected value. A strip that has not learned its enum type yet passes null, and gets its selected item.
        /// Picking a [Flags] enum's zero value (a None) makes the value zero, whatever else is still selected: it says
        /// "none of these", so a strip that applies the value back to its items turns the rest off.
        /// </summary>
        public static object? ValueFromSelection (Type? enumType, bool allowMultiSelect, IList currentSelection, object? selectedItem)
        {
            if (!allowMultiSelect || (enumType == null) || !IsFlags(enumType))
            {
                return selectedItem;
            }

            if ((selectedItem is Enum picked) && (Convert.ToInt64(picked) == 0) && currentSelection.Contains(picked))
            {
                return picked;
            }

            return CombineFlags(enumType, currentSelection);
        }
    }
}
