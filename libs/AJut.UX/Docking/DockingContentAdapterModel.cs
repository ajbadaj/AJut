namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    public class DockingContentAdapterModel : NotifyPropertyChanged, IDisposable
    {
        // ===========[ Instance Fields ]===================================
        private DockZoneViewModel m_location;
        private int m_tabOrder;
        private object m_titleContent;
        private object m_titleTemplate;
        private object m_tooltipContent;
        private object m_tooltipTemplate;
        private bool m_isClosable = true;
        private bool m_hideDontClose;
        private bool m_canTearoff = true;

        // ===========[ Construction ]===================================
        public DockingContentAdapterModel (IDockingManager dockingOwner)
        {
            this.DockingOwner = dockingOwner;
        }

        public void Dispose ()
        {
            if (this.Location?.DockedContent is INotifyCollectionChanged locationCollection)
            {
                locationCollection.CollectionChanged -= this.OnOrderChangedInDockZone;
            }
        }

        // ===========[ Events ]===================================
        public event EventHandler<EventArgs<object>> SetupComplete;
        public event EventHandler<EventArgs> Docked;
        public event EventHandler<EventArgs> TabOrderChanged;
        public event EventHandler<IsReadyToCloseEventArgs> CanClose;
        public event EventHandler<ClosedEventArgs> Closed;

        // ===========[ Properties ]===================================
        public DockZoneViewModel Location
        {
            get => m_location;
            private set => this.SetAndRaiseIfChanged(ref m_location, value);
        }

        public int TabOrder
        {
            get => m_tabOrder;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_tabOrder, value))
                {
                    this.TabOrderChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public object TitleContent
        {
            get => m_titleContent;
            set => this.SetAndRaiseIfChanged(ref m_titleContent, value);
        }

        /// <summary>
        /// Platform-specific DataTemplate (WPF: System.Windows.DataTemplate; WinUI3: Microsoft.UI.Xaml.DataTemplate).
        /// Stored as <c>object?</c> to keep this class platform-agnostic.
        /// </summary>
        public object TitleTemplate
        {
            get => m_titleTemplate;
            set => this.SetAndRaiseIfChanged(ref m_titleTemplate, value);
        }

        public object TooltipContent
        {
            get => m_tooltipContent;
            set => this.SetAndRaiseIfChanged(ref m_tooltipContent, value);
        }

        /// <summary>
        /// Platform-specific DataTemplate. Stored as <c>object?</c> to keep this class platform-agnostic.
        /// </summary>
        public object TooltipTemplate
        {
            get => m_tooltipTemplate;
            set => this.SetAndRaiseIfChanged(ref m_tooltipTemplate, value);
        }

        /// <summary>
        /// The owning DockingManager - typed as <c>object</c> to keep this class platform-agnostic.
        /// Cast to the concrete manager type when platform-specific operations are needed.
        /// </summary>
        public IDockingManager DockingOwner { get; }

        public IDockableDisplayElement Display { get; set; }

        /// <summary>
        /// When false, the close button is hidden and <see cref="Close"/> is blocked.
        /// Set by the panel in its <see cref="IDockableDisplayElement.Setup"/> method. Default: true.
        /// </summary>
        public bool IsClosable
        {
            get => m_isClosable;
            set => this.SetAndRaiseIfChanged(ref m_isClosable, value);
        }

        /// <summary>
        /// When true, closing the panel hides it instead of disposing it. The manager stores
        /// the display and adapter for later re-show. Default: false.
        /// </summary>
        public bool HideDontClose
        {
            get => m_hideDontClose;
            set => this.SetAndRaiseIfChanged(ref m_hideDontClose, value);
        }

        /// <summary>
        /// When false, prevents this panel from being torn off into a separate window.
        /// Default: true.
        /// </summary>
        public bool CanTearoff
        {
            get => m_canTearoff;
            set => this.SetAndRaiseIfChanged(ref m_canTearoff, value);
        }

        /// <summary>
        /// Custom context menu entries shown on the panel's header right-click menu,
        /// in addition to the standard "Tear Off" and "Close" items.
        /// Set by the panel in its <see cref="IDockableDisplayElement.Setup"/> method.
        /// </summary>
        public List<DockPanelMenuOption> AdditionalContextMenuItems { get; set; }

        // ===========[ Public Interface ]===================================
        public bool CheckCanClose ()
        {
            if (!this.IsClosable)
            {
                return false;
            }

            var readyToClose = new IsReadyToCloseEventArgs();
            this.CanClose?.Invoke(this, readyToClose);
            return readyToClose.IsReadyToClose;
        }

        public bool Close ()
        {
            if (this.CheckCanClose())
            {
                this.InternalClose(false);
                return true;
            }

            return false;
        }

        // ===========[ Internal Utilities ]===================================
        internal void InternalClose (bool isForForcedClose)
        {
            this.Closed?.Invoke(this, new ClosedEventArgs { IsForForcedClose = isForForcedClose });
        }

        internal void SetNewLocation (DockZoneViewModel dockZone)
        {
            if (this.Location != null)
            {
                ((INotifyCollectionChanged)this.Location.DockedContent).CollectionChanged -= this.OnOrderChangedInDockZone;
            }

            this.Location = dockZone;
            if (this.Location != null)
            {
                this.Docked?.Invoke(this, EventArgs.Empty);
                if (this.Location.DockedContent is INotifyCollectionChanged locationCollection)
                {
                    locationCollection.CollectionChanged += this.OnOrderChangedInDockZone;
                }
            }

            this.ResetTabOrder();
        }

        internal void FinalizeSetup (object state)
        {
            this.SetupComplete?.Invoke(this, new EventArgs<object>(state));
        }

        private void OnOrderChangedInDockZone (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.ResetTabOrder();
        }

        private void ResetTabOrder ()
        {
            this.TabOrder = this.Location?.DockedContent.IndexOf(this) ?? -1;
        }
    }
}
