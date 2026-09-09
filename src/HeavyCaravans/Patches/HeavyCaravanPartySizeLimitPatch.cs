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
    /// Keeps caravan troop *capacity* in step with the caravan troop *counts* this mod hands out,
    /// so caravans don't shed the extra troops again through vanilla desertion
    /// (DefaultPartyDesertionModel.GetTroopsToDesertDueToWageAndPartySize deserts 25% of anything
    /// above PartySizeLimit every day, caravans included):
    ///
    ///  - normal/elite caravans: capacity x GlobalCaravanTroopMultiplier, because
    ///    CaravanTroopMultiplierPatch scales every caravan's starting roster by exactly that
    ///    (phase 8). Without this a global multiplier above 1.0 made *every* caravan - vanilla elite
    ///    ones too - desert.
    ///  - Heavy caravans: capacity = the party's *actual, frozen* starting troop count
    ///    (HeavyCaravanBehavior.GetBaseTroopCount, recorded once by HeavyCaravanService right after
    ///    its roster is fully scaled) + HeavyCaravanBonusCapacity, set as an exact target rather than
    ///    recomputed independently. That's deliberate: GlobalCaravanTroopMultiplier and
    ///    HeavyCaravanTroopMultiplier are applied to the roster per troop stack (each stack rounded
    ///    on its own in HeavyCaravanService/CaravanTroopMultiplierPatch), so the *sum* the roster
    ///    lands on and "eliteCapacityBase * global * heavy" computed as one multiplication can differ
    ///    by a few troops after two rounds of per-stack rounding - small enough to go unnoticed until
    ///    vanilla desertion trims exactly that difference off, 25%/day, which is what happened before
    ///    this fix (a handful of troops kept deserting even though the bulk of the earlier
    ///    global-multiplier bug was already fixed). Targeting the real troop count instead of a
    ///    parallel formula makes the two numbers structurally unable to drift apart, and guarantees
    ///    HeavyCaravanBonusCapacity is genuine, always-available headroom on top of however many
    ///    troops the caravan actually started with.
    ///
    /// Math note: ExplainedNumber factors are additive on the base (Result = Base * (1 + SumOfFactors)),
    /// so an Add() after an AddFactor gets multiplied by the factor too. Both branches below instead
    /// compute the exact flat delta needed and divide it by (1 + SumOfFactors) before adding, so the
    /// *final* number moves by exactly the intended amount regardless of what earlier models already
    /// added to this ExplainedNumber - the same trick TAOM's TroopWeightService.SubtractResultFramePenalty
    /// uses.
    ///
    /// TAOM: TaomPartySizeModel calls this base method (so this postfix fires inside it) and then
    /// applies its own adjustments, notably the troop-weight penalty. That penalty is computed from
    /// TroopWeights/troop_weights.xml, which weights 105 elite kingdom units (2.0-10.0) and not a
    /// single troop that appears in any caravan party template (verified against
    /// taom_partyTemplates.xml), so for caravans it is exactly 0.
    /// </summary>
    [HarmonyPatch(typeof(DefaultPartySizeLimitModel), nameof(DefaultPartySizeLimitModel.GetPartyMemberSizeLimit))]
    public static class HeavyCaravanPartySizeLimitPatch
    {
        private static readonly TextObject HeavyCaravanText = new TextObject("{=!}Heavy Caravan");
        private static readonly TextObject CaravanMultiplierText = new TextObject("{=!}Caravan troop multiplier");

        private static void Postfix(PartyBase party, ref ExplainedNumber __result)
        {
            MobileParty mobileParty = party?.MobileParty;
            if (mobileParty == null || !mobileParty.IsCaravan)
            {
                return;
            }

            bool isHeavy = HeavyCaravanBehavior.Instance != null && HeavyCaravanBehavior.Instance.IsHeavyCaravan(mobileParty);
            if (isHeavy)
            {
                int baseTroopCount = HeavyCaravanBehavior.Instance.GetBaseTroopCount(mobileParty);
                if (baseTroopCount <= 0)
                {
                    // Only expected for a Heavy Caravan restored from a save made before
                    // HeavyCaravanBehavior recorded a base count - fall back to its current troop
                    // count so it still gets a sensible floor instead of 0 + bonus.
                    baseTroopCount = mobileParty.MemberRoster.TotalManCount;
                }
                SetExactTarget(ref __result, baseTroopCount + HeavyCaravanSettings.HeavyCaravanBonusCapacity, HeavyCaravanText);
            }
            else
            {
                ApplyGlobalMultiplier(ref __result, HeavyCaravanSettings.GlobalCaravanTroopMultiplier);
            }
        }

        private static void ApplyGlobalMultiplier(ref ExplainedNumber result, float globalMultiplier)
        {
            if (globalMultiplier == 1f)
            {
                return;
            }
            float denominator = 1f + result.SumOfFactors;
            if (denominator <= 0.01f)
            {
                return;
            }
            float current = result.ResultNumber;
            AddFlat(ref result, current * (globalMultiplier - 1f), denominator, CaravanMultiplierText);
        }

        private static void SetExactTarget(ref ExplainedNumber result, int target, TextObject description)
        {
            float denominator = 1f + result.SumOfFactors;
            if (denominator <= 0.01f)
            {
                return;
            }
            AddFlat(ref result, target - result.ResultNumber, denominator, description);
        }

        private static void AddFlat(ref ExplainedNumber result, float delta, float denominator, TextObject description)
        {
            if (delta != 0f)
            {
                result.Add(delta / denominator, description);
            }
        }
    }
}
