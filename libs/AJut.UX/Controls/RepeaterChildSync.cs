namespace AJut.UX.Controls
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    // ===========[ RepeaterChildSync ]===========================================
    // UIElementRepeater's rules, the same on every stack: keeps the leading children of a container
    // the same as a collection's items, in its order.
    //
    //  - The repeated children come first in the container, ahead of any it has of its own, which are
    //    never touched.
    //  - Every change is caught up by comparing the collection with what is placed, whatever the change
    //    event says happened. A reset names no items, so following the event's items instead misses it.
    //    Items gone come out, new ones go in at their index, ones out of place move, and the rest are
    //    left where they are.
    //  - An item that is null, not a TChild, or already earlier in the collection is skipped.
    //  - Setting a source places its items at once. Following its changes is separate (StartFollowing
    //    and StopFollowing), so a toolkit can follow only while its view is in a window: a collection that
    //    outlives the view then never holds it from off screen, and starting again catches up on whatever
    //    changed in between.
    public sealed class RepeaterChildSync<TChild> where TChild : class
    {
        private readonly IRepeaterChildHost<TChild> m_host;
        private readonly List<TChild> m_placed = new List<TChild>();
        private IEnumerable? m_source;
        private INotifyCollectionChanged? m_followed;
        private bool m_isFollowing;

        // ===========[ Setup/Construction/Teardown ]===========================
        public RepeaterChildSync (IRepeaterChildHost<TChild> host)
        {
            m_host = host;
        }

        // ===========[ Events ]================================================
        private void OnSourceCollectionChanged (object? sender, NotifyCollectionChangedEventArgs e)
        {
            this.Sync();
        }

        // ===========[ Properties ]============================================
        /// <summary>The collection whose items are placed, or null for none</summary>
        public IEnumerable? Source => m_source;

        /// <summary>The children placed in the container, in order: the container's first children</summary>
        public IReadOnlyList<TChild> Placed => m_placed;

        /// <summary>Whether a change the source raises is caught up as it happens</summary>
        public bool IsFollowing => m_isFollowing;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Places <paramref name="source"/>'s items in place of the last source's, following its changes if following</summary>
        public void SetSource (IEnumerable? source)
        {
            if (!ReferenceEquals(source, m_source))
            {
                this.Unfollow();
                m_source = source;
                if (m_isFollowing)
                {
                    this.Follow();
                }
            }

            this.Sync();
        }

        /// <summary>Follows the source's changes from now on, after catching up on any it made while not followed</summary>
        public void StartFollowing ()
        {
            m_isFollowing = true;
            this.Follow();
            this.Sync();
        }

        /// <summary>Stops following the source's changes, leaving what is placed as it is</summary>
        public void StopFollowing ()
        {
            m_isFollowing = false;
            this.Unfollow();
        }

        /// <summary>
        /// Takes every placed child out of the container and leaves the source as it is: done before the host changes to
        /// another container, with a <see cref="Sync"/> after to place them in the new one
        /// </summary>
        public void RemoveAll ()
        {
            for (int index = m_placed.Count - 1; index >= 0; --index)
            {
                m_host.RemoveChild(m_placed[index]);
            }

            m_placed.Clear();
        }

        /// <summary>Brings the placed children in line with the source's items, touching only what differs</summary>
        public void Sync ()
        {
            List<TChild> wanted = this.GetWantedChildren();

            // 1. Take out what the source no longer has
            var wantedSet = new HashSet<TChild>(wanted, ReferenceEqualityComparer.Instance);
            for (int index = m_placed.Count - 1; index >= 0; --index)
            {
                if (!wantedSet.Contains(m_placed[index]))
                {
                    m_host.RemoveChild(m_placed[index]);
                    m_placed.RemoveAt(index);
                }
            }

            // 2. In the source's order, put each child at its index, moving it there if it is placed further along
            for (int index = 0; index < wanted.Count; ++index)
            {
                TChild child = wanted[index];
                if ((index < m_placed.Count) && ReferenceEquals(m_placed[index], child))
                {
                    continue;
                }

                // A child moved further along has the rest shift up past it. Taking it out and putting it in where it goes
                //  when the walk gets there moves one child, where moving each of the others back into place would move all
                //  of them.
                if (((index + 1) < m_placed.Count) && ReferenceEquals(m_placed[index + 1], child))
                {
                    m_host.RemoveChild(m_placed[index]);
                    m_placed.RemoveAt(index);
                    continue;
                }

                int placedAt = this.FindPlaced(child, index);
                if (placedAt >= 0)
                {
                    m_host.RemoveChild(child);
                    m_placed.RemoveAt(placedAt);
                }

                m_host.InsertChild(index, child);
                m_placed.Insert(index, child);
            }
        }

        // ===========[ Helper Methods ]========================================
        private void Follow ()
        {
            if (m_source is INotifyCollectionChanged observable)
            {
                observable.CollectionChanged -= this.OnSourceCollectionChanged;
                observable.CollectionChanged += this.OnSourceCollectionChanged;
                m_followed = observable;
            }
        }

        private void Unfollow ()
        {
            if (m_followed != null)
            {
                m_followed.CollectionChanged -= this.OnSourceCollectionChanged;
                m_followed = null;
            }
        }

        private List<TChild> GetWantedChildren ()
        {
            var wanted = new List<TChild>();
            if (m_source == null)
            {
                return wanted;
            }

            var seen = new HashSet<TChild>(ReferenceEqualityComparer.Instance);
            foreach (object? item in m_source)
            {
                if ((item is TChild child) && seen.Add(child))
                {
                    wanted.Add(child);
                }
            }

            return wanted;
        }

        // By reference: a child view is one object, whatever its Equals says
        private int FindPlaced (TChild child, int startIndex)
        {
            for (int index = startIndex; index < m_placed.Count; ++index)
            {
                if (ReferenceEquals(m_placed[index], child))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
