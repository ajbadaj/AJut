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
            this.IsHiddenFromToolbar = rules.IsHiddenFromToolbar;
            this.IsHiddenFromMenu = rules.IsHiddenFromMenu;
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

        /// <summary>When true, this panel type is excluded from the <see cref="AJut.UX.Controls.DockPanelAddRemoveToolbar"/>.</summary>
        public bool IsHiddenFromToolbar { get; set; }

        /// <summary>When true, this panel type is excluded from menus managed by <c>DockingManager.ManageMenu</c>.</summary>
        public bool IsHiddenFromMenu { get; set; }

        /// <summary>Convenience: when read, returns true if hidden from both toolbar and menu. When set, applies to both.</summary>
        public bool IsHiddenFromUI
        {
            get => this.IsHiddenFromToolbar && this.IsHiddenFromMenu;
            set
            {
                this.IsHiddenFromToolbar = value;
                this.IsHiddenFromMenu = value;
            }
        }

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
