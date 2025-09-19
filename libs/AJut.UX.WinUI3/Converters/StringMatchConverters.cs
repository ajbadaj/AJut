namespace AJut.UX.Converters
{
    using Microsoft.UI.Xaml.Media;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public abstract class StringSwitchConverter<T> : SimpleValueConverter<string, T>
    {
        private T m_last;
        public StringSwitchConverter(T defaultValue)
        {
            this.Default = defaultValue;
        }

        public Dictionary<string, T> Cases { get; set; }
        public T Default { get; set; }

        protected override T Convert(string key)
        {
            if (this.Cases.TryGetValue(key, out T result))
            {
                return _Return(result);
            }

            return _Return(m_last ?? this.Default);

            T _Return(T value)
            {
                m_last = value;
                return m_last;
            }
        }

    }

    public class BrushDictionary : Dictionary<string, Brush> { }

    public class StringToBrushSwitchConverter : StringSwitchConverter<Brush>
    {
        private static readonly Brush kTransparent = new SolidColorBrush(new Windows.UI.Color { A = 0 });
        public StringToBrushSwitchConverter() : base(kTransparent) { }
    }

}
