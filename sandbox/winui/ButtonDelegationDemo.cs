namespace AJutShowRoomWinUI
{
    using AJut;
    using AJut.UX.PropertyInteraction;
    using System.Collections.Generic;

    // ===========[ Button delegation demo ]==========================================
    // A property grid source that is a wrapper edit manager delegating target generation
    // to a different inner data object. The grid auto-harvests [PGButton] methods only
    // from the object it is handed (the wrapper), never from the delegated inner one - so
    // a delegating manager has to surface the inner object's buttons itself. This one does,
    // via GenerateButtonsForMethodsOf.
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

    public class ButtonDelegationEditManager : IPropertyEditManager
    {
        private readonly DelegatedButtonInner m_inner = new DelegatedButtonInner();

        public IEnumerable<PropertyEditTarget> GenerateEditTargets ()
        {
            foreach (PropertyEditTarget target in PropertyEditTarget.GenerateForPropertiesOf(m_inner))
            {
                yield return target;
            }

            // The grid won't auto-harvest buttons off the inner object for us, so do it here.
            foreach (PropertyEditTarget button in PropertyEditTarget.GenerateButtonsForMethodsOf(m_inner))
            {
                yield return button;
            }
        }
    }
}
