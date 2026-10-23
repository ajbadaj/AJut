namespace AJut.UX.CodeBinding
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows.Input;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AppKit;

    // ===========[ AppKitBindings ]=============================================
    // The AppKit half of BindingSet: which property each AppKit control shows a value through,
    // and which event means the user changed it. Everything else (watching the source, the
    // main-thread hop, the echo guard, writing only when the value differs, undoing it all on
    // Dispose) is BindingSet's.
    //
    // The user-edit signals used:
    //  - NSControl.Activated, AppKit's target-action, for buttons, sliders, steppers, pop-ups,
    //    segmented controls, and a text field's Return (or focus loss, see BindText).
    //  - NSTextField.Changed for live text editing, which fires on every keystroke the user
    //    types and never for a programmatic StringValue write.
    //
    // NSTextField.Changed is implemented by the bindings through an internal delegate, the same
    // way NSWindow's events are, so a live BindText on a field whose Delegate is set elsewhere
    // replaces that delegate (or is replaced by it). Bind live text only on fields nobody has
    // given a delegate. Activated is target-action and has no such conflict.
    //
    // A text field that has the value it is showing written back into it loses its caret, which
    // is why the write-only-when-different rule in BindingSet matters more on AppKit than most.
    //
    // Restoring state cannot overwrite a bound value here, so the binds do not opt anything out.
    // On Android a view with an id restores its own value after the binding's first push, and the
    // restore arrives as a user edit. AppKit's controls keep no value of their own across
    // launches: state restoration covers windows (their frame, and what a responder lists in
    // restorableStateKeyPaths, which the stock controls leave empty) and only for a window with a
    // restoration class, and autosave names cover layout (split view dividers, table columns),
    // never a control's value. If a subclass ever lists its value in restorableStateKeyPaths,
    // that restore would run after the push and write the stale value back; don't.
    public static class AppKitBindings
    {
        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Binds a text field's text both ways. With <paramref name="commitOnly"/> false, every keystroke writes
        /// the source; with it true, only Return or leaving the field does (the field is set to send its
        /// action on end editing for that).
        /// </summary>
        public static BindingSet.TwoWayChannel BindText (this BindingSet set, NSTextField field, INotifyPropertyChanged source, string propertyName, Func<string> readSource, Action<string> writeSource, bool commitOnly = false)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                field.ReadText, field.WriteText
            );

            if (commitOnly)
            {
                field.Cell.SetSendsActionOnEndEditing(true);
                set.Add(new ActivatedLink(field, channel.OnTargetEdited));
            }
            else
            {
                set.Add(new TextChangedLink(field, channel.OnTargetEdited));
            }

            return channel;
        }

        /// <summary>Shows a source string in a label (or any text field the user does not edit)</summary>
        public static void BindLabel (this BindingSet set, NSTextField label, INotifyPropertyChanged source, string propertyName, Func<string> readSource)
        {
            set.Watch(source, propertyName, Push);

            void Push ()
            {
                string text = readSource() ?? String.Empty;
                if (label.StringValue != text)
                {
                    label.StringValue = text;
                }
            }
        }

        /// <summary>Binds a check box, switch or toggle button's on/off state both ways</summary>
        public static BindingSet.TwoWayChannel BindChecked (this BindingSet set, NSButton button, INotifyPropertyChanged source, string propertyName, Func<bool> readSource, Action<bool> writeSource)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                button.ReadIsOn, button.WriteIsOn
            );

            set.Add(new ActivatedLink(button, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>
        /// Binds a control's DoubleValue both ways: an NSSlider, an NSStepper, or anything else that reports its
        /// value there. Pass <see cref="BindingComparers.Tolerance"/> as <paramref name="comparer"/> for a slider, so
        /// rounding noise in its value is not pushed back and forth. A continuous slider writes the source while
        /// it is dragged.
        /// </summary>
        public static BindingSet.TwoWayChannel BindDoubleValue (this BindingSet set, NSControl control, INotifyPropertyChanged source, string propertyName, Func<double> readSource, Action<double> writeSource, IEqualityComparer<double> comparer = null)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                control.ReadDoubleValue, control.WriteDoubleValue,
                comparer
            );

            set.Add(new ActivatedLink(control, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>Binds a pop-up button's selected index both ways (-1 for nothing selected)</summary>
        public static BindingSet.TwoWayChannel BindSelectedIndex (this BindingSet set, NSPopUpButton popUp, INotifyPropertyChanged source, string propertyName, Func<int> readSource, Action<int> writeSource)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                popUp.ReadSelectedIndex, popUp.WriteSelectedIndex
            );

            set.Add(new ActivatedLink(popUp, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>Binds a single-selection segmented control's selected segment both ways (-1 for none)</summary>
        public static BindingSet.TwoWayChannel BindSelectedSegment (this BindingSet set, NSSegmentedControl segments, INotifyPropertyChanged source, string propertyName, Func<int> readSource, Action<int> writeSource)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                segments.ReadSelectedSegment, segments.WriteSelectedSegment
            );

            set.Add(new ActivatedLink(segments, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>
        /// Binds a NumericEditor's value both ways. The editor keeps the value's numeric type, so read and write
        /// the model's own type through <paramref name="readSource"/> and <paramref name="writeSource"/> (boxed).
        /// </summary>
        public static BindingSet.TwoWayChannel BindValue (this BindingSet set, NumericEditor editor, INotifyPropertyChanged source, string propertyName, Func<object> readSource, Action<object> writeSource)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                editor.ReadValue, editor.WriteValue
            );

            set.Add(new NumericEditorValueLink(editor, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>Binds a ColorEditIngressControl's color both ways; the source sees every live change while the panel is open</summary>
        public static BindingSet.TwoWayChannel BindEditColor (this BindingSet set, ColorEditIngressControl swatch, INotifyPropertyChanged source, string propertyName, Func<ColorArgb> readSource, Action<ColorArgb> writeSource)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                swatch.ReadEditColor, swatch.WriteEditColor
            );

            set.Add(new ColorEditLink(swatch, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>Binds a ToggleStrip's selected item both ways</summary>
        public static BindingSet.TwoWayChannel BindSelectedItem (this BindingSet set, ToggleStrip strip, INotifyPropertyChanged source, string propertyName, Func<object> readSource, Action<object> writeSource)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                strip.ReadSelectedItem, strip.WriteSelectedItem
            );

            set.Add(new ToggleStripSelectionLink(strip, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>Binds an EnumComboBox's selected value both ways</summary>
        public static BindingSet.TwoWayChannel BindSelectedItem (this BindingSet set, EnumComboBox comboBox, INotifyPropertyChanged source, string propertyName, Func<object> readSource, Action<object> writeSource)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                comboBox.ReadSelectedItem, comboBox.WriteSelectedItem
            );

            set.Add(new EnumComboBoxSelectionLink(comboBox, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>Binds an EnumToggleStrip's selected value (combined flags, for a multi select [Flags] enum) both ways</summary>
        public static BindingSet.TwoWayChannel BindSelectedItem (this BindingSet set, EnumToggleStrip strip, INotifyPropertyChanged source, string propertyName, Func<object> readSource, Action<object> writeSource)
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                readSource, writeSource,
                strip.ReadSelectedItem, strip.WriteSelectedItem
            );

            set.Add(new EnumToggleStripSelectionLink(strip, channel.OnTargetEdited));
            return channel;
        }

        /// <summary>Hides a view while the source says so</summary>
        public static void BindHidden (this BindingSet set, NSView view, INotifyPropertyChanged source, string propertyName, Func<bool> readIsHidden)
        {
            set.Watch(source, propertyName, Push);

            void Push ()
            {
                bool isHidden = readIsHidden();
                if (view.Hidden != isHidden)
                {
                    view.Hidden = isHidden;
                }
            }
        }

        /// <summary>Enables a control while the source says so</summary>
        public static void BindEnabled (this BindingSet set, NSControl control, INotifyPropertyChanged source, string propertyName, Func<bool> readIsEnabled)
        {
            set.Watch(source, propertyName, Push);

            void Push ()
            {
                bool isEnabled = readIsEnabled();
                if (control.Enabled != isEnabled)
                {
                    control.Enabled = isEnabled;
                }
            }
        }

        /// <summary>Runs a command when the control is activated, and enables the control only while the command can execute</summary>
        public static BindingSet.CommandChannel BindCommand (this BindingSet set, NSControl control, ICommand command, object parameter = null)
        {
            BindingSet.CommandChannel channel = set.Command(command, parameter, control.WriteEnabled);
            set.Add(new ActivatedLink(control, channel.OnActivated));
            return channel;
        }

        /// <summary>Runs a command when the menu item is chosen, and enables the item only while the command can execute</summary>
        public static BindingSet.CommandChannel BindCommand (this BindingSet set, NSMenuItem item, ICommand command, object parameter = null)
        {
            BindingSet.CommandChannel channel = set.Command(command, parameter, item.WriteEnabled);
            set.Add(new MenuItemActivatedLink(item, channel.OnActivated));
            return channel;
        }

        // ===========[ Helper Methods ]========================================
        // Target reads and writes as named methods, so each binding above hands BindingSet a method
        // group rather than a lambda. BindingSet only stores and invokes these, never subscribes them.
        private static string ReadText (this NSTextField field) => field.StringValue ?? String.Empty;
        private static void WriteText (this NSTextField field, string value) => field.StringValue = value ?? String.Empty;
        private static bool ReadIsOn (this NSButton button) => button.State == NSCellStateValue.On;
        private static void WriteIsOn (this NSButton button, bool isOn) => button.State = isOn ? NSCellStateValue.On : NSCellStateValue.Off;
        private static double ReadDoubleValue (this NSControl control) => control.DoubleValue;
        private static void WriteDoubleValue (this NSControl control, double value) => control.DoubleValue = value;
        private static int ReadSelectedIndex (this NSPopUpButton popUp) => (int)popUp.IndexOfSelectedItem;
        private static void WriteSelectedIndex (this NSPopUpButton popUp, int index) => popUp.SelectItem(index);
        private static int ReadSelectedSegment (this NSSegmentedControl segments) => (int)segments.SelectedSegment;
        private static void WriteSelectedSegment (this NSSegmentedControl segments, int index) => segments.SelectedSegment = index;
        private static void WriteEnabled (this NSControl control, bool isEnabled) => control.Enabled = isEnabled;
        private static void WriteEnabled (this NSMenuItem item, bool isEnabled) => item.Enabled = isEnabled;
        private static ColorArgb ReadEditColor (this ColorEditIngressControl swatch) => swatch.EditColor;
        private static void WriteEditColor (this ColorEditIngressControl swatch, ColorArgb value) => swatch.EditColor = value;
        private static object ReadValue (this NumericEditor editor) => editor.Value;
        private static void WriteValue (this NumericEditor editor, object value) => editor.Value = value;
        private static object ReadSelectedItem (this ToggleStrip strip) => strip.SelectedItem;
        private static void WriteSelectedItem (this ToggleStrip strip, object value) => strip.SelectedItem = value;
        private static object ReadSelectedItem (this EnumComboBox comboBox) => comboBox.SelectedItem;
        private static void WriteSelectedItem (this EnumComboBox comboBox, object value) => comboBox.SelectedItem = value;
        private static object ReadSelectedItem (this EnumToggleStrip strip) => strip.SelectedItem;
        private static void WriteSelectedItem (this EnumToggleStrip strip, object value) => strip.SelectedItem = value;

        // ===========[ Subclasses/structs ]====================================
        // One link per view subscription, each owning its += and the matching -= in Dispose
        private sealed class ActivatedLink : IDisposable
        {
            private readonly NSControl m_control;
            private readonly Action m_onActivated;

            public ActivatedLink (NSControl control, Action onActivated)
            {
                m_control = control;
                m_onActivated = onActivated;
                m_control.Activated += this.OnActivated;
            }

            public void Dispose () => m_control.Activated -= this.OnActivated;

            private void OnActivated (object sender, EventArgs e) => m_onActivated();
        }

        private sealed class TextChangedLink : IDisposable
        {
            private readonly NSTextField m_field;
            private readonly Action m_onChanged;

            public TextChangedLink (NSTextField field, Action onChanged)
            {
                m_field = field;
                m_onChanged = onChanged;
                m_field.Changed += this.OnChanged;
            }

            public void Dispose () => m_field.Changed -= this.OnChanged;

            private void OnChanged (object sender, EventArgs e) => m_onChanged();
        }

        private sealed class ColorEditLink : IDisposable
        {
            private readonly ColorEditIngressControl m_swatch;
            private readonly Action m_onChanged;

            public ColorEditLink (ColorEditIngressControl swatch, Action onChanged)
            {
                m_swatch = swatch;
                m_onChanged = onChanged;
                m_swatch.EditColorChanged += this.OnEditColorChanged;
            }

            public void Dispose () => m_swatch.EditColorChanged -= this.OnEditColorChanged;

            private void OnEditColorChanged (object sender, EventArgs e) => m_onChanged();
        }

        private sealed class NumericEditorValueLink : IDisposable
        {
            private readonly NumericEditor m_editor;
            private readonly Action m_onChanged;

            public NumericEditorValueLink (NumericEditor editor, Action onChanged)
            {
                m_editor = editor;
                m_onChanged = onChanged;
                m_editor.ValueChanged += this.OnValueChanged;
            }

            public void Dispose () => m_editor.ValueChanged -= this.OnValueChanged;

            private void OnValueChanged (object sender, EventArgs e) => m_onChanged();
        }

        private sealed class ToggleStripSelectionLink : IDisposable
        {
            private readonly ToggleStrip m_strip;
            private readonly Action m_onChanged;

            public ToggleStripSelectionLink (ToggleStrip strip, Action onChanged)
            {
                m_strip = strip;
                m_onChanged = onChanged;
                m_strip.SelectionChanged += this.OnSelectionChanged;
            }

            public void Dispose () => m_strip.SelectionChanged -= this.OnSelectionChanged;

            private void OnSelectionChanged (object sender, ToggleStrip.ToggleStripSelectionChangedEventArgs e) => m_onChanged();
        }

        private sealed class EnumComboBoxSelectionLink : IDisposable
        {
            private readonly EnumComboBox m_comboBox;
            private readonly Action m_onChanged;

            public EnumComboBoxSelectionLink (EnumComboBox comboBox, Action onChanged)
            {
                m_comboBox = comboBox;
                m_onChanged = onChanged;
                m_comboBox.SelectedItemChanged += this.OnSelectedItemChanged;
            }

            public void Dispose () => m_comboBox.SelectedItemChanged -= this.OnSelectedItemChanged;

            private void OnSelectedItemChanged (object sender, EventArgs e) => m_onChanged();
        }

        private sealed class EnumToggleStripSelectionLink : IDisposable
        {
            private readonly EnumToggleStrip m_strip;
            private readonly Action m_onChanged;

            public EnumToggleStripSelectionLink (EnumToggleStrip strip, Action onChanged)
            {
                m_strip = strip;
                m_onChanged = onChanged;
                m_strip.SelectedItemChanged += this.OnSelectedItemChanged;
            }

            public void Dispose () => m_strip.SelectedItemChanged -= this.OnSelectedItemChanged;

            private void OnSelectedItemChanged (object sender, EventArgs e) => m_onChanged();
        }

        private sealed class MenuItemActivatedLink : IDisposable
        {
            private readonly NSMenuItem m_item;
            private readonly Action m_onActivated;

            public MenuItemActivatedLink (NSMenuItem item, Action onActivated)
            {
                m_item = item;
                m_onActivated = onActivated;
                m_item.Activated += this.OnActivated;
            }

            public void Dispose () => m_item.Activated -= this.OnActivated;

            private void OnActivated (object sender, EventArgs e) => m_onActivated();
        }
    }
}
