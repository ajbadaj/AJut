namespace AJut.UX
{
    using System;
    using System.Windows;
    using System.Windows.Data;
    using DPUtils = AJut.UX.DPUtils<AJut.UX.DPWatcher>;

    /// <summary>
    /// Not a fan of the DependencyPropertyDescriptor, utilizing this instead
    /// </summary>
    public class DPWatcher : FrameworkElement
    {
        public readonly object m_source;

        public DPWatcher (object source)
        {
            m_source = source;
        }

        public event EventHandler<EventArgs<object>> WatchedValueChanged;

        public static readonly DependencyProperty ValueProperty = DPUtils.Register(_ => _.Value, (d,e)=>d.OnValueChanged(e));
        public object Value
        {
            get => (object)this.GetValue(ValueProperty);
            set => this.SetValue(ValueProperty, value);
        }

        private void OnValueChanged (DependencyPropertyChangedEventArgs<object> e)
        {
            this.WatchedValueChanged?.Invoke(this, new EventArgs<object>(e.NewValue));
        }

        public void Watch(params DependencyProperty[] properties)
        {
            foreach (var property in properties)
            {
                var binding = m_source.CreateBinding(property, BindingMode.OneWay);
                this.SetBinding(ValueProperty, binding);
            }
        }

        public void Watch (string path)
        {
            var binding = m_source.CreateBinding(path, BindingMode.OneWay);
            this.SetBinding(ValueProperty, binding);
        }
    }
}