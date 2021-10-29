namespace AJut.UX.Controls
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Markup;
    using DPUtils = AJut.UX.DPUtils<UIElementRepeater>;

    [ContentProperty(nameof(Container))]
    public class UIElementRepeater : Control
    {
        static UIElementRepeater ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UIElementRepeater), new FrameworkPropertyMetadata(typeof(UIElementRepeater)));
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
        }

        public static readonly DependencyProperty ContainerProperty = DPUtils.Register(_ => _.Container);
        public Panel Container
        {
            get => (Panel)this.GetValue(ContainerProperty);
            set => this.SetValue(ContainerProperty, value);
        }

        public static readonly DependencyProperty DisplayChildrenProperty = DPUtils.Register(_ => _.DisplayChildren, (d,e)=>d.OnDisplayChildrenChanged(e));
        public IEnumerable<UIElement> DisplayChildren
        {
            get => (IEnumerable<UIElement>)this.GetValue(DisplayChildrenProperty);
            set => this.SetValue(DisplayChildrenProperty, value);
        }

        private void OnDisplayChildrenChanged (DependencyPropertyChangedEventArgs<IEnumerable<UIElement>> e)
        {
            if (e.OldValue != null)
            {
                this.Container.Children.RemoveEach(e.OldValue);
                if (e.OldValue is INotifyCollectionChanged collectionChanged)
                {
                    collectionChanged.CollectionChanged -= _OnItemsChanged;
                }
            }

            if (e.NewValue != null)
            {
                this.Container.Children.AddEach(e.NewValue.OfType<UIElement>().Where(ui => !this.Container.Children.Contains(ui)));
                if (e.NewValue is INotifyCollectionChanged collectionChanged)
                {
                    collectionChanged.CollectionChanged -= _OnItemsChanged;
                    collectionChanged.CollectionChanged += _OnItemsChanged;
                }
            }

            void _OnItemsChanged (object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.OldItems != null)
                {
                    foreach (UIElement removed in e.OldItems)
                    {
                        this.Container.Children.Remove(removed);
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (UIElement added in e.NewItems)
                    {
                        this.Container.Children.Add(added);
                    }
                }
            }
        }
    }
}
