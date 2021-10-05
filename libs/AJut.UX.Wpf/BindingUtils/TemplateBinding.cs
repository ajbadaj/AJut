namespace AJut.UX
{
    using System.Windows;
    using System.Windows.Data;

    public class TemplateBinding : Binding
    {
        public TemplateBinding ()
        {
            this.RelativeSource = new RelativeSource
            {
                Mode = RelativeSourceMode.TemplatedParent
            };
        }

        public TemplateBinding (string path) : this()
        {
            this.Path = new PropertyPath(path);
        }

    }
}
