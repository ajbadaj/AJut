namespace AJut.Application.Controls
{
    using System;
    using System.Collections.Generic;

#if WINDOWS_UWP
    using Windows.UI;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
#else
    using System.Windows;
    using System.Windows.Controls;
#endif

    using DPUtils = DPUtils<Svg>;
    using AJut;
    using System.Windows.Input;
    using System.Windows.Data;
    using System.Windows.Media;
    using AJut.Storage;

    public class Svg : Control
    {

        // ===========================[ Construction ]============================================
        public Svg ()
        {
        }

        static Svg ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Svg), new FrameworkPropertyMetadata(typeof(Svg)));
        }

        public static readonly DependencyProperty SourceProperty = DPUtils.Register(_ => _.Source, (d,e)=>d.Items = new ObservableFlatTreeStore<SvgTreeElement>(e.NewValue?.Root));
        public SvgSource Source
        {
            get => (SvgSource)this.GetValue(SourceProperty);
            set => this.SetValue(SourceProperty, value);
        }

        private static readonly DependencyPropertyKey ItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.Items);
        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;
        public ObservableFlatTreeStore<SvgTreeElement> Items
        {
            get => (ObservableFlatTreeStore<SvgTreeElement>)this.GetValue(ItemsProperty);
            protected set => this.SetValue(ItemsPropertyKey, value);
        }
    }
}