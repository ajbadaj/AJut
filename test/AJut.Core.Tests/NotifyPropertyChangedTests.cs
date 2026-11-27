namespace AJut.Core.UnitTests
{
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class NotifyPropertyChangedTests
    {
        [TestMethod]
        public void NotifyPropertyChanged_CallerMemberNameDefaultWorks_Basic ()
        {
            bool gotPropertyChanged = false;
            var test = new TestNotifyPropChangedThing();
            string expected = nameof(TestNotifyPropChangedThing.Foo);
            test.PropertyChanged += _OnPropertyChanged;
            test.Foo = 21;
            Assert.IsTrue(gotPropertyChanged);

            void _OnPropertyChanged (object _sender, PropertyChangedEventArgs _e)
            {
                Assert.AreEqual(expected, _e.PropertyName);
                gotPropertyChanged = true;
            }
        }

        private class TestNotifyPropChangedThing : NotifyPropertyChanged
        {
            private int m_foo;

            public int Foo
            {
                get => m_foo;
                set => this.SetAndRaiseIfChanged(ref m_foo, value);
            }
        }
    }
}
