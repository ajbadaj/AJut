namespace AJut.Application
{
    using System;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media.Animation;
    using AJut.IO;

    [TypeConverter(typeof(SvgTypeConverter))]
    public class SvgSource : Animatable
    {
        public SvgSource ()
        {
        }

        public SvgSource (SvgTreeElement root, SvgMetadata metadata)
        {
            this.Root = root;
            this.Metadata = metadata;
        }

        public SvgMetadata Metadata { get; set; }
        public SvgTreeElement Root { get; private set; }

        protected override Freezable CreateInstanceCore ()
        {
            return new SvgSource();
        }

        protected override void CloneCore (Freezable sourceFreezable)
        {
            base.CloneCore(sourceFreezable);
            var casted = (SvgSource)sourceFreezable;
            casted.Root = this.Root.Duplicate();
            casted.Metadata = this.Metadata.Duplicate();
        }

        public class SvgTypeConverter : FileBasedSourceTypeConverter
        {
            protected override object ConvertFrom (ITypeDescriptorContext context, CultureInfo culture, Stream stream)
            {
                IntPtr hwnd = IntPtr.Zero;
                if (context is System.Windows.Markup.IProvideValueTarget castedContext && castedContext.TargetObject is DependencyObject thingOnUI)
                {
                    hwnd = WindowXT.GetHwnd(thingOnUI);
                }

                return SvgSerialization.LoadSvg(stream, hwnd);
            }
        }
    }
}
