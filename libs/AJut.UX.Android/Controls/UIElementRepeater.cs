namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using Android.Content;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// Shows <see cref="DisplayChildren"/> in <see cref="Container"/>, the Android twin of WPF's UIElementRepeater: the
    /// container's first children are kept the same as the collection's views, in its order, following its changes when it
    /// raises them (an ObservableCollection, say), and any children the container has of its own come after them and are
    /// left alone. The rules are AJut.UX's <see cref="RepeaterChildSync{TChild}"/>, shared with every stack's repeater.
    /// </summary>
    /// <remarks>
    /// The container fills the repeater, which is a FrameLayout holding it and nothing else, so any ViewGroup works: an
    /// <see cref="AutoGrid"/>, a LinearLayout, a <see cref="SimpleWrapPanel"/>. In a layout, the one view group written
    /// inside the repeater is its container, as XAML's content is WPF's, and it fills the repeater whatever size it was
    /// written with. The repeater follows the collection's changes only while attached to a window, and while detached it
    /// lets go of its container, which keeps its views: a collection that outlives the screen keeps only the container
    /// alive, and on attaching again the container comes back and the repeater catches up on whatever changed. A view
    /// can only have one parent, so a view the collection has that is still in another view group (an older repeater's
    /// container, say) is taken out of it first. A view holds its context, so a collection that outlives an activity
    /// should not hold that activity's views.
    /// </remarks>
    [Register("ajut.ux.controls.UIElementRepeater")]
    public class UIElementRepeater : FrameLayout
    {
        private readonly ContainerHost m_host = new ContainerHost();
        private RepeaterChildSync<View> m_sync = null!;

        // ===========[ Setup/Construction/Teardown ]===================================
        public UIElementRepeater (Context context) : this(context, null)
        {
        }

        public UIElementRepeater (Context context, IAttributeSet? attrs) : this(context, attrs, 0)
        {
        }

        public UIElementRepeater (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Setup();
        }

        // A peer made again for a repeater that is already there takes the one view group it holds as its container
        protected UIElementRepeater (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
            this.Setup();
            this.AdoptOnlyChildAsContainer();
        }

        private void Setup ()
        {
            m_sync = new RepeaterChildSync<View>(m_host);
        }

        // ===========[ Properties ]===================================
        /// <summary>The view group the repeated views go in, filling the repeater. The repeated views move to a new one.</summary>
        public ViewGroup? Container
        {
            get => m_host.Target;
            set
            {
                if (ReferenceEquals(value, m_host.Target))
                {
                    return;
                }

                m_sync.RemoveAll();
                if (m_host.Target != null)
                {
                    this.RemoveView(m_host.Target);
                }

                m_host.Target = value;
                if (value != null)
                {
                    this.AddView(value, FillingLayout());
                }

                m_sync.Sync();
            }
        }

        /// <summary>The views to show in <see cref="Container"/>, in order. Null shows none.</summary>
        public IEnumerable<View>? DisplayChildren
        {
            get => (IEnumerable<View>?)m_sync.Source;
            set => m_sync.SetSource(value);
        }

        // ===========[ Public Interface Methods ]===================================
        // A layout's one child view group is the container, as WPF's repeater takes its content as the container
        protected override void OnFinishInflate ()
        {
            base.OnFinishInflate();
            this.AdoptOnlyChildAsContainer();
        }

        // Out of a window the repeater lets go of its container, which keeps its children, so the views a long-lived
        //  collection still has keep only the container alive, and an AutoGrid in it keeps its rows, columns and splits.
        //  Back in one, the container comes back as it was and the repeater catches up on whatever changed in between.
        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();
            if ((m_host.Target != null) && (m_host.Target.Parent == null))
            {
                this.AddView(m_host.Target, FillingLayout());
            }

            m_sync.StartFollowing();
        }

        protected override void OnDetachedFromWindow ()
        {
            m_sync.StopFollowing();
            if ((m_host.Target != null) && ReferenceEquals(m_host.Target.Parent, this))
            {
                this.RemoveView(m_host.Target);
            }

            base.OnDetachedFromWindow();
        }

        // ===========[ Helper Methods ]===================================
        private void AdoptOnlyChildAsContainer ()
        {
            if ((m_host.Target == null) && (this.ChildCount == 1) && (this.GetChildAt(0) is ViewGroup only))
            {
                only.LayoutParameters = FillingLayout();
                m_host.Target = only;
                m_sync.Sync();
            }
        }

        private static LayoutParams FillingLayout ()
        {
            return new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        }

        // ===========[ Subclasses/structs ]===================================
        // Puts the repeated views in whichever view group is the container now; with none, there is nowhere to put them yet
        private sealed class ContainerHost : IRepeaterChildHost<View>
        {
            public ViewGroup? Target { get; set; }

            public void InsertChild (int index, View child)
            {
                if ((this.Target == null) || ReferenceEquals(child.Parent, this.Target))
                {
                    return;
                }

                // A view has one parent, and adding one that has another throws, where UIKit and AppKit move it
                (child.Parent as ViewGroup)?.RemoveView(child);
                this.Target.AddView(child, Math.Min(index, this.Target.ChildCount));
            }

            public void RemoveChild (View child)
            {
                if ((this.Target != null) && ReferenceEquals(child.Parent, this.Target))
                {
                    this.Target.RemoveView(child);
                }
            }
        }
    }
}
