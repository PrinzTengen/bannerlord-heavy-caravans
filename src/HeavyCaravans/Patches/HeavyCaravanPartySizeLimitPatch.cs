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
    ///  - every caravan: capacity x GlobalCaravanTroopMultiplier, because CaravanTroopMultiplierPatch
    ///    scales every caravan's starting roster by exactly that (phase 8:
    ///    "EliteBaseCapacity = VanillaEliteTroops * GlobalCaravanTroopMultiplier"). Without this a
    ///    global multiplier above 1.0 made *every* caravan - vanilla elite ones too - desert: a TAOM
    ///    elite caravan template is 49 troops, vanilla capacity for a player elite caravan is 50.
    ///  - Heavy caravans additionally: x HeavyCaravanTroopMultiplier, plus HeavyCaravanBonusCapacity
    ///    flat on top (phases 8 + 9).
    ///
    /// Math note: ExplainedNumber factors are additive on the base (Result = Base * (1 + SumOfFactors)),
    /// so stacking two AddFactor calls would give x4 for 2.5 x 2.5 instead of x6.25, and an Add()
    /// after an AddFactor gets multiplied by the factor as well (+20 became +50). Both are therefore
    /// applied as a single flat delta on the current result, divided by (1 + SumOfFactors) so the
    /// *final* number moves by exactly the intended amount - the same trick TAOM's
    /// TroopWeightService.SubtractResultFramePenalty uses.
    ///
    /// TAOM: TaomPartySizeModel calls this base method (so this postfix fires inside it) and then
    /// applies its own adjustments, notably the troop-weight penalty. That penalty is computed from
    /// TroopWeights/troop_weights.xml, which weights 105 elite kingdom units (2.0-10.0) and not a single
    /// troop that appears in any caravan party template (verified against taom_partyTemplates.xml), so
    /// for caravans it is exactly 0 - and even if it weren't, applying our boost *before* it is the
    /// coherent order (the penalty then only subtracts the weight excess from the already boosted
    /// limit, whereas applying a factor *after* it would multiply the penalty as well). The earlier
    /// dynamic postfix on TaomPartySizeModel was built on the wrong theory and has been removed.
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
            ApplyCaravanCapacity(ref __result, isHeavy,
                HeavyCaravanSettings.GlobalCaravanTroopMultiplier,
                HeavyCaravanSettings.HeavyCaravanTroopMultiplier,
                HeavyCaravanSettings.HeavyCaravanBonusCapacity);
        }

        /// <summary>
        /// Final = current * global (all caravans); Heavy: final = current * global * heavy + bonus.
        /// </summary>
        internal static void ApplyCaravanCapacity(ref ExplainedNumber result, bool isHeavy, float globalMultiplier, float heavyMultiplier, int bonusCapacity)
        {
            float denominator = 1f + result.SumOfFactors;
            if (denominator <= 0.01f)
            {
                return;
            }
            float current = result.ResultNumber;
            if (globalMultiplier != 1f)
            {
                AddFlat(ref result, current * (globalMultiplier - 1f), denominator, CaravanMultiplierText);
            }
            if (isHeavy)
            {
                float afterGlobal = current * globalMultiplier;
                AddFlat(ref result, afterGlobal * (heavyMultiplier - 1f) + bonusCapacity, denominator, HeavyCaravanText);
            }
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
