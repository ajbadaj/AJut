namespace AJut.UX.CodeBinding
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows.Input;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using UIKit;

    /// <summary>
    /// The UIKit side of <see cref="BindingSet"/>: binds UIKit views to plain C# models. Every UIKit control reports a
    /// user edit through a UIControl event (ValueChanged, EditingChanged, PrimaryActionTriggered), and none of them
    /// raises one for a value set from code, so a single generic adapter, <see cref="BindControl{TControl, T}"/>,
    /// covers the stock controls and every AJut UIKit control alike (each sends ValueChanged on a user edit). The rest
    /// are that adapter with the event and the property already picked.
    /// </summary>
    public static class BindingSetXT
    {
        // ===========[ Two way ]===================================
        /// <summary>
        /// Binds a control property both ways: the control shows the source now and on every change, and
        /// <paramref name="editEvent"/> (the control's "the user changed it" event) writes the user's edit back.
        /// </summary>
        public static BindingSet.TwoWayChannel BindControl<TControl, T> (
            this BindingSet set, TControl control, UIControlEvent editEvent,
            Func<TControl, T> readTarget, Action<TControl, T> writeTarget,
            INotifyPropertyChanged source, string propertyName, Func<T> read, Action<T> write,
            IEqualityComparer<T> targetComparer = null)
            where TControl : UIControl
        {
            // Saved view state never overwrites the model here, unlike Android, where a view with an id restores its
            // own value after the binding's first push and that restore arrives as a user edit. On UIKit:
            //  - State preservation is opt in (a restorationIdentifier on the view and the app agreeing to save), and
            //    the only stock views that take part are the scroll, table, collection, text and image views, which
            //    save scroll position, text selection and image framing, never a control's value. Scene restoration hands the app an NSUserActivity and restores
            //    nothing on its own.
            //  - Anything a restore does set is set from code, and a UIControl never sends its edit events
            //    (ValueChanged, EditingChanged) for a change made from code, so it cannot reach the edit link below.
            // So the bind leaves restorationIdentifier alone (a consumer may rely on it for scroll position), and the
            // model stays the only owner of the value. A custom control that wants to restore its own value has to
            // set it without sending ValueChanged, which every AJut UIKit control already does.
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName, read, write,
                () => readTarget(control), value => writeTarget(control, value),
                targetComparer
            );

            set.Add(new ControlEditLink(control, editEvent, channel));
            return channel;
        }

        /// <summary>Binds a control property both ways through a conversion, for a control that shows a different type than the source holds.</summary>
        public static BindingSet.TwoWayChannel BindControl<TControl, TSource, TTarget> (
            this BindingSet set, TControl control, UIControlEvent editEvent,
            Func<TControl, TTarget> readTarget, Action<TControl, TTarget> writeTarget,
            INotifyPropertyChanged source, string propertyName, Func<TSource> read, Action<TSource> write,
            BindingConversion<TSource, TTarget> conversion)
            where TControl : UIControl
        {
            BindingSet.TwoWayChannel channel = set.TwoWay(
                source, propertyName, read, write,
                () => readTarget(control), value => writeTarget(control, value),
                conversion
            );

            set.Add(new ControlEditLink(control, editEvent, channel));
            return channel;
        }

        /// <summary>A text field edits a string property as the user types, and shows it when it changes elsewhere, keeping the caret where the user had it.</summary>
        public static BindingSet.TwoWayChannel BindText (this BindingSet set, UITextField field, INotifyPropertyChanged source, string propertyName, Func<string> read, Action<string> write)
        {
            return set.BindControl(field, UIControlEvent.EditingChanged, ReadText, WriteTextKeepingCaret, source, propertyName, read, write);
        }

        /// <summary>A switch edits a bool property.</summary>
        public static BindingSet.TwoWayChannel BindOn (this BindingSet set, UISwitch toggle, INotifyPropertyChanged source, string propertyName, Func<bool> read, Action<bool> write)
        {
            return set.BindControl(toggle, UIControlEvent.ValueChanged, ReadOn, WriteOn, source, propertyName, read, write);
        }

        /// <summary>A slider edits a float property, continuously while dragged unless the slider's Continuous is off.</summary>
        public static BindingSet.TwoWayChannel BindValue (this BindingSet set, UISlider slider, INotifyPropertyChanged source, string propertyName, Func<float> read, Action<float> write)
        {
            return set.BindControl(slider, UIControlEvent.ValueChanged, ReadSliderValue, WriteSliderValue, source, propertyName, read, write);
        }

        /// <summary>A stepper edits a double property.</summary>
        public static BindingSet.TwoWayChannel BindValue (this BindingSet set, UIStepper stepper, INotifyPropertyChanged source, string propertyName, Func<double> read, Action<double> write)
        {
            return set.BindControl(stepper, UIControlEvent.ValueChanged, ReadStepperValue, WriteStepperValue, source, propertyName, read, write);
        }

        /// <summary>A segmented control edits an index property; -1 is no selection.</summary>
        public static BindingSet.TwoWayChannel BindSelectedSegment (this BindingSet set, UISegmentedControl segments, INotifyPropertyChanged source, string propertyName, Func<int> read, Action<int> write)
        {
            return set.BindControl(segments, UIControlEvent.ValueChanged, ReadSelectedSegment, WriteSelectedSegment, source, propertyName, read, write);
        }

        /// <summary>
        /// A NumericEditor edits a numeric property of type <typeparamref name="T"/>. The editor keeps the type of the
        /// value it is handed, so the edits it hands back are already a <typeparamref name="T"/>.
        /// </summary>
        public static BindingSet.TwoWayChannel BindValue<T> (this BindingSet set, NumericEditor editor, INotifyPropertyChanged source, string propertyName, Func<T> read, Action<T> write)
        {
            var conversion = new BindingConversion<T, object>(_Box, _Unbox);
            return set.BindControl(editor, UIControlEvent.ValueChanged, ReadEditorValue, WriteEditorValue, source, propertyName, read, write, conversion);

            static object _Box (T value) => value;
            static bool _Unbox (object target, out T value)
            {
                if (target is T typed)
                {
                    value = typed;
                    return true;
                }

                value = default;
                return false;
            }
        }

        /// <summary>A ColorEditIngressControl edits a color property, live while the picker is open.</summary>
        public static BindingSet.TwoWayChannel BindEditColor (this BindingSet set, ColorEditIngressControl editor, INotifyPropertyChanged source, string propertyName, Func<ColorArgb> read, Action<ColorArgb> write)
        {
            return set.BindControl(editor, UIControlEvent.ValueChanged, ReadEditColor, WriteEditColor, source, propertyName, read, write);
        }

        // ===========[ One way ]===================================
        /// <summary>A label shows a string property.</summary>
        public static void BindLabel (this BindingSet set, UILabel label, INotifyPropertyChanged source, string propertyName, Func<string> read)
        {
            set.Watch(source, propertyName, () => _PushLabel(label, read()));

            static void _PushLabel (UILabel target, string value)
            {
                value ??= string.Empty;
                if (target.Text != value)
                {
                    target.Text = value;
                }
            }
        }

        /// <summary>Hides <paramref name="view"/> while the property reads true. A hidden view inside a UIStackView also gives up its space.</summary>
        public static void BindHidden (this BindingSet set, UIView view, INotifyPropertyChanged source, string propertyName, Func<bool> read)
        {
            set.Watch(source, propertyName, () => view.Hidden = read());
        }

        /// <summary>Enables <paramref name="control"/> off a bool property.</summary>
        public static void BindEnabled (this BindingSet set, UIControl control, INotifyPropertyChanged source, string propertyName, Func<bool> read)
        {
            set.Watch(source, propertyName, () => control.Enabled = read());
        }

        // ===========[ Commands ]===================================
        /// <summary>
        /// The control's primary action (a button's tap, or the equivalent for any UIControl) executes
        /// <paramref name="command"/>, and the control's Enabled follows its CanExecute.
        /// </summary>
        public static BindingSet.CommandChannel BindCommand (this BindingSet set, UIControl control, ICommand command, object parameter = null)
        {
            BindingSet.CommandChannel channel = set.Command(command, parameter, canExecute => control.Enabled = canExecute);
            set.Add(new ControlActivatedLink(control, channel));
            return channel;
        }

        // ===========[ Helper Methods ]===================================
        private static string ReadText (UITextField field) => field.Text ?? string.Empty;

        // Setting Text puts the caret at the end, which would throw the user's caret around whenever the model
        // changes the text while they are editing. The binding core already skips a push that only echoes their own
        // keystroke, so this only runs for a real change from the model; it puts the caret back at the same offset
        // from the start, clamped to the new length.
        private static void WriteTextKeepingCaret (UITextField field, string value)
        {
            value ??= string.Empty;
            UITextRange selection = field.IsFirstResponder ? field.SelectedTextRange : null;
            nint caret = selection == null ? 0 : field.GetOffsetFromPosition(field.BeginningOfDocument, selection.Start);

            field.Text = value;

            if (selection != null)
            {
                UITextPosition position = field.GetPosition(field.BeginningOfDocument, Math.Min(caret, value.Length));
                if (position != null)
                {
                    field.SelectedTextRange = field.GetTextRange(position, position);
                }
            }
        }

        private static bool ReadOn (UISwitch toggle) => toggle.On;
        private static void WriteOn (UISwitch toggle, bool value) => toggle.SetState(value, animated: true);
        private static float ReadSliderValue (UISlider slider) => slider.Value;
        private static void WriteSliderValue (UISlider slider, float value) => slider.SetValue(value, animated: false);
        private static double ReadStepperValue (UIStepper stepper) => stepper.Value;
        private static void WriteStepperValue (UIStepper stepper, double value) => stepper.Value = value;
        private static int ReadSelectedSegment (UISegmentedControl segments) => (int)segments.SelectedSegment;
        private static void WriteSelectedSegment (UISegmentedControl segments, int value) => segments.SelectedSegment = value;
        private static object ReadEditorValue (NumericEditor editor) => editor.Value;
        private static void WriteEditorValue (NumericEditor editor, object value) => editor.Value = value;
        private static ColorArgb ReadEditColor (ColorEditIngressControl editor) => editor.EditColor;
        private static void WriteEditColor (ColorEditIngressControl editor, ColorArgb value) => editor.EditColor = value;

        // ===========[ Subclasses/structs ]===================================
        // Owns the one UIControl subscription for a two-way binding: the control's edit event tells the channel the
        // user changed the value. UIKit does not retain action targets, so the control keeps nothing alive here.
        private sealed class ControlEditLink : IDisposable
        {
            private readonly UIControl m_control;
            private readonly UIControlEvent m_editEvent;
            private readonly BindingSet.TwoWayChannel m_channel;

            public ControlEditLink (UIControl control, UIControlEvent editEvent, BindingSet.TwoWayChannel channel)
            {
                m_control = control;
                m_editEvent = editEvent;
                m_channel = channel;
                m_control.AddTarget(this.OnEdited, m_editEvent);
            }

            public void Dispose () => m_control.RemoveTarget(this.OnEdited, m_editEvent);

            private void OnEdited (object sender, EventArgs e) => m_channel.OnTargetEdited();
        }

        private sealed class ControlActivatedLink : IDisposable
        {
            private readonly UIControl m_control;
            private readonly BindingSet.CommandChannel m_channel;

            public ControlActivatedLink (UIControl control, BindingSet.CommandChannel channel)
            {
                m_control = control;
                m_channel = channel;
                m_control.AddTarget(this.OnActivated, UIControlEvent.PrimaryActionTriggered);
            }

            public void Dispose () => m_control.RemoveTarget(this.OnActivated, UIControlEvent.PrimaryActionTriggered);

            private void OnActivated (object sender, EventArgs e) => m_channel.OnActivated();
        }
    }
}
