namespace AJut.UX.Controls
{
    using System.Windows;

    /// <summary>
    /// The PropertyGrid's built-in row templates for the Nullable, Button and List editors, declared in XAML and read out
    /// sealed. Only <see cref="PropertyGridTemplateSelector"/> builds one, once.
    /// </summary>
    internal partial class PropertyGridBuiltInTemplates : ResourceDictionary
    {
        // ===========[ Setup/Construction/Teardown ]==========================
        public PropertyGridBuiltInTemplates ()
        {
            this.InitializeComponent();
            this.NullableTemplate = this.TakeSealed("NullableTemplate");
            this.ButtonTemplate = this.TakeSealed("ButtonTemplate");
            this.ListTemplate = this.TakeSealed("ListTemplate");
        }

        // ===========[ Properties ]===========================================
        /// <summary>A <see cref="NullableEditor"/> for a Nullable&lt;T&gt; property's row</summary>
        public DataTemplate NullableTemplate { get; }

        /// <summary>A button showing the row's EditValue that runs its EditContext command, disabled in a read-only grid</summary>
        public DataTemplate ButtonTemplate { get; }

        /// <summary>A <see cref="PropertyGridListEditor"/> for a list property's row</summary>
        public DataTemplate ListTemplate { get; }

        // ===========[ Helper Methods ]=======================================
        // Sealing a template also frees it from the thread that loaded it, so grids on every UI thread share these
        private DataTemplate TakeSealed (string key)
        {
            var template = (DataTemplate)this[key];
            template.Seal();
            return template;
        }
    }
}
