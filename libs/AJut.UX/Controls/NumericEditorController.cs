namespace AJut.UX.Controls
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// What a toolkit's NumericEditor gives <see cref="NumericEditorController"/>: its settings, plus the three things
    /// the controller writes back to it. Each toolkit implements it once, on its NumericEditor.
    /// </summary>
    public interface INumericEditorHost : INumericEditorSettings
    {
        /// <summary>
        /// Puts <paramref name="text"/> in the editor's text field. The controller ignores the text changed edit this
        /// causes (Android raises one for a programmatic set), so the host can route its text changed event straight
        /// to <see cref="NumericEditorController.OnTextEdited"/>. Leave the caret where it was, clamped to the new
        /// length, or a field that is being typed in jumps its caret on every reformat.
        /// </summary>
        void ShowText (string? text);

        /// <summary>
        /// Writes the editor's bound Value. The controller ignores the Value change this causes, so the host can route
        /// its Value change straight to <see cref="NumericEditorController.OnValueChanged"/>.
        /// </summary>
        void ShowValue (object value);

        /// <summary>Shows the out of bounds error message, or clears it when <paramref name="errorMessage"/> is null.</summary>
        void ShowError (string? errorMessage);
    }

    /// <summary>
    /// The part of a NumericEditor that is the same on every toolkit: it owns the <see cref="NumericEditorViewModel"/>,
    /// turns the host's events (a Value change, a text edit, a commit, a nudge) into view model calls, and writes the
    /// results back through <see cref="INumericEditorHost"/> under a re-entrancy guard, so the host's own change events
    /// for those writes do not loop back in. The host keeps only what is its toolkit's: the views, focus visuals, the
    /// label, reading modifier keys, and the Value property itself.
    /// </summary>
    public sealed class NumericEditorController
    {
        private readonly INumericEditorHost m_host;
        private NumericEditorViewModel? m_viewModel;
        private bool m_isPushing;

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        private Type? m_enforcedType;

        // ===========[ Setup/Construction/Teardown ]===================================
        public NumericEditorController (INumericEditorHost host)
        {
            m_host = host;
        }

        // ===========[ Properties ]===================================
        /// <summary>What the text field shows. Null until a Value or an edit has arrived.</summary>
        public string? Text => m_viewModel?.Text;

        /// <summary>The value, capped to the bounds and kept in its detected type. Null until a Value or an edit has arrived.</summary>
        public object? Value => m_viewModel?.SourceValue;

        /// <summary>True while the controller is writing to the host; edits and Value changes that arrive meanwhile are its own echoes.</summary>
        public bool IsPushing => m_isPushing;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Maps a Minimum in the host's double form to the form <see cref="INumericEditorSettings.Minimum"/> takes:
        /// negative infinity (the host's "unconstrained" default) is null, and anything else is the value boxed.
        /// </summary>
        public static object? ToMinimumBound (double minimum) => double.IsNegativeInfinity(minimum) ? null : (object)minimum;

        /// <summary>
        /// Maps a Maximum in the host's double form to the form <see cref="INumericEditorSettings.Maximum"/> takes:
        /// positive infinity (the host's "unconstrained" default) is null, and anything else is the value boxed.
        /// </summary>
        public static object? ToMaximumBound (double maximum) => double.IsPositiveInfinity(maximum) ? null : (object)maximum;

        /// <summary>
        /// Picks the step for a nudge from the modifiers the host read: the big step when its big modifier is held
        /// (Ctrl on Windows), else the small step when its small modifier is held (Shift), else the normal step. Big
        /// wins when both are held. A toolkit with no modifier keys passes false for both.
        /// </summary>
        public static double PickNudgeAmount (bool isBigModifier, bool isSmallModifier, double nudge, double bigNudge, double smallNudge)
        {
            if (isBigModifier)
            {
                return bigNudge;
            }

            return isSmallModifier ? smallNudge : nudge;
        }

        /// <summary>
        /// Call from the host's Value change. Rebuilds the view model so the type is detected again from the new value
        /// (a null value is treated as 0.0), or kept in the enforced type when there is one, then shows its text and
        /// error state. A value that changes type on the way in is written back as the number it became: text (a XAML
        /// literal such as Value="42") as the number it parsed to, and a number of another type as the enforced type.
        /// Ignored while the controller is writing the Value itself.
        /// </summary>
        public void OnValueChanged (object? newValue)
        {
            if (m_isPushing)
            {
                return;
            }

            m_viewModel = new NumericEditorViewModel(m_host, newValue ?? 0.0, m_enforcedType);
            bool changedType = (newValue != null) && (newValue.GetType() != m_viewModel.SourceValue.GetType());
            this.Push(m_viewModel, showText: true, showValue: changedType);
            this.RefreshErrorState();
        }

        /// <summary>
        /// Call from the host's enforced type change (WPF and WinUI3 name it EnforceNumericType). The value is then kept in
        /// <paramref name="enforcedType"/> however it arrives: text, or a number of another type. Null goes back to
        /// detecting the type from each value. A value that already arrived is converted to the new type and written
        /// back, so it doesn't matter whether the type or the value is set first.
        /// </summary>
        public void OnEnforcedTypeChanged ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? enforcedType)
        {
            m_enforcedType = enforcedType;
            if (m_viewModel == null)
            {
                return;
            }

            object previousValue = m_viewModel.SourceValue;
            m_viewModel = new NumericEditorViewModel(m_host, previousValue, m_enforcedType);
            this.Push(m_viewModel, showText: true, showValue: !Equals(previousValue, m_viewModel.SourceValue));
            this.RefreshErrorState();
        }

        /// <summary>
        /// Call from the host's text field change. Routes the text through the view model (parse, cap, keep the type)
        /// and writes the resulting value back. An edit that arrives before any Value starts from 0.0. Ignored while
        /// the controller is writing the text itself.
        /// </summary>
        public void OnTextEdited (string? text)
        {
            if (m_isPushing)
            {
                return;
            }

            m_viewModel ??= new NumericEditorViewModel(m_host, 0.0, m_enforcedType);
            m_viewModel.Text = text ?? string.Empty;
            this.Push(m_viewModel, showText: false, showValue: true);
            this.RefreshErrorState();
        }

        /// <summary>
        /// Call when an edit ends: Enter, or focus leaving the field. Under
        /// <see cref="eOutOfBoundsResponse.FixOnCommit"/> the text snaps to the clamped value (5500 against a max of
        /// 10 becomes 10); under <see cref="eOutOfBoundsResponse.ErrorAndToolTip"/> the out of bounds text stays,
        /// still flagged. Calling it twice in a row (an Enter, then the focus loss that follows) is harmless: the
        /// second call writes the same text and value again.
        /// </summary>
        public void Commit ()
        {
            if (m_viewModel == null)
            {
                return;
            }

            m_viewModel.CommitEdit();
            this.Push(m_viewModel, showText: true, showValue: true);
            this.RefreshErrorState();
        }

        /// <summary>
        /// Steps the value up (<paramref name="positive"/>) or down by <paramref name="amount"/>, capped to the
        /// bounds, and shows the result. The host picks the amount (see <see cref="PickNudgeAmount"/>). A nudge that
        /// arrives before any Value starts from 0.0.
        /// </summary>
        public void Nudge (bool positive, double amount)
        {
            m_viewModel ??= new NumericEditorViewModel(m_host, 0.0, m_enforcedType);
            m_viewModel.Nudge(positive, amount);
            this.Push(m_viewModel, showText: true, showValue: true);
            this.RefreshErrorState();
        }

        /// <summary>
        /// Call once the host's view is built or rebuilt (a WinUI3 template applied after the Value was already set):
        /// shows the current text and error state again. Does nothing to the text before any Value or edit.
        /// </summary>
        public void RefreshDisplay ()
        {
            if (m_viewModel != null)
            {
                this.Push(m_viewModel, showText: true, showValue: false);
            }

            this.RefreshErrorState();
        }

        /// <summary>
        /// Shows the error when the text is out of bounds and the response is
        /// <see cref="eOutOfBoundsResponse.ErrorAndToolTip"/>, and clears it otherwise. Call when the out of bounds
        /// response changes; the other calls here already run it.
        /// </summary>
        public void RefreshErrorState ()
        {
            bool showError = (m_viewModel != null) && m_viewModel.ShouldShowError;
            m_host.ShowError(showError ? m_viewModel!.TextErrorMessage : null);
        }

        // ===========[ Helper Methods ]===================================
        private void Push (NumericEditorViewModel viewModel, bool showText, bool showValue)
        {
            m_isPushing = true;
            try
            {
                if (showText)
                {
                    m_host.ShowText(viewModel.Text);
                }

                if (showValue)
                {
                    m_host.ShowValue(viewModel.SourceValue);
                }
            }
            finally
            {
                m_isPushing = false;
            }
        }
    }
}
