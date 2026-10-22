namespace AJut.UX.CodeBinding
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Threading;
    using System.Windows.Input;
    using AJut;

    /// <summary>
    /// Binds plain C# models (<see cref="INotifyPropertyChanged"/>, <see cref="INotifyCollectionChanged"/> and
    /// <see cref="ICommand"/>) onto the views of a toolkit that has no binding engine of its own for them: UIKit,
    /// AppKit, Android. A view or page owns one of these, routes every hookup through it, and disposes it when it
    /// unbinds, which undoes every subscription it made in one place. It is single use: dispose it, then make a new
    /// one to bind again. It is for toolkits without XAML binding; WPF and WinUI3 bind with XAML and do not use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the toolkit-free half of a binder. Each toolkit package adds extension methods on BindingSet that
    /// know its controls: which property to read and write, and which event means "the user edited it". Those
    /// adapters register their own links with <see cref="Add"/>, and route edits through
    /// <see cref="TwoWayChannel.OnTargetEdited"/> or <see cref="CommandChannel.OnActivated"/>.
    /// </para>
    /// <para>
    /// Bindings are typed lambdas plus the property name, never a string path resolved by reflection. The name only
    /// filters PropertyChanged; the read and write delegates do the access, so a typo is a compile error instead of
    /// a binding that silently never updates, and nothing here needs reflection metadata the IL trimmer (always on
    /// for iOS, and on for any AOT build) would strip. A nested source is bound with <see cref="WatchNested"/>, which
    /// re-hooks when the intermediate object is swapped.
    /// </para>
    /// <para>
    /// The set belongs to the main thread, the thread it is made on. Every hookup, and Dispose, must happen there,
    /// and throws anywhere else. Every push runs there too: a change raised on the main thread runs inline, and one
    /// raised on any other thread is posted to the main thread's SynchronizationContext (UIKit, AppKit and Android
    /// each install one), and only looked up and fanned out once it runs there. Without that, a model raising
    /// PropertyChanged from a worker thread touches a view off the main thread, which every one of these toolkits
    /// rejects with an exception, and inside a fire-and-forget task that exception is swallowed and the view simply
    /// never updates. A push posted before the set was disposed but run after it is skipped.
    /// </para>
    /// <para>
    /// Every subscription lives on a small link object with a named handler, so each += has its -= in that link's
    /// Dispose. The read, write and push delegates handed in are stored and invoked, never subscribed to an event,
    /// so one that captures the view is fine: the set goes when the view unbinds.
    /// </para>
    /// </remarks>
    public sealed class BindingSet : IDisposable
    {
        private static readonly SendOrPostCallback kRunPostedPush = RunPostedPush;

        private readonly SynchronizationContext m_mainThread;
        private readonly int m_mainThreadId;
        // By reference: two different models that compare equal (records, say) still each need their own subscription
        private readonly Dictionary<INotifyPropertyChanged, SourceWatch> m_watches = new Dictionary<INotifyPropertyChanged, SourceWatch>(ReferenceEqualityComparer.Instance);
        private readonly List<IDisposable> m_links = new List<IDisposable>();
        private bool m_isDisposed;

        // ===========[ Setup/Construction/Teardown ]===================================
        /// <summary>
        /// Makes a set on the main thread, bound to its SynchronizationContext. Throws when the current thread has no
        /// context: a set made on a worker thread would have nowhere to send its pushes, and would quietly touch views
        /// off the main thread, the exact failure it exists to prevent.
        /// </summary>
        public BindingSet ()
            : this(SynchronizationContext.Current ?? throw new InvalidOperationException(
                "A BindingSet has to be made on the main thread, and this thread has no SynchronizationContext. "
                + "Make it on the main thread, or pass the main thread's context to the BindingSet(SynchronizationContext) constructor."
            ))
        {
        }

        /// <summary>
        /// Makes a set that posts its off-thread pushes through <paramref name="mainThread"/>, for tests, or for a main
        /// thread that has a known context but has not installed it as Current. It must still be made on the main
        /// thread: the thread it is made on is the one its hookups, Dispose and inline pushes belong to.
        /// </summary>
        public BindingSet (SynchronizationContext mainThread)
        {
            m_mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
            m_mainThreadId = Environment.CurrentManagedThreadId;
        }

        /// <summary>
        /// Undoes every hookup made through this set, in reverse order. Safe to call more than once, and main thread
        /// only. A link that throws while unhooking is logged and skipped, so the rest still unhook.
        /// </summary>
        public void Dispose ()
        {
            if (m_isDisposed)
            {
                return;
            }

            this.ThrowIfNotOnMainThread();
            m_isDisposed = true;
            for (int index = m_links.Count - 1; index >= 0; --index)
            {
                IDisposable link = m_links[index];
                try
                {
                    link.Dispose();
                }
                catch (Exception exc)
                {
                    // An adapter whose native view is already gone can throw here. Stopping would leave every link
                    // before it subscribed, which is the leak this class exists to prevent.
                    Logger.LogError($"BindingSet: a {link.GetType().Name} threw while unhooking; the rest still unhook", exc);
                }
            }

            m_links.Clear();
            m_watches.Clear();
        }

        // ===========[ Properties ]===================================
        /// <summary>True once <see cref="Dispose"/> has run; every hookup made through this set is undone by then.</summary>
        public bool IsDisposed => m_isDisposed;

        /// <summary>The context pushes raised off the main thread are posted through.</summary>
        public SynchronizationContext MainThread => m_mainThread;

        /// <summary>True on the thread the set was made on, the one its hookups and pushes belong to.</summary>
        public bool IsOnMainThread => Environment.CurrentManagedThreadId == m_mainThreadId;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Runs <paramref name="push"/> now, and again whenever <paramref name="propertyName"/> changes on
        /// <paramref name="source"/> (or whenever the source raises a change for every property, with an empty name).
        /// </summary>
        public void Watch (INotifyPropertyChanged source, string propertyName, Action push)
        {
            this.ThrowIfUnusable();
            this.GetOrCreateWatch(source).Add(propertyName, push);
            push();
        }

        /// <summary>Runs <paramref name="push"/> now, and again whenever any of <paramref name="propertyNames"/> changes.</summary>
        public void Watch (INotifyPropertyChanged source, string[] propertyNames, Action push)
        {
            this.ThrowIfUnusable();
            SourceWatch watch = this.GetOrCreateWatch(source);
            foreach (string propertyName in propertyNames)
            {
                watch.Add(propertyName, push);
            }

            push();
        }

        /// <summary>
        /// Runs <paramref name="onChanged"/> now with a Reset, and again with each change <paramref name="collection"/>
        /// raises. An adapter that can apply an add, remove or move in place (a table, a recycler) does; one that cannot
        /// just reloads. The change arrives on the main thread, but its indices are the collection's at the moment it
        /// raised, so an incremental adapter wants a collection that is only changed on the main thread.
        /// </summary>
        public void WatchCollection (INotifyCollectionChanged collection, Action<NotifyCollectionChangedEventArgs> onChanged)
        {
            this.ThrowIfUnusable();
            m_links.Add(new CollectionLink(this, collection, onChanged));
            onChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Binds against an object that hangs off <paramref name="source"/>, re-hooking when it is swapped. It reads
        /// the child now and hands it, with a set of its own, to <paramref name="bindChild"/>, which binds against it
        /// fully typed. When <paramref name="propertyName"/> changes to a different object, that inner set is disposed
        /// (undoing every child hookup) and <paramref name="bindChild"/> runs again for the new one. A null child binds
        /// nothing, and a change that leaves the same object in place leaves its bindings alone. A deeper path nests
        /// the call: bindChild calls WatchNested on the set it is handed.
        /// </summary>
        public void WatchNested<TChild> (INotifyPropertyChanged source, string propertyName, Func<TChild?> readChild, Action<BindingSet, TChild> bindChild)
            where TChild : class
        {
            this.ThrowIfUnusable();
            var link = new NestedLink<TChild>(this, readChild, bindChild);
            m_links.Add(link);
            this.Watch(source, propertyName, link.Refresh);
        }

        /// <summary>
        /// Binds a source property and a target of the same type both ways. The target shows the source now and on
        /// every change; the toolkit adapter calls <see cref="TwoWayChannel.OnTargetEdited"/> from the target's edit
        /// event to write the user's edit back. A push leaves the target alone when it already shows the value, per
        /// <paramref name="targetComparer"/> (default equality when null).
        /// </summary>
        public TwoWayChannel TwoWay<T> (
            INotifyPropertyChanged source, string propertyName,
            Func<T> readSource, Action<T> writeSource,
            Func<T> readTarget, Action<T> writeTarget,
            IEqualityComparer<T>? targetComparer = null)
        {
            var conversion = new BindingConversion<T, T>(_Identity, _IdentityBack)
            {
                TargetComparer = targetComparer ?? EqualityComparer<T>.Default,
            };

            return this.TwoWay(source, propertyName, readSource, writeSource, readTarget, writeTarget, conversion);

            static T _Identity (T value) => value;
            static bool _IdentityBack (T target, out T value)
            {
                value = target;
                return true;
            }
        }

        /// <summary>
        /// Binds a source property and a target of a different type both ways, through <paramref name="conversion"/>.
        /// A conversion with no ConvertBack shows the source and ignores edits; one whose ConvertBack refuses an edit
        /// writes nothing for it.
        /// </summary>
        public TwoWayChannel TwoWay<TSource, TTarget> (
            INotifyPropertyChanged source, string propertyName,
            Func<TSource> readSource, Action<TSource> writeSource,
            Func<TTarget> readTarget, Action<TTarget> writeTarget,
            BindingConversion<TSource, TTarget> conversion)
        {
            this.ThrowIfUnusable();
            var channel = new TwoWayChannel(this, _PushToTarget, _WriteToSource);
            this.Watch(source, propertyName, channel.PushToTarget);
            return channel;

            void _PushToTarget ()
            {
                TTarget shown = conversion.Convert(readSource());
                if (!conversion.TargetComparer.Equals(readTarget(), shown))
                {
                    writeTarget(shown);
                }
            }

            void _WriteToSource ()
            {
                if ((conversion.ConvertBack != null)
                    && conversion.ConvertBack(readTarget(), out TSource edited))
                {
                    writeSource(edited);
                }
            }
        }

        /// <summary>
        /// Tracks <paramref name="command"/>'s CanExecute for <paramref name="parameter"/>: <paramref name="pushCanExecute"/>
        /// runs now and on every CanExecuteChanged (on the main thread). The toolkit adapter calls
        /// <see cref="CommandChannel.OnActivated"/> from the view's click.
        /// </summary>
        public CommandChannel Command (ICommand command, object? parameter, Action<bool> pushCanExecute)
        {
            this.ThrowIfUnusable();
            var channel = new CommandChannel(this, command, parameter, pushCanExecute);
            m_links.Add(new CommandLink(channel));
            channel.PushCanExecute();
            return channel;
        }

        /// <summary>Adds a toolkit adapter's own link (its subscription to a view event), so <see cref="Dispose"/> undoes it with everything else.</summary>
        public void Add (IDisposable link)
        {
            this.ThrowIfUnusable();
            m_links.Add(link);
        }

        // ===========[ Helper Methods ]===================================
        private void ThrowIfUnusable ()
        {
            if (m_isDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(BindingSet),
                    "A BindingSet is single use. A hookup made after Dispose would never be undone, so make a new set instead."
                );
            }

            this.ThrowIfNotOnMainThread();
        }

        private void ThrowIfNotOnMainThread ()
        {
            if (!this.IsOnMainThread)
            {
                throw new InvalidOperationException(
                    "A BindingSet's hookups and Dispose belong to the main thread it was made on. Off that thread they would race "
                    + "the main thread on the set's own lists, so marshal the call to the main thread first."
                );
            }
        }

        private SourceWatch GetOrCreateWatch (INotifyPropertyChanged source)
        {
            if (!m_watches.TryGetValue(source, out SourceWatch? watch))
            {
                watch = new SourceWatch(this, source);
                m_watches[source] = watch;
                m_links.Add(watch);
            }

            return watch;
        }

        // Runs the push now when already on the main thread, which is the common case and costs nothing. Off the main
        // thread it posts instead, and the disposed check when it runs skips a push queued before Dispose. The push
        // itself must not read the set's lists; SourceWatch posts its whole fan-out rather than each push for that reason.
        private void RunOnMainThread (Action push)
        {
            if (m_isDisposed)
            {
                return;
            }

            if (this.IsOnMainThread)
            {
                push();
                return;
            }

            m_mainThread.Post(kRunPostedPush, new PostedPush(this, push));
        }

        private static void RunPostedPush (object? state)
        {
            var posted = (PostedPush)state!;
            if (!posted.Owner.m_isDisposed)
            {
                posted.Push();
            }
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// The re-entrancy guard for one two-way binding. While a push from the source is writing the target, an edit
        /// event the target raises for that programmatic write (Android's TextChanged and CheckedChange do; UIKit's
        /// controls do not) is ignored rather than echoed back to the source.
        /// </summary>
        public sealed class TwoWayChannel
        {
            private readonly BindingSet m_owner;
            private readonly Action m_pushToTarget;
            private readonly Action m_writeToSource;
            private bool m_isPushing;

            internal TwoWayChannel (BindingSet owner, Action pushToTarget, Action writeToSource)
            {
                m_owner = owner;
                m_pushToTarget = pushToTarget;
                m_writeToSource = writeToSource;
            }

            /// <summary>True while a push from the source is writing the target.</summary>
            public bool IsPushing => m_isPushing;

            /// <summary>
            /// Call from the target's user edit event: reads the target, converts it back, and writes the source. Does
            /// nothing during a push, or once the set is disposed.
            /// </summary>
            public void OnTargetEdited ()
            {
                if (m_isPushing || m_owner.m_isDisposed)
                {
                    return;
                }

                m_writeToSource();
            }

            internal void PushToTarget ()
            {
                m_isPushing = true;
                try
                {
                    m_pushToTarget();
                }
                finally
                {
                    m_isPushing = false;
                }
            }
        }

        /// <summary>
        /// A command bound to a view: CanExecute is pushed to the view (usually its enabled state) now and whenever the
        /// command says it changed, and an activation executes the command if it can. Its subscription is undone by the
        /// set's Dispose, never on its own, so a consumer cannot unhook a channel a view still uses.
        /// </summary>
        public sealed class CommandChannel
        {
            private readonly BindingSet m_owner;
            private readonly ICommand m_command;
            private readonly object? m_parameter;
            private readonly Action<bool> m_pushCanExecute;

            internal CommandChannel (BindingSet owner, ICommand command, object? parameter, Action<bool> pushCanExecute)
            {
                m_owner = owner;
                m_command = command;
                m_parameter = parameter;
                m_pushCanExecute = pushCanExecute;
                m_command.CanExecuteChanged += this.OnCanExecuteChanged;
            }

            /// <summary>
            /// Call from the view's click: executes the command if it can execute now. CanExecute is checked here and
            /// not only through the view's enabled state, because a disabled view can still be clicked from code.
            /// </summary>
            public void OnActivated ()
            {
                if (!m_owner.m_isDisposed && m_command.CanExecute(m_parameter))
                {
                    m_command.Execute(m_parameter);
                }
            }

            internal void Unhook ()
            {
                m_command.CanExecuteChanged -= this.OnCanExecuteChanged;
            }

            internal void PushCanExecute ()
            {
                m_pushCanExecute(m_command.CanExecute(m_parameter));
            }

            // Reads only this channel's own fixed fields, so posting it from another thread is safe
            private void OnCanExecuteChanged (object? sender, EventArgs e)
            {
                m_owner.RunOnMainThread(this.PushCanExecute);
            }
        }

        private sealed class CommandLink : IDisposable
        {
            private readonly CommandChannel m_channel;

            public CommandLink (CommandChannel channel)
            {
                m_channel = channel;
            }

            public void Dispose () => m_channel.Unhook();
        }

        // One PropertyChanged subscription per source, fanned out by property name; an empty name reaches every push.
        private sealed class SourceWatch : IDisposable
        {
            private static readonly SendOrPostCallback kFanOutPosted = FanOutPosted;

            private readonly BindingSet m_owner;
            private readonly INotifyPropertyChanged m_source;
            private readonly Dictionary<string, List<Action>> m_pushes = new Dictionary<string, List<Action>>();

            public SourceWatch (BindingSet owner, INotifyPropertyChanged source)
            {
                m_owner = owner;
                m_source = source;
                m_source.PropertyChanged += this.OnPropertyChanged;
            }

            public void Dispose ()
            {
                m_source.PropertyChanged -= this.OnPropertyChanged;
                m_pushes.Clear();
            }

            public void Add (string propertyName, Action push)
            {
                if (!m_pushes.TryGetValue(propertyName, out List<Action>? pushes))
                {
                    pushes = new List<Action>();
                    m_pushes[propertyName] = pushes;
                }

                pushes.Add(push);
            }

            private void OnPropertyChanged (object? sender, PropertyChangedEventArgs e)
            {
                if (m_owner.m_isDisposed)
                {
                    return;
                }

                // The push lists belong to the main thread, which adds to them (a Watch) and clears them (a Dispose) at
                // any moment, so off the main thread they are not read at all: the change is posted once, as just its
                // name, and looked up and fanned out when it runs on the main thread.
                if (!m_owner.IsOnMainThread)
                {
                    m_owner.m_mainThread.Post(kFanOutPosted, new PostedChange(this, e.PropertyName));
                    return;
                }

                this.FanOut(e.PropertyName);
            }

            private static void FanOutPosted (object? state)
            {
                var posted = (PostedChange)state!;
                if (!posted.Watch.m_owner.m_isDisposed)
                {
                    posted.Watch.FanOut(posted.PropertyName);
                }
            }

            // Main thread only. A push can add a hookup on this same source, or dispose the whole set (a view unbinding
            // in response to a change), so neither loop below can be a foreach over the live collections.
            private void FanOut (string? propertyName)
            {
                if (string.IsNullOrEmpty(propertyName))
                {
                    // A push watching several names sits in several lists, and still runs once for "everything"
                    var everything = new List<Action>();
                    var seen = new HashSet<Action>();
                    foreach (List<Action> pushes in m_pushes.Values)
                    {
                        foreach (Action push in pushes)
                        {
                            if (seen.Add(push))
                            {
                                everything.Add(push);
                            }
                        }
                    }

                    this.RunPushes(everything, everything.Count);
                    return;
                }

                if (m_pushes.TryGetValue(propertyName, out List<Action>? named))
                {
                    this.RunPushes(named, named.Count);
                }
            }

            // Runs only the pushes that were there when the change arrived (a push added during the loop already ran its
            // opening push). A Dispose partway through stops the rest: RunOnMainThread skips every push once the set is
            // disposed, and the bound on the list's own count keeps the loop inside a list the Dispose cleared.
            private void RunPushes (List<Action> pushes, int count)
            {
                for (int index = 0; (index < count) && (index < pushes.Count); ++index)
                {
                    m_owner.RunOnMainThread(pushes[index]);
                }
            }
        }

        private sealed class CollectionLink : IDisposable
        {
            private static readonly SendOrPostCallback kRaisePosted = RaisePosted;

            private readonly BindingSet m_owner;
            private readonly INotifyCollectionChanged m_collection;
            private readonly Action<NotifyCollectionChangedEventArgs> m_onChanged;

            public CollectionLink (BindingSet owner, INotifyCollectionChanged collection, Action<NotifyCollectionChangedEventArgs> onChanged)
            {
                m_owner = owner;
                m_collection = collection;
                m_onChanged = onChanged;
                m_collection.CollectionChanged += this.OnCollectionChanged;
            }

            public void Dispose () => m_collection.CollectionChanged -= this.OnCollectionChanged;

            // Reads only this link's own fixed fields, so posting it from another thread is safe
            private void OnCollectionChanged (object? sender, NotifyCollectionChangedEventArgs e)
            {
                if (m_owner.m_isDisposed)
                {
                    return;
                }

                if (m_owner.IsOnMainThread)
                {
                    m_onChanged(e);
                    return;
                }

                m_owner.m_mainThread.Post(kRaisePosted, new PostedCollectionChange(this, e));
            }

            private static void RaisePosted (object? state)
            {
                var posted = (PostedCollectionChange)state!;
                if (!posted.Link.m_owner.m_isDisposed)
                {
                    posted.Link.m_onChanged(posted.Change);
                }
            }
        }

        // Holds the child currently bound and the inner set its bindings live in. Disposing it (with the outer set)
        // disposes the inner set, so the child's subscriptions go with everything else.
        private sealed class NestedLink<TChild> : IDisposable
            where TChild : class
        {
            private readonly BindingSet m_owner;
            private readonly Func<TChild?> m_readChild;
            private readonly Action<BindingSet, TChild> m_bindChild;
            private TChild? m_child;
            private BindingSet? m_childSet;

            public NestedLink (BindingSet owner, Func<TChild?> readChild, Action<BindingSet, TChild> bindChild)
            {
                m_owner = owner;
                m_readChild = readChild;
                m_bindChild = bindChild;
            }

            public void Dispose ()
            {
                m_childSet?.Dispose();
                m_childSet = null;
                m_child = null;
            }

            // Main thread only: it runs as a push, and the inner set it makes belongs to this thread
            public void Refresh ()
            {
                TChild? child = m_readChild();

                // The very first refresh always binds; after that only a different object re-hooks, so a source that
                // re-raises the name (or raises an empty name) without a swap keeps its child bindings as they are.
                if ((m_childSet != null) && ReferenceEquals(child, m_child))
                {
                    return;
                }

                m_childSet?.Dispose();
                m_childSet = null;
                m_child = child;
                if (child == null)
                {
                    return;
                }

                m_childSet = new BindingSet(m_owner.m_mainThread);
                m_bindChild(m_childSet, child);
            }
        }

        private sealed record PostedPush (BindingSet Owner, Action Push);
        private sealed record PostedChange (SourceWatch Watch, string? PropertyName);
        private sealed record PostedCollectionChange (CollectionLink Link, NotifyCollectionChangedEventArgs Change);
    }
}
