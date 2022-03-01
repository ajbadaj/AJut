namespace AJut.Threading
{
    using System;

    public class ThreadWorkerDataQueue<T> : ThreadWorkerState<T>
    {
        /* Note: I was using ConcurrentQueue and opted to update to ThreadWorkerState base class
                 because I wanted to operate on the data easier, some operations i needed (removal
                 based particuarly) were just too much. ******************************************* */

        // ==================[ Properties ]========================
        public bool ManuallyNotifyDataReceivedOnly { get; set; } = true;

        // ==================[ Events ]============================
        public event EventHandler<EventArgs> DataReceived;
        public event EventHandler<EventArgs> DataProcessed;

        // ==================[ Methods ]============================
        public T TakeNext () => this.Take(0);

        public void NotifyDataReceived () => this.DataReceived?.Invoke(this, EventArgs.Empty);

        protected override void OnItemsAdded ()
        {
            base.OnItemsAdded();

            if (!this.ManuallyNotifyDataReceivedOnly)
            {
                this.NotifyDataReceived();
            }
        }

        protected override void OnItemsRemoved ()
        {
            base.OnItemsRemoved();
            this.DataProcessed?.Invoke(this, EventArgs.Empty);
        }
    }
}
