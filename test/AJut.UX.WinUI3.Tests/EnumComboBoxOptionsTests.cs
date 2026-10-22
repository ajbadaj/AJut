namespace AJut.UX.WinUI3.Tests
{
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>The options EnumComboBox lists for an enum type, checked without making the control.</summary>
    [TestClass]
    public class EnumComboBoxOptionsTests
    {
        [TestMethod]
        public void BuildOptions_ListsTheValuesInDeclarationOrder_NamedByValue ()
        {
            EnumComboBox.EnumItemStorage[] options = EnumComboBox.EnumItemStorage.BuildOptions(typeof(ePlain));
            CollectionAssert.AreEqual(new object[] { ePlain.Zeta, ePlain.Alpha, ePlain.Mid }, options.Select(o => o.Value).ToArray());
            CollectionAssert.AreEqual(new[] { "Zeta", "Alpha", "Mid" }, options.Select(o => o.Name).ToArray());
        }

        [TestMethod]
        public void BuildOptions_IsDeclarationOrder_NotValueOrder ()
        {
            // The options follow EnumSelectionOptions, which keeps the order the values are written in, the same as
            // EnumToggleStrip. Enum.GetValues, which EnumComboBox used before, sorts them by value.
            EnumComboBox.EnumItemStorage[] options = EnumComboBox.EnumItemStorage.BuildOptions(typeof(eOutOfOrder));
            CollectionAssert.AreEqual(new object[] { eOutOfOrder.High, eOutOfOrder.Low, eOutOfOrder.Mid }, options.Select(o => o.Value).ToArray());
        }

        [TestMethod]
        public void BuildOptions_HidesBrowsableFalseAndExcludeFromSelection ()
        {
            EnumComboBox.EnumItemStorage[] options = EnumComboBox.EnumItemStorage.BuildOptions(typeof(eWithHidden));
            CollectionAssert.AreEqual(new object[] { eWithHidden.Shown, eWithHidden.AlsoShown }, options.Select(o => o.Value).ToArray());
        }

        // ===========[ Subclasses/structs ]===================================
        public enum eOutOfOrder
        {
            High = 3,
            Low = 1,
            Mid = 2,
        }

        public enum eWithHidden
        {
            Shown,
            [System.ComponentModel.Browsable(false)]
            NotBrowsable,
            [ExcludeFromSelection]
            Excluded,
            AlsoShown,
        }

        public enum ePlain
        {
            Zeta,
            Alpha,
            Mid,
        }
    }
}
