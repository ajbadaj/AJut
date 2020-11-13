namespace AJut.Application
{
    using System;
    using System.Diagnostics;
    using System.Windows;

    /// <summary>
    /// Attached event registration helper, because it's templated this can only work with non-static classes, simplify further by making a using alias.
    /// <code>
    /// Using example (consider using the snippet `aeux`):
    ///  using AEUtils = AJut.Application.AEUtils&lt;YourType&gt;
    /// </code>
    /// </summary>
    public class AEUtils<TOwner>
    {
        public RoutedEvent Register<TRoutedEventHandler> (StaticAEUtils.AttachedEventHandler<TRoutedEventHandler> addFunc, StaticAEUtils.AttachedEventHandler<TRoutedEventHandler> removeFunc, RoutingStrategy routingStrategy = RoutingStrategy.Bubble)
        {
            return StaticAEUtils.Register(typeof(TOwner), addFunc, removeFunc, routingStrategy);
        }

        public RoutedEvent Register (StaticAEUtils.AttachedEventHandler<RoutedEventHandler> addFunc, StaticAEUtils.AttachedEventHandler<RoutedEventHandler> removeFunc, RoutingStrategy routingStrategy = RoutingStrategy.Bubble)
        {
            return StaticAEUtils.Register(typeof(TOwner), addFunc, removeFunc, routingStrategy);
        }
    }

    /// <summary>
    /// Helper class to create an instance of in your static classes who wish to have Attached Event wrapping
    /// </summary>
    /// <example>
    /// Inside a static class YourType...
    /// 
    /// private static readonly AEUtilsRegistrationHelper AEUtils = new AEUtilsRegistrationHelper(typeof(YourType));
    /// public static readonly RoutedEvent MyCoolThingEvent = AEUtils.Register(nameof(MyCoolThingEvent));
    /// </example>
    public class AEUtilsRegistrationHelper
    {
        private readonly Type m_ownerType;
        public AEUtilsRegistrationHelper (Type ownerType)
        {
            m_ownerType = ownerType;
        }

        public RoutedEvent Register<TRoutedEventHandler> (StaticAEUtils.AttachedEventHandler<TRoutedEventHandler> addFunc, StaticAEUtils.AttachedEventHandler<TRoutedEventHandler> removeFunc, RoutingStrategy routingStrategy = RoutingStrategy.Bubble)
        {
            return StaticAEUtils.Register(m_ownerType, addFunc, removeFunc, routingStrategy);
        }

        public RoutedEvent Register (StaticAEUtils.AttachedEventHandler<RoutedEventHandler> addFunc, StaticAEUtils.AttachedEventHandler<RoutedEventHandler> removeFunc, RoutingStrategy routingStrategy = RoutingStrategy.Bubble)
        {
            return StaticAEUtils.Register(m_ownerType, addFunc, removeFunc, routingStrategy);
        }
    }

    /// <summary>
    /// The base set of Attached Event Utilities, the owner type is passed in instead of typed to the utility class, but still has some minor improvements over directly calling RegisterRoutedEvent.
    /// </summary>
    public static class StaticAEUtils
    {
        public delegate void AttachedEventHandler<TRoutedEventHandler> (DependencyObject obj, TRoutedEventHandler handler);
        public static RoutedEvent Register<TRoutedEventHandler> (Type ownerType, AttachedEventHandler<TRoutedEventHandler> addFunc, AttachedEventHandler<TRoutedEventHandler> removeFunc, RoutingStrategy routingStrategy = RoutingStrategy.Bubble)
        {
            DebugValidateHandlers(addFunc, removeFunc);

            // Add{Name}Handler ---- Magic Numbers: Add = 3 letters, Add+Handler = 10 Characters
            string name = addFunc.Method.Name.Substring(3, addFunc.Method.Name.Length - 10);
            return EventManager.RegisterRoutedEvent(name, routingStrategy, typeof(TRoutedEventHandler), ownerType);
        }

        [Conditional("DEBUG")]
        public static void DebugValidateHandlers<TRoutedEventHandler> (AttachedEventHandler<TRoutedEventHandler> addFunc, AttachedEventHandler<TRoutedEventHandler> removeFunc)
        {
            if (addFunc == null)
            {
                throw new Exception("AEUtils - Error With Adder: Attached Events need add functions");
            }

            if (removeFunc == null)
            {
                throw new Exception("AEUtils - Error With Remover: Attached Events need remove functions");
            }

            string addName = addFunc.Method.Name;
            string removeName = removeFunc.Method.Name;

            if (!addName.StartsWith("Add") || !addName.EndsWith("Handler"))
            {
                throw new Exception("AEUtils - Error With Adder: Attached events require an add handler function whos name is formatted Add{name}Handler.");
            }

            if (!removeName.StartsWith("Remove") || !removeName.EndsWith("Handler"))
            {
                throw new Exception("AEUtils - Error With Remover: Attached events require a remove handler function whos name is formatted Remove{name}Handler.");
            }

            if (addName.Substring("Add".Length) != removeName.Substring("Remove".Length))
            {
                throw new Exception("AEUtils - Error with Adder & Remover: Attached events require matching names, name of Add{name}Handler adder function does not match Remove{name}Handler remover function.");
            }
        }
    }
}
