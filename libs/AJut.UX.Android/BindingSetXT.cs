namespace AJut.UX.CodeBinding
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows.Input;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using Android.Text;
    using Android.Views;
    using Android.Widget;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// The Android half of <see cref="BindingSet"/>: which property each Android view shows a value through, and
    /// which of its events means the user edited it. The set does the rest (watching the model, running every push
    /// on the main thread, guarding against a push echoing back, and undoing it all on Dispose).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every method here is a hookup, so it belongs on the main thread the set was made on, and the set throws
    /// anywhere else. A model may still raise its changes from any thread; the set carries those to the main thread.
    /// </para>
    /// <para>
    /// Android's TextChanged and CheckedChange fire for a value set from code as well as for a user's edit, so the
    /// set's two-way guard is what stops a model push from writing itself straight back to the model.
    /// </para>
    /// <para>
    /// A two-way bind turns off the view's own saved state (SaveEnabled). A view with an id saves its value when the
    /// activity is torn down and puts it back after the new one's OnCreate, which is after the binding has pushed the
    /// model's value; the restore then lands as a user edit and writes the old value over the model. With the model
    /// bound, it is the one place the value lives.
    /// </para>
    /// <para>
    /// Any view binds two ways through <see cref="BindView{TView, TArgs, T}"/>, the way UIKit's BindControl takes any
    /// control. Android has no one event every view raises for an edit, as UIKit's ValueChanged is, so the caller names
    /// the view's edit event with a hook and an unhook. Every two-way bind hands back its channel, as UIKit's do.
    /// </para>
    /// </remarks>
    public static class BindingSetXT
    {
        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Binds a view property both ways: the view shows the source now and on every change, and its edit event writes
        /// the user's edit back. <paramref name="hookEdited"/> and <paramref name="unhookEdited"/> add and remove the
        /// handler they are given on that event (<c>(view, handler) => view.ProgressChanged += handler</c>, and the same
        /// with <c>-=</c>); the set unhooks it on Dispose. The type arguments are given in full, since nothing passed in
        /// names the event's argument type: <c>BindView&lt;SeekBar, SeekBar.ProgressChangedEventArgs, int&gt;</c>.
        /// </summary>
        public static BindingSet.TwoWayChannel BindView<TView, TArgs, T> (
            this BindingSet set, TView view,
            Action<TView, EventHandler<TArgs>> hookEdited, Action<TView, EventHandler<TArgs>> unhookEdited,
            Func<TView, T> readTarget, Action<TView, T> writeTarget,
            INotifyPropertyChanged source, string propertyName, Func<T> read, Action<T> write,
            IEqualityComparer<T>? targetComparer = null)
            where TView : View
        {
            // The channel pushes the model's value as it is made, before the edit event is hooked, so that first push's
            // own edit event goes nowhere; after it, the channel's guard ignores the edit event each push raises.
            view.SaveEnabled = false;
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                read, write,
                () => readTarget(view), value => writeTarget(view, value),
                targetComparer
            );

            set.Add(new ViewEditLink<TView, TArgs>(view, hookEdited, unhookEdited, channel));
            return channel;
        }

        /// <summary>Binds a view property both ways through a conversion, for a view that shows a different type than the source holds.</summary>
        public static BindingSet.TwoWayChannel BindView<TView, TArgs, TSource, TTarget> (
            this BindingSet set, TView view,
            Action<TView, EventHandler<TArgs>> hookEdited, Action<TView, EventHandler<TArgs>> unhookEdited,
            Func<TView, TTarget> readTarget, Action<TView, TTarget> writeTarget,
            INotifyPropertyChanged source, string propertyName, Func<TSource> read, Action<TSource> write,
            BindingConversion<TSource, TTarget> conversion)
            where TView : View
        {
            view.SaveEnabled = false;
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                read, write,
                () => readTarget(view), value => writeTarget(view, value),
                conversion
            );

            set.Add(new ViewEditLink<TView, TArgs>(view, hookEdited, unhookEdited, channel));
            return channel;
        }

        /// <summary>Runs <paramref name="action"/> whenever <paramref name="view"/> is clicked, until the set is disposed.</summary>
        public static void OnClick (this BindingSet set, View view, Action action)
        {
            set.Add(new ClickLink(view, action));
        }

        /// <summary>Clicking <paramref name="view"/> executes <paramref name="command"/>, and the view's Enabled follows its CanExecute.</summary>
        public static BindingSet.CommandChannel BindCommand (this BindingSet set, View view, ICommand command, object? parameter = null)
        {
            BindingSet.CommandChannel channel = set.Command(command, parameter, canExecute => view.Enabled = canExecute);
            set.Add(new CommandClickLink(view, channel));
            return channel;
        }

        /// <summary>A text field edits a string property as the user types, and shows it when it changes elsewhere.</summary>
        public static BindingSet.TwoWayChannel BindText (this BindingSet set, EditText field, INotifyPropertyChanged source, string propertyName, Func<string?> read, Action<string> write)
        {
            field.SaveEnabled = false;
            var link = new TextEditLink(field);
            set.Add(link);
            BindingSet.TwoWayChannel channel = set.TwoWay<string>(
                source, propertyName,
                () => read() ?? string.Empty, write,
                () => field.Text ?? string.Empty, link.Push
            );

            link.Channel = channel;
            return channel;
        }

        /// <summary>A text view shows a string property.</summary>
        public static void BindLabel (this BindingSet set, TextView label, INotifyPropertyChanged source, string propertyName, Func<string?> read)
        {
            set.Watch(source, propertyName, () => _PushLabel(label, read()));
        }

        /// <summary>A check box, switch or toggle edits a bool property.</summary>
        public static BindingSet.TwoWayChannel BindChecked (this BindingSet set, CompoundButton check, INotifyPropertyChanged source, string propertyName, Func<bool> read, Action<bool> write)
        {
            check.SaveEnabled = false;
            var link = new CheckedLink(check);
            set.Add(link);
            BindingSet.TwoWayChannel channel = set.TwoWay<bool>(
                source, propertyName,
                read, write,
                () => check.Checked, value => check.Checked = value
            );

            link.Channel = channel;
            return channel;
        }

        /// <summary>
        /// A seek bar's progress edits an int property, continuously while it is dragged, as a UISlider bound on UIKit
        /// does. The property's range is the seek bar's to set (Min and Max).
        /// </summary>
        public static BindingSet.TwoWayChannel BindProgress (this BindingSet set, SeekBar seekBar, INotifyPropertyChanged source, string propertyName, Func<int> read, Action<int> write)
        {
            return set.BindView<SeekBar, SeekBar.ProgressChangedEventArgs, int>(
                seekBar,
                _HookProgressChanged,
                _UnhookProgressChanged,
                _ReadProgress,
                _WriteProgress,
                source,
                propertyName,
                read,
                write
            );
        }

        /// <summary>
        /// A spinner's selected position edits an index property, as a UISegmentedControl bound on UIKit does. An empty
        /// spinner reads back -1 (no selection), and a position outside its items leaves it as it is.
        /// </summary>
        /// <remarks>
        /// A spinner reports a selection when it next lays one out, one set from code included, so a model push comes back
        /// as an edit after the push has finished. It writes the position the model already holds, which changes nothing.
        /// </remarks>
        public static BindingSet.TwoWayChannel BindSelectedPosition (this BindingSet set, Spinner spinner, INotifyPropertyChanged source, string propertyName, Func<int> read, Action<int> write)
        {
            return set.BindView<Spinner, AdapterView.ItemSelectedEventArgs, int>(
                spinner,
                _HookItemSelected,
                _UnhookItemSelected,
                _ReadSelectedPosition,
                _WriteSelectedPosition,
                source,
                propertyName,
                read,
                write
            );
        }

        /// <summary>Shows <paramref name="view"/> when the property reads true, and removes it from layout (Gone) when false.</summary>
        public static void BindVisible (this BindingSet set, View view, INotifyPropertyChanged source, string propertyName, Func<bool> read)
        {
            set.Watch(source, propertyName, () => view.Visibility = read() ? ViewStates.Visible : ViewStates.Gone);
        }

        /// <summary>Enables <paramref name="view"/> off a bool property.</summary>
        public static void BindEnabled (this BindingSet set, View view, INotifyPropertyChanged source, string propertyName, Func<bool> read)
        {
            set.Watch(source, propertyName, () => view.Enabled = read());
        }

        /// <summary>A <see cref="ColorEditIngressControl"/> edits a color property, live as the picker moves, and shows it when it changes elsewhere.</summary>
        public static BindingSet.TwoWayChannel BindEditColor (this BindingSet set, ColorEditIngressControl control, INotifyPropertyChanged source, string propertyName, Func<ColorArgb> read, Action<ColorArgb> write)
        {
            var link = new EditColorLink(control);
            set.Add(link);
            BindingSet.TwoWayChannel channel = set.TwoWay<ColorArgb>(
                source, propertyName,
                read, write,
                () => control.EditColor, value => control.EditColor = value
            );

            link.Channel = channel;
            return channel;
        }

        /// <summary>A <see cref="ToggleStrip"/>'s selected item edits a property, and follows it when it changes elsewhere.</summary>
        public static BindingSet.TwoWayChannel BindSelectedItem (this BindingSet set, ToggleStrip strip, INotifyPropertyChanged source, string propertyName, Func<object?> read, Action<object?> write)
        {
            var link = new ToggleStripSelectionLink(strip);
            set.Add(link);
            BindingSet.TwoWayChannel channel = set.TwoWay<object?>(
                source, propertyName,
                read, write,
                () => strip.SelectedItem, value => strip.SelectedItem = value
            );

            link.Channel = channel;
            return channel;
        }

        /// <summary>An <see cref="EnumToggleStrip"/>'s selected value edits an enum property (the combined value, for a multi-select [Flags] strip).</summary>
        public static BindingSet.TwoWayChannel BindSelectedValue<TEnum> (this BindingSet set, EnumToggleStrip strip, INotifyPropertyChanged source, string propertyName, Func<TEnum> read, Action<TEnum> write)
            where TEnum : struct, Enum
        {
            var link = new SelectedItemLink(strip);
            set.Add(link);
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                read, write,
                () => strip.SelectedItem, value => strip.SelectedItem = value,
                new BindingConversion<TEnum, object?>(_Box, _TryUnbox)
            );

            link.Channel = channel;
            return channel;
        }

        /// <summary>An <see cref="EnumComboBox"/>'s selected value edits an enum property.</summary>
        public static BindingSet.TwoWayChannel BindSelectedValue<TEnum> (this BindingSet set, EnumComboBox comboBox, INotifyPropertyChanged source, string propertyName, Func<TEnum> read, Action<TEnum> write)
            where TEnum : struct, Enum
        {
            var link = new SelectedItemLink(comboBox);
            set.Add(link);
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                read, write,
                () => comboBox.SelectedItem, value => comboBox.SelectedItem = value,
                new BindingConversion<TEnum, object?>(_Box, _TryUnbox)
            );

            link.Channel = channel;
            return channel;
        }

        /// <summary>
        /// A <see cref="NumericEditor"/> edits a numeric property of type <typeparamref name="T"/>, and shows it when it
        /// changes elsewhere. The editor works in <typeparamref name="T"/>, so an int property steps and caps as an int.
        /// </summary>
        public static BindingSet.TwoWayChannel BindValue<T> (this BindingSet set, NumericEditor editor, INotifyPropertyChanged source, string propertyName, Func<T> read, Action<T> write)
            where T : struct, IConvertible
        {
            var link = new NumericValueLink(editor);
            set.Add(link);
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName,
                read, write,
                () => editor.Value, value => editor.Value = value!,
                new BindingConversion<T, object?>(_BoxNumber, _TryConvertNumber)
            );

            link.Channel = channel;
            return channel;
        }

        // ===========[ Helper Methods ]===================================
        private static void _HookProgressChanged (SeekBar seekBar, EventHandler<SeekBar.ProgressChangedEventArgs> handler) => seekBar.ProgressChanged += handler;
        private static void _UnhookProgressChanged (SeekBar seekBar, EventHandler<SeekBar.ProgressChangedEventArgs> handler) => seekBar.ProgressChanged -= handler;
        private static int _ReadProgress (SeekBar seekBar) => seekBar.Progress;
        private static void _WriteProgress (SeekBar seekBar, int value) => seekBar.Progress = value;

        private static void _HookItemSelected (Spinner spinner, EventHandler<AdapterView.ItemSelectedEventArgs> handler) => spinner.ItemSelected += handler;
        private static void _UnhookItemSelected (Spinner spinner, EventHandler<AdapterView.ItemSelectedEventArgs> handler) => spinner.ItemSelected -= handler;
        private static int _ReadSelectedPosition (Spinner spinner) => spinner.SelectedItemPosition;

        private static void _WriteSelectedPosition (Spinner spinner, int value)
        {
            if ((value >= 0) && (value < (spinner.Adapter?.Count ?? 0)))
            {
                spinner.SetSelection(value, false);
            }
        }

        private static object? _BoxNumber<T> (T value) where T : struct, IConvertible => value;

        private static bool _TryConvertNumber<T> (object? target, out T value) where T : struct, IConvertible
        {
            if (target is IConvertible convertible)
            {
                value = (T)convertible.ToType(typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            value = default;
            return false;
        }

        private static object? _Box<TEnum> (TEnum value) where TEnum : struct, Enum => value;

        // A view showing nothing (null) or a value of another type writes nothing back.
        private static bool _TryUnbox<TEnum> (object? target, out TEnum value) where TEnum : struct, Enum
        {
            if (target is TEnum typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        // Only write when different, so a push that echoes the user's own edit does not reset the view.
        private static void _PushLabel (TextView label, string? value)
        {
            value ??= string.Empty;
            if (label.Text != value)
            {
                label.Text = value;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        // Owns the one view event subscription for a BindView: the hook and unhook it is given add and remove its own
        // handler on the view's edit event, so the += and its -= are both here.
        private sealed class ViewEditLink<TView, TArgs> : IDisposable
            where TView : View
        {
            private readonly TView m_view;
            private readonly Action<TView, EventHandler<TArgs>> m_unhookEdited;
            private readonly BindingSet.TwoWayChannel m_channel;

            public ViewEditLink (TView view, Action<TView, EventHandler<TArgs>> hookEdited, Action<TView, EventHandler<TArgs>> unhookEdited, BindingSet.TwoWayChannel channel)
            {
                m_view = view;
                m_unhookEdited = unhookEdited;
                m_channel = channel;
                hookEdited(m_view, this.OnEdited);
            }

            public void Dispose () => m_unhookEdited(m_view, this.OnEdited);

            private void OnEdited (object? sender, TArgs e) => m_channel.OnTargetEdited();
        }

        private sealed class ClickLink : IDisposable
        {
            private readonly View m_view;
            private readonly Action m_action;

            public ClickLink (View view, Action action)
            {
                m_view = view;
                m_action = action;
                m_view.Click += this.OnClick;
            }

            public void Dispose () => m_view.Click -= this.OnClick;

            private void OnClick (object? sender, EventArgs e) => m_action();
        }

        private sealed class CommandClickLink : IDisposable
        {
            private readonly View m_view;
            private readonly BindingSet.CommandChannel m_channel;

            public CommandClickLink (View view, BindingSet.CommandChannel channel)
            {
                m_view = view;
                m_channel = channel;
                m_view.Click += this.OnClick;
            }

            public void Dispose () => m_view.Click -= this.OnClick;

            private void OnClick (object? sender, EventArgs e) => m_channel.OnActivated();
        }

        // Subscribes before the channel exists, so the opening push's own TextChanged finds no channel and goes
        // nowhere; after that the channel's guard ignores the TextChanged every push raises.
        private sealed class TextEditLink : IDisposable
        {
            private readonly EditText m_field;

            public TextEditLink (EditText field)
            {
                m_field = field;
                m_field.TextChanged += this.OnTextChanged;
            }

            public BindingSet.TwoWayChannel? Channel { get; set; }

            public void Dispose () => m_field.TextChanged -= this.OnTextChanged;

            /// <summary>
            /// Shows a value from the model. Setting the text puts the caret at the start, so it goes back where it was,
            /// clamped to the new length. The set only calls this when the text actually differs, so a push that echoes
            /// the user's own keystroke never gets here and never moves the caret.
            /// </summary>
            public void Push (string value)
            {
                int caret = Math.Min(Math.Max(m_field.SelectionEnd, 0), value.Length);
                m_field.Text = value;
                m_field.SetSelection(caret);
            }

            private void OnTextChanged (object? sender, TextChangedEventArgs e) => this.Channel?.OnTargetEdited();
        }

        private sealed class CheckedLink : IDisposable
        {
            private readonly CompoundButton m_check;

            public CheckedLink (CompoundButton check)
            {
                m_check = check;
                m_check.CheckedChange += this.OnCheckedChange;
            }

            public BindingSet.TwoWayChannel? Channel { get; set; }

            public void Dispose () => m_check.CheckedChange -= this.OnCheckedChange;

            private void OnCheckedChange (object? sender, CompoundButton.CheckedChangeEventArgs e) => this.Channel?.OnTargetEdited();
        }

        private sealed class ToggleStripSelectionLink : IDisposable
        {
            private readonly ToggleStrip m_strip;

            public ToggleStripSelectionLink (ToggleStrip strip)
            {
                m_strip = strip;
                m_strip.SelectionChanged += this.OnSelectionChanged;
            }

            public BindingSet.TwoWayChannel? Channel { get; set; }

            public void Dispose () => m_strip.SelectionChanged -= this.OnSelectionChanged;

            private void OnSelectionChanged (object? sender, ToggleStripSelectionChange e) => this.Channel?.OnTargetEdited();
        }

        // The enum pickers both raise SelectedItemChanged; this listens to whichever one it is given.
        private sealed class SelectedItemLink : IDisposable
        {
            private readonly EnumToggleStrip? m_strip;
            private readonly EnumComboBox? m_comboBox;

            public SelectedItemLink (EnumToggleStrip strip)
            {
                m_strip = strip;
                m_strip.SelectedItemChanged += this.OnSelectedItemChanged;
            }

            public SelectedItemLink (EnumComboBox comboBox)
            {
                m_comboBox = comboBox;
                m_comboBox.SelectedItemChanged += this.OnSelectedItemChanged;
            }

            public BindingSet.TwoWayChannel? Channel { get; set; }

            public void Dispose ()
            {
                if (m_strip != null)
                {
                    m_strip.SelectedItemChanged -= this.OnSelectedItemChanged;
                }

                if (m_comboBox != null)
                {
                    m_comboBox.SelectedItemChanged -= this.OnSelectedItemChanged;
                }
            }

            private void OnSelectedItemChanged (object? sender, EventArgs<object?> e) => this.Channel?.OnTargetEdited();
        }

        private sealed class NumericValueLink : IDisposable
        {
            private readonly NumericEditor m_editor;

            public NumericValueLink (NumericEditor editor)
            {
                m_editor = editor;
                m_editor.ValueChanged += this.OnValueChanged;
            }

            public BindingSet.TwoWayChannel? Channel { get; set; }

            public void Dispose () => m_editor.ValueChanged -= this.OnValueChanged;

            private void OnValueChanged (object? sender, EventArgs<object> e) => this.Channel?.OnTargetEdited();
        }

        private sealed class EditColorLink : IDisposable
        {
            private readonly ColorEditIngressControl m_control;

            public EditColorLink (ColorEditIngressControl control)
            {
                m_control = control;
                m_control.EditColorChanged += this.OnEditColorChanged;
            }

            public BindingSet.TwoWayChannel? Channel { get; set; }

            public void Dispose () => m_control.EditColorChanged -= this.OnEditColorChanged;

            private void OnEditColorChanged (object? sender, EventArgs<ColorArgb> e) => this.Channel?.OnTargetEdited();
        }
    }
}
