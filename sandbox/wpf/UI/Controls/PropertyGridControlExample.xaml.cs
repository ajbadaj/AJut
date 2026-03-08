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

        private void SetElevatedTopSpeed_OnClick (object sender, RoutedEventArgs e)
        {
            ((SelfAwarePropertyGridSource)((FrameworkElement)e.OriginalSource).DataContext).Stats.TopSpeed = App.kRNG.Next(5, 60);
        }
    }

    /// <summary>Sub-object with its own editable properties - shows up as an expandable node.</summary>
    public class DogOwner
    {
        public string? OwnerName { get; set; } = "Unknown Owner";
        [PGEditor("Number")]
        public int OwnerAge { get; set; } = 30;
    }

    /// <summary>Sub-object for testing elevated child property recache.</summary>
    public class DogStats : NotifyPropertyChanged
    {
        private int m_topSpeed = 25;

        [PGEditor("Number")]
        public int TopSpeed
        {
            get => m_topSpeed;
            set => this.SetAndRaiseIfChanged(ref m_topSpeed, value);
        }

        private string m_favoriteToy = "Ball";
        public string FavoriteToy
        {
            get => m_favoriteToy;
            set => this.SetAndRaiseIfChanged(ref m_favoriteToy, value);
        }
    }

    public class SelfAwarePropertyGridSource : NotifyPropertyChanged, IPropertyEditManager
    {
        private string m_dogsName = string.Empty;
        [PGEditor("Text")]
        [PGLabel("Name", IconSource = "Images/PenguinExample.png", IconMargin = 4)]
        public string DogsName
        {
            get => m_dogsName;
            set => this.SetAndRaiseIfChanged(ref m_dogsName, value);
        }

        private int m_dogsAge;
        [PGEditor("Number")]
        [PGGroup("Stats")]
        public int DogsAge
        {
            get => m_dogsAge;
            set => this.SetAndRaiseIfChanged(ref m_dogsAge, value);
        }

        /// <summary>Non-null sub-object - should appear as expandable in the PropertyGrid tree.</summary>
        [PGGroup("Stats")]
        public DogOwner Owner { get; set; } = new DogOwner();

        private string m_dogSaveFileLocation = string.Empty;
        [PGEditor("SavePath")]
        public string DogSaveFileLocation
        {
            get => m_dogSaveFileLocation;
            set => this.SetAndRaiseIfChanged(ref m_dogSaveFileLocation, value);
        }

        /// <summary>Elevated child property - inline editor for TopSpeed. Tests RecacheEditValue cascade.</summary>
        [PGElevateChildProperty(nameof(DogStats.TopSpeed))]
        [PGGroup("Stats")]
        public DogStats Stats { get; set; } = new DogStats();

        // ------ ShowIf demo: show full stats only when ShowDetails is checked ------
        public bool ShowDetails { get; set; } = true;

        private int m_dogsWeight = 50;

        [PGShowIf(nameof(ShowDetails))]
        [PGEditor("Number")]
        [PGGroup("Stats")]
        public int DogsWeight
        {
            get => m_dogsWeight;
            set => this.SetAndRaiseIfChanged(ref m_dogsWeight, value);
        }


        // ------ PGCoerce demo: age clamped to 0-30 ------
        private int m_coercedAge = 5;

        [PGCoerce(nameof(CoerceAge))]
        [PGEditor("Number")]
        [PGGroup("Stats")]
        public int CoercedAge
        {
            get => m_coercedAge;
            set => this.SetAndRaiseIfChanged(ref m_coercedAge, value);
        }

        private object CoerceAge (object value)
        {
            if (value is int i)
            {
                return System.Math.Clamp(i, 0, 30);
            }

            return value;
        }

        // ------ PGButton demo ------
        [PGButton("Randomize Age")]
        public void RandomizeAge ()
        {
            this.DogsAge = App.kRNG.Next(1, 18);
            this.RaisePropertyChanged(nameof(DogsAge));
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
