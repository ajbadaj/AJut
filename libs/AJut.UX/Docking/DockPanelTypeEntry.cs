namespace AJut.UX.Docking
{
    using System;

    /// <summary>
    /// Observable entry representing a registered panel type in the <see cref="DockPanelAddRemoveUISync"/>.
    /// Tracks display name, icon, visibility state, and whether instances currently exist.
    /// </summary>
    public class DockPanelTypeEntry : NotifyPropertyChanged
    {
        // ===========[ Instance Fields ]===================================
        private int m_activeInstanceCount;

        // ===========[ Construction ]===================================
        internal DockPanelTypeEntry (Type panelType, DockPanelRegistrationRules rules)
        {
            this.PanelType = panelType;
            this.Rules = rules;
            this.DisplayName = StringXT.ConvertToFriendlyEn(panelType.Name);
        }

        // ===========[ Properties ]===================================

        /// <summary>The registered panel type.</summary>
        public Type PanelType { get; }

        /// <summary>Registration-time rules for this panel type.</summary>
        public DockPanelRegistrationRules Rules { get; }

        /// <summary>Display name shown in toolbar buttons and menu items. Defaults to friendly version of type name.</summary>
        public string DisplayName { get; set; }

        /// <summary>Path to an icon resource (e.g. "Assets/Icons/MyPanel.png"). Null means no icon.</summary>
        public string IconPath { get; set; }

        /// <summary>When true, this panel type is excluded from toolbar and menu UI entirely.</summary>
        public bool IsHiddenFromUI { get; set; }

        /// <summary>Number of currently active (docked or visible) instances of this panel type.</summary>
        public int ActiveInstanceCount
        {
            get => m_activeInstanceCount;
            set => this.SetAndRaiseIfChanged(ref m_activeInstanceCount, value);
        }

        /// <summary>True when at least one instance is currently active (docked, not hidden).</summary>
        public bool HasActiveInstance => m_activeInstanceCount > 0;

        /// <summary>True when this is a single-instance type.</summary>
        public bool IsSingleInstance => this.Rules.SingleInstanceOnly;
    }
}
