namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.Storage;
    using DPUtils = AJut.UX.DPUtils<MessageBoxPopover>;

    public class MessageBoxPopover : Control, IStackNavPopoverDisplay<string>
    {
        static MessageBoxPopover ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MessageBoxPopover), new FrameworkPropertyMetadata(typeof(MessageBoxPopover)));
        }

        private readonly bool m_includeCancel;
        private MessageBoxPopover (string message, bool includeCancel, params string[] options)
        {
            m_includeCancel = includeCancel;
            this.Message = message;
            this.Options = new List<Option>();
            this.Options.AddRange(options.Select(o => new Option(false, o)));
            if (m_includeCancel)
            {
                this.Options.Add(new Option(true, "Cancel"));
            }

            this.AddHandler(Button.ClickEvent, new RoutedEventHandler(Option_OnClick));
        }

        public string Message { get; }
        public List<Option> Options { get; }

        public static readonly DependencyProperty OptionButtonStyleProperty = DPUtils.Register(_ => _.OptionButtonStyle);
        public Style OptionButtonStyle
        {
            get => (Style)this.GetValue(OptionButtonStyleProperty);
            set => this.SetValue(OptionButtonStyleProperty, value);
        }

        public static readonly DependencyProperty OptionsPaddingProperty = DPUtils.Register(_ => _.OptionsPadding);
        public Thickness OptionsPadding
        {
            get => (Thickness)this.GetValue(OptionsPaddingProperty);
            set => this.SetValue(OptionsPaddingProperty, value);
        }

        public static readonly DependencyProperty SeparatorLineHeightProperty = DPUtils.Register(_ => _.SeparatorLineHeight);
        public double SeparatorLineHeight
        {
            get => (double)this.GetValue(SeparatorLineHeightProperty);
            set => this.SetValue(SeparatorLineHeightProperty, value);
        }

        public static readonly DependencyProperty OptionsPanelTemplateProperty = DPUtils.Register(_ => _.OptionsPanelTemplate);
        public ItemsPanelTemplate OptionsPanelTemplate
        {
            get => (ItemsPanelTemplate)this.GetValue(OptionsPanelTemplateProperty);
            set => this.SetValue(OptionsPanelTemplateProperty, value);
        }

        public static readonly DependencyProperty PromptTextAlignmentProperty = DPUtils.Register(_ => _.PromptTextAlignment);
        public TextAlignment PromptTextAlignment
        {
            get => (TextAlignment)this.GetValue(PromptTextAlignmentProperty);
            set => this.SetValue(PromptTextAlignmentProperty, value);
        }

        public static MessageBoxPopover Generate (string message, params string[] options)
        {
            return new MessageBoxPopover(message, false, options);
        }

        public static MessageBoxPopover GenerateWithCancel (string message, params string[] options)
        {
            return new MessageBoxPopover(message, true, options);
        }

        public event EventHandler<EventArgs<Result<string>>> ResultSet;

        public void Cancel (string cancelReason = null)
        {
            this.Raise(Result<string>.Error(cancelReason));
        }

        private void Raise (Result<string> result)
        {
            this.ResultSet?.Invoke(this, new EventArgs<Result<string>>(result));
        }

        private void Option_OnClick (object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is Option option)
            {
                if (option.IsCancel)
                {
                    this.Cancel("User selected cancel");
                }
                else
                {
                    this.Raise(new Result<string>(option.Name));
                }
            }
        }

        public class Option
        {
            public Option (bool isCancel, string name)
            {
                this.IsCancel = isCancel;
                this.Name = name;
            }

            public bool IsCancel { get; }
            public string Name { get; }
        }
    }
}
