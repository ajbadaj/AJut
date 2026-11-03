namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Windows.Graphics;

    /// <summary>
    /// Drives the showroom's own WindowManager (App.Windows): opens tool windows it tracks, picks how they show and hide
    /// with the main window, and shows the activation order. What the page changes it puts back when it is left.
    /// </summary>
    public sealed partial class WindowManagerPage : Page
    {
        private const int kToolWindowWidth = 360;
        private const int kToolWindowHeight = 160;

        private readonly List<Window> m_toolWindows = new List<Window>();
        private eChildWindowDisplayCoordinationStyle m_styleBefore;
        private int m_toolWindowCount;

        public WindowManagerPage ()
        {
            this.InitializeComponent();
            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;
        }

        // ===========[ Events ]================================================
        private void OnLoaded (object? sender, RoutedEventArgs e)
        {
            m_styleBefore = this.Manager.ShowHideChildrenWhen;
            ((INotifyCollectionChanged)this.Manager).CollectionChanged -= this.OnOrderChanged;
            ((INotifyCollectionChanged)this.Manager).CollectionChanged += this.OnOrderChanged;
            this.RefreshOrder();
        }

        private void OnUnloaded (object? sender, RoutedEventArgs e)
        {
            ((INotifyCollectionChanged)this.Manager).CollectionChanged -= this.OnOrderChanged;
            foreach (Window toolWindow in m_toolWindows.ToList())
            {
                toolWindow.Close();
            }

            m_toolWindows.Clear();
            this.Manager.ShowHideChildrenWhen = m_styleBefore;
        }

        private void OnOrderChanged (object? sender, NotifyCollectionChangedEventArgs e) => this.RefreshOrder();

        private void OnToolWindowClosed (object? sender, WindowEventArgs e)
        {
            if (sender is Window toolWindow)
            {
                toolWindow.Closed -= this.OnToolWindowClosed;
                m_toolWindows.Remove(toolWindow);
            }
        }

        private void OpenToolWindow_OnClick (object? sender, RoutedEventArgs e)
        {
            ++m_toolWindowCount;
            var toolWindow = new Window
            {
                Title = $"Tool window {m_toolWindowCount}",
                Content = new TextBlock
                {
                    Text = $"Tool window {m_toolWindowCount}. Click between it and the main window: the order on the page follows.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(16),
                },
            };

            toolWindow.AppWindow.Resize(new SizeInt32(kToolWindowWidth, kToolWindowHeight));
            toolWindow.Closed += this.OnToolWindowClosed;
            m_toolWindows.Add(toolWindow);
            this.Manager.Track(toolWindow);
        }

        private void HideToolWindows_OnClick (object? sender, RoutedEventArgs e) => this.Manager.HideAllWindows();
        private void ShowToolWindows_OnClick (object? sender, RoutedEventArgs e) => this.Manager.ShowAllWindows();
        private void CloseToolWindows_OnClick (object? sender, RoutedEventArgs e) => this.Manager.CloseAllChildWindows();

        // ===========[ Properties ]============================================
        /// <summary>How the tool windows show and hide with the main window, bound to the strip</summary>
        public eChildWindowDisplayCoordinationStyle CoordinationStyle
        {
            get => this.Manager.ShowHideChildrenWhen;
            set => this.Manager.ShowHideChildrenWhen = value;
        }

        // The app sets its instance before any page exists
        private WindowManager Manager => App.Instance!.Windows;

        // ===========[ Helper Methods ]========================================
        private void RefreshOrder ()
        {
            this.OrderReadout.Text = String.Join(", ", this.Manager.Select(this.NameOf));
        }

        private string NameOf (Window window) => (window == this.Manager.Root) ? "Main window" : window.Title;
    }
}
