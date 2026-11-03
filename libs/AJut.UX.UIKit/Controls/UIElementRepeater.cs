namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// Shows <see cref="DisplayChildren"/> in <see cref="Container"/>, the UIKit twin of WPF's UIElementRepeater: the
    /// container's first subviews are kept the same as the collection's views, in its order, following its changes when it
    /// raises them (an ObservableCollection, say), and any subviews the container has of its own come after them and are
    /// left alone. The rules are AJut.UX's <see cref="RepeaterChildSync{TChild}"/>, shared with every stack's repeater.
    /// </summary>
    /// <remarks>
    /// The container is pinned to the repeater's edges by constraints, so the repeater wants the size its container does,
    /// and any view works: an <see cref="AutoGrid"/>, a <see cref="SimpleWrapPanel"/>, or a UIStackView, whose views go in
    /// as arranged subviews. A view the collection has that is in some other view is moved, as UIKit moves a subview, and
    /// one that leaves a stack view is placed by frame again. The repeater follows the collection's changes only while in
    /// a window, and out of one it lets go of its container, which keeps its views: a collection that outlives the screen
    /// keeps only the container alive, and on coming back the container returns and the repeater catches up.
    /// </remarks>
    public class UIElementRepeater : UIView
    {
        private readonly ContainerHost m_host = new ContainerHost();
        private RepeaterChildSync<UIView> m_sync;

        // ===========[ Setup/Construction/Teardown ]===================================
        public UIElementRepeater () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>
        /// Used by the runtime when UIKit creates the managed peer for an existing native view, which takes the one view it
        /// holds as its container
        /// </summary>
        protected UIElementRepeater (NativeHandle handle) : base(handle)
        {
            this.Setup();
            if (this.Subviews.Length == 1)
            {
                m_host.Target = this.Subviews[0];
            }
        }

        private void Setup ()
        {
            m_sync = new RepeaterChildSync<UIView>(m_host);
        }

        // ===========[ Properties ]===================================
        /// <summary>The view the repeated views go in, filling the repeater. The repeated views move to a new one.</summary>
        public UIView Container
        {
            get => m_host.Target;
            set
            {
                if (ReferenceEquals(value, m_host.Target))
                {
                    return;
                }

                m_sync.RemoveAll();
                if (ReferenceEquals(m_host.Target?.Superview, this))
                {
                    m_host.Target.RemoveFromSuperview();
                }

                m_host.Target = value;
                if (value != null)
                {
                    this.AttachContainer(value);
                }

                m_sync.Sync();
            }
        }

        /// <summary>The views to show in <see cref="Container"/>, in order. Null shows none.</summary>
        public IEnumerable<UIView> DisplayChildren
        {
            get => (IEnumerable<UIView>)m_sync.Source;
            set => m_sync.SetSource(value);
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>The size the container wants, for a parent that places the repeater by frame</summary>
        public override CGSize SizeThatFits (CGSize size) => m_host.Target?.SizeThatFits(size) ?? CGSize.Empty;

        // Out of a window the repeater lets go of its container, which keeps its views, so a collection that outlives the
        //  screen keeps only the container alive, and an AutoGrid in it keeps its rows, columns and splits. Back in one, the
        //  container comes back as it was and the repeater catches up on whatever changed in between.
        public override void MovedToWindow ()
        {
            base.MovedToWindow();
            UIView container = m_host.Target;
            if (this.Window != null)
            {
                if ((container != null) && (container.Superview == null))
                {
                    this.AttachContainer(container);
                }

                m_sync.StartFollowing();
            }
            else
            {
                m_sync.StopFollowing();
                if (ReferenceEquals(container?.Superview, this))
                {
                    container.RemoveFromSuperview();
                }
            }
        }

        // ===========[ Helper Methods ]===================================
        private void AttachContainer (UIView container)
        {
            container.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(container);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                container.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                container.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                container.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                container.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
            });
        }

        // ===========[ Subclasses/structs ]===================================
        // Puts the repeated views in whichever view is the container now, as arranged subviews in a stack view; with none,
        //  there is nowhere to put them yet
        private sealed class ContainerHost : IRepeaterChildHost<UIView>
        {
            public UIView Target { get; set; }

            public void InsertChild (int index, UIView child)
            {
                if ((this.Target == null) || ReferenceEquals(child.Superview, this.Target))
                {
                    return;
                }

                // Taking a view out of its superview takes it out of a stack view's arranged subviews too
                child.RemoveFromSuperview();
                if (this.Target is UIStackView stack)
                {
                    stack.InsertArrangedSubview(child, (nuint)Math.Min(index, stack.ArrangedSubviews.Length));
                }
                else
                {
                    this.Target.InsertSubview(child, Math.Min(index, this.Target.Subviews.Length));
                }
            }

            public void RemoveChild (UIView child)
            {
                if ((this.Target == null) || !ReferenceEquals(child.Superview, this.Target))
                {
                    return;
                }

                // A stack view lays its views out by constraints, so one taken out of it is placed by frame again, as a
                //  view made in code is
                bool wasArranged = this.Target is UIStackView;
                child.RemoveFromSuperview();
                if (wasArranged)
                {
                    child.TranslatesAutoresizingMaskIntoConstraints = true;
                }
            }
        }
    }
}
