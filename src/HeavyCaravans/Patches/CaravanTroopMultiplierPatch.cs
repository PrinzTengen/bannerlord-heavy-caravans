using System;
using System.Linq;
using HarmonyLib;
using HeavyCaravans.Config;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace HeavyCaravans.Patches
{
    /// <summary>
    /// Applies GlobalCaravanTroopMultiplier to every caravan's starting roster - normal, elite and
    /// Heavy alike (phases 8/14, DoD: "Multiplier gilt ... für normale, Elite- und Heavy Caravan").
    /// Patches the single shared creation entry point (CaravanPartyComponent.CreateCaravanParty) that
    /// both vanilla/TAOM and HeavyCaravans itself go through - see phases/02-analyse-caravan-system.md.
    /// Runs before HeavyCaravanService's own Heavy-specific x2 roster doubling (which happens after
    /// CreateCaravanParty returns), so the two multipliers stack as intended: GlobalCaravanTroopMultiplier
    /// first, Heavy's own doubling on top.
    ///
    /// At the default multiplier of 1.0 this is a hard no-op (early return) - not just "close to
    /// vanilla via rounding" - to satisfy the phase 8 regression requirement exactly.
    /// </summary>
    [HarmonyPatch(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.CreateCaravanParty))]
    public static class CaravanTroopMultiplierPatch
    {
        private static void Postfix(MobileParty __result)
        {
            float multiplier = HeavyCaravanSettings.GlobalCaravanTroopMultiplier;
            if (__result == null || multiplier == 1f)
            {
                return;
            }

            foreach (var element in __result.MemberRoster.GetTroopRoster().ToList())
            {
                if (element.Character.IsHero || element.Number <= 0)
                {
                    continue;
                }

                int scaled = Math.Max(1, (int)Math.Round(element.Number * multiplier, MidpointRounding.AwayFromZero));
                int delta = scaled - element.Number;
                if (delta != 0)
                {
                    __result.MemberRoster.AddToCounts(element.Character, delta);
                }
            }
        }
    }
}
