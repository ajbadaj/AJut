namespace AJutShowRoomWinUI.UI.Controls
{
    using System;
    using System.ComponentModel;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// EnumComboBox over DayOfWeek. The bound property is a plain property that checks equality before raising, not a
    /// dependency property: WinUI treats every set of an enum-typed dependency property as a change (the value is boxed
    /// each time), so a two-way x:Bind between one of those and SelectedItem feeds itself until the stack overflows.
    /// </summary>
    public sealed partial class EnumComboBoxPage : Page, INotifyPropertyChanged
    {
        private DayOfWeek m_day = DayOfWeek.Friday;
        private string m_dayDescription = "Friday is a fine choice.";

        public EnumComboBoxPage ()
        {
            this.InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DayOfWeek Day
        {
            get => m_day;
            set
            {
                if (m_day == value)
                {
                    return;
                }

                m_day = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Day)));
                this.DayDescription = this.Day switch
                {
                    DayOfWeek.Saturday or DayOfWeek.Sunday => $"{this.Day} is a weekend day.",
                    DayOfWeek.Friday => "Friday is a fine choice.",
                    _ => $"{this.Day} is a weekday.",
                };
            }
        }

        public string DayDescription
        {
            get => m_dayDescription;
            private set
            {
                if (m_dayDescription == value)
                {
                    return;
                }

                m_dayDescription = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DayDescription)));
            }
        }

        public string DemoCode { get; } =
@"<ajut:EnumComboBox SelectedItem=""{x:Bind Day, Mode=TwoWay}"" Width=""160""/>

// A plain property with an equality check. Not a dependency property: WinUI treats every set of an
// enum-typed dependency property as a change, so a two-way binding to SelectedItem would loop.
public DayOfWeek Day
{
    get => m_day;
    set
    {
        if (m_day == value)
        {
            return;
        }

        m_day = value;
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Day)));
    }
}";
    }
}
