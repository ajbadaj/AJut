namespace AJut.UnitTests.Core
{
    using AJut.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TrackedStringManagerTests
    {
        TrackedStringManager m_manager = new TrackedStringManager("test1 test2 test3 test4");
        TrackedString m_ts1, m_ts2, m_ts3, m_ts4;

        [TestInitialize]
        public void TestSetup()
        {
            m_ts1 = m_manager.Track(0, 5);
            m_ts2 = m_manager.Track(6, 5);
            m_ts3 = m_manager.Track(12, 5);
            m_ts4 = m_manager.Track(18, 5);
        }



        [TestMethod]
        public void TrackedStringManager_BasicTracking_CanTrackSimpleStringPieces()
        {
            Assert.AreEqual("test1", m_ts1.StringValue);
            Assert.AreEqual("test2", m_ts2.StringValue);
            Assert.AreEqual("test3", m_ts3.StringValue);
            Assert.AreEqual("test4", m_ts4.StringValue);
        }

        [TestMethod]
        public void TrackedStringManager_UpdateTrackedValue_WithSmaller_StringUpdatesPropogateProperly()
        {
            TrackedStringManager_BasicTracking_CanTrackSimpleStringPieces();

            m_ts2.StringValue = "2";
            Assert.AreEqual("test1", m_ts1.StringValue);
            Assert.AreEqual("2", m_ts2.StringValue);
            Assert.AreEqual("test3", m_ts3.StringValue);
            Assert.AreEqual("test4", m_ts4.StringValue);
        }

        [TestMethod]
        public void TrackedStringManager_UpdateTrackedValue_WithLarger_StringUpdatesPropogateProperly()
        {
            TrackedStringManager_BasicTracking_CanTrackSimpleStringPieces();

            m_ts2.StringValue = "largerthanbefore2";
            Assert.AreEqual("test1", m_ts1.StringValue);
            Assert.AreEqual("largerthanbefore2", m_ts2.StringValue);
            Assert.AreEqual("test3", m_ts3.StringValue);
            Assert.AreEqual("test4", m_ts4.StringValue);
        }
    }
}
