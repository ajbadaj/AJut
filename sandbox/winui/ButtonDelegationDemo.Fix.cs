namespace AJutShowRoomWinUI
{
    using System.Collections.Generic;
    using AJut.UX.PropertyInteraction;

    // ===========[ Button delegation FIX ]===========================================
    // This file IS the fix. A delegating IPropertyEditManager surfaces its inner object's
    // [PGButton] methods by harvesting them itself - exactly what AriPropertyGrid.GenerateFor
    // does in Call Familiar. Stash this single file to reproduce the bug (no button); pop it
    // to restore the fix (button appears).
    // ===============================================================================

    public partial class ButtonDelegationEditManager
    {
        partial void HarvestDelegatedButtons (List<PropertyEditTarget> into)
        {
            into.AddRange(PropertyEditTarget.GenerateButtonsForMethodsOf(m_inner));
        }
    }
}
