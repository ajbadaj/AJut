namespace TheAJutShowRoom.UI.Controls.Strategies
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.Storage;
    using DPUtils = AJut.UX.DPUtils<StratabaseOverridesStrategyExample>;

    public partial class StratabaseOverridesStrategyExample : UserControl
    {
        private const int kProjectLayer = 0;
        private const int kUserLayer = 1;
        private const string kTitleProperty = "Title";

        private readonly Stratabase m_stratabase;
        private readonly Guid m_documentId = Guid.NewGuid();
        private readonly StrataPropertyValueAccess<string> m_title;

        public StratabaseOverridesStrategyExample ()
        {
            // Two override layers: 0 for the project, 1 for the user. Higher layers win.
            m_stratabase = new Stratabase(2);
            m_title = m_stratabase.GeneratePropertyAccess<string>(m_documentId, kTitleProperty);
            m_stratabase.SetBaselinePropertyValue(m_documentId, kTitleProperty, "Default title");

            this.InitializeComponent();
            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;
            this.RefreshResolved();
        }

        public static readonly DependencyProperty ResolvedValueProperty = DPUtils.Register(_ => _.ResolvedValue, string.Empty);
        public string ResolvedValue
        {
            get => (string)this.GetValue(ResolvedValueProperty);
            set => this.SetValue(ResolvedValueProperty, value);
        }

        public static readonly DependencyProperty ResolvedFromTextProperty = DPUtils.Register(_ => _.ResolvedFromText, string.Empty);
        public string ResolvedFromText
        {
            get => (string)this.GetValue(ResolvedFromTextProperty);
            set => this.SetValue(ResolvedFromTextProperty, value);
        }

        public string CodeText { get; } =
@"private const int kProjectLayer = 0;
private const int kUserLayer = 1;

// Two override layers above the baseline. The count is fixed at construction.
Stratabase stratabase = new Stratabase(2);
Guid documentId = Guid.NewGuid();

// A typed accessor for one property of one object; it tracks which layer is active
StrataPropertyValueAccess<string> title = stratabase.GeneratePropertyAccess<string>(documentId, ""Title"");
title.ValueChanged += this.OnTitleChanged;   // and -= on teardown

// Baseline, then overrides. Setting a layer that is already the winner just changes the value.
stratabase.SetBaselinePropertyValue(documentId, ""Title"", ""Default title"");
stratabase.SetOverridePropertyValue(kProjectLayer, documentId, ""Title"", ""Project title"");
stratabase.SetOverridePropertyValue(kUserLayer, documentId, ""Title"", ""My title"");

title.GetValue();           // ""My title""
title.ActiveLayerIndex;     // 1, the user layer

// Clearing a layer lets the next one down show through
stratabase.ClearPropertyOverride(kUserLayer, documentId, ""Title"");
title.GetValue();           // ""Project title""
title.ActiveLayerIndex;     // 0

// The accessor has the same operations, scoped to its property:
//   title.SetOverrideValue(kUserLayer, ""My title"");
//   title.ClearOverrideValue(kUserLayer);
//   title.IsSet, title.IsBaselineSet, title.IsActiveLayerBaseline

// Beyond single values: GenerateListPropertyAccess for lists whose elements can come from any layer,
// SetObjectWithProperties to load a whole object's properties as the baseline, and the
// StratabaseBackedModel base class for models whose properties read and write the store directly.";

        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            m_title.ValueChanged -= this.OnTitleValueChanged;
            m_title.ValueChanged += this.OnTitleValueChanged;
            this.RefreshResolved();
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            m_title.ValueChanged -= this.OnTitleValueChanged;
        }

        private void OnTitleValueChanged (object? sender, EventArgs e)
        {
            this.RefreshResolved();
        }

        private void RefreshResolved ()
        {
            this.ResolvedValue = m_title.GetValue() ?? "(not set)";
            this.ResolvedFromText = m_title.ActiveLayerIndex switch
            {
                kUserLayer => "from the user override (layer 1)",
                kProjectLayer => "from the project override (layer 0)",
                -1 => "from the baseline (layer -1)",
                _ => "nothing is set on any layer",
            };
        }

        private void SetBaseline_OnClick (object sender, RoutedEventArgs e)
        {
            m_stratabase.SetBaselinePropertyValue(m_documentId, kTitleProperty, this.BaselineInput.Text);
        }

        private void ProjectOverride_OnCheckedChanged (object sender, RoutedEventArgs e)
        {
            this.ApplyOverride(kProjectLayer, this.ProjectOverrideActive.IsChecked == true, this.ProjectOverrideInput.Text);
        }

        private void ProjectOverrideInput_OnTextChanged (object sender, TextChangedEventArgs e)
        {
            if (this.ProjectOverrideActive.IsChecked == true)
            {
                this.ApplyOverride(kProjectLayer, true, this.ProjectOverrideInput.Text);
            }
        }

        private void UserOverride_OnCheckedChanged (object sender, RoutedEventArgs e)
        {
            this.ApplyOverride(kUserLayer, this.UserOverrideActive.IsChecked == true, this.UserOverrideInput.Text);
        }

        private void UserOverrideInput_OnTextChanged (object sender, TextChangedEventArgs e)
        {
            if (this.UserOverrideActive.IsChecked == true)
            {
                this.ApplyOverride(kUserLayer, true, this.UserOverrideInput.Text);
            }
        }

        private void ApplyOverride (int layer, bool isActive, string value)
        {
            if (isActive)
            {
                m_stratabase.SetOverridePropertyValue(layer, m_documentId, kTitleProperty, value);
            }
            else
            {
                m_stratabase.ClearPropertyOverride(layer, m_documentId, kTitleProperty);
            }
        }
    }
}
