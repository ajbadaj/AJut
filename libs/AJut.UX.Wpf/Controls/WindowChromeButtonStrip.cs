﻿namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.UX.AttachedProperties;
    using DPUtils = AJut.UX.DPUtils<WindowChromeButtonStrip>;

    [TemplatePart(Name = nameof(PART_ChromeCloseButton), Type = typeof(ButtonBase))]
    public class WindowChromeButtonStrip : Control
    {
        static WindowChromeButtonStrip ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WindowChromeButtonStrip), new FrameworkPropertyMetadata(typeof(WindowChromeButtonStrip)));
        }

        public WindowChromeButtonStrip()
        {
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, OnMinimzeWindow, OnCanMinimizeWindow));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, OnMaximizeWindow, OnCanMaximizeWindow));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, OnRestoreWindow, OnCanRestoreWindow));
            this.CommandBindings.Add(new CommandBinding(WindowXTA.ToggleFullscreenCommand, OnToggleFullscreen, OnCanToggleFullscreen));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, OnCloseWindow, OnCanCloseWindow));
        }

        private void OnCanMinimizeWindow (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OnMinimzeWindow (object sender, ExecutedRoutedEventArgs e)
        {
            Window.GetWindow(this).WindowState = WindowState.Minimized;
        }

        private void OnCanMaximizeWindow (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OnMaximizeWindow (object sender, ExecutedRoutedEventArgs e)
        {
            Window.GetWindow(this).WindowState = WindowState.Maximized;
        }

        private void OnCanRestoreWindow (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OnRestoreWindow (object sender, ExecutedRoutedEventArgs e)
        {
            Window.GetWindow(this).WindowState = WindowState.Normal;
        }

        private void OnCanToggleFullscreen (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OnToggleFullscreen (object sender, ExecutedRoutedEventArgs e)
        {
            WindowXTA.ToggleIsFullscreen(Window.GetWindow(this));
        }

        private void OnCanCloseWindow (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OnCloseWindow (object sender, ExecutedRoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        public override void OnApplyTemplate ()
        {
            if (this.PART_ChromeCloseButton != null)
            {
                this.PART_ChromeCloseButton.MouseEnter -= _OnChromeCloseButtonMouseEnter;
                this.PART_ChromeCloseButton.MouseLeave -= _OnChromeCloseButtonMouseLeave;
            }

            base.OnApplyTemplate();
            this.PART_ChromeCloseButton = this.GetTemplateChild(nameof(PART_ChromeCloseButton)) as ButtonBase;
            if (this.PART_ChromeCloseButton != null)
            {
                this.PART_ChromeCloseButton.MouseEnter += _OnChromeCloseButtonMouseEnter;
                this.PART_ChromeCloseButton.MouseLeave += _OnChromeCloseButtonMouseLeave;
            }

            void _OnChromeCloseButtonMouseEnter (object sender, MouseEventArgs e)
            {
                this.IsMouseOverClose = true;
            }

            void _OnChromeCloseButtonMouseLeave (object sender, MouseEventArgs e)
            {
                this.IsMouseOverClose = false;
            }
        }

        private ButtonBase PART_ChromeCloseButton { get; set; }

        public static readonly DependencyProperty ButtonPaddingProperty = DPUtils.Register(_ => _.ButtonPadding);
        public Thickness ButtonPadding
        {
            get => (Thickness)this.GetValue(ButtonPaddingProperty);
            set => this.SetValue(ButtonPaddingProperty, value);
        }

        public static readonly DependencyProperty ButtonGlyphFontSizeProperty = DPUtils.Register(_ => _.ButtonGlyphFontSize);
        public double ButtonGlyphFontSize
        {
            get => (double)this.GetValue(ButtonGlyphFontSizeProperty);
            set => this.SetValue(ButtonGlyphFontSizeProperty, value);
        }

        public static readonly DependencyProperty ButtonCornerRadiusProperty = DPUtils.Register(_ => _.ButtonCornerRadius);
        public CornerRadius ButtonCornerRadius
        {
            get => (CornerRadius)this.GetValue(ButtonCornerRadiusProperty);
            set => this.SetValue(ButtonCornerRadiusProperty, value);
        }

        // == Glyph Foreground

        public static readonly DependencyProperty ButtonGlyphForegroundProperty = DPUtils.Register(_ => _.ButtonGlyphForeground);
        public Brush ButtonGlyphForeground
        {
            get => (Brush)this.GetValue(ButtonGlyphForegroundProperty);
            set => this.SetValue(ButtonGlyphForegroundProperty, value);
        }

        public static readonly DependencyProperty ButtonHoverGlyphForegroundProperty = DPUtils.Register(_ => _.ButtonHoverGlyphForeground);
        public Brush ButtonHoverGlyphForeground
        {
            get => (Brush)this.GetValue(ButtonHoverGlyphForegroundProperty);
            set => this.SetValue(ButtonHoverGlyphForegroundProperty, value);
        }

        public static readonly DependencyProperty ButtonPressedGlyphForegroundProperty = DPUtils.Register(_ => _.ButtonPressedGlyphForeground);
        public Brush ButtonPressedGlyphForeground
        {
            get => (Brush)this.GetValue(ButtonPressedGlyphForegroundProperty);
            set => this.SetValue(ButtonPressedGlyphForegroundProperty, value);
        }

        public static readonly DependencyProperty CloseButtonHoverGlyphForegroundProperty = DPUtils.Register(_ => _.CloseButtonHoverGlyphForeground);
        public Brush CloseButtonHoverGlyphForeground
        {
            get => (Brush)this.GetValue(CloseButtonHoverGlyphForegroundProperty);
            set => this.SetValue(CloseButtonHoverGlyphForegroundProperty, value);
        }

        // == Background

        public static readonly DependencyProperty ButtonBackgroundProperty = DPUtils.Register(_ => _.ButtonBackground);
        public Brush ButtonBackground
        {
            get => (Brush)this.GetValue(ButtonBackgroundProperty);
            set => this.SetValue(ButtonBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonHoverBackgroundProperty = DPUtils.Register(_ => _.ButtonHoverBackground);
        public string ButtonHoverBackground
        {
            get => (string)this.GetValue(ButtonHoverBackgroundProperty);
            set => this.SetValue(ButtonHoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty ButtonPressedBackgroundProperty = DPUtils.Register(_ => _.ButtonPressedBackground);
        public Brush ButtonPressedBackground
        {
            get => (Brush)this.GetValue(ButtonPressedBackgroundProperty);
            set => this.SetValue(ButtonPressedBackgroundProperty, value);
        }

        public static readonly DependencyProperty CloseButtonHoverBackgroundProperty = DPUtils.Register(_ => _.CloseButtonHoverBackground);
        public Brush CloseButtonHoverBackground
        {
            get => (Brush)this.GetValue(CloseButtonHoverBackgroundProperty);
            set => this.SetValue(CloseButtonHoverBackgroundProperty, value);
        }

        // == Border

        public static readonly DependencyProperty ButtonBorderProperty = DPUtils.Register(_ => _.ButtonBorder);
        public Brush ButtonBorder
        {
            get => (Brush)this.GetValue(ButtonBorderProperty);
            set => this.SetValue(ButtonBorderProperty, value);
        }

        public static readonly DependencyProperty ButtonHoverBorderProperty = DPUtils.Register(_ => _.ButtonHoverBorder);
        public Brush ButtonHoverBorder
        {
            get => (Brush)this.GetValue(ButtonHoverBorderProperty);
            set => this.SetValue(ButtonHoverBorderProperty, value);
        }

        public static readonly DependencyProperty ButtonPressedBorderProperty = DPUtils.Register(_ => _.ButtonPressedBorder);
        public Brush ButtonPressedBorder
        {
            get => (Brush)this.GetValue(ButtonPressedBorderProperty);
            set => this.SetValue(ButtonPressedBorderProperty, value);
        }

        public static readonly DependencyProperty CloseButtonHoverBorderProperty = DPUtils.Register(_ => _.CloseButtonHoverBorder);
        public Brush CloseButtonHoverBorder
        {
            get => (Brush)this.GetValue(CloseButtonHoverBorderProperty);
            set => this.SetValue(CloseButtonHoverBorderProperty, value);
        }

        // == Interactability

        public static readonly DependencyProperty AllowMinimizeProperty = DPUtils.Register(_ => _.AllowMinimize);
        public bool AllowMinimize
        {
            get => (bool)this.GetValue(AllowMinimizeProperty);
            set => this.SetValue(AllowMinimizeProperty, value);
        }

        public static readonly DependencyProperty AllowMaximizeRestoreProperty = DPUtils.Register(_ => _.AllowMaximizeRestore);
        public bool AllowMaximizeRestore
        {
            get => (bool)this.GetValue(AllowMaximizeRestoreProperty);
            set => this.SetValue(AllowMaximizeRestoreProperty, value);
        }

        public static readonly DependencyProperty AllowFullscreenProperty = DPUtils.Register(_ => _.AllowFullscreen);
        public bool AllowFullscreen
        {
            get => (bool)this.GetValue(AllowFullscreenProperty);
            set => this.SetValue(AllowFullscreenProperty, value);
        }

        public static readonly DependencyProperty MinimizeToolTipProperty = DPUtils.Register(_ => _.MinimizeToolTip);
        public string MinimizeToolTip
        {
            get => (string)this.GetValue(MinimizeToolTipProperty);
            set => this.SetValue(MinimizeToolTipProperty, value);
        }

        public static readonly DependencyProperty MaximizeWindowedToolTipProperty = DPUtils.Register(_ => _.MaximizeWindowedToolTip);
        public string MaximizeWindowedToolTip
        {
            get => (string)this.GetValue(MaximizeWindowedToolTipProperty);
            set => this.SetValue(MaximizeWindowedToolTipProperty, value);
        }

        public static readonly DependencyProperty RestoreMaximizedWindowToolTipProperty = DPUtils.Register(_ => _.RestoreMaximizedWindowToolTip);
        public string RestoreMaximizedWindowToolTip
        {
            get => (string)this.GetValue(RestoreMaximizedWindowToolTipProperty);
            set => this.SetValue(RestoreMaximizedWindowToolTipProperty, value);
        }

        public static readonly DependencyProperty EnterFullscreenToolTipProperty = DPUtils.Register(_ => _.EnterFullscreenToolTip);
        public string EnterFullscreenToolTip
        {
            get => (string)this.GetValue(EnterFullscreenToolTipProperty);
            set => this.SetValue(EnterFullscreenToolTipProperty, value);
        }

        public static readonly DependencyProperty LeaveFullscreenToolTipProperty = DPUtils.Register(_ => _.LeaveFullscreenToolTip);
        public string LeaveFullscreenToolTip
        {
            get => (string)this.GetValue(LeaveFullscreenToolTipProperty);
            set => this.SetValue(LeaveFullscreenToolTipProperty, value);
        }

        public static readonly DependencyProperty CloseToolTipProperty = DPUtils.Register(_ => _.CloseToolTip);
        public string CloseToolTip
        {
            get => (string)this.GetValue(CloseToolTipProperty);
            set => this.SetValue(CloseToolTipProperty, value);
        }

        private static readonly DependencyPropertyKey IsMouseOverClosePropertyKey = DPUtils.RegisterReadOnly(_ => _.IsMouseOverClose);
        public static readonly DependencyProperty IsMouseOverCloseProperty = IsMouseOverClosePropertyKey.DependencyProperty;
        public bool IsMouseOverClose
        {
            get => (bool)this.GetValue(IsMouseOverCloseProperty);
            protected set => this.SetValue(IsMouseOverClosePropertyKey, value);
        }
    }
}