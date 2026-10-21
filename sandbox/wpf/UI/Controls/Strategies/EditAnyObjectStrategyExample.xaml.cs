namespace TheAJutShowRoom.UI.Controls.Strategies
{
    using System.Windows.Controls;
    using AJut;
    using AJut.UX.PropertyInteraction;

    public partial class EditAnyObjectStrategyExample : UserControl
    {
        public EditAnyObjectStrategyExample ()
        {
            this.Target = new SpaceshipSettings();
            this.InitializeComponent();
        }

        public SpaceshipSettings Target { get; }

        public string CodeText { get; } =
@"// The object: plain properties, attributes say how each one is edited
public class SpaceshipSettings : NotifyPropertyChanged
{
    [PGEditor(""Text"")]
    [PGLabel(""Ship name"")]
    [PGToolTip(""Shown on the hull and in the hangar list"")]
    public string Name { get; set; }

    [PGEditor(""Number"")]
    [PGGroup(""Engines"")]
    public float MaxSpeed { get; set; }

    [PGEditor(""Toggle"")]
    [PGGroup(""Engines"")]
    [PGLabel(""Afterburner"")]
    public bool HasAfterburner { get; set; }

    [PGEditor(""Enum"")]
    [PGGroup(""Loadout"")]
    public eWeaponClass Weapon { get; set; }

    [PGButton(""Reset to factory"")]
    public void ResetToFactory () { ... }
}

// The editor: one grid, pointed at the object
<ajut:PropertyGrid SingleItemSource=""{Binding ElementName=Self, Path=Target}"" LabelColumnWidth=""150"">
    <ajut:PropertyGrid.ItemTemplateSelector>
        <ajut:PropertyGridTemplateSelector>
            <ajut:PropertyGridTemplateSelector.RegisteredTemplates>
                <DataTemplate x:Key=""Text"" DataType=""{x:Type ajprop:PropertyEditTarget}"">
                    <ajut:EditableTextBlock Text=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
                <DataTemplate x:Key=""Number"" DataType=""{x:Type ajprop:PropertyEditTarget}"">
                    <ajut:NumericEditor Value=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
                <DataTemplate x:Key=""Toggle"" DataType=""{x:Type ajprop:PropertyEditTarget}"">
                    <ajut:ToggleSwitchButton IsChecked=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
                <DataTemplate x:Key=""Enum"" DataType=""{x:Type ajprop:PropertyEditTarget}"">
                    <ajut:EnumComboBox SelectedItem=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
            </ajut:PropertyGridTemplateSelector.RegisteredTemplates>
            <ajut:PropertyGridTemplateSelector.Default>
                <DataTemplate DataType=""{x:Type ajprop:PropertyEditTarget}"">
                    <TextBlock Text=""{Binding Path=EditValue}""/>
                </DataTemplate>
            </ajut:PropertyGridTemplateSelector.Default>
        </ajut:PropertyGridTemplateSelector>
    </ajut:PropertyGrid.ItemTemplateSelector>
</ajut:PropertyGrid>

// The same template selector can live in a resource dictionary and serve every grid in the app.
// More control when you need it: PGShowIf, PGCoerce, PGMemberOrder, PGList, PGElevateChildProperty,
// and IPropertyEditManager for objects that build their own edit targets. The Controls page has them all.";

        public enum eWeaponClass
        {
            Laser,
            Railgun,
            Missiles,
        }

        /// <summary>
        /// The object being edited. The attributes are the whole editor specification.
        /// </summary>
        public class SpaceshipSettings : NotifyPropertyChanged
        {
            private string m_name = "Kestrel";
            [PGEditor("Text")]
            [PGLabel("Ship name")]
            [PGToolTip("Shown on the hull and in the hangar list")]
            public string Name
            {
                get => m_name;
                set => this.SetAndRaiseIfChanged(ref m_name, value, nameof(Name), nameof(Summary));
            }

            private float m_maxSpeed = 120.0f;
            [PGEditor("Number")]
            [PGGroup("Engines")]
            [PGLabel("Max speed")]
            public float MaxSpeed
            {
                get => m_maxSpeed;
                set => this.SetAndRaiseIfChanged(ref m_maxSpeed, value, nameof(MaxSpeed), nameof(Summary));
            }

            private bool m_hasAfterburner = true;
            [PGEditor("Toggle")]
            [PGGroup("Engines")]
            [PGLabel("Afterburner")]
            public bool HasAfterburner
            {
                get => m_hasAfterburner;
                set => this.SetAndRaiseIfChanged(ref m_hasAfterburner, value, nameof(HasAfterburner), nameof(Summary));
            }

            private eWeaponClass m_weapon = eWeaponClass.Laser;
            [PGEditor("Enum")]
            [PGGroup("Loadout")]
            public eWeaponClass Weapon
            {
                get => m_weapon;
                set => this.SetAndRaiseIfChanged(ref m_weapon, value, nameof(Weapon), nameof(Summary));
            }

            [PGButton("Reset to factory")]
            public void ResetToFactory ()
            {
                this.Name = "Kestrel";
                this.MaxSpeed = 120.0f;
                this.HasAfterburner = true;
                this.Weapon = eWeaponClass.Laser;
            }

            public string Summary => $"{this.Name}: max speed {this.MaxSpeed}, afterburner {(this.HasAfterburner ? "fitted" : "not fitted")}, armed with {this.Weapon}";
        }
    }
}
