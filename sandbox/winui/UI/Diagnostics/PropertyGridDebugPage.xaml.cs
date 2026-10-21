namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using AJut.Text.AJson;
    using AJut.UX.PropertyInteraction;
    using AJutShowRoomWinUI;
    using AJutShowRoomWinUI.UI.Samples;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// PropertyGrid demos: source swapping across several sample types, the wrapped-mode PGShowIf matrix with its
    /// diagnostics, the selection surface, and a live JSON mirror of the current source.
    /// </summary>
    public sealed partial class PropertyGridDebugPage : Page
    {
        // Source swap test objects.
        // ShowRoomAlpha (5 props) -> ShowRoomBeta (2 props) replicates an external scenario:
        //   - same float property names (X, Y) but very different values so mismatch is obvious
        //   - count difference (5 to 2) forces WinUI3 container recycling
        //   - Alpha has String+Bool rows that vanish in Beta; if they persist, the bug is present
        private readonly ShowRoomAlpha m_alphaObj = new ShowRoomAlpha();
        private readonly ShowRoomBeta m_betaObj = new ShowRoomBeta();
        private readonly ShowRoomTester m_complexObj = new ShowRoomTester();
        // Second instance of the SAME type as m_complexObj. Used to test the property grid's ephemeral
        // expansion memory: expand a sub-object / group on Complex A, switch to Complex B, and the same
        // tree pathway should stay expanded so the two objects can be compared.
        private readonly ShowRoomTester m_complexObj2 = new ShowRoomTester { Name = "Complex B - same type, compare expansion" };
        // Delegating edit manager: wraps an inner object that carries a [PGButton] and surfaces it
        // itself, since the grid only auto-harvests buttons off the source it's handed. See
        // ButtonDelegationDemo.cs.
        private readonly ButtonDelegationEditManager m_delegatedButtonsObj = new ButtonDelegationEditManager();
        // Mixed ordering: [MemberOrder] properties + a [PGMemberOrder] button - exposes that the two
        // attributes don't sort on a shared axis (see MixedOrderButtonSource).
        private readonly MixedOrderButtonSource m_mixedOrderObj = new MixedOrderButtonSource();
        // Same mix, but built by hand (manual PETs) - positions every row via MemberSortOrder.
        private readonly ManualMixedOrderManager m_manualMixedOrderObj = new ManualMixedOrderManager();
        private readonly WrappedModeMatrixSource m_wrappedMatrixObj = new WrappedModeMatrixSource();
        private object m_currentPGTestObj;

        // Used only by the matrix-mode diagnostic counters so I can see whether
        // the chain wrapper -> OnElevatedSubObjectPropertyChanged -> SourceCommitted ->
        // PropertyGrid.OnAnyTargetPropertyChanged -> UpdateConditionalVisibility actually fires.
        private int m_matrixWrapperInpcCount;
        private int m_matrixTreeChangedCount;
        private bool m_matrixDiagnosticsAttached;

        // ===========[ Setup/Construction/Teardown ]=====================================

        public PropertyGridDebugPage ()
        {
            this.InitializeComponent();
            this.TestPropertyGrid.PropertyTreeChanged += this.TestPropertyGrid_OnPropertyTreeChanged;
            this.TestPropertyGrid.SelectedSourceObjectChanged += this.TestPropertyGrid_OnSelectedSourceObjectChanged;
            this.SetPGSource(m_alphaObj);
        }

        // The page is rebuilt by every navigation to it, so everything the constructor and the matrix
        // diagnostics hooked up comes off here, and the grid is disposed so it severs its own source.
        private void Page_OnUnloaded (object sender, RoutedEventArgs e)
        {
            this.TestPropertyGrid.PropertyTreeChanged -= this.TestPropertyGrid_OnPropertyTreeChanged;
            this.TestPropertyGrid.SelectedSourceObjectChanged -= this.TestPropertyGrid_OnSelectedSourceObjectChanged;
            this.DetachMatrixDiagnostics();
            this.TestPropertyGrid.Dispose();
        }

        // ===========[ Properties ]======================================================

        /// <summary>The grid markup with its template selector, shown beside the demo.</summary>
        public string DemoCode { get; } = @"<ajut:PropertyGrid x:Name=""TestPropertyGrid""
                   RowSpacing=""5"" FixedRowHeight=""40"" LabelColumnWidth=""180""
                   PropertyToolTipPlacement=""PropertyNameAndValue"">
    <ajut:PropertyGrid.ItemTemplateSelector>
        <ajut:PropertyGridTemplateSelector>
            <ajut:PropertyGridTemplateSelector.Default>
                <DataTemplate>
                    <TextBlock Text=""{Binding Path=EditValue}"" VerticalAlignment=""Center""/>
                </DataTemplate>
            </ajut:PropertyGridTemplateSelector.Default>
            <ajut:PropertyGridTemplateSelector.RegisteredTemplates>
                <DataTemplate x:Key=""Single"" x:DataType=""ajut_pg:PropertyEditTarget"">
                    <ajut:NumericEditor Value=""{Binding Path=EditValue, Mode=TwoWay}"" TextBoxHeight=""20""
                                        Minimum=""{Binding Path=EditContext.Min, Mode=TwoWay, FallbackValue=-10000}""
                                        Maximum=""{Binding Path=EditContext.Max, Mode=TwoWay, FallbackValue=10000}""/>
                </DataTemplate>
                <DataTemplate x:Key=""Boolean"">
                    <CheckBox IsChecked=""{Binding Path=EditValue, Mode=TwoWay}"" VerticalAlignment=""Center""/>
                </DataTemplate>
                <DataTemplate x:Key=""String"">
                    <TextBox Text=""{Binding Path=EditValue, Mode=TwoWay}""/>
                </DataTemplate>
            </ajut:PropertyGridTemplateSelector.RegisteredTemplates>
        </ajut:PropertyGridTemplateSelector>
    </ajut:PropertyGrid.ItemTemplateSelector>
</ajut:PropertyGrid>

// Code-behind
this.TestPropertyGrid.SingleItemSource = new ShowRoomTester();";

        // ===========[ Source switching ]================================================

        private void SetPGSource (object obj)
        {
            m_currentPGTestObj = obj;
            this.TestPropertyGrid.SingleItemSource = obj;
            this.ResetJsonDisplay();

            // Swapping the source rebuilds the tree and drops any selection - reset the readout to match.
            this.PGSelectionReadout.Text = "(none)";
        }

        private void PGSource_OnAlphaClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_alphaObj);
        private void PGSource_OnBetaClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_betaObj);
        private void PGSource_OnComplexClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_complexObj);
        private void PGSource_OnComplexBClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_complexObj2);
        private void PGSource_OnDelegatedButtonsClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_delegatedButtonsObj);
        private void PGSource_OnMixedOrderClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_mixedOrderObj);
        private void PGSource_OnManualMixedOrderClicked (object sender, RoutedEventArgs e) => this.SetPGSource(m_manualMixedOrderObj);
        private void PGSource_OnWrappedMatrixClicked (object sender, RoutedEventArgs e)
        {
            this.SetPGSource(m_wrappedMatrixObj);
            this.AttachMatrixDiagnostics();
        }

        // Directly poke the wrapper's Value - simulates the consumer scenario where an
        // enum-behind-a-wrapper changes without the outer object's INPC firing, and
        // several PGShowIf-gated rows have to re-evaluate in response.
        private void PGMatrix_SetModeA (object sender, RoutedEventArgs e) => this.CycleMatrixMode(eMatrixMode.A);
        private void PGMatrix_SetModeB (object sender, RoutedEventArgs e) => this.CycleMatrixMode(eMatrixMode.B);
        private void PGMatrix_SetModeC (object sender, RoutedEventArgs e) => this.CycleMatrixMode(eMatrixMode.C);

        private void AttachMatrixDiagnostics ()
        {
            if (m_matrixDiagnosticsAttached)
            {
                return;
            }

            m_matrixDiagnosticsAttached = true;
            m_wrappedMatrixObj.Mode.PropertyChanged -= this.MatrixWrapper_OnPropertyChanged;
            m_wrappedMatrixObj.Mode.PropertyChanged += this.MatrixWrapper_OnPropertyChanged;
            // PropertyTreeChanged fires from PropertyGrid.OnAnyTargetPropertyChanged on any
            // SourceCommitted. If clicks increment the wrapper counter but not this one,
            // the wrapper INPC is reaching the wrapper but the elevated-subobject-to-
            // SourceCommitted bridge isn't firing or isn't being heard.
            this.TestPropertyGrid.PropertyTreeChanged -= this.MatrixDiag_OnPropertyTreeChanged;
            this.TestPropertyGrid.PropertyTreeChanged += this.MatrixDiag_OnPropertyTreeChanged;
        }

        private void DetachMatrixDiagnostics ()
        {
            if (!m_matrixDiagnosticsAttached)
            {
                return;
            }

            m_matrixDiagnosticsAttached = false;
            m_wrappedMatrixObj.Mode.PropertyChanged -= this.MatrixWrapper_OnPropertyChanged;
            this.TestPropertyGrid.PropertyTreeChanged -= this.MatrixDiag_OnPropertyTreeChanged;
        }

        private void MatrixWrapper_OnPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            ++m_matrixWrapperInpcCount;
            this.UpdateMatrixDiagnosticLabel();
        }

        private void MatrixDiag_OnPropertyTreeChanged (object sender, EventArgs e)
        {
            ++m_matrixTreeChangedCount;
            this.UpdateMatrixDiagnosticLabel();
        }

        private void CycleMatrixMode (eMatrixMode mode)
        {
            m_wrappedMatrixObj.Mode.Value = mode;
            this.UpdateMatrixDiagnosticLabel();
        }

        private void UpdateMatrixDiagnosticLabel ()
        {
            if (this.PGMatrixDiagnostic == null)
            {
                return;
            }

            string liveChildren = "(null)";
            var root = this.TestPropertyGrid?.Manager?.RootNode;
            if (root != null)
            {
                liveChildren = string.Join(", ",
                    root.Children.OfType<PropertyEditTarget>().Select(t => t.PropertyPathTarget));
            }

            this.PGMatrixDiagnostic.Text = string.Format(
                "Mode.Value={0}  INPC={1}  TreeChanged={2}  root.Children=[{3}]",
                m_wrappedMatrixObj.Mode.Value,
                m_matrixWrapperInpcCount,
                m_matrixTreeChangedCount,
                liveChildren
            );
        }

        private void PGSource_OnSetElevatedClicked (object sender, RoutedEventArgs e)
        {
            // Externally change elevated property values on the complex object,
            // then re-set the source to trigger RecacheEditValue cascade.
            // Before the fix, the elevated child editors would show stale values.
            Random rng = new Random();
            m_complexObj.Value = rng.Next(1, 555);
            m_complexObj.SubObjWithElevation.SubObjValue = rng.Next(1, 9999);
            m_complexObj.WrappedFloat.Value = (float)Math.Round(rng.NextDouble() * 100.0, 1);
            this.SetPGSource(m_complexObj);
        }


        private void PGSource_OnNoneClicked(object sender, RoutedEventArgs e)
        {
            this.SetPGSource(null);
        }
        private void TestPropertyGrid_OnPropertyTreeChanged(object sender, EventArgs e)
        {
            this.ResetJsonDisplay();
        }

        // ------ Selection-surface tester wiring ------
        private void TestPropertyGrid_OnSelectedSourceObjectChanged (object sender, EventArgs e)
        {
            this.UpdatePGSelectionReadout();
        }

        private void UpdatePGSelectionReadout ()
        {
            object selected = this.TestPropertyGrid.SelectedSourceObject;
            this.PGSelectionReadout.Text = selected is ShowRoomSubObject sub
                ? $"ShowRoomSubObject (SubObjValue={sub.SubObjValue})"
                : selected?.ToString() ?? "(none)";
        }

        private void PGSelect_OnRandomElementClicked (object sender, RoutedEventArgs e)
        {
            // Complex A/B carry ListWithElevation (complex elements with identity). Selecting one by
            // reference drives the programmatic selection path - the one that hit the Single-mode
            // SelectedItems COMException, and (when the list node is collapsed) the expand-then-select path.
            if (this.TestPropertyGrid.SingleItemSource is ShowRoomTester tester && tester.ListWithElevation.Count > 0)
            {
                ShowRoomSubObject element = tester.ListWithElevation[new Random().Next(0, tester.ListWithElevation.Count)];
                if (!this.TestPropertyGrid.TrySelectSourceObject(element))
                {
                    this.PGSelectionReadout.Text = "(could not find a row for that element)";
                }
            }
            else
            {
                this.PGSelectionReadout.Text = "(switch to Complex A or B - it has the element list)";
            }
        }

        private void PGSelect_OnClearClicked (object sender, RoutedEventArgs e)
        {
            this.TestPropertyGrid.SelectedTarget = null;
        }
        private void ResetJsonDisplay()
        {
            if (m_currentPGTestObj == null)
            {
                return;
            }

            try
            {
                Json json = JsonHelper.BuildJsonForObject(m_currentPGTestObj);
                this.PropertyGridJsonDisplay.Text = json.HasErrors ? json.GetErrorReport() : json.ToString();
            }
            catch (Exception ex)
            {
                this.PropertyGridJsonDisplay.Text = $"[Serialization error]\n{ex.Message}";
            }
        }
    }
}
