#nullable enable
namespace AJut.UX.Tests
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using AJut;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EnumSelectionOptionsTests
    {
        [TestMethod]
        public void GetSelectableValues_HidesBrowsableFalseAndExcludeFromSelection ()
        {
            Enum[] values = EnumSelectionOptions.GetSelectableValues(typeof(eWithHidden)).ToArray();
            CollectionAssert.AreEqual(new Enum[] { eWithHidden.Shown, eWithHidden.AlsoShown }, values);
        }

        [TestMethod]
        public void GetSelectableValues_KeepsDeclarationOrder ()
        {
            Enum[] values = EnumSelectionOptions.GetSelectableValues(typeof(ePlain)).ToArray();
            CollectionAssert.AreEqual(new Enum[] { ePlain.Zeta, ePlain.Alpha, ePlain.Mid }, values);
        }

        [TestMethod]
        public void GetSelectableValues_IsDeclarationOrder_NotValueOrder ()
        {
            // Enum.GetValues would sort these by value (Low, Mid, High); the options keep the order they are written in.
            Enum[] values = EnumSelectionOptions.GetSelectableValues(typeof(eOutOfOrder)).ToArray();
            CollectionAssert.AreEqual(new Enum[] { eOutOfOrder.High, eOutOfOrder.Low, eOutOfOrder.Mid }, values);
        }

        [TestMethod]
        public void CombineFlags_OrsTheValues ()
        {
            Enum combined = EnumSelectionOptions.CombineFlags(typeof(eFlags), new object[] { eFlags.One, eFlags.Four });
            Assert.AreEqual(eFlags.One | eFlags.Four, combined);
        }

        [TestMethod]
        public void IsFlagSelected_NeedsEveryBit_AndNeverCountsZero ()
        {
            Enum combined = eFlags.One | eFlags.Two;
            Assert.IsTrue(EnumSelectionOptions.IsFlagSelected(combined, eFlags.One));
            Assert.IsTrue(EnumSelectionOptions.IsFlagSelected(combined, eFlags.OneAndTwo));
            Assert.IsFalse(EnumSelectionOptions.IsFlagSelected(combined, eFlags.Four));
            Assert.IsFalse(EnumSelectionOptions.IsFlagSelected(combined, eFlags.None), "None is in every value, so it never counts as picked");
        }

        [TestMethod]
        public void IsFlagSelected_ZeroCountsWhenTheValueIsZero ()
        {
            Assert.IsTrue(EnumSelectionOptions.IsFlagSelected(eFlags.None, eFlags.None), "A value of None is None picked");
            Assert.IsFalse(EnumSelectionOptions.IsFlagSelected(eFlags.None, eFlags.One));
        }

        [TestMethod]
        public void ApplyValueToItems_FlagsMultiSelect_ZeroValueSelectsOnlyTheZeroItem ()
        {
            ToggleStripItemCollection items = MakeItems(allowMultiSelect: true, EnumSelectionOptions.GetSelectableValues(typeof(eFlags)));
            items.SelectData(eFlags.Four);

            EnumSelectionOptions.ApplyValueToItems(items, eFlags.None, allowMultiSelect: true);

            CollectionAssert.AreEqual(new object[] { eFlags.None }, items.Where(i => i.IsSelected).Select(i => i.Data).ToArray());
        }

        [TestMethod]
        public void ValueFromSelection_FlagsMultiSelect_PickingTheZeroValueClearsTheRest ()
        {
            // Picking None says "none of these", so it wins over the flags still selected beside it
            var selection = new object[] { eFlags.One, eFlags.Four, eFlags.None };
            Assert.AreEqual(eFlags.None, EnumSelectionOptions.ValueFromSelection(typeof(eFlags), allowMultiSelect: true, selection, eFlags.None));
        }

        [TestMethod]
        public void ApplyValueToItems_FlagsMultiSelect_SelectsExactlyTheSetFlags ()
        {
            ToggleStripItemCollection items = MakeItems(allowMultiSelect: true, EnumSelectionOptions.GetSelectableValues(typeof(eFlags)));
            items.SelectData(eFlags.Four);

            // Moving from Four to One|Two must never pass through "nothing selected", which the strip would refuse.
            EnumSelectionOptions.ApplyValueToItems(items, eFlags.One | eFlags.Two, allowMultiSelect: true);

            CollectionAssert.AreEqual(new object[] { eFlags.One, eFlags.Two, eFlags.OneAndTwo }, items.Where(i => i.IsSelected).Select(i => i.Data).ToArray());
        }

        [TestMethod]
        public void ApplyValueToItems_SingleSelect_TurnsTheValueOn ()
        {
            ToggleStripItemCollection items = MakeItems(allowMultiSelect: false, EnumSelectionOptions.GetSelectableValues(typeof(ePlain)));

            EnumSelectionOptions.ApplyValueToItems(items, ePlain.Mid, allowMultiSelect: false);

            CollectionAssert.AreEqual(new object[] { ePlain.Mid }, items.Where(i => i.IsSelected).Select(i => i.Data).ToArray());
        }

        [TestMethod]
        public void ApplyValueToItems_PlainEnumMultiSelect_KeepsTheOthers ()
        {
            ToggleStripItemCollection items = MakeItems(allowMultiSelect: true, EnumSelectionOptions.GetSelectableValues(typeof(ePlain)));

            EnumSelectionOptions.ApplyValueToItems(items, ePlain.Mid, allowMultiSelect: true);

            CollectionAssert.AreEqual(new object[] { ePlain.Zeta, ePlain.Mid }, items.Where(i => i.IsSelected).Select(i => i.Data).ToArray(), "One value only says what to add");
        }

        [TestMethod]
        public void ValueFromSelection_CombinesFlags_OnlyForFlagsMultiSelect ()
        {
            var selection = new object[] { eFlags.One, eFlags.Four };
            Assert.AreEqual(eFlags.One | eFlags.Four, EnumSelectionOptions.ValueFromSelection(typeof(eFlags), allowMultiSelect: true, selection, eFlags.Four));
            Assert.AreEqual(eFlags.Four, EnumSelectionOptions.ValueFromSelection(typeof(eFlags), allowMultiSelect: false, selection, eFlags.Four));
            Assert.AreEqual(ePlain.Mid, EnumSelectionOptions.ValueFromSelection(typeof(ePlain), allowMultiSelect: true, new object[] { ePlain.Zeta, ePlain.Mid }, ePlain.Mid));
        }

        [TestMethod]
        public void ValueFromSelection_EnumTypeNotKnownYet_IsTheSelectedItem ()
        {
            // An enum strip learns its enum type from its first value. Until then there is no type to check for [Flags],
            // so the selected item is the only value there is, the same as on a strip that is not multi-select.
            object? value = EnumSelectionOptions.ValueFromSelection(null!, allowMultiSelect: true, new object[] { eFlags.Four }, eFlags.Four);
            Assert.AreEqual(eFlags.Four, value);
        }

        [TestMethod]
        public void IsFlags_ReadsTheFlagsAttribute ()
        {
            Assert.IsTrue(EnumSelectionOptions.IsFlags(typeof(eFlags)));
            Assert.IsFalse(EnumSelectionOptions.IsFlags(typeof(ePlain)));
        }

        [TestMethod]
        public void GetDisplayName_IsTheValueName ()
        {
            Assert.AreEqual("Alpha", EnumSelectionOptions.GetDisplayName(ePlain.Alpha));
        }

        // ===========[ Helper Methods ]===================================
        // An enum strip's items, the way a toolkit's EnumToggleStrip builds them; ResetFromSource selects the first value.
        private static ToggleStripItemCollection MakeItems (bool allowMultiSelect, System.Collections.Generic.IEnumerable<Enum> values)
        {
            var items = new ToggleStripItemCollection(new StripHost(allowMultiSelect));
            items.ResetFromSource(values.ToArray(), previousSelection: null);
            return items;
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class StripHost : IToggleStripHost
        {
            public StripHost (bool allowMultiSelect)
            {
                this.AllowMultiSelect = allowMultiSelect;
            }

            public bool AllowMultiSelect { get; }

            public bool AllowNoSelection => false;

            public string? DisplayPropertyPath => null;

            public Func<object, string?>? ItemNameSelector => null;

            public void OnSelectionChanged (ToggleStripSelectionChange change)
            {
            }
        }

        public enum ePlain
        {
            Zeta,
            Alpha,
            Mid,
        }

        public enum eOutOfOrder
        {
            High = 3,
            Low = 1,
            Mid = 2,
        }

        public enum eWithHidden
        {
            Shown,
            [Browsable(false)]
            NotBrowsable,
            [ExcludeFromSelection]
            Excluded,
            AlsoShown,
        }

        [Flags]
        public enum eFlags
        {
            None = 0,
            One = 1,
            Two = 2,
            OneAndTwo = One | Two,
            Four = 4,
        }
    }
}
