using HarmonyLib;
using HeavyCaravans.Behaviors;
using HeavyCaravans.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace HeavyCaravans.Patches
{
    /// <summary>
    /// Scales the troop capacity of Heavy Caravans by HeavyCaravanTroopMultiplier and adds the
    /// configurable bonus capacity on top (phases 8 + 9). See phases/02-analyse-caravan-system.md for
    /// why this patches the *base* DefaultPartySizeLimitModel method rather than registering a
    /// competing PartySizeLimitModel via AddModel: TAOM's own TaomPartySizeModel calls this exact
    /// base method internally (a non-virtual `base.` call), so patching it here is honored whether
    /// TAOM is loaded or not, without having to guess module load order or duplicate TAOM's
    /// cultural-feat/career/AI adjustments.
    ///
    /// Heavy Caravans are created with the vanilla `isElite: true` flag internally (see
    /// HeavyCaravanService), so by the time this postfix runs, __result already holds the normal
    /// elite-caravan total (which itself already includes GlobalCaravanTroopMultiplier, applied
    /// earlier at creation by CaravanTroopMultiplierPatch) - scaling it here on top is exactly
    /// "HeavyBaseCapacity = EliteBaseCapacity * HeavyCaravanTroopMultiplier".
    /// </summary>
    [HarmonyPatch(typeof(DefaultPartySizeLimitModel), nameof(DefaultPartySizeLimitModel.GetPartyMemberSizeLimit))]
    public static class HeavyCaravanPartySizeLimitPatch
    {
        private static readonly TextObject HeavyCaravanBonusText = new TextObject("{=!}Heavy Caravan");

        private static void Postfix(PartyBase party, ref ExplainedNumber __result)
        {
            MobileParty mobileParty = party?.MobileParty;
            if (mobileParty == null || HeavyCaravanBehavior.Instance == null || !HeavyCaravanBehavior.Instance.IsHeavyCaravan(mobileParty))
            {
                return;
            }

            // AddFactor takes the *extra* fraction on top of 1.0 - e.g. multiplier 2.0 => +100%.
            __result.AddFactor(HeavyCaravanSettings.HeavyCaravanTroopMultiplier - 1f, HeavyCaravanBonusText);
            __result.Add(HeavyCaravanSettings.HeavyCaravanBonusCapacity, HeavyCaravanBonusText);
        }
    }
}
