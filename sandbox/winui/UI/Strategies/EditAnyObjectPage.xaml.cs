namespace AJutShowRoomWinUI.UI.Strategies
{
    using AJut;
    using AJut.TypeManagement;
    using AJut.UX.PropertyInteraction;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Strategy: edit any object by handing it to a PropertyGrid, and tune the rows with attributes on the class
    /// rather than with a form per type.
    /// </summary>
    public sealed partial class EditAnyObjectPage : Page
    {
        public EditAnyObjectPage ()
        {
            this.InitializeComponent();
            this.Unloaded += this.OnUnloaded;
        }

        // ===========[ Properties ]===================================================

        /// <summary>
        /// The object under the grid. The readout binds to the same instance, which is the point.
        /// </summary>
        public SampleCharacter Sample { get; } = new SampleCharacter();

        public string DemoCode { get; } = @"// Any class works. The attributes are optional and only tune the rows.
public class SampleCharacter : NotifyPropertyChanged
{
    [PGLabel(""Display name"", ""Shown on the character's card"")]
    [PGToolTip(""Letters, numbers and spaces"")]
    public string DisplayName { get => m_displayName; set => this.SetAndRaiseIfChanged(ref m_displayName, value); }

    // A range for the numeric editor: a TypeId-registered context, built from the JSON
    [PGLabel(""Level"")]
    [PGEditContextBuilder(""Showroom-NumberRange"", ""{ Min: 1, Max: 20, Step: 1 }"")]
    public int Level { get => m_level; set => this.SetAndRaiseIfChanged(ref m_level, value); }

    [PGLabel(""Scale"", ""Render scale, 1 is life size"")]
    [PGEditContextBuilder(""Showroom-NumberRange"", ""{ Min: 0.1, Max: 3.0, Step: 0.1 }"")]
    public float Scale { get => m_scale; set => this.SetAndRaiseIfChanged(ref m_scale, value); }

    public eSampleQuality Quality { get; set; }
    public bool IsVisible { get; set; }

    [PGHidden]
    public Guid Id { get; } = Guid.NewGuid();

    [PGButton(""Reset to defaults"")]
    public void ResetToDefaults () { ... }
}

[TypeId(""Showroom-NumberRange"")]
public class NumberRange
{
    public float Min { get; set; } = float.NegativeInfinity;
    public float Max { get; set; } = float.PositiveInfinity;
    public float Step { get; set; } = 1.0f;
}

<!-- The grid picks a template by the property's type name, or by [PGEditor] when one is set -->
<ajut:PropertyGrid SingleItemSource=""{x:Bind Sample}"" LabelColumnWidth=""140"">
    <ajut:PropertyGrid.ItemTemplateSelector>
        <ajut:PropertyGridTemplateSelector>
            <ajut:PropertyGridTemplateSelector.Default>
                <DataTemplate x:DataType=""ajut_pg:PropertyEditTarget"">
                    <TextBlock Text=""{Binding Path=EditValue}""/>
                </DataTemplate>
            </ajut:PropertyGridTemplateSelector.Default>
            <ajut:PropertyGridTemplateSelector.RegisteredTemplates>
                <DataTemplate x:Key=""String"" x:DataType=""ajut_pg:PropertyEditTarget"">
                    <TextBox Text=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
                <DataTemplate x:Key=""Int32"" x:DataType=""ajut_pg:PropertyEditTarget"">
                    <ajut:NumericEditor Value=""{Binding Path=EditValue, Mode=TwoWay}""
                                        Minimum=""{Binding Path=EditContext.Min, Mode=OneWay, FallbackValue=-10000}""
                                        Maximum=""{Binding Path=EditContext.Max, Mode=OneWay, FallbackValue=10000}""
                                        Nudge=""{Binding Path=EditContext.Step, Mode=OneWay, FallbackValue=1}""/>
                </DataTemplate>
                <DataTemplate x:Key=""eSampleQuality"" x:DataType=""ajut_pg:PropertyEditTarget"">
                    <ajut:EnumComboBox SelectedItem=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
            </ajut:PropertyGridTemplateSelector.RegisteredTemplates>
        </ajut:PropertyGridTemplateSelector>
    </ajut:PropertyGrid.ItemTemplateSelector>
</ajut:PropertyGrid>";

        // ===========[ Event handlers ]===============================================
        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            this.Unloaded -= this.OnUnloaded;
            this.SampleGrid.Dispose();
        }

        // ===========[ Subclasses ]===================================================

        /// <summary>
        /// Quality levels for the sample, so the grid has an enum row to show.
        /// </summary>
        public enum eSampleQuality
        {
            /// <summary>The cheapest render.</summary>
            Low,
            /// <summary>The default.</summary>
            Medium,
            /// <summary>Every effect on.</summary>
            High,
        }

        /// <summary>
        /// The object the grid edits. NotifyPropertyChanged so the readout beside the grid follows every edit; a
        /// class without it edits just as well, it only stops telling anybody.
        /// </summary>
        public class SampleCharacter : NotifyPropertyChanged
        {
            private const string kDefaultDisplayName = "Avery";
            private const int kDefaultLevel = 3;
            private const float kDefaultScale = 1.0f;

            private string m_displayName = kDefaultDisplayName;
            private int m_level = kDefaultLevel;
            private float m_scale = kDefaultScale;
            private eSampleQuality m_quality = eSampleQuality.Medium;
            private bool m_isVisible = true;

            [PGLabel("Display name", "Shown on the character's card")]
            [PGToolTip("Letters, numbers and spaces")]
            public string DisplayName
            {
                get => m_displayName;
                set => this.SetAndRaiseIfChanged(ref m_displayName, value, nameof(DisplayName), nameof(Summary));
            }

            [PGLabel("Level")]
            [PGToolTip("1 through 20, nudged a whole level at a time")]
            [PGEditContextBuilder("Showroom-NumberRange", "{ Min: 1, Max: 20, Step: 1 }")]
            public int Level
            {
                get => m_level;
                set => this.SetAndRaiseIfChanged(ref m_level, value, nameof(Level), nameof(Summary));
            }

            [PGLabel("Scale", "Render scale, 1 is life size")]
            [PGEditContextBuilder("Showroom-NumberRange", "{ Min: 0.1, Max: 3.0, Step: 0.1 }")]
            public float Scale
            {
                get => m_scale;
                set => this.SetAndRaiseIfChanged(ref m_scale, value, nameof(Scale), nameof(Summary));
            }

            [PGLabel("Quality")]
            public eSampleQuality Quality
            {
                get => m_quality;
                set => this.SetAndRaiseIfChanged(ref m_quality, value, nameof(Quality), nameof(Summary));
            }

            [PGLabel("Is visible")]
            public bool IsVisible
            {
                get => m_isVisible;
                set => this.SetAndRaiseIfChanged(ref m_isVisible, value, nameof(IsVisible), nameof(Summary));
            }

            /// <summary>
            /// Hidden from the grid: an id is not something to edit by hand.
            /// </summary>
            [PGHidden]
            public System.Guid Id { get; } = System.Guid.NewGuid();

            /// <summary>
            /// Read only here and hidden from the grid; the readout shows it to prove the edits landed.
            /// </summary>
            [PGHidden]
            public string Summary => $"{m_displayName}, level {m_level}, at {m_scale:0.0}x, {m_quality} quality, {(m_isVisible ? "visible" : "hidden")}";

            /// <summary>
            /// Shown by the grid as a button row, because of the attribute.
            /// </summary>
            [PGButton("Reset to defaults")]
            public void ResetToDefaults ()
            {
                this.DisplayName = kDefaultDisplayName;
                this.Level = kDefaultLevel;
                this.Scale = kDefaultScale;
                this.Quality = eSampleQuality.Medium;
                this.IsVisible = true;
            }
        }

        /// <summary>
        /// The edit context a numeric row builds from its [PGEditContextBuilder] JSON. The NumericEditor template binds
        /// its Minimum, Maximum and Nudge to these. Found by its TypeId because the app registers this assembly.
        /// </summary>
        [TypeId("Showroom-NumberRange")]
        public class NumberRange
        {
            public float Min { get; set; } = float.NegativeInfinity;
            public float Max { get; set; } = float.PositiveInfinity;
            public float Step { get; set; } = 1.0f;
        }
    }
}
