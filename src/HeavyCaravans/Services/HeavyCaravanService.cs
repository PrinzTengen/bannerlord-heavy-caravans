using System;
using System.Linq;
using Helpers;
using HeavyCaravans.Behaviors;
using HeavyCaravans.Config;
using HeavyCaravans.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace HeavyCaravans.Services
{
    /// <summary>
    /// Core Heavy Caravan mechanics (phases 7/8/9). Deliberately reuses the vanilla elite caravan
    /// machinery (isElite: true) for template selection, price and party-size-limit base values
    /// instead of hard-coding numbers - see phases/02-analyse-caravan-system.md for why: this way
    /// TAOM's own CaravanModel/PartySizeLimitModel overrides (and any future balance changes) are
    /// automatically inherited instead of silently diverging from them.
    /// </summary>
    public static class HeavyCaravanService
    {
        public static bool IsNavalCaravan(Settlement settlement)
        {
            return settlement != null && settlement.HasPort;
        }

        /// <summary>
        /// HeavyCaravanPriceMultiplier defaults to 2.0 (matches the troop doubling below) but is
        /// configurable via ModuleData/HeavyCaravans.ini - see phases/06-dritte-caravan-option.md.
        /// Not a fixed design decision otherwise: revisit in phases/22-balancing.md once testable.
        /// </summary>
        public static int GetHeavyCaravanFormingCost(bool navalCaravan)
        {
            int eliteCost = Campaign.Current.Models.CaravanModel.GetCaravanFormingCost(true, navalCaravan);
            return Math.Max(1, (int)Math.Round(eliteCost * HeavyCaravanSettings.HeavyCaravanPriceMultiplier, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Creates a Heavy Caravan: a vanilla elite caravan (same template/price plumbing) whose
        /// starting roster is then scaled by HeavyCaravanTroopMultiplier and which is marked as
        /// Heavy (see HeavyCaravanBehavior) so the party-size-limit patch and dialog can recognize it
        /// afterwards. Gold is charged by the caller (mirrors vanilla's
        /// CaravanConversationsCampaignBehavior, which also charges gold in the dialog consequence
        /// rather than in the party-creation helper).
        ///
        /// Marks the party as Heavy BEFORE scaling its roster (not after): that way
        /// HeavyCaravanPartySizeLimitPatch already reports the boosted capacity while
        /// ScaleInitialRoster is adding troops, in case anything downstream ever keys off the
        /// party's current capacity while this runs.
        /// </summary>
        public static MobileParty CreateHeavyCaravan(Hero owner, Settlement settlement, Hero leader)
        {
            bool isNaval = IsNavalCaravan(settlement);
            PartyTemplateObject template = CaravanHelper.GetRandomCaravanTemplate(settlement.Culture, isElite: true, isLand: !isNaval);
            if (template == null)
            {
                Log.Error($"No suitable {(isNaval ? "naval" : "land")} elite caravan template for culture '{settlement.Culture?.StringId}' - cannot create Heavy Caravan.");
                return null;
            }

            MobileParty party = CaravanPartyComponent.CreateCaravanParty(owner, settlement, template, isInitialSpawn: false, leader, null, isElite: true);
            HeavyCaravanBehavior.Instance?.MarkAsHeavyCaravan(party);
            ScaleInitialRoster(party);
            Log.Info($"Created Heavy Caravan for {owner?.Name} at {settlement?.Name}, leader {leader?.Name}.");
            return party;
        }

        private static void ScaleInitialRoster(MobileParty party)
        {
            float multiplier = HeavyCaravanSettings.HeavyCaravanTroopMultiplier;
            // Snapshot first: AddToCounts mutates the roster we would otherwise be iterating.
            foreach (var element in party.MemberRoster.GetTroopRoster().ToList())
            {
                if (element.Character.IsHero || element.Number <= 0)
                {
                    continue;
                }
                int scaled = Math.Max(1, (int)Math.Round(element.Number * multiplier, MidpointRounding.AwayFromZero));
                int delta = scaled - element.Number;
                if (delta != 0)
                {
                    party.MemberRoster.AddToCounts(element.Character, delta);
                }
            }
        }
    }
}
