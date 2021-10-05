namespace AJut.UX
{
    using System;
    using System.Diagnostics;
    using System.Windows;

    /// <summary>
    /// Routed event registration helper, because it's templated this can only work with non-static classes, simplify further by making a using alias.
    /// <code>
    /// Using example (consider using the snippet `reux`):
    ///  using REUtils = AJut.UX.REUtils&lt;YourType&gt;
    /// </code>
    /// </summary>
    public static class REUtils<TOwner>
        where TOwner : UIElement
    {
        public static RoutedEvent Register<TEventHandler> (string eventFieldName, RoutingStrategy strategy = RoutingStrategy.Bubble)
            where TEventHandler : Delegate
        {
            return StaticREUtils.Register<TEventHandler>(eventFieldName, typeof(TOwner), strategy);
        }

        public static RoutedEvent Register (string eventFieldName, RoutingStrategy strategy = RoutingStrategy.Bubble)
        {
            return StaticREUtils.Register(eventFieldName, typeof(TOwner), strategy);
        }
    }

    /// <summary>
    /// Helper class to create an instance of in your static classes who wish to have RoutedEvent wrapping
    /// </summary>
    /// <example>
    /// Inside a static class YourType...
    /// 
    /// private static readonly REUtilsRegistrationHelper REUtils = new REUtilsRegistrationHelper(typeof(YourType));
    /// public static readonly RoutedEvent MyCoolThingEvent = REUtils.Register(nameof(MyCoolThingEvent));
    /// </example>
    public class REUtilsRegistrationHelper
    {
        Type m_ownerType;
        public REUtilsRegistrationHelper (Type ownerType)
        {
            m_ownerType = ownerType;
        }

        public RoutedEvent Register<TEventHandler> (string eventFieldName, RoutingStrategy strategy = RoutingStrategy.Bubble)
            where TEventHandler : Delegate
        {
            return StaticREUtils.Register<TEventHandler>(eventFieldName, m_ownerType, strategy);
        }

        public RoutedEvent Register (string eventFieldName, RoutingStrategy strategy = RoutingStrategy.Bubble)
        {
            return StaticREUtils.Register(eventFieldName, m_ownerType, strategy);
        }
    }

    /// <summary>
    /// The base set of Routed Event Utilities, the owner type is passed in instead of typed to the utility class, but still has some minor improvements over directly calling RegisterRoutedEvent.
    /// </summary>
    public static class StaticREUtils
    {
        public static RoutedEvent Register<TEventHandler> (string eventFieldName, Type ownerType, RoutingStrategy strategy = RoutingStrategy.Bubble)
            where TEventHandler : Delegate
        {
            ValidateEventName(eventFieldName);
            eventFieldName = eventFieldName.SubstringFromRelativeEnd(0, 5);
            return EventManager.RegisterRoutedEvent(eventFieldName, strategy, typeof(TEventHandler), ownerType);
        }

        public static RoutedEvent Register (string eventFieldName, Type ownerType, RoutingStrategy strategy = RoutingStrategy.Bubble)
        {
            ValidateEventName(eventFieldName);
            eventFieldName = eventFieldName.SubstringFromRelativeEnd(0, 5);
            return EventManager.RegisterRoutedEvent(eventFieldName, strategy, typeof(RoutedEventHandler), ownerType);
        }

        [Conditional("DEBUG")]
        private static void ValidateEventName (string eventName)
        {
            if (eventName == null || !eventName.EndsWith("Event"))
            {
                throw new Exception("A valid event field must be indicated, this should be your event's name, and end with Event, ie TapEvent for Tap.");
            }
        }
    }

    public delegate void RoutedEventHandler<T> (object sender, RoutedEventArgs<T> e);
    public class RoutedEventArgs<T> : RoutedEventArgs
    {
        public RoutedEventArgs (T value)
        {
            this.Value = value;
        }
        public RoutedEventArgs (RoutedEvent routedEvent, T value) : base(routedEvent)
        {
            this.Value = value;
        }

        public RoutedEventArgs (RoutedEvent routedEvent, object source, T value) : base(routedEvent, source)
        {
            this.Value = value;
        }

        public T Value { get; }
    }
}
