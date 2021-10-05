namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Specialized;
    using System.Windows;
    using AJut.UX.Controls;

    public class DockingContentAdapterModel : NotifyPropertyChanged, IDisposable
    {
        public DockingContentAdapterModel (DockingManager manager)
        {
            this.DockingOwner = manager;
        }

        public void Dispose ()
        {
            if (this.Location?.LocallyDockedElements is INotifyCollectionChanged locationCollection)
            {
                locationCollection.CollectionChanged -= this.OnOrderChangedInDockZone;
            }
        }

        public event EventHandler<EventArgs<object>> SetupComplete;
        public event EventHandler<EventArgs> Docked;
        public event EventHandler<EventArgs> TabOrderChanged;
        public event EventHandler<IsReadyToCloseEventArgs> CanClose;
        public event EventHandler<EventArgs> Closed;

        private DockZone m_location;
        public DockZone Location
        {
            get => m_location;
            private set => this.SetAndRaiseIfChanged(ref m_location, value);
        }

        private int m_tabOrder;
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

        private string m_debugName;
        public string DebugName
        {
            get => m_debugName;
            set => this.SetAndRaiseIfChanged(ref m_debugName, value);
        }

        private object m_titleContent;
        public object TitleContent
        {
            get => m_titleContent;
            set => this.SetAndRaiseIfChanged(ref m_titleContent, value);
        }

        private DataTemplate m_titleTemplate;
        public DataTemplate TitleTemplate
        {
            get => m_titleTemplate;
            set => this.SetAndRaiseIfChanged(ref m_titleTemplate, value);
        }

        private object m_tooltipContent;
        public object TooltipContent
        {
            get => m_tooltipContent;
            set => this.SetAndRaiseIfChanged(ref m_tooltipContent, value);
        }

        private DataTemplate m_tooltipTemplate;
        public DataTemplate TooltipTemplate
        {
            get => m_tooltipTemplate;
            set => this.SetAndRaiseIfChanged(ref m_tooltipTemplate, value);
        }

        public DockingManager DockingOwner { get; }
        public IDockableDisplayElement Display { get; internal set; }


        public bool CheckCanClose ()
        {
            var readyToClose = new IsReadyToCloseEventArgs();
            this.CanClose?.Invoke(this, readyToClose);
            return readyToClose.IsReadyToClose;
        }

        internal void SetNewLocation (DockZone dockZone)
        {
            if (this.Location != null)
            {
                ((INotifyCollectionChanged)this.Location.LocallyDockedElements).CollectionChanged -= this.OnOrderChangedInDockZone;
            }

            this.Location = dockZone;
            if (this.Location != null)
            {
                this.Docked?.Invoke(this, EventArgs.Empty);
                if (this.Location.LocallyDockedElements is INotifyCollectionChanged locationCollection)
                {
                    locationCollection.CollectionChanged += this.OnOrderChangedInDockZone;
                }
            }

            this.ResetTabOrder();
        }

        private void OnOrderChangedInDockZone (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.ResetTabOrder();
        }

        private void ResetTabOrder ()
        {
            this.TabOrder = this.Location?.LocallyDockedElements.IndexOf(this) ?? -1;
        }

        internal void FinalizeSetup (object state)
        {
            this.SetupComplete?.Invoke(this, new EventArgs<object>(state));
            if (this.DebugName == null)
            {
                this.DebugName = this.Display?.GetType().Name ?? "-display not set-";
            }
        }
    }
}
