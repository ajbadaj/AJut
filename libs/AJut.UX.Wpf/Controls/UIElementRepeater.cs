namespace AJut.UX.Controls
{
    using System.Collections;
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
                this.RemoveEach(e.OldValue);
                if (e.OldValue is INotifyCollectionChanged collectionChanged)
                {
                    collectionChanged.CollectionChanged -= _OnItemsChanged;
                }
            }

            if (e.NewValue != null)
            {
                this.AddEach(e.NewValue);
                if (e.NewValue is INotifyCollectionChanged collectionChanged)
                {
                    collectionChanged.CollectionChanged -= _OnItemsChanged;
                    collectionChanged.CollectionChanged += _OnItemsChanged;
                }
            }

            void _OnItemsChanged (object sender, NotifyCollectionChangedEventArgs e)
            {
                this.RemoveEach(e.OldItems);
                this.AddEach(e.NewItems);
            }
        }

        private void AddEach (IEnumerable children)
        {
            if (children == null)
            {
                return;
            }

            foreach (UIElement child in children.OfType<UIElement>().Where(c => c != null && !this.Container.Children.Contains(c)))
            {
                this.Container.Children.Add(child);
            }
        }

        private void RemoveEach (IEnumerable children)
        {
            if (children == null)
            {
                return;
            }

            foreach (UIElement child in children.OfType<UIElement>().Where(c => c != null))
            {
                this.Container.Children.Remove(child);
            }
        }

    }
}
