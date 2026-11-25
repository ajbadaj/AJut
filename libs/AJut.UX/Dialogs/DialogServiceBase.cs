namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    // ===========[ DialogServiceBase ]==========================================
    // The toolkit free half of a dialog service: which dialog shows, which wait, which are hidden to come back to, and how
    // each one ends. A stack derives from it, shows and hides its own dialog type when asked, and reports a window closing;
    // every rule is decided here, so it is the same on every stack and can be tested without a window.
    //
    // One service covers everything that can show only one dialog at a time. For WinUI3 that is a UI thread, not a
    // window: its ContentDialog allows one open per thread, even across separate windows. So a service can hold dialogs
    // from several windows, and a window closing ends only that window's dialogs.
    //
    // The model is one stack. The top of the stack is the dialog that should be showing, and beneath it are the dialogs
    // hidden for nav back. Beside the stack is a waiting line of queued dialogs, which waits for the stack to empty.
    // Requests change that model at once, and then the model is reconciled with what is on screen: a dialog showing that
    // is no longer the top is asked to hide, and nothing new is shown until it has finished closing, since most toolkits
    // refuse to show a second dialog while the first is still on its way out.
    //
    // Telling the service's own hide from the person's close. A toolkit's hide usually ends a dialog the same way its close
    // button does (WinUI3's ContentDialog.Hide ends ShowAsync with None, as Escape does), so the ending alone can't say who
    // closed it. The service only ever asks a dialog to hide once it is no longer the top, and that settles it:
    //  - A primary or secondary answer is always the person's. They answered before the hide landed, or the dialog refused
    //    to hide; either way the answer wins and the dialog leaves the stack.
    //  - No choice, on a dialog the service asked to hide, is the service's doing. If the dialog is still in the stack it
    //    was hidden for nav back and stays; otherwise it was replaced or popped, and ends that way.
    //  - No choice the service did not ask for is the person's (or app code closing it directly): it ends as None, and the
    //    dialog beneath it comes back.
    // A dialog that refuses to hide simply keeps showing, and whatever asked to show over it waits until it closes.
    //
    // Re-entrancy. A caller's continuation can run inline the moment its dialog's task ends (await on the UI thread does
    // that), and that continuation can ask for the next dialog straight away. So endings are only collected while the
    // model changes, and each task is ended once the model is settled. A request that arrives mid-update makes the update
    // run another pass rather than working on a half changed model.
    /// <summary>
    /// A dialog service, toolkit free: shows one dialog at a time, and decides what happens to a dialog asked for while
    /// another is showing (see <see cref="eDialogOverlapResponse"/>). Dialogs hidden for nav back are kept and come back
    /// when the dialog over them closes or is popped (<see cref="PopBack"/>, <see cref="CanPopBack"/>). Each stack's
    /// dialog service derives from this.
    /// </summary>
    /// <remarks>
    /// Use it on the thread its dialogs live on. <see cref="ShowAsync"/> sends itself there when called from another
    /// thread, on a stack that says how.
    /// </remarks>
    /// <typeparam name="TDialog">The stack's dialog type</typeparam>
    public abstract class DialogServiceBase<TDialog> : NotifyPropertyChanged
        where TDialog : class
    {
        private readonly List<Entry> m_stack = new List<Entry>();
        private readonly List<Entry> m_waiting = new List<Entry>();
        private readonly List<Entry> m_ended = new List<Entry>();
        private Entry? m_showing;
        private bool m_isUpdating;
        private bool m_needsAnotherPass;
        private bool m_canPopBack;

        // ===========[ Properties ]============================================
        /// <summary>
        /// Whether there is a dialog hidden beneath the one showing, for <see cref="PopBack"/> to go back to. Bind a back
        /// button's enabled state to it.
        /// </summary>
        public bool CanPopBack
        {
            get => m_canPopBack;
            private set => this.SetAndRaiseIfChanged(ref m_canPopBack, value);
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Shows <paramref name="dialog"/>, or decides what to do with it when another dialog is showing. The task ends once,
        /// when the dialog leaves for good: with the person's answer, or with why it left without one. Being hidden for nav
        /// back is not an ending; the same task carries on when the dialog comes back.
        /// </summary>
        /// <remarks>
        /// Asking again for a dialog this service already holds (showing, hidden or waiting) returns the task it already has
        /// and changes nothing, so a double click on whatever opens a dialog does no harm. Called from another thread, this
        /// is sent to the dialogs' thread first.
        /// </remarks>
        /// <param name="dialog">The dialog to show. The same instance is shown again after a nav back, so it keeps its state.</param>
        /// <param name="ifAnotherIsShowing">What to do when another dialog is showing (see <see cref="eDialogOverlapResponse"/>)</param>
        /// <exception cref="ArgumentNullException"><paramref name="dialog"/> is null</exception>
        public Task<eDialogOutcome> ShowAsync (TDialog dialog, eDialogOverlapResponse ifAnotherIsShowing = eDialogOverlapResponse.Queue)
        {
            if (dialog == null)
            {
                throw new ArgumentNullException(nameof(dialog));
            }

            return this.IsOnDialogThread ? this.ShowOnDialogThread(dialog, ifAnotherIsShowing) : this.ShowFromAnotherThread(dialog, ifAnotherIsShowing);
        }

        /// <summary>
        /// Goes back to the dialog hidden beneath the one showing: the showing dialog ends as
        /// <see cref="eDialogOutcome.PoppedBack"/>, and the one beneath is shown again. Returns false, and does nothing, when
        /// there is nothing beneath it (<see cref="CanPopBack"/>).
        /// </summary>
        /// <remarks>
        /// Called from another thread, it is sent to the dialogs' thread and returns false, since it can't know yet whether it
        /// will pop.
        /// </remarks>
        public bool PopBack ()
        {
            if (!this.IsOnDialogThread)
            {
                this.TryRunOnDialogThread(() => this.PopBackOnDialogThread());
                return false;
            }

            return this.PopBackOnDialogThread();
        }

        // ===========[ Helpers For The Toolkit ]===============================
        /// <summary>
        /// A window has closed: every dialog held that <paramref name="wasInIt"/> ends as
        /// <see cref="eDialogOutcome.WindowClosed"/> (one already on its way out as replaced or popped ends that way). None
        /// of them is hidden or closed, since the window is gone, and a late ending from one that was showing is ignored.
        /// The dialogs of other windows carry on, and the next of them shows.
        /// </summary>
        protected void OnWindowClosed (Predicate<TDialog> wasInIt)
        {
            if (m_showing != null && wasInIt(m_showing.Dialog))
            {
                if (!m_stack.Contains(m_showing))
                {
                    this.End(m_showing, m_showing.EndingOnceClosed ?? eDialogOutcome.WindowClosed);
                }

                m_showing = null;
            }

            for (int index = m_stack.Count - 1; index >= 0; --index)
            {
                if (wasInIt(m_stack[index].Dialog))
                {
                    this.End(m_stack[index], eDialogOutcome.WindowClosed);
                    m_stack.RemoveAt(index);
                }
            }

            for (int index = m_waiting.Count - 1; index >= 0; --index)
            {
                if (wasInIt(m_waiting[index].Dialog))
                {
                    this.End(m_waiting[index], eDialogOutcome.WindowClosed);
                    m_waiting.RemoveAt(index);
                }
            }

            this.Update();
        }

        // ===========[ Toolkit Hooks ]=========================================
        /// <summary>
        /// Called when the service takes a dialog it does not hold yet, before anything is decided for it. Throw to refuse a
        /// dialog that can't be shown (the throw reaches the caller); a stack also uses it to start following the dialog's
        /// window, so it can report it closing.
        /// </summary>
        protected virtual void OnHoldingDialog (TDialog dialog)
        {
        }

        /// <summary>
        /// Shows <paramref name="dialog"/>. The task ends when it closes, for any reason: <see cref="eDialogOutcome.Primary"/>
        /// or <see cref="eDialogOutcome.Secondary"/> for the person's answer, anything else for a close with no choice. A
        /// throw, or a faulted task, ends the dialog as <see cref="eDialogOutcome.NotShown"/> and is logged.
        /// </summary>
        protected abstract Task<eDialogOutcome> ShowDialogAsync (TDialog dialog);

        /// <summary>
        /// Asks a showing <paramref name="dialog"/> to close, so another can show. Its <see cref="ShowDialogAsync"/> task
        /// ending is the sign it has; a dialog may refuse, and stay showing.
        /// </summary>
        protected abstract void HideDialog (TDialog dialog);

        /// <summary>Whether the calling thread is the one this service's dialogs live on. True unless a stack says otherwise.</summary>
        protected virtual bool IsOnDialogThread => true;

        /// <summary>
        /// Runs <paramref name="action"/> later, on the thread this service's dialogs live on. Returns false when it can't,
        /// such as when that thread has shut down. Only called when <see cref="IsOnDialogThread"/> is false.
        /// </summary>
        protected virtual bool TryRunOnDialogThread (Action action)
        {
            action();
            return true;
        }

        // ===========[ Helper Methods ]========================================
        private Task<eDialogOutcome> ShowOnDialogThread (TDialog dialog, eDialogOverlapResponse ifAnotherIsShowing)
        {
            Entry? held = this.FindHeld(dialog);
            if (held != null)
            {
                return held.Completion.Task;
            }

            this.OnHoldingDialog(dialog);
            var entry = new Entry(dialog);
            if (m_stack.Count == 0 && m_waiting.Count == 0)
            {
                m_stack.Add(entry);
            }
            else
            {
                switch (ifAnotherIsShowing)
                {
                    case eDialogOverlapResponse.Queue:
                        m_waiting.Add(entry);
                        break;

                    case eDialogOverlapResponse.HideForNavBack:
                        m_stack.Add(entry);
                        break;

                    case eDialogOverlapResponse.Replace:
                        if (m_stack.Count > 0)
                        {
                            Entry replaced = m_stack[m_stack.Count - 1];
                            m_stack.RemoveAt(m_stack.Count - 1);
                            this.EndOnceClosed(replaced, eDialogOutcome.Replaced);
                        }

                        m_stack.Add(entry);
                        break;

                    case eDialogOverlapResponse.SkipIfBusy:
                        return Task.FromResult(eDialogOutcome.NotShown);

                    default:
                        throw new ArgumentOutOfRangeException(nameof(ifAnotherIsShowing), ifAnotherIsShowing, "Not a dialog overlap response");
                }
            }

            this.Update();
            return entry.Completion.Task;
        }

        private Task<eDialogOutcome> ShowFromAnotherThread (TDialog dialog, eDialogOverlapResponse ifAnotherIsShowing)
        {
            // Ended asynchronously so the caller's continuation goes back to its own thread, not the dialogs'
            var forwarded = new TaskCompletionSource<eDialogOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!this.TryRunOnDialogThread(_ShowAndForward))
            {
                forwarded.TrySetResult(eDialogOutcome.WindowClosed);
            }

            return forwarded.Task;

            void _ShowAndForward ()
            {
                _ = _ForwardAsync();
            }

            // Fire and forget on purpose: every ending, a throw included, goes to the forwarded task, so nothing is unobserved
            async Task _ForwardAsync ()
            {
                try
                {
                    forwarded.TrySetResult(await this.ShowOnDialogThread(dialog, ifAnotherIsShowing));
                }
                catch (Exception exc)
                {
                    forwarded.TrySetException(exc);
                }
            }
        }

        private bool PopBackOnDialogThread ()
        {
            if (m_stack.Count < 2)
            {
                return false;
            }

            Entry popped = m_stack[m_stack.Count - 1];
            m_stack.RemoveAt(m_stack.Count - 1);
            this.EndOnceClosed(popped, eDialogOutcome.PoppedBack);
            this.Update();
            return true;
        }

        private Entry? FindHeld (TDialog dialog)
        {
            foreach (Entry entry in m_stack)
            {
                if (ReferenceEquals(entry.Dialog, dialog))
                {
                    return entry;
                }
            }

            foreach (Entry entry in m_waiting)
            {
                if (ReferenceEquals(entry.Dialog, dialog))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Ends a dialog that has been taken out of the stack: at once if it isn't showing, or once it has closed if it is
        /// (the update asks it to hide, since it is no longer the top)
        /// </summary>
        private void EndOnceClosed (Entry entry, eDialogOutcome outcome)
        {
            if (entry == m_showing)
            {
                entry.EndingOnceClosed = outcome;
            }
            else
            {
                this.End(entry, outcome);
            }
        }

        private void End (Entry entry, eDialogOutcome outcome)
        {
            entry.Outcome = outcome;
            m_ended.Add(entry);
        }

        private void Update ()
        {
            if (m_isUpdating)
            {
                m_needsAnotherPass = true;
                return;
            }

            m_isUpdating = true;
            try
            {
                do
                {
                    m_needsAnotherPass = false;
                    this.Reconcile();
                }
                while (m_needsAnotherPass);
            }
            finally
            {
                m_isUpdating = false;
            }

            // Only now that the model is settled does anybody hear about it, since what they do next can come straight back in
            this.CanPopBack = m_stack.Count > 1;
            while (m_ended.Count > 0)
            {
                Entry ended = m_ended[0];
                m_ended.RemoveAt(0);
                ended.Completion.TrySetResult(ended.Outcome);
            }
        }

        private void Reconcile ()
        {
            // 1. A queued dialog waits for there to be no dialog at all
            if (m_stack.Count == 0 && m_waiting.Count > 0)
            {
                m_stack.Add(m_waiting[0]);
                m_waiting.RemoveAt(0);
            }

            Entry? top = m_stack.Count > 0 ? m_stack[m_stack.Count - 1] : null;

            // 2. A dialog showing that is no longer the top is asked to hide, and nothing new shows until it has closed
            if (m_showing != null)
            {
                if (m_showing != top && !m_showing.IsHideRequested)
                {
                    m_showing.IsHideRequested = true;
                    this.HideDialog(m_showing.Dialog);
                }

                return;
            }

            // 3. Nothing is showing: show the top
            if (top != null)
            {
                m_showing = top;
                this.BeginShowing(top);
            }
        }

        private void BeginShowing (Entry entry)
        {
            Task<eDialogOutcome> showing;
            try
            {
                showing = this.ShowDialogAsync(entry.Dialog);
            }
            catch (Exception exc)
            {
                showing = Task.FromException<eDialogOutcome>(exc);
            }

            _ = this.WatchShowingAsync(entry, showing);
        }

        // Fire and forget on purpose: it catches everything itself, so nothing is unobserved. It resumes on the thread that
        //  began showing, the dialogs' thread, since that is where it awaits from.
        private async Task WatchShowingAsync (Entry entry, Task<eDialogOutcome> showing)
        {
            eDialogOutcome? closedWith;
            try
            {
                closedWith = await showing;
            }
            catch (Exception exc)
            {
                // A failed show would otherwise leave the service waiting on a dialog that never comes. Whatever went wrong,
                //  this one ends as not shown, the log says why, and the other dialogs carry on.
                Logger.LogError($"[DialogService] Could not show a {entry.Dialog.GetType().Name}, so it ends as {eDialogOutcome.NotShown}", exc);
                closedWith = null;
            }

            this.OnShowingEnded(entry, closedWith);
        }

        /// <summary>
        /// A showing dialog has closed (<paramref name="closedWith"/> is how), or failed to show (null). The rules for who
        /// closed it are in the summary at the top of the class.
        /// </summary>
        private void OnShowingEnded (Entry entry, eDialogOutcome? closedWith)
        {
            // Its window closed first, and that already ended it
            if (m_showing != entry)
            {
                return;
            }

            m_showing = null;
            bool wasHideRequested = entry.IsHideRequested;
            entry.IsHideRequested = false;
            bool isInStack = m_stack.Contains(entry);

            if (closedWith == null)
            {
                m_stack.Remove(entry);
                this.End(entry, eDialogOutcome.NotShown);
            }
            else if (closedWith == eDialogOutcome.Primary || closedWith == eDialogOutcome.Secondary)
            {
                m_stack.Remove(entry);
                this.End(entry, closedWith.Value);
            }
            else if (isInStack && wasHideRequested)
            {
                // Hidden for nav back: it stays in the stack, to be shown again once it is the top
            }
            else if (isInStack)
            {
                m_stack.Remove(entry);
                this.End(entry, eDialogOutcome.None);
            }
            else
            {
                this.End(entry, entry.EndingOnceClosed ?? eDialogOutcome.None);
            }

            this.Update();
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>A dialog the service holds, from the request to its ending</summary>
        private sealed class Entry
        {
            public Entry (TDialog dialog)
            {
                this.Dialog = dialog;
            }

            public TDialog Dialog { get; }

            /// <summary>The caller's wait, ended once, when the dialog leaves for good</summary>
            public TaskCompletionSource<eDialogOutcome> Completion { get; } = new TaskCompletionSource<eDialogOutcome>();

            /// <summary>Asked to hide while showing, and not yet closed</summary>
            public bool IsHideRequested { get; set; }

            /// <summary>Taken out of the stack while showing (replaced or popped): how it ends once it has closed</summary>
            public eDialogOutcome? EndingOnceClosed { get; set; }

            /// <summary>How it ended, set when the ending is collected and given to the caller once the update settles</summary>
            public eDialogOutcome Outcome { get; set; }
        }
    }
}
