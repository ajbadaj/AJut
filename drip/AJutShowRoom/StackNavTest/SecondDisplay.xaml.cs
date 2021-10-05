namespace AJut.TestApp.WPF.StackNavTest
{
    using System;
    using System.Timers;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<SecondDisplay>;

    public partial class SecondDisplay : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter m_adapter;
        public SecondDisplay ()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "Second Page";
        }

        public static readonly DependencyProperty NumberFromFirstProperty = DPUtils.Register(_ => _.NumberFromFirst);
        public double NumberFromFirst
        {
            get => (double)this.GetValue(NumberFromFirstProperty);
            set => this.SetValue(NumberFromFirstProperty, value);
        }

        void IStackNavDisplayControl.SetState (object state) 
        {
            this.NumberFromFirst = (double)state;
        }

        private void RunSpinWait_OnClick (object sender, RoutedEventArgs e)
        {
            var busyWaitTracker = m_adapter.GenerateBusyWait();

            Timer spinWaitStackNavTimer = new Timer();
            spinWaitStackNavTimer.Interval = 2000;
            spinWaitStackNavTimer.Elapsed += _OnCloseSpinWait;
            spinWaitStackNavTimer.Start();

            void _OnCloseSpinWait (object _sender, ElapsedEventArgs _e)
            {
                spinWaitStackNavTimer.Elapsed -= _OnCloseSpinWait;
                busyWaitTracker.Dispose();
                spinWaitStackNavTimer.Stop();
                spinWaitStackNavTimer.Dispose();
                spinWaitStackNavTimer = null;
            }
        }
    }
}
