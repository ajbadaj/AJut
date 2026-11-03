namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using AppKit;
    using ObjCRuntime;

    // ===========[ UIElementRepeater ]==========================================
    // Shows DisplayChildren in Container, the AppKit twin of WPF's UIElementRepeater: the container's
    // first subviews are kept the same as the collection's views, in its order, following its changes
    // when it raises them (an ObservableCollection, say), and any subviews the container has of its own
    // come after them and are left alone. The rules are AJut.UX's RepeaterChildSync, shared with every
    // stack's repeater.
    //
    // The container fills the repeater, pinned to its edges by constraints, so the repeater wants the
    // size its container does. Any view works: an AutoGrid, a FillStack (whose views go in with its
    // own Insert), an NSStackView (as arranged subviews), or a plain view. A view the collection has
    // that is in some other view is moved, as AppKit moves a subview, and one taken out of a stack is
    // placed by frame again. It follows the collection's changes only while in a window, and out of
    // one it lets go of its container, which keeps its views: a collection that outlives the window
    // keeps only the container alive, and on coming back the container returns and the repeater
    // catches up on whatever changed in between.
    public class UIElementRepeater : NSView
    {
        private readonly ContainerHost m_host = new ContainerHost();
        private RepeaterChildSync<NSView> m_sync;

        // ===========[ Setup/Construction/Teardown ]===========================
        public UIElementRepeater ()
        {
            this.Setup();
        }

        /// <summary>
        /// Used by the runtime when AppKit creates the managed peer for an existing native view, which takes the one view it
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
            m_sync = new RepeaterChildSync<NSView>(m_host);
        }

        // ===========[ Properties ]============================================
        /// <summary>Top-left origin, as the other AJut views have it</summary>
        public override bool IsFlipped => true;

        /// <summary>The view the repeated views go in, filling the repeater. The repeated views move to a new one.</summary>
        public NSView Container
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
        public IEnumerable<NSView> DisplayChildren
        {
            get => (IEnumerable<NSView>)m_sync.Source;
            set => m_sync.SetSource(value);
        }

        // ===========[ Public Interface Methods ]==============================
        // Out of a window the repeater lets go of its container, which keeps its views, so a collection that outlives the
        //  window keeps only the container alive, and an AutoGrid in it keeps its rows, columns and splits. Back in one,
        //  the container comes back as it was and the repeater catches up on whatever changed in between.
        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            NSView container = m_host.Target;
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

        // ===========[ Helper Methods ]========================================
        private void AttachContainer (NSView container)
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

        // ===========[ Subclasses/structs ]====================================
        // Puts the repeated views in whichever view is the container now, the way each kind takes a view; with none, there
        //  is nowhere to put them yet
        private sealed class ContainerHost : IRepeaterChildHost<NSView>
        {
            public NSView Target { get; set; }

            public void InsertChild (int index, NSView child)
            {
                if ((this.Target == null) || ReferenceEquals(child.Superview, this.Target))
                {
                    return;
                }

                // A view in some other view is moved, as AppKit moves a subview. One in another FillStack leaves through
                //  that stack, which keeps its own list of what it holds.
                if (child.Superview is FillStack otherFillStack)
                {
                    otherFillStack.Remove(child);
                }
                else
                {
                    child.RemoveFromSuperview();
                }

                if (this.Target is FillStack fillStack)
                {
                    fillStack.Insert(index, child);
                }
                else if (this.Target is NSStackView stack)
                {
                    stack.InsertArrangedSubview(child, Math.Min(index, stack.ArrangedSubviews.Length));
                }
                else if (index < this.Target.Subviews.Length)
                {
                    this.Target.AddSubview(child, NSWindowOrderingMode.Below, this.Target.Subviews[index]);
                }
                else
                {
                    this.Target.AddSubview(child);
                }
            }

            public void RemoveChild (NSView child)
            {
                if ((this.Target == null) || !ReferenceEquals(child.Superview, this.Target))
                {
                    return;
                }

                // A FillStack or a stack view lays its views out by constraints, so one taken out of it is placed by frame
                //  again, as a view made in code is (an AutoGrid places its cells by frame)
                if (this.Target is FillStack fillStack)
                {
                    fillStack.Remove(child);
                    child.TranslatesAutoresizingMaskIntoConstraints = true;
                }
                else
                {
                    child.RemoveFromSuperview();
                    if (this.Target is NSStackView)
                    {
                        child.TranslatesAutoresizingMaskIntoConstraints = true;
                    }
                }
            }
        }
    }
}
