namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using APUtils = AJut.UX.APUtils<DockZone>;
    using DPUtils = AJut.UX.DPUtils<DockZone>;
    using REUtils = AJut.UX.REUtils<DockZone>;

    public sealed partial class DockZone // Drag Drop
    {

        private static readonly DependencyPropertyKey IsDirectDropTargetPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsDirectDropTarget);
        public static readonly DependencyProperty IsDirectDropTargetProperty = IsDirectDropTargetPropertyKey.DependencyProperty;
        public bool IsDirectDropTarget
        {
            get => (bool)this.GetValue(IsDirectDropTargetProperty);
            internal set => this.SetValue(IsDirectDropTargetPropertyKey, value);
        }

        public static readonly DependencyProperty IsDropScootHoverLeftProperty = DPUtils.Register(_ => _.IsDropScootHoverLeft);
        public bool IsDropScootHoverLeft
        {
            get => (bool)this.GetValue(IsDropScootHoverLeftProperty);
            set => this.SetValue(IsDropScootHoverLeftProperty, value);
        }

        public static readonly DependencyProperty IsDropScootHoverTopProperty = DPUtils.Register(_ => _.IsDropScootHoverTop);
        public bool IsDropScootHoverTop
        {
            get => (bool)this.GetValue(IsDropScootHoverTopProperty);
            set => this.SetValue(IsDropScootHoverTopProperty, value);
        }

        public static readonly DependencyProperty IsDropScootHoverRightProperty = DPUtils.Register(_ => _.IsDropScootHoverRight);
        public bool IsDropScootHoverRight
        {
            get => (bool)this.GetValue(IsDropScootHoverRightProperty);
            set => this.SetValue(IsDropScootHoverRightProperty, value);
        }

        public static readonly DependencyProperty IsDropScootHoverBottomProperty = DPUtils.Register(_ => _.IsDropScootHoverBottom);
        public bool IsDropScootHoverBottom
        {
            get => (bool)this.GetValue(IsDropScootHoverBottomProperty);
            set => this.SetValue(IsDropScootHoverBottomProperty, value);
        }

    }
}
