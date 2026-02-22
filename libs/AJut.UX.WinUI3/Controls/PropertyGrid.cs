namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using AJut.UX.PropertyInteraction;
    using DPUtils = AJut.UX.DPUtils<PropertyGrid>;

    // ===========[ PropertyGrid ]===============================================
    // Reflection-based property editor. Given a C# object (or view model),
    // discovers its public properties and renders each with a type-appropriate
    // editor via ItemTemplateSelector.
    //
    // Consumers register editor templates on PropertyGridTemplateSelector keyed by
    // PropertyEditTarget.Editor (the property type name or [PGEditor] override).
    //
    // Template parts:
    //   PART_ListView  - the inner ListView that renders property rows

    [TemplatePart(Name = nameof(PART_ListView), Type = typeof(ListView))]
    public class PropertyGrid : Control, IPropertyGrid, IDisposable
    {
        // ===========[ Instance fields ]==========================================
        private readonly PropertyGridManager m_manager;
        private ListView PART_ListView;

        // ===========[ Construction ]=============================================
        public PropertyGrid ()
        {
            this.DefaultStyleKey = typeof(PropertyGrid);
            m_manager = new PropertyGridManager(this);
            m_manager.Items.IncludeRoot = false;
        }

        public void Dispose ()
        {
            m_manager.Dispose();
            this.SingleItemSource = null;
            this.ItemsSource = null;
        }

        // ===========[ IPropertyGrid ]===========================================
        IEnumerable IPropertyGrid.ItemsSource => this.ItemsSource;
        object IPropertyGrid.SingleItemSource => this.SingleItemSource;

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e.OldValue, e.NewValue));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty SingleItemSourceProperty = DPUtils.Register(_ => _.SingleItemSource, (d, e) => d.OnSingleItemSourceChanged(e.OldValue, e.NewValue));
        public object SingleItemSource
        {
            get => this.GetValue(SingleItemSourceProperty);
            set => this.SetValue(SingleItemSourceProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateSelectorProperty = DPUtils.Register(_ => _.ItemTemplateSelector, (d, e) => d.ApplyTemplateSelector());
        public PropertyGridTemplateSelector ItemTemplateSelector
        {
            get => (PropertyGridTemplateSelector)this.GetValue(ItemTemplateSelectorProperty);
            set => this.SetValue(ItemTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty RowTemplateProperty = DPUtils.Register(_ => _.RowTemplate, (d, e) => d.ApplyRowTemplate());
        public DataTemplate RowTemplate
        {
            get => (DataTemplate)this.GetValue(RowTemplateProperty);
            set => this.SetValue(RowTemplateProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly, false);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_ListView != null)
            {
                this.PART_ListView.ContainerContentChanging -= this.ListView_OnContainerContentChanging;
            }

            this.PART_ListView = (ListView)this.GetTemplateChild(nameof(PART_ListView));
            if (this.PART_ListView == null)
            {
                return;
            }

            this.PART_ListView.ContainerContentChanging += this.ListView_OnContainerContentChanging;
            this.PART_ListView.ItemTemplate = this.RowTemplate;
            // m_manager.Items is observable — ListView stays in sync when RebuildEditTargets is called.
            this.PART_ListView.ItemsSource = m_manager.Items;
        }

        // ===========[ Container content changing ]===============================
        private void ListView_OnContainerContentChanging (ListViewBase sender, ContainerContentChangingEventArgs e)
        {
            // Push EditorTemplateSelector to each realized PropertyGridItemRow so the
            // ContentControl inside it can select the type-appropriate editor template.
            if (e.ItemContainer?.ContentTemplateRoot is PropertyGridItemRow row)
            {
                row.EditorTemplateSelector = this.ItemTemplateSelector;
            }
        }

        // ===========[ Property change handlers ]=================================
        private void OnSingleItemSourceChanged (object oldValue, object newValue)
        {
            if (oldValue is INotifyPropertyChanged oldPropChanged)
            {
                oldPropChanged.PropertyChanged -= this.OnSourceItemPropertyChanged;
            }

            if (newValue != null)
            {
                // SingleItemSource and ItemsSource are mutually exclusive
                this.ItemsSource = null;
                m_manager.RebuildEditTargets();

                if (newValue is INotifyPropertyChanged newPropChanged)
                {
                    newPropChanged.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }
            else
            {
                m_manager.Dispose();
            }
        }

        private void OnItemsSourceChanged (IEnumerable oldValue, IEnumerable newValue)
        {
            if (oldValue is INotifyCollectionChanged oldCollectionChange)
            {
                oldCollectionChange.CollectionChanged -= this.ItemsSource_OnCollectionChanged;
                foreach (INotifyPropertyChanged pc in oldValue.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged -= this.OnSourceItemPropertyChanged;
                }
            }

            if (newValue != null)
            {
                m_manager.RebuildEditTargets();

                if (newValue is INotifyCollectionChanged newCollectionChange)
                {
                    newCollectionChange.CollectionChanged += this.ItemsSource_OnCollectionChanged;
                }

                foreach (INotifyPropertyChanged pc in newValue.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }
            else
            {
                m_manager.Dispose();
            }
        }

        private void OnSourceItemPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            foreach (var target in m_manager.Items.OfType<PropertyEditTarget>())
            {
                if (target.ShouldEvaluateFor(e.PropertyName))
                {
                    target.RecacheEditValue();
                }
            }
        }

        private void ItemsSource_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            m_manager.RebuildEditTargets();
        }

        private void ApplyRowTemplate ()
        {
            if (this.PART_ListView != null)
            {
                this.PART_ListView.ItemTemplate = this.RowTemplate;
            }
        }

        private void ApplyTemplateSelector ()
        {
            if (this.PART_ListView == null)
            {
                return;
            }

            // Push updated EditorTemplateSelector to all currently realized PropertyGridItemRows.
            // New rows are handled via ContainerContentChanging.
            for (int i = 0; i < this.PART_ListView.Items.Count; ++i)
            {
                var container = this.PART_ListView.ContainerFromIndex(i) as ListViewItem;
                if (container?.ContentTemplateRoot is PropertyGridItemRow row)
                {
                    row.EditorTemplateSelector = this.ItemTemplateSelector;
                }
            }
        }
    }
}
