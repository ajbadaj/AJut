namespace AJutShowRoomWinUI.UI.Controls
{
    using System;
    using System.ComponentModel;
    using AJut;
    using AJut.UX.PropertyInteraction;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using DPUtils = AJut.UX.DPUtils<PropertyGridPage>;

    /// <summary>
    /// The PropertyGrid as a control: two small annotated sources to switch between, the grid over them, and a readout
    /// of the current source. The exhaustive source switcher and the repros live on the Debug area's PropertyGrid debug page.
    /// </summary>
    public sealed partial class PropertyGridPage : Page
    {
        private readonly Lamp m_lamp = new Lamp();
        private readonly Speaker m_speaker = new Speaker();
        private INotifyPropertyChanged m_current;

        public PropertyGridPage ()
        {
            this.InitializeComponent();
            this.SetSource(m_lamp);
        }

        public static readonly DependencyProperty SourceSummaryProperty = DPUtils.Register(_ => _.SourceSummary, "(no source)");
        public string SourceSummary
        {
            get => (string)this.GetValue(SourceSummaryProperty);
            set => this.SetValue(SourceSummaryProperty, value);
        }

        public string DemoCode { get; } =
@"// A source is any object. The attributes are optional and only tune the rows.
public class Lamp : NotifyPropertyChanged
{
    [PGLabel(""Name"", ""Shown in the room list"")]
    public string Name { get; set; }

    [PGGroup(""Light"")]
    [PGLabel(""Brightness"")]
    [PGToolTip(""0 is off, 100 is full"")]
    public int Brightness { get; set; }

    [PGGroup(""Light"")]
    public eLampMode Mode { get; set; }

    [PGGroup(""Light"")]
    [PGLabel(""Is on"")]
    public bool IsOn { get; set; }

    [PGHidden]
    public Guid Id { get; }

    [PGButton(""Toggle"")]
    public void Toggle () => this.IsOn = !this.IsOn;
}

// The grid picks a template by the property's type name, or by [PGEditor] when one is set
<ajut:PropertyGrid x:Name=""DemoGrid"" LabelColumnWidth=""140"">
    <ajut:PropertyGrid.ItemTemplateSelector>
        <ajut:PropertyGridTemplateSelector>
            <ajut:PropertyGridTemplateSelector.RegisteredTemplates>
                <DataTemplate x:Key=""String"" x:DataType=""ajut_pg:PropertyEditTarget"">
                    <TextBox Text=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
                <DataTemplate x:Key=""Int32"" x:DataType=""ajut_pg:PropertyEditTarget"">
                    <ajut:NumericEditor Value=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
                <DataTemplate x:Key=""eLampMode"" x:DataType=""ajut_pg:PropertyEditTarget"">
                    <ajut:EnumComboBox SelectedItem=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
            </ajut:PropertyGridTemplateSelector.RegisteredTemplates>
        </ajut:PropertyGridTemplateSelector>
    </ajut:PropertyGrid.ItemTemplateSelector>
</ajut:PropertyGrid>

// Point it at the object; swap the object and the rows rebuild
this.DemoGrid.SingleItemSource = lamp;";

        private void SetSource (INotifyPropertyChanged source)
        {
            if (m_current != null)
            {
                m_current.PropertyChanged -= this.Source_OnPropertyChanged;
            }

            m_current = source;
            this.DemoGrid.SingleItemSource = source;
            if (m_current != null)
            {
                m_current.PropertyChanged -= this.Source_OnPropertyChanged;
                m_current.PropertyChanged += this.Source_OnPropertyChanged;
            }

            this.RefreshSummary();
        }

        private void RefreshSummary ()
        {
            this.SourceSummary = m_current?.ToString() ?? "(no source)";
        }

        private void Source_OnPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            this.RefreshSummary();
        }

        private void SourceLamp_OnClick (object sender, RoutedEventArgs e) => this.SetSource(m_lamp);
        private void SourceSpeaker_OnClick (object sender, RoutedEventArgs e) => this.SetSource(m_speaker);
        private void SourceNone_OnClick (object sender, RoutedEventArgs e) => this.SetSource(null);

        private void Page_OnUnloaded (object sender, RoutedEventArgs e)
        {
            this.SetSource(null);
            this.DemoGrid.Dispose();
        }

        public enum eLampMode
        {
            Steady,
            Pulse,
            Candle,
        }

        /// <summary>
        /// A lamp: a label with a subtitle, a group, a tooltip, an enum, a hidden property and a button.
        /// </summary>
        public class Lamp : NotifyPropertyChanged
        {
            private string m_name = "Reading lamp";
            private int m_brightness = 60;
            private eLampMode m_mode = eLampMode.Steady;
            private bool m_isOn = true;

            [PGLabel("Name", "Shown in the room list")]
            public string Name
            {
                get => m_name;
                set => this.SetAndRaiseIfChanged(ref m_name, value);
            }

            [PGGroup("Light")]
            [PGLabel("Brightness")]
            [PGToolTip("0 is off, 100 is full")]
            public int Brightness
            {
                get => m_brightness;
                set => this.SetAndRaiseIfChanged(ref m_brightness, value);
            }

            [PGGroup("Light")]
            [PGLabel("Mode")]
            public eLampMode Mode
            {
                get => m_mode;
                set => this.SetAndRaiseIfChanged(ref m_mode, value);
            }

            [PGGroup("Light")]
            [PGLabel("Is on")]
            public bool IsOn
            {
                get => m_isOn;
                set => this.SetAndRaiseIfChanged(ref m_isOn, value);
            }

            [PGHidden]
            public Guid Id { get; } = Guid.NewGuid();

            [PGButton("Toggle")]
            public void Toggle ()
            {
                this.IsOn = !this.IsOn;
            }

            public override string ToString () => $"{this.Name}: {(this.IsOn ? "on" : "off")}, brightness {this.Brightness}, {this.Mode}";
        }

        /// <summary>
        /// A speaker: the same grid, a different shape, so switching sources shows the rows rebuild.
        /// </summary>
        public class Speaker : NotifyPropertyChanged
        {
            private string m_name = "Kitchen speaker";
            private float m_volume = 0.4f;
            private bool m_isMuted;

            [PGLabel("Name")]
            public string Name
            {
                get => m_name;
                set => this.SetAndRaiseIfChanged(ref m_name, value);
            }

            [PGLabel("Volume", "0 to 1")]
            public float Volume
            {
                get => m_volume;
                set => this.SetAndRaiseIfChanged(ref m_volume, value);
            }

            [PGLabel("Is muted")]
            public bool IsMuted
            {
                get => m_isMuted;
                set => this.SetAndRaiseIfChanged(ref m_isMuted, value);
            }

            public override string ToString () => $"{this.Name}: volume {this.Volume:0.00}{(this.IsMuted ? ", muted" : "")}";
        }
    }
}
