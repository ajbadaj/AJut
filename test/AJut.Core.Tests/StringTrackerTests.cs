namespace AJut.Core.UnitTests
{
    using AJut.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StringTrackerTests
    {
        [TestMethod]
        public void StringValue_Update_SourceIsChangedProperly()
        {
            TrackedStringManager source = new TrackedStringManager("test 123");
            TrackedString number = source.Track(5, 3);

            Assert.AreEqual("123", number.StringValue);
            number.StringValue = "456";

            Assert.AreEqual("test 456", source.Text);
        }
    }
}
