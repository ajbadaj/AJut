namespace Tests
{
    using AJut.Application;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Windows;

    public static class FakeAPUtilsCompiles
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(FakeAPUtilsCompiles));
        
        public static readonly DependencyProperty StatusProperty = APUtils.Register(GetStatus, SetStatus);
        
        public static string GetStatus(DependencyObject obj)
        {
            return (string)obj.GetValue(StatusProperty);
        }
        public static void SetStatus(DependencyObject obj, string value)
        {
            obj.SetValue(StatusProperty, value);
        }
    }

    [TestClass]
    public class Application_ProcRunner_Tests
    {
        [TestMethod]
        public void APUtils_Works()
        {
            DependencyObject obj = new DependencyObject();
            FakeAPUtilsCompiles.SetStatus(obj, "sweet");

            Assert.AreEqual("sweet", FakeAPUtilsCompiles.GetStatus(obj));
        }
    }
}
