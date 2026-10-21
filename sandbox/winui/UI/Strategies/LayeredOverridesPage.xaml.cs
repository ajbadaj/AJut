namespace AJutShowRoomWinUI.UI.Strategies
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using AJut.Storage;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Strategy: a baseline value with an override layered over it, kept in a Stratabase and read through one
    /// property access that always resolves to the highest layer that has a value.
    /// </summary>
    public sealed partial class LayeredOverridesPage : Page, INotifyPropertyChanged
    {
        private const int kOverrideLayerCount = 1;
        private const int kOverrideLayer = 0;
        private const string kTitleProperty = "Title";
        private const string kStartingBaseline = "Default title";
        private const string kStartingOverride = "My custom title";

        private readonly Stratabase m_stratabase = new Stratabase(kOverrideLayerCount);
        private readonly Guid m_documentId = Guid.NewGuid();
        private readonly StrataPropertyValueAccess<string> m_titleAccess;
        private string m_baselineTitle = kStartingBaseline;
        private string m_overrideTitle = kStartingOverride;
        private bool m_isOverrideOn;
        private int m_changeCount;
        private string m_changeLogText = string.Empty;

        public LayeredOverridesPage ()
        {
            this.InitializeComponent();

            m_titleAccess = m_stratabase.GeneratePropertyAccess<string>(m_documentId, kTitleProperty);
            m_titleAccess.SetBaselineValue(m_baselineTitle);
            m_titleAccess.ValueChanged += this.OnTitleValueChanged;
            this.Unloaded += this.OnUnloaded;
        }

        // ===========[ Events ]=======================================================
        public event PropertyChangedEventHandler? PropertyChanged;

        // ===========[ Properties ]===================================================

        /// <summary>
        /// The baseline layer's value. Writing it is a baseline set on the stratabase, nothing more.
        /// </summary>
        public string BaselineTitle
        {
            get => m_baselineTitle;
            set
            {
                if (m_baselineTitle == value)
                {
                    return;
                }

                m_baselineTitle = value;
                m_titleAccess.SetBaselineValue(value);
                this.RaisePropertyChanged();
            }
        }

        /// <summary>
        /// The override layer's value. It only reaches the stratabase while the override is on.
        /// </summary>
        public string OverrideTitle
        {
            get => m_overrideTitle;
            set
            {
                if (m_overrideTitle == value)
                {
                    return;
                }

                m_overrideTitle = value;
                if (m_isOverrideOn)
                {
                    m_titleAccess.SetOverrideValue(kOverrideLayer, value);
                }

                this.RaisePropertyChanged();
            }
        }

        /// <summary>
        /// On sets the override layer from <see cref="OverrideTitle"/>; off clears that layer, so the baseline shows
        /// through again without anybody copying it back.
        /// </summary>
        public bool IsOverrideOn
        {
            get => m_isOverrideOn;
            set
            {
                if (m_isOverrideOn == value)
                {
                    return;
                }

                m_isOverrideOn = value;
                if (value)
                {
                    m_titleAccess.SetOverrideValue(kOverrideLayer, m_overrideTitle);
                }
                else
                {
                    m_titleAccess.ClearOverrideValue(kOverrideLayer);
                }

                this.RaisePropertyChanged();
            }
        }

        public string ResolvedTitle => m_titleAccess.GetValue() ?? "(nothing set)";

        public string WinningLayerText => m_titleAccess.IsActiveLayerBaseline
            ? "Winning layer: baseline"
            : $"Winning layer: override {m_titleAccess.ActiveLayerIndex}";

        public string ChangeLogText
        {
            get => m_changeLogText;
            private set
            {
                m_changeLogText = value;
                this.RaisePropertyChanged();
            }
        }

        public string DemoCode { get; } = @"// One baseline layer plus the override layers you ask for. The count is fixed at construction.
private readonly Stratabase m_stratabase = new Stratabase(overrideLayerCount: 1);
private readonly Guid m_documentId = Guid.NewGuid();
private readonly StrataPropertyValueAccess<string> m_titleAccess;

// A property access is the handle for one property of one object, across every layer
m_titleAccess = m_stratabase.GeneratePropertyAccess<string>(m_documentId, ""Title"");
m_titleAccess.SetBaselineValue(""Default title"");
m_titleAccess.ValueChanged += this.OnTitleValueChanged;

// The override sits on layer 0 and wins while it is set
m_titleAccess.SetOverrideValue(0, ""My custom title"");

// Clearing it falls back to the baseline; nothing copies the default around
m_titleAccess.ClearOverrideValue(0);

// GetValue resolves top down: the highest layer with a value set
string resolved = m_titleAccess.GetValue();
bool fromBaseline = m_titleAccess.IsActiveLayerBaseline;
int winningLayer = m_titleAccess.ActiveLayerIndex;

// ValueChanged fires for a change on any layer that changes what resolves
private void OnTitleValueChanged (object? sender, EventArgs e)
{
    this.RaisePropertyChanged(nameof(ResolvedTitle));
    this.RaisePropertyChanged(nameof(WinningLayerText));
}

// Tear the access down with the page; it is the stratabase's flyweight, not the page's
m_titleAccess.ValueChanged -= this.OnTitleValueChanged;
m_titleAccess.Dispose();";

        // ===========[ Event handlers ]===============================================
        private void OnTitleValueChanged (object? sender, EventArgs e)
        {
            ++m_changeCount;
            this.ChangeLogText = $"ValueChanged #{m_changeCount}: now \"{m_titleAccess.GetValue()}\" from {(m_titleAccess.IsActiveLayerBaseline ? "baseline" : "override " + m_titleAccess.ActiveLayerIndex)}";
            this.RaisePropertyChanged(nameof(ResolvedTitle));
            this.RaisePropertyChanged(nameof(WinningLayerText));
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            this.Unloaded -= this.OnUnloaded;
            m_titleAccess.ValueChanged -= this.OnTitleValueChanged;
            m_titleAccess.Dispose();
        }

        // ===========[ Helpers ]======================================================
        private void RaisePropertyChanged ([CallerMemberName] string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
