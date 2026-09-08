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
    /// configurable bonus capacity on top (phases 8 + 9).
    ///
    /// Two application points, mutually exclusive:
    ///
    ///  - No TAOM: this postfix on the *base* DefaultPartySizeLimitModel method is the final word on
    ///    party size limit, so applying the boost here is correct and sufficient.
    ///  - TAOM loaded: TaomPartySizeModel.GetPartyMemberSizeLimit calls this same base method first
    ///    (a non-virtual `base.` call, so this postfix still fires), then applies its own further
    ///    adjustments - critically TAOM's troop-weight penalty
    ///    (TAOM.Features.TroopWeight.TroopWeightService.ApplyPartySizeWeightPenalty), which subtracts
    ///    capacity proportional to how much heavier-than-average the roster is. A Heavy Caravan is
    ///    built entirely from elite (high-weight) troops and then has its troop count doubled, so that
    ///    penalty roughly doubles too - applying our boost *before* it (i.e. here) leaves the final,
    ///    TAOM-adjusted limit short of the actual (doubled) troop count, which used to cause daily
    ///    desertion once the caravan was sent off. So when TAOM is present, this postfix backs off
    ///    entirely and HeavyCaravanTaomPostfix (registered dynamically in SubModule, since
    ///    TaomPartySizeModel isn't a compile-time reference) applies the boost *after* TAOM's full
    ///    pipeline instead, scaling the true final number rather than an intermediate one.
    ///
    /// Deliberately not a competing PartySizeLimitModel registered via AddModel: that would either
    /// silently replace TAOM's model (losing its cultural-feat/career/AI/troop-weight adjustments,
    /// depending on module load order) or require duplicating TAOM-internal logic to reimplement them
    /// - see phases/02-analyse-caravan-system.md and phases/13-taom-kompatibilitaet.md.
    /// </summary>
    [HarmonyPatch(typeof(DefaultPartySizeLimitModel), nameof(DefaultPartySizeLimitModel.GetPartyMemberSizeLimit))]
    public static class HeavyCaravanPartySizeLimitPatch
    {
        private static readonly TextObject HeavyCaravanBonusText = new TextObject("{=!}Heavy Caravan");

        /// <summary>
        /// True when TAOM's own PartySizeLimitModel override is loaded - detected purely via
        /// reflection (no compile-time reference to TAOM.dll), so HeavyCaravans keeps working
        /// standalone when TAOM isn't installed.
        /// </summary>
        internal static readonly bool TaomPartySizeModelPresent =
            AccessTools.TypeByName("TAOM.Features.CulturalFeats.Models.TaomPartySizeModel") != null;

        private static void Postfix(PartyBase party, ref ExplainedNumber __result)
        {
            if (TaomPartySizeModelPresent)
            {
                // TAOM's own wrapper (patched separately, see HeavyCaravanTaomPostfix) applies the
                // boost after its troop-weight penalty instead - applying it here too would double it.
                return;
            }
            ApplyHeavyCaravanBoost(party, ref __result);
        }

        /// <summary>
        /// Registered dynamically against TaomPartySizeModel.GetPartyMemberSizeLimit from
        /// SubModule.OnSubModuleLoad, only when TaomPartySizeModelPresent - see that method for why
        /// this can't be a compile-time [HarmonyPatch] attribute.
        /// </summary>
        internal static void HeavyCaravanTaomPostfix(PartyBase party, ref ExplainedNumber __result)
        {
            ApplyHeavyCaravanBoost(party, ref __result);
        }

        private static void ApplyHeavyCaravanBoost(PartyBase party, ref ExplainedNumber result)
        {
            MobileParty mobileParty = party?.MobileParty;
            if (mobileParty == null || HeavyCaravanBehavior.Instance == null || !HeavyCaravanBehavior.Instance.IsHeavyCaravan(mobileParty))
            {
                return;
            }

            // AddFactor takes the *extra* fraction on top of 1.0 - e.g. multiplier 2.0 => +100%.
            result.AddFactor(HeavyCaravanSettings.HeavyCaravanTroopMultiplier - 1f, HeavyCaravanBonusText);
            result.Add(HeavyCaravanSettings.HeavyCaravanBonusCapacity, HeavyCaravanBonusText);
        }
    }
}
