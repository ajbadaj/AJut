namespace AJut.Core.UnitTests
{
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using AJut.MathUtilities;
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

        [TestMethod]
        public void NotifyPropertyChanged_CallerMemberNameDefaultWorks_Complex ()
        {
            bool gotPropertyChanged = false;
            var test = new TestNotifyPropChangedThing();
            string expected = nameof(TestNotifyPropChangedThing.FooComplex);
            test.PropertyChanged += _OnPropertyChanged;
            test.FooComplex = 22.0;
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

            public double Min { get; set; } = -1000.0;
            public double Max { get; set; } = 1000.0;

            public bool ComplexWasSet { get; set; }


            private double m_fooComplex;
            public double FooComplex
            {
                get => m_fooComplex;
                set
                {
                    this.ComplexWasSet = false;
                    if (this.SetAndRaiseIfChanged(ref m_fooComplex, Cap.Within(this.Min, this.Max, value)))
                    {
                        this.ComplexWasSet = true;
                    }
                }
            }


        }
    }
}
