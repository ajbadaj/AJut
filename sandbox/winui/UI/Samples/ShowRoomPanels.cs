namespace AJutShowRoomWinUI.UI.Samples
{
    using System.Globalization;
    using AJut.TypeManagement;
    using AJut.UX.Docking;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    // ===========[ ShowRoomPanelState - DockZone save/load state bag ]===============
    [TypeId("ShowRoomPanelState")]
    public class ShowRoomPanelState
    {
        public string Label { get; set; }
        public double Value { get; set; }
    }

    // ===========[ ShowRoomPanel - DockZone smoke-test display element ]=============
    [TypeId("ShowRoomPanel")]
    public class ShowRoomPanel : ContentControl, IDockableDisplayElement
    {
        // ===========[ Fields ]===================================================
        private string m_label = "Panel";
        private double m_value = 0.0;

        private TextBlock m_labelBlock;
        private TextBox m_valueBox;
        private CheckBox m_blockCloseCheckBox;

        // ===========[ Construction ]=============================================
        public ShowRoomPanel()
        {
            this.HorizontalContentAlignment = HorizontalAlignment.Center;
            this.VerticalContentAlignment = VerticalAlignment.Center;
            this.Padding = new Thickness(8);
            this.BuildContent();
        }

        // ===========[ Properties ]===============================================

        public string Label
        {
            get => m_label;
            set
            {
                m_label = value;
                if (m_labelBlock != null)
                {
                    m_labelBlock.Text = value;
                }
            }
        }

        public double Value
        {
            get => m_value;
            set
            {
                m_value = value;
                if (m_valueBox != null)
                {
                    m_valueBox.Text = value.ToString("G", CultureInfo.InvariantCulture);
                }
            }
        }

        public DockingContentAdapterModel DockingAdapter { get; private set; }

        // ===========[ IDockableDisplayElement ]===================================

        public virtual void Setup(DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            adapter.CanClose -= this.DockingAdapter_OnCanClose;
            adapter.CanClose += this.DockingAdapter_OnCanClose;
        }

        public object GenerateState()
            => new ShowRoomPanelState { Label = m_label, Value = m_value };

        public void ApplyState(object state)
        {
            if (state is not ShowRoomPanelState typed)
            {
                return;
            }

            this.Label = typed.Label;
            if (this.DockingAdapter != null)
            {
                this.DockingAdapter.TitleContent = typed.Label;
            }

            this.Value = typed.Value;
        }

        // ===========[ Event handlers ]============================================

        // The adapter and this panel share one lifetime (the adapter is built for this panel and dropped with
        // it), so the subscription lives exactly as long as the panel does and has nothing to unhook from.
        private void DockingAdapter_OnCanClose (object sender, IsReadyToCloseEventArgs e)
        {
            if (m_blockCloseCheckBox?.IsChecked == true)
            {
                e.IsReadyToClose = false;
            }
        }

        // The value box is a child this panel builds and owns, so the subscription lives and dies with it.
        private void ValueBox_OnLostFocus (object sender, RoutedEventArgs e)
        {
            if (double.TryParse(
                m_valueBox.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double val))
            {
                m_value = val;
            }
            else
            {
                m_valueBox.Text = m_value.ToString("G", CultureInfo.InvariantCulture);
            }
        }

        // ===========[ UI Building ]===============================================

        private void BuildContent()
        {
            m_labelBlock = new TextBlock
            {
                Text = m_label,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            };

            m_valueBox = new TextBox
            {
                Text = m_value.ToString("G", CultureInfo.InvariantCulture),
                PlaceholderText = "Value (double)",
                Width = 140,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            };
            m_valueBox.LostFocus += this.ValueBox_OnLostFocus;

            m_blockCloseCheckBox = new CheckBox
            {
                Content = "Block close",
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            panel.Children.Add(m_labelBlock);
            panel.Children.Add(m_valueBox);
            panel.Children.Add(m_blockCloseCheckBox);
            this.Content = panel;
        }
    }

    [TypeId("ShowRoomPanel-single-inst")]
    public class SingleInstanceShowRoomPanel : ShowRoomPanel
    {
        public override void Setup(DockingContentAdapterModel adapter)
        {
            base.Setup(adapter);
            adapter.HideDontClose = true;
        }
    }

    /// <summary>
    /// A single-instance panel that is hidden from the toolbar and menu UI.
    /// Models an infrastructure panel that is always present in the layout but should
    /// not appear in the "View" menu or the DockPanelAddRemoveToolbar.
    /// </summary>
    [TypeId("ShowRoomPanel-hidden-infra")]
    public class HiddenInfraShowRoomPanel : ShowRoomPanel
    {
        public override void Setup (DockingContentAdapterModel adapter)
        {
            base.Setup(adapter);
            this.Label = "Hidden Infra";
            adapter.TitleContent = "Hidden Infra";
        }
    }
}
