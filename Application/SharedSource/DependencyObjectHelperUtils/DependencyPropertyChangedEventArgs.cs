namespace AJut.Application
{
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
#endif

    public class DependencyPropertyChangedEventArgs<T>
    {
        private static readonly T kDefault = default(T);
        DependencyPropertyChangedEventArgs m_originalEventArgs;

        public DependencyPropertyChangedEventArgs (DependencyPropertyChangedEventArgs e)
        {
            m_originalEventArgs = e;
        }

        public bool HasOldValue => m_originalEventArgs.OldValue != null;
        public bool HasNewValue => m_originalEventArgs.NewValue != null;

        public T OldValue => this.HasOldValue ? (T)m_originalEventArgs.OldValue : kDefault;
        public T NewValue => this.HasNewValue ? (T)m_originalEventArgs.NewValue : kDefault;

        public DependencyProperty Property => m_originalEventArgs.Property;
    }
}