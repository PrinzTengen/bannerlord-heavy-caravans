namespace HeavyCaravans.Coop
{
    /// <summary>
    /// Single choke point for "is it this simulation's job to perform this state-changing action"
    /// (phases/12-coop-autoritaet.md). Currently always true: per phases/03-coop-analyse.md, the only
    /// coop mod available for analysis (Bannerlord Online) turned out not to be a Joke's-Coop-style
    /// host/client campaign-sync mod, so there is no real API here to check against yet, and Max
    /// confirmed HeavyCaravans has no hard Coop dependency either way.
    ///
    /// Every HeavyCaravans action that mutates campaign state (creating a Heavy Caravan, marking it,
    /// transferring troops, charging gold) already only runs from a dialog *consequence* callback -
    /// same as vanilla's own caravan creation - which by construction only fires once, for whichever
    /// player is actually having that conversation. That already matches the "host validates the
    /// action" principle from phases/03-coop-analyse.md without needing an explicit check here.
    ///
    /// This method exists so that if/when real Joke's-Coop testing (phases 18/19) turns up an actual
    /// API to gate against, there is exactly one place to wire it in rather than scattering ad-hoc
    /// network checks across the mod.
    /// </summary>
    public static class CoopInterop
    {
        public static bool CanPerformAuthoritativeAction()
        {
            return true;
        }
    }
}
