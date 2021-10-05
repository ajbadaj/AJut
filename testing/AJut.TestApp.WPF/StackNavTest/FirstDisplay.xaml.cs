namespace AJut.TestApp.WPF.StackNavTest
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.Application;
    using AJut.Storage;
    using DPUtils = AJut.Application.DPUtils<FirstDisplay>;

    public partial class FirstDisplay : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter m_adapter;
        public FirstDisplay ()
        {
            this.InitializeComponent();
        }

        object IStackNavDisplayControl.GenerateState()
        {
            return new State { Number  =  this.Number, Name = this.UserSelectedName };
        }

        void IStackNavDisplayControl.SetState (object state)
        {
            if (state is State stateCasted)
            {
                this.UserSelectedName = stateCasted.Name;
                this.Number = stateCasted.Number;
            }
        }
        private class State
        {
            public string Name { get; init; }
            public double Number { get; init; }
        }

        public static readonly DependencyProperty NumberProperty = DPUtils.Register(_ => _.Number);
        public double Number
        {
            get => (double)this.GetValue(NumberProperty);
            set => this.SetValue(NumberProperty, value);
        }


        public static readonly DependencyProperty UserSelectedNameProperty = DPUtils.Register(_ => _.UserSelectedName, "Default Name");
        public string UserSelectedName
        {
            get => (string)this.GetValue(UserSelectedNameProperty);
            set => this.SetValue(UserSelectedNameProperty, value);
        }


        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "First Page";
        }

        private void GoToSecond_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter.Navigator.GenerateAndPushDisplay<SecondDisplay>(this.Number);
        }

        private async void ChangeName_OnClick (object sender, RoutedEventArgs e)
        {
            Result<string> result = await m_adapter.ShowPopover(new NameSelectorPopover(this.UserSelectedName));
            if (result)
            {
                this.UserSelectedName = result.Value;
            }
        }
    }
}
