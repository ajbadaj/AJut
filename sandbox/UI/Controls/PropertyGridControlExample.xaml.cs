namespace TheAJutShowRoom.UI.Controls
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using AJut;
    using AJut.Storage;
    using AJut.UX.PropertyInteraction;
    using DPUtils = AJut.UX.DPUtils<PropertyGridControlExample>;

    public partial class PropertyGridControlExample : UserControl
    {
        public PropertyGridControlExample ()
        {
            this.InitializeComponent();

            this.PropertyGridItems = new SelfAwarePropertyGridSource[]
            {
                new SelfAwarePropertyGridSource () { DogsName = "Wart", DogsAge = 18 },
                new SelfAwarePropertyGridSource () { DogsName = "Bandit", DogsAge = 7 },
                new SelfAwarePropertyGridSource () { DogsName = "Brosephina", DogsAge = 3 },
            };
        }

        public static readonly DependencyProperty PropertyGridItemsProperty = DPUtils.Register(_ => _.PropertyGridItems);
        public SelfAwarePropertyGridSource[] PropertyGridItems
        {
            get => (SelfAwarePropertyGridSource[])this.GetValue(PropertyGridItemsProperty);
            set => this.SetValue(PropertyGridItemsProperty, value);
        }

        private void SetDogAge_OnClick (object sender, RoutedEventArgs e)
        {
            ((SelfAwarePropertyGridSource)((FrameworkElement)e.OriginalSource).DataContext).DogsAge = App.kRNG.Next(1, 18);
        }
    }

    public class Person
    {
        public string Name { get; set; }
    }

    public class SelfAwarePropertyGridSource : NotifyPropertyChanged, IPropertyEditManager
    {
        private string m_dogsName = string.Empty;
        [PGEditor("Text")]
        public string DogsName
        {
            get => m_dogsName;
            set => this.SetAndRaiseIfChanged(ref m_dogsName, value);
        }

        private int m_dogsAge;
        [PGEditor("Number")]
        public int DogsAge
        {
            get => m_dogsAge;
            set => this.SetAndRaiseIfChanged(ref m_dogsAge, value);
        }

        public Person? NullThing { get; set; }

        private string m_dogSaveFileLocation = string.Empty;
        [PGEditor("SavePath")]
        public string DogSaveFileLocation
        {
            get => m_dogSaveFileLocation;
            set => this.SetAndRaiseIfChanged(ref m_dogSaveFileLocation, value);
        }

        public IEnumerable<PropertyEditTarget> GenerateEditTargets ()
        {
            foreach (var p in PropertyEditTarget.GenerateForPropertiesOf(this))
            {
                yield return p;
            }

            yield return new PropertyEditTarget("Name Alias", () => this.DogsName, (v) => this.DogsName = (string)v) { Editor = "Text", AdditionalEvalTargets = new[] { nameof(DogsName) } };
        }
    }
}
