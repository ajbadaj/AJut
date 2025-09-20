namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using AJut;
    using AJut.UX.PropertyInteraction;
    using AJut.Storage;
    using DPUtils = DPUtils<PropertyGrid>;

    public class PropertyGrid : Control, IDisposable, IPropertyGrid
    {
        private readonly PropertyGridManager m_manager;
        static PropertyGrid ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGrid), new FrameworkPropertyMetadata(typeof(PropertyGrid)));
        }

        public PropertyGrid ()
        {
            m_manager = new PropertyGridManager(this);
            this.Items.IncludeRoot = false;
        }

        public void Dispose ()
        {
            this.Items.Clear();
            this.ItemsSource = null;
            this.SingleItemSource = null;
        }

        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty SingleItemSourceProperty = DPUtils.Register(_ => _.SingleItemSource, (d, e) => d.OnSingleItemSourceChanged(e));
        public object SingleItemSource
        {
            get => (object)this.GetValue(SingleItemSourceProperty);
            set => this.SetValue(SingleItemSourceProperty, value);
        }

        public ObservableFlatTreeStore<PropertyEditTarget> Items => m_manager.Items;

        public static readonly DependencyProperty ItemTemplateSelectorProperty = DPUtils.Register(_ => _.ItemTemplateSelector);
        public PropertyGridTemplateSelector ItemTemplateSelector
        {
            get => (PropertyGridTemplateSelector)this.GetValue(ItemTemplateSelectorProperty);
            set => this.SetValue(ItemTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty TextLabelStyleProperty = DPUtils.Register(_ => _.TextLabelStyle);
        public Style TextLabelStyle
        {
            get => (Style)this.GetValue(TextLabelStyleProperty);
            set => this.SetValue(TextLabelStyleProperty, value);
        }

        private void OnSingleItemSourceChanged (DependencyPropertyChangedEventArgs<object> e)
        {
            if (e.OldValue is INotifyPropertyChanged oldPropChanged)
            {
                oldPropChanged.PropertyChanged -= this.OnSourceItemPropertyChanged;
            }

            this.Items.Clear();
            if (e.HasNewValue)
            {
                // There can only be one (of these set)
                this.ItemsSource = null;
                this.RebuildEditTargets();

                if (e.NewValue is INotifyPropertyChanged newPropChanged)
                {
                    newPropChanged.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }
        }

        private void OnItemsSourceChanged (DependencyPropertyChangedEventArgs<IEnumerable> e)
        {
            if (e.OldValue is INotifyCollectionChanged oldCollectionChange)
            {
                oldCollectionChange.CollectionChanged -= this.NotifyCollectionChangedItemsSource_OnCollectionChanged;

                foreach (INotifyPropertyChanged pc in e.OldValue.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged -= this.OnSourceItemPropertyChanged;
                }
            }

            this.Items.Clear();

            if (e.HasNewValue)
            {
                // There can only be one (of these set)
                this.SingleItemSource = null;
                this.RebuildEditTargets();

                if (e.NewValue is INotifyCollectionChanged newCollectionChange)
                {
                    newCollectionChange.CollectionChanged -= this.NotifyCollectionChangedItemsSource_OnCollectionChanged;
                    newCollectionChange.CollectionChanged += this.NotifyCollectionChangedItemsSource_OnCollectionChanged;
                }

                foreach (INotifyPropertyChanged pc in e.NewValue.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }
        }

        private void OnSourceItemPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            foreach (var item in this.Items.Where(i => i.ShouldEvaluateFor(e.PropertyName)))
            {
                item.RecacheEditValue();
            }
        }

        private void NotifyCollectionChangedItemsSource_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (INotifyPropertyChanged pc in e.OldItems?.OfType<INotifyPropertyChanged>() ?? Enumerable.Empty<INotifyPropertyChanged>())
            {
                pc.PropertyChanged -= this.OnSourceItemPropertyChanged;
            }

            foreach (INotifyPropertyChanged pc in e.NewItems?.OfType<INotifyPropertyChanged>() ?? Enumerable.Empty<INotifyPropertyChanged>())
            {
                pc.PropertyChanged += this.OnSourceItemPropertyChanged;
            }

            this.RebuildEditTargets();
        }

        private void RebuildEditTargets ()
        {
            m_manager.RebuildEditTargets();
        }
    }
}
