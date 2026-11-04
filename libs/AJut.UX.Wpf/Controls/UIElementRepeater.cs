namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Markup;
    using DPUtils = AJut.UX.DPUtils<UIElementRepeater>;

    // ===========[ UIElementRepeater ]=============================================
    // WinUI3 port: pending.
    /// <summary>
    /// Shows <see cref="DisplayChildren"/> in <see cref="Container"/>, keeping the container's first children the same as
    /// the collection's items, in its order, and following its changes when it raises them. The rules are AJut.UX's
    /// <see cref="RepeaterChildSync{TChild}"/>, shared with every stack's repeater. Any children the container has of
    /// its own come after the repeated ones and are left alone.
    /// </summary>
    [ContentProperty(nameof(Container))]
    public class UIElementRepeater : Control
    {
        private readonly PanelHost m_host = new PanelHost();
        private readonly RepeaterChildSync<UIElement> m_sync;
        private bool m_isUnloaded;

        static UIElementRepeater ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UIElementRepeater), new FrameworkPropertyMetadata(typeof(UIElementRepeater)));
        }

        public UIElementRepeater ()
        {
            m_sync = new RepeaterChildSync<UIElement>(m_host);

            // It follows the collection from the start, so a repeater that is never shown still follows it. Once shown, it
            //  follows only while loaded, and while unloaded it lets go of its container, which keeps its children. A view
            //  holds its parent, so the views a long-lived collection still has (a view model's, say) keep the container
            //  alive, and with the container out of the repeater's tree that is all they keep: never the repeater or the
            //  window above it. Loading again puts the container back as it was and catches up on whatever changed in
            //  between. The container is let go of, rather than emptied, so an AutoGrid in it keeps its rows, columns and
            //  dragged splits. Its own events, so there is nothing to unhook.
            m_sync.StartFollowing();
            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;
        }

        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            m_isUnloaded = false;
            this.ShowContainer();
            m_sync.StartFollowing();
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            m_sync.StopFollowing();
            m_isUnloaded = true;
            this.ShowContainer();
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
        }

        public static readonly DependencyProperty ContainerProperty = DPUtils.Register(_ => _.Container, (d, e) => d.OnContainerChanged(e));
        public Panel Container
        {
            get => (Panel)this.GetValue(ContainerProperty);
            set => this.SetValue(ContainerProperty, value);
        }

        // The repeated children move to the new container, and wait for one when there is none
        private void OnContainerChanged (DependencyPropertyChangedEventArgs<Panel> e)
        {
            m_sync.RemoveAll();
            m_host.Target = e.NewValue;
            m_sync.Sync();
            this.ShowContainer();
        }

        private static readonly DependencyPropertyKey ShownContainerPropertyKey = DPUtils.RegisterReadOnly(_ => _.ShownContainer);
        public static readonly DependencyProperty ShownContainerProperty = ShownContainerPropertyKey.DependencyProperty;

        /// <summary>
        /// The <see cref="Container"/> while the repeater is loaded (or has never been), and none while it is unloaded: what
        /// the template shows, so the repeater lets go of its container while it is out of its window
        /// </summary>
        public Panel ShownContainer
        {
            get => (Panel)this.GetValue(ShownContainerProperty);
            private set => this.SetValue(ShownContainerPropertyKey, value);
        }

        public static readonly DependencyProperty DisplayChildrenProperty = DPUtils.Register(_ => _.DisplayChildren, (d,e)=>d.OnDisplayChildrenChanged(e));
        public IEnumerable<UIElement> DisplayChildren
        {
            get => (IEnumerable<UIElement>)this.GetValue(DisplayChildrenProperty);
            set => this.SetValue(DisplayChildrenProperty, value);
        }

        private void OnDisplayChildrenChanged (DependencyPropertyChangedEventArgs<IEnumerable<UIElement>> e)
        {
            m_sync.SetSource(e.NewValue);
        }

        // ===========[ Helper Methods ]========================================
        private void ShowContainer ()
        {
            this.ShownContainer = m_isUnloaded ? null : this.Container;
        }

        // ===========[ Subclasses/structs ]====================================
        // Puts the repeated children in whichever panel is the container now; with none, there is nowhere to put them yet
        private sealed class PanelHost : IRepeaterChildHost<UIElement>
        {
            public Panel Target { get; set; }

            public void InsertChild (int index, UIElement child)
            {
                if ((this.Target != null) && !this.Target.Children.Contains(child))
                {
                    this.Target.Children.Insert(Math.Min(index, this.Target.Children.Count), child);
                }
            }

            public void RemoveChild (UIElement child)
            {
                this.Target?.Children.Remove(child);
            }
        }
    }
}
