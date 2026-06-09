namespace AJutShowRoomWinUI
{
    using AJut;
    using AJut.UX.PropertyInteraction;
    using System.Collections.Generic;

    // ===========[ Button delegation repro ]=========================================
    // Mirrors the Call Familiar shape: the property grid source is a wrapper edit
    // manager that delegates target generation to a different inner data object. The
    // grid auto-harvests [PGButton] methods only from the object it is handed (the
    // wrapper), never from the delegated inner object - so the inner button is invisible
    // unless the manager surfaces it itself.
    //
    // The surfacing code lives in ButtonDelegationDemo.Fix.cs. Stash that file to see the
    // bug (no button), pop it to see the fix (button appears).
    // ===============================================================================

    // Inner data object whose [PGButton] is reachable only through the manager below.
    public class DelegatedButtonInner : NotifyPropertyChanged
    {
        private string m_name = "delegated inner";
        [PGEditor("Text")]
        [PGLabel("Inner Name")]
        public string Name
        {
            get => m_name;
            set => this.SetAndRaiseIfChanged(ref m_name, value);
        }

        public int ResetCount { get; private set; }

        [PGButton("Reset From Source (delegated)")]
        public void ResetFromSource ()
        {
            ++this.ResetCount;
            this.Name = $"reset #{this.ResetCount}";
        }
    }

    public partial class ButtonDelegationEditManager : IPropertyEditManager
    {
        private readonly DelegatedButtonInner m_inner = new DelegatedButtonInner();

        public IEnumerable<PropertyEditTarget> GenerateEditTargets ()
        {
            foreach (PropertyEditTarget target in PropertyEditTarget.GenerateForPropertiesOf(m_inner))
            {
                yield return target;
            }

            // HarvestDelegatedButtons is implemented in ButtonDelegationDemo.Fix.cs. When that file is
            // stashed the partial method has no body and the compiler elides this call, so the inner
            // object's [PGButton] never surfaces - reproducing the Call Familiar bug. Pop the file to fix.
            var delegatedButtons = new List<PropertyEditTarget>();
            this.HarvestDelegatedButtons(delegatedButtons);
            foreach (PropertyEditTarget button in delegatedButtons)
            {
                yield return button;
            }
        }

        partial void HarvestDelegatedButtons (List<PropertyEditTarget> into);
    }
}
