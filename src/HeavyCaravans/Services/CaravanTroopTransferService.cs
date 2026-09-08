using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using HeavyCaravans.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace HeavyCaravans.Services
{
    /// <summary>
    /// Reinforcing your own Heavy Caravan (phases 10/11). DoD/phases/11-truppentransfer.md rules:
    /// player party -> caravan only (never the reverse), regular troops only (no prisoners, heroes,
    /// companions or lords), capped by the caravan's own capacity.
    ///
    /// The native PartyScreenHelper API only exposes a single, symmetric TransferState flag for both
    /// directions and doesn't let a caller plug in a custom per-troop/per-side rule on a live
    /// MobileParty (only the disconnected "dummy roster" OpenScreenWithCondition overload does, which
    /// trades this problem for a different one - manually re-implementing roster commit). Instead of
    /// trusting an uncertain internal semantic, this opens the standard native troop+prisoner screen
    /// and then reconciles the result against a pre-screen snapshot: prisoners are always restored
    /// exactly, any hero/companion/lord that ended up in the caravan is sent back, and any regular
    /// troop that *left* the caravan is put straight back. Worst case for the player is a startling
    /// "that didn't stick" on an attempted rule violation - never actually possible - which matches
    /// the DoD guarantee no matter what the UI allowed mid-session.
    /// </summary>
    public static class CaravanTroopTransferService
    {
        public static int GetFreeSlots(MobileParty caravan)
        {
            int limit = (int)Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(caravan.Party).ResultNumber;
            return Math.Max(0, limit - caravan.MemberRoster.TotalManCount);
        }

        public static void OpenReinforceScreen(MobileParty caravan)
        {
            var beforeMembers = Snapshot(caravan.MemberRoster);
            var beforePrisoners = Snapshot(caravan.PrisonRoster);
            Log.Info($"Party roster before transfer ({caravan.Name}): {caravan.MemberRoster.TotalManCount} troops.");

            PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners(caravan, (leftOwnerParty, leftMemberRoster, leftPrisonRoster, rightOwnerParty, rightMemberRoster, rightPrisonRoster, fromCancel) =>
            {
                if (fromCancel)
                {
                    return;
                }
                Reconcile(caravan, beforeMembers, beforePrisoners);
                Log.Info($"Party roster after transfer ({caravan.Name}): {caravan.MemberRoster.TotalManCount} troops.");
            });
        }

        private static Dictionary<CharacterObject, int> Snapshot(TaleWorlds.CampaignSystem.Roster.TroopRoster roster)
        {
            return roster.GetTroopRoster().ToDictionary(element => element.Character, element => element.Number);
        }

        private static void Reconcile(MobileParty caravan, Dictionary<CharacterObject, int> beforeMembers, Dictionary<CharacterObject, int> beforePrisoners)
        {
            MobileParty mainParty = MobileParty.MainParty;
            int beforeTotal = beforeMembers.Values.Sum();

            // Prisoners are entirely out of scope for this feature - restore exactly, both directions.
            RestoreExact(caravan.PrisonRoster, mainParty.PrisonRoster, beforePrisoners, "prisoner");

            // Regular troops: additions from the player's own party are fine, but no pull-out and no
            // hero/companion/lord ever ends up in the caravan roster through this screen.
            foreach (var element in caravan.MemberRoster.GetTroopRoster().ToList())
            {
                beforeMembers.TryGetValue(element.Character, out int priorCount);

                if (element.Character.IsHero)
                {
                    int toRevert = Math.Max(0, element.Number - priorCount);
                    if (toRevert > 0)
                    {
                        Log.Warn($"Reverted an attempted hero/companion transfer into a Heavy Caravan ({element.Character.Name}) - not allowed.");
                        caravan.MemberRoster.AddToCounts(element.Character, -toRevert);
                        mainParty.MemberRoster.AddToCounts(element.Character, toRevert);
                    }
                    continue;
                }

                if (element.Number < priorCount)
                {
                    int missing = priorCount - element.Number;
                    Log.Warn($"Reverted an attempted troop pull-out from a Heavy Caravan ({missing}x {element.Character.Name}) - transfer is player -> caravan only.");
                    caravan.MemberRoster.AddToCounts(element.Character, missing);
                    mainParty.MemberRoster.AddToCounts(element.Character, -missing);
                }
            }

            int netTransferred = caravan.MemberRoster.TotalManCount - beforeTotal;
            if (netTransferred > 0)
            {
                Log.Info($"Troops transferred to Heavy Caravan {caravan.Name}: {netTransferred}.");
            }
        }

        private static void RestoreExact(TaleWorlds.CampaignSystem.Roster.TroopRoster caravanRoster, TaleWorlds.CampaignSystem.Roster.TroopRoster mainPartyRoster, Dictionary<CharacterObject, int> before, string label)
        {
            foreach (var element in caravanRoster.GetTroopRoster().ToList())
            {
                before.TryGetValue(element.Character, out int priorCount);
                int diff = element.Number - priorCount;
                if (diff != 0)
                {
                    Log.Warn($"Reverted an attempted {label} transfer involving a Heavy Caravan ({element.Character.Name}) - not supported by this feature.");
                    caravanRoster.AddToCounts(element.Character, -diff);
                    mainPartyRoster.AddToCounts(element.Character, diff);
                }
            }
        }
    }
}
