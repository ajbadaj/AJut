namespace AJut.UnitTests.Core
{
    using System;
    using AJut.Text;
    using AJut.Text.AJson;
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

        [TestMethod]
        public void JsonStringTracking_TestLargeFile_NumberOfItemsIsExpected()
        {
            string jsonText =
@"
{  
    items : { test : 2 },
    arr : [ 3, 20, 0 ],
    value : 6
}
";
            Json json = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            Assert.IsFalse(json.TextTracking.IsInPlaceholderMode);
            Assert.AreEqual(12, json.TextTracking.Count);
        }

    }
}
