namespace AJut.UnitTests.Core
{
    using System;
    using AJut.Text;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public static class TestHelpers
    {
        public static void AssertIsValid(this TrackedStringManager mgr)
        {
            int last = -1;
            foreach (TrackedString ts in mgr)
            {
                //Assert.IsTrue(ts.OffsetInSource >= last, "Offset was less last! Source is out of order.");
                last = ts.OffsetInSource;

                string stringValueAfterOffsetChange = mgr.Text.Substring(ts.OffsetInSource, ts.StringValue.Length);
                Assert.AreEqual(ts.StringValue, stringValueAfterOffsetChange);
            }
        }

        public static void AssertSourceIsValid(this Json json)
        {
            json.TextTracking.AssertIsValid();
        }

        public static string BuildJsonErrorReport(this Json json)
        {
            return json.HasErrors
                    ? "Json parse errors:\n" + String.Join("\n\t", json.Errors)
                    : "<No Errors>";
        }
    }
}
