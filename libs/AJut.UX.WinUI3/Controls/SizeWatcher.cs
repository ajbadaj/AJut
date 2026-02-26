using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AJut.UX.Controls;

using DPUtils = DPUtils<SizeWatcher>;

// ===========[ SizeWatcher ]================================================
// WinUI3-specific: no WPF equivalent.
// ContentControl wrapper that surfaces its inner content element's SizeChanged event.
public sealed class SizeWatcher : ContentControl
{
    public SizeWatcher()
    {
        this.DefaultStyleKey = typeof(SizeWatcher);

        // Ensure the internal content stretches to fill the SizeWatcher
        this.Padding = new Thickness(0.0);
        this.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        this.VerticalContentAlignment = VerticalAlignment.Stretch;
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (oldContent is FrameworkElement oldFE)
        {
            oldFE.SizeChanged -= this.OnSizeChanged;
        }
        if (newContent is FrameworkElement newFE)
        {
            newFE.SizeChanged += this.OnSizeChanged;
        }
        else
        {
            this.SizeChanged += this.OnSizeChanged;
        }
    }

    public static readonly DependencyProperty BindableActualWidthProperty = DPUtils.Register(_ => _.BindableActualWidth);
    public double BindableActualWidth
    {
        get => (double)this.GetValue(BindableActualWidthProperty);
        set => this.SetValue(BindableActualWidthProperty, value);
    }

    public static readonly DependencyProperty BindableActualHeightProperty = DPUtils.Register(_ => _.BindableActualHeight);
    public double BindableActualHeight
    {
        get => (double)this.GetValue(BindableActualHeightProperty);
        set => this.SetValue(BindableActualHeightProperty, value);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        this.BindableActualWidth = e.NewSize.Width;
        this.BindableActualHeight = e.NewSize.Height;
    }
}
