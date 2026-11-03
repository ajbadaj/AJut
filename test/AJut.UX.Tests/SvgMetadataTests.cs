namespace AJut.UX.Tests
{
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>The metadata an svg keeps beside its drawing: <see cref="SvgMetadata"/> and its storage classes</summary>
    [TestClass]
    public class SvgMetadataTests
    {
        [TestMethod]
        public void NamespaceStorage_AddWithItsPrefix_KeepsTheWholeName ()
        {
            var storage = new SvgNamespaceMetadataStorage("inkscape");
            storage.Add("inkscape:label", "Layer 1");
            Assert.IsTrue(storage.TryGetArbitraryData("label", out string label), "The prefix and its colon come off, and nothing more");
            Assert.AreEqual("Layer 1", label);
        }

        [TestMethod]
        public void Duplicate_NamespaceStorage_IsACopyNotTheSameStorage ()
        {
            var original = new SvgMetadata("doc");
            original.GetOrGenerateNamespace("inkscape", "http://www.inkscape.org/namespaces/inkscape").Add("version", "1.0");

            SvgMetadata duplicate = original.Duplicate();
            SvgNamespaceMetadataStorage copied = duplicate.GetOrGenerateNamespace("inkscape");
            Assert.AreEqual("http://www.inkscape.org/namespaces/inkscape", copied.SourceUrl);
            Assert.IsTrue(copied.TryGetArbitraryData("version", out string version));
            Assert.AreEqual("1.0", version);

            copied.Add("label", "changed in the duplicate");
            Assert.IsFalse(original.GetOrGenerateNamespace("inkscape").TryGetArbitraryData("label", out _), "Changing the duplicate leaves the original alone");
        }
    }
}
