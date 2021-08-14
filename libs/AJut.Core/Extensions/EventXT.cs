using System;

namespace AJut
{
    public static class EventXT
    {
        public static void Execute<TEventArgs>(this EventHandler<TEventArgs> e, object sender, TEventArgs args) where TEventArgs : EventArgs
        {
            e?.Invoke(sender, args);
        }

        public static void Execute(this EventHandler e, object sender)
        {
            e?.Invoke(sender, EventArgs.Empty);
        }
        public static void Execute(this EventHandler e, object sender, EventArgs args)
        {
            e?.Invoke(sender, args);
        }
    }
}
