namespace AJut.Application
{
#if WINDOWS_UWP
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;
#else
    using System.Windows;
    using System.Windows.Data;
#endif

    public class TemplateBinding : Binding
    {
        public TemplateBinding()
        {
            this.RelativeSource = new RelativeSource
            {
                Mode = RelativeSourceMode.TemplatedParent
            };
        }

        public TemplateBinding(string path) : this()
        {
            this.Path = new PropertyPath(path);
        }

    }
}
