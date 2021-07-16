namespace AJut.Application.Controls
{
    using System.Windows;

    public class DefaultDockTearoffWindow : Window
    {
        static DefaultDockTearoffWindow ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DefaultDockTearoffWindow), new FrameworkPropertyMetadata(typeof(DefaultDockTearoffWindow)));
        }

        public DefaultDockTearoffWindow ()
        {
            //this.Items = new ReadOnlyObservableCollection<HeaderItem>(m_items);
            //this.CommandBindings.Add(new CommandBinding(SelectItemCommand, OnSelectedItem, OnCanSelectItem));
        }
    }
}
