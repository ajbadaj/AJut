namespace AJutShowRoom.StackNavTest
{
    using System;
    using System.Timers;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.Controls;
    using DPUtils = AJut.UX.DPUtils<SecondDisplay>;

    public partial class SecondDisplay : UserControl, IStackNavDisplayControl
    {
        public SecondDisplay ()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            this.NavAdapter = adapter;
            this.NavAdapter.Title = "Second Page";
            this.NavAdapter.Closing += this.OnClosing;
        }


        public static readonly DependencyProperty NavAdapterProperty = DPUtils.Register(_ => _.NavAdapter);
        public StackNavAdapter NavAdapter
        {
            get => (StackNavAdapter)this.GetValue(NavAdapterProperty);
            set => this.SetValue(NavAdapterProperty, value);
        }


        private async void OnClosing (object sender, StackNavAttemptingDisplayCloseEventArgs e)
        {
            if (!this.AllowClose)
            {
                e.CanClose = false;
                await NavAdapter.ShowPopover(new ErrorPopover("Example: Stopping close - you need to check 'Allow Close' before closing will work!"));
                return;
            }
        }

        public static readonly DependencyProperty AllowCloseProperty = DPUtils.Register(_ => _.AllowClose);
        public bool AllowClose
        {
            get => (bool)this.GetValue(AllowCloseProperty);
            set => this.SetValue(AllowCloseProperty, value);
        }

        public static readonly DependencyProperty NumberFromFirstProperty = DPUtils.Register(_ => _.NumberFromFirst);
        public double NumberFromFirst
        {
            get => (double)this.GetValue(NumberFromFirstProperty);
            set => this.SetValue(NumberFromFirstProperty, value);
        }

        void IStackNavDisplayControl.SetState (object state) 
        {
            this.NumberFromFirst = Convert.ToDouble(state);
        }

        private void RunSpinWait_OnClick (object sender, RoutedEventArgs e)
        {
            var busyWaitTracker = NavAdapter.GenerateBusyWait();

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

        private void OpenThirdDisplay_OnClick (object sender, RoutedEventArgs e)
        {
            NavAdapter.Navigator.GenerateAndPushDisplay<ThirdDisplay>();
        }

        private async void ShowMessagePopoverExample_OnClick (object sender, RoutedEventArgs e)
        {
            Result<string> basicMessageBox = await NavAdapter.ShowPopover(
                MessageBoxPopover.Generate(
                    "Here's an example messagebox-like-popover. Perhaps it will give you many choices, perhaps it will just give you a few.\n\nIn the end it will probably ask a question?", 
                    "Yes", "No"
                )
            );
            if (basicMessageBox && basicMessageBox.Value == "Yes")
            {
                MessageBox.Show("You picked yes!");
            }
        }
    }
}
