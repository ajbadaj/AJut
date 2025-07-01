namespace TheAJutShowRoom.UI.Controls
{
    using System.Collections.ObjectModel;
    using System.Windows;
    using System.Windows.Controls;
    using DPUtils = AJut.UX.DPUtils<ToggleStripExampleDisplay>;

    public partial class ToggleStripExampleDisplay : UserControl
    {
        private int m_addedItemCounter;
        public ToggleStripExampleDisplay()
        {
            this.ZeroToggleElementsSource = new ObservableCollection<string>();
            this.InitializeComponent();
        }


        public ObservableCollection<string> ZeroToggleElementsSource { get; }

        public string ToggleStripExample { get; } = @"<ajut:ToggleStrip Margin=""0,0,0,5"">
    <ajut:ToggleStrip.ItemsSource>
        <x:Array Type=""{x:Type sys:String}"">
            <sys:String>Plain</sys:String>
            <sys:String>Toggle</sys:String>
            <sys:String>Strip</sys:String>
            <sys:String>No</sys:String>
            <sys:String>Formatting</sys:String>
        </x:Array>
    </ajut:ToggleStrip.ItemsSource>
</ajut:ToggleStrip>";

        private void AddItemToZeroToggle_OnClick (object sender, System.Windows.RoutedEventArgs e)
        {
            this.ZeroToggleElementsSource.Add($"item #{++m_addedItemCounter}");
        }

        private void ClearItemsFromZeroToggle_OnClick (object sender, System.Windows.RoutedEventArgs e)
        {
            this.ZeroToggleElementsSource.Clear();
        }
    }
}
