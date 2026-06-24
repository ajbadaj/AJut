namespace AJut.UX.Controls
{
    using System.Linq;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;

    /// <summary>
    /// Shared plumbing for the brush driven buttons. The standard WinUI Button drives its background,
    /// foreground, and border brushes through the {ThemeResource ButtonBackground...} keys baked into its
    /// template's VisualStateManager, which is why recoloring a single button forces that awkward per
    /// instance ResourceDictionary override (and rules out binding). This base sidesteps it: it stays a
    /// plain Button - so ButtonBase still owns the Normal/PointerOver/Pressed/Disabled state machine - but
    /// instead of theme resource VSM setters it listens for the state change and pushes the resolved
    /// brushes straight onto the template parts. Subclasses only have to answer "which brushes for this
    /// state".
    ///
    /// Not meant to be used directly - use BrushButton or AutoBrushButton.
    /// </summary>
    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_ContentPresenter), Type = typeof(ContentPresenter))]
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "PointerOver", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Pressed", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    public abstract class _ManagedBrushButton : Button
    {
        // ===========[ Constants ]===================================
        private const string kCommonStatesGroup = "CommonStates";
        private const string kStateNormal = "Normal";
        private const string kStatePointerOver = "PointerOver";
        private const string kStatePressed = "Pressed";
        private const string kStateDisabled = "Disabled";

        // ===========[ Instance Fields ]===================================
        private VisualStateGroup m_commonStates;
        private eManagedButtonState m_activeState = eManagedButtonState.Normal;

        // ===========[ Setup/Construction/Teardown ]===================================
        protected _ManagedBrushButton ()
        {
        }

        // ===========[ Properties ]===================================
        protected Border PART_Root { get; private set; }
        protected ContentPresenter PART_ContentPresenter { get; private set; }

        // ===========[ Public Interface Methods ]===================================
        protected override void OnApplyTemplate ()
        {
            // ButtonBase wires up the CommonStates machine in its own OnApplyTemplate, so let it run first.
            base.OnApplyTemplate();

            if (m_commonStates != null)
            {
                m_commonStates.CurrentStateChanged -= this.OnCommonStateChanged;
            }

            this.PART_Root = this.GetTemplateChild(nameof(PART_Root)) as Border;
            this.PART_ContentPresenter = this.GetTemplateChild(nameof(PART_ContentPresenter)) as ContentPresenter;

            m_commonStates = this.PART_Root == null
                ? null
                : VisualStateManager.GetVisualStateGroups(this.PART_Root).FirstOrDefault(group => group.Name == kCommonStatesGroup);

            if (m_commonStates != null)
            {
                m_commonStates.CurrentStateChanged -= this.OnCommonStateChanged;
                m_commonStates.CurrentStateChanged += this.OnCommonStateChanged;
            }

            // base.OnApplyTemplate already drove the opening GoToState, so seed from wherever it landed.
            m_activeState = _ParseState(m_commonStates?.CurrentState?.Name)
                ?? (this.IsEnabled ? eManagedButtonState.Normal : eManagedButtonState.Disabled);
            this.RefreshActiveStateBrushes();
        }

        /// <summary>Re-resolves and re-applies the brushes for the current visual state. Call when a brush source changes.</summary>
        protected void RefreshActiveStateBrushes ()
        {
            if (this.PART_Root == null)
            {
                return;
            }

            SurfaceBrushes brushes = this.GetBrushesForState(m_activeState);
            this.PART_Root.Background = brushes.Background;
            this.PART_Root.BorderBrush = brushes.Border;
            if (this.PART_ContentPresenter != null)
            {
                this.PART_ContentPresenter.Foreground = brushes.Foreground;
            }
        }

        /// <summary>Subclasses answer which brushes to paint for a given interaction state. Any may be null (no paint).</summary>
        protected abstract SurfaceBrushes GetBrushesForState (eManagedButtonState state);

        // ===========[ Events ]===================================
        private void OnCommonStateChanged (object sender, VisualStateChangedEventArgs e)
        {
            m_activeState = _ParseState(e.NewState?.Name) ?? eManagedButtonState.Normal;
            this.RefreshActiveStateBrushes();
        }

        // ===========[ Helper Methods ]===================================
        private static eManagedButtonState? _ParseState (string stateName)
        {
            return stateName switch
            {
                kStateNormal => eManagedButtonState.Normal,
                kStatePointerOver => eManagedButtonState.PointerOver,
                kStatePressed => eManagedButtonState.Pressed,
                kStateDisabled => eManagedButtonState.Disabled,
                _ => null,
            };
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The resolved brushes to paint for one interaction state. Any may be null (no paint).</summary>
        protected readonly record struct SurfaceBrushes (Brush Background, Brush Foreground, Brush Border);
    }

    public enum eManagedButtonState
    {
        Normal,
        PointerOver,
        Pressed,
        Disabled,
    }
}
