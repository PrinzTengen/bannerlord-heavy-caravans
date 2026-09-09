using System.Collections.Generic;
using Helpers;
using HeavyCaravans.Coop;
using HeavyCaravans.Logging;
using HeavyCaravans.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeavyCaravans.Behaviors
{
    /// <summary>
    /// The third ("Heavy") caravan dialog option (phases 6/7/10). Hooks onto the vanilla
    /// "magistrate_form_a_caravan_player_answer" node as a sibling player line - dialog trees are
    /// additive/string-keyed in Bannerlord, so this does not require patching
    /// TaleWorlds.CampaignSystem.CampaignBehaviors.CaravanConversationsCampaignBehavior at all.
    /// Deliberately its own, independent leader-selection/accept sub-tree (rather than trying to
    /// reach into that vanilla behavior's private `_selectedCaravanType` field via reflection) - see
    /// phases/phase-06-devlog.md for why.
    /// </summary>
    public class HeavyCaravanDialogBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No behavior-local state - HeavyCaravanBehavior owns the persisted Heavy-caravan list.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("heavy_caravan_offer", "magistrate_form_a_caravan_player_answer", "heavy_caravan_offer_cost", "{=!}{HEAVY_CARAVAN_OFFER_TEXT}", OnOfferCondition, null);
            // On a Bannerlord Coop client the caravan could only be formed by the coop server (see
            // CoopInterop) - say so up front instead of failing after the leader was picked. The two
            // NPC replies below are mutually exclusive by condition, so their order does not matter.
            starter.AddDialogLine("heavy_caravan_offer_coop_blocked", "heavy_caravan_offer_cost", "lord_pretalk", "{=!}That cannot be arranged in a shared (Bannerlord Coop) campaign yet - the host's server would have to raise such a caravan, and it does not know how. Perhaps another time.", OnCoopBlockedCondition, null);
            starter.AddDialogLine("heavy_caravan_offer_cost_line", "heavy_caravan_offer_cost", "heavy_caravan_player_answer", "{=!}{HEAVY_CARAVAN_COST_TEXT}", OnCostCondition, null);
            starter.AddPlayerLine("heavy_caravan_accept", "heavy_caravan_player_answer", "heavy_caravan_accepted", "{=!}{HEAVY_CARAVAN_ACCEPT_TEXT}", OnAcceptCondition, OnAcceptConsequence);
            starter.AddPlayerLine("heavy_caravan_no_gold", "heavy_caravan_player_answer", "lord_pretalk", "{=w6WFuDn0}I am sorry, I don't have that much money.", OnNoGoldCondition, null);
            starter.AddPlayerLine("heavy_caravan_reject", "heavy_caravan_player_answer", "lord_pretalk", "{=2mJjDTAZ}That sounds expensive.", OnRejectCondition, null);
            starter.AddDialogLine("heavy_caravan_choose_leader_line", "heavy_caravan_accepted", "heavy_caravan_choose_leader", "{=!}{HEAVY_CARAVAN_LEADER_CHOOSE_TEXT}", OnChooseLeaderCondition, null);
            starter.AddRepeatablePlayerLine("heavy_caravan_leader_chosen", "heavy_caravan_choose_leader", "heavy_caravan_leader_is_chosen", "{=!}{HERO.NAME}", "{=UNFE1BeG}I am thinking of a different person", "heavy_caravan_accepted", OnLeaderChosenCondition, OnLeaderAcceptConsequence);
            starter.AddPlayerLine("heavy_caravan_choose_leader_cancel", "heavy_caravan_choose_leader", "lord_pretalk", "{=PznWhAdU}Actually, never mind.", null, null);
            starter.AddDialogLine("heavy_caravan_final_line", "heavy_caravan_leader_is_chosen", "close_window", "{=!}{HEAVY_CARAVAN_FINAL_TEXT}", null, null);

            // Reinforcing an already-existing Heavy Caravan (phases 10/11). Hooks onto the same
            // "talk to my own caravan-leading companion" node vanilla itself uses for e.g. changing
            // the caravan's home settlement (CaravansCampaignBehavior's
            // "caravan_companion_talk_start_reply").
            starter.AddPlayerLine("heavy_caravan_status_offer", "caravan_companion_talk_start_reply", "heavy_caravan_status", "{=!}How does your escort fare?", OnStatusOfferCondition, null);
            starter.AddDialogLine("heavy_caravan_status_line", "heavy_caravan_status", "caravan_companion_anything_else", "{=!}{HEAVY_CARAVAN_STATUS_TEXT}", null, null);
            starter.AddPlayerLine("heavy_caravan_reinforce_offer", "caravan_companion_talk_start_reply", "close_window", "{=!}I wish to reinforce your escort.", OnReinforceOfferCondition, OnReinforceOfferConsequence);
        }

        private bool OnOfferCondition()
        {
            // No extra eligibility check here: this node ("magistrate_form_a_caravan_player_answer")
            // is only reachable after vanilla's own conversation_caravan_build_on_condition already
            // passed (talking to a merchant/artisan notable) - see phase-16 devlog for how the
            // earlier CanHeroCreateCaravan-based check was wrong here (that gate is for the NPC
            // auto-spawn simulation, not the player dialog, and made the option always invisible).
            if (FindSuitableCompanionsToLeadCaravan().Count == 0)
            {
                return false;
            }
            MBTextManager.SetTextVariable("HEAVY_CARAVAN_OFFER_TEXT", new TextObject("{=!}Is there a way to form an even larger caravan - one with twice the troops of your finest?"));
            return true;
        }

        private bool OnCoopBlockedCondition()
        {
            if (CoopInterop.CanPerformAuthoritativeAction())
            {
                return false;
            }
            Log.Warn("Heavy Caravan offer declined: this game is a Bannerlord Coop client, and parties can only be created by the coop server (CoopInterop).");
            return true;
        }

        private bool OnCostCondition()
        {
            if (!CoopInterop.CanPerformAuthoritativeAction())
            {
                return false;
            }
            MBTextManager.SetTextVariable("AMOUNT", GetCost());
            MBTextManager.SetTextVariable("HEAVY_CARAVAN_COST_TEXT", new TextObject("{=!}That can be arranged, but it will cost considerably more: {AMOUNT}{GOLD_ICON}."));
            return true;
        }

        private bool OnAcceptCondition()
        {
            int cost = GetCost();
            MBTextManager.SetTextVariable("AMOUNT", cost);
            MBTextManager.SetTextVariable("HEAVY_CARAVAN_ACCEPT_TEXT", new TextObject("{=!}I accept these conditions and I am ready to pay {AMOUNT}{GOLD_ICON}."));
            return Hero.MainHero.Gold >= cost;
        }

        private void OnAcceptConsequence()
        {
            Log.Info("Heavy caravan creation requested.");
            ConversationSentence.SetObjectsToRepeatOver(FindSuitableCompanionsToLeadCaravan());
        }

        private bool OnNoGoldCondition()
        {
            return Hero.MainHero.Gold < GetCost();
        }

        private bool OnRejectCondition()
        {
            return Hero.MainHero.Gold >= GetCost();
        }

        private bool OnChooseLeaderCondition()
        {
            MBTextManager.SetTextVariable("HEAVY_CARAVAN_LEADER_CHOOSE_TEXT", new TextObject("{=aeCYFe1g}Whom do you want to lead the caravan?"));
            return true;
        }

        private bool OnLeaderChosenCondition()
        {
            if (ConversationSentence.CurrentProcessedRepeatObject is CharacterObject character)
            {
                StringHelpers.SetRepeatableCharacterProperties("HERO", character);
                return true;
            }
            return false;
        }

        private void OnLeaderAcceptConsequence()
        {
            // heavy_caravan_final_line's text is {HEAVY_CARAVAN_FINAL_TEXT} - if we return (or throw)
            // without ever setting it, that dialog line has nothing to render, which is the prime
            // suspect for the "stuck in companion selection" report: the window never gets an error,
            // it just has no text to show. Every exit path below sets it, success or failure, so the
            // dialog can always close cleanly - and every failure path logs *why*, since none of them
            // did before.
            MBTextManager.SetTextVariable("HEAVY_CARAVAN_FINAL_TEXT", new TextObject("{=!}Something went wrong and no caravan was formed. Check HeavyCaravans.log for details."));

            try
            {
                if (!CoopInterop.CanPerformAuthoritativeAction())
                {
                    // Normally unreachable (OnCostCondition already hides this path on a coop
                    // client), kept as a hard stop: Coop's patches skip the CaravanPartyComponent
                    // constructor on clients and CreateCaravanParty would throw - see CoopInterop.
                    Log.Warn("Heavy caravan creation blocked: this game is a Bannerlord Coop client. " + CoopInterop.DescribeSession());
                    MBTextManager.SetTextVariable("HEAVY_CARAVAN_FINAL_TEXT", new TextObject("{=!}Forgive me - in a shared campaign only the host's server may raise such a caravan, and it does not know how yet. No gold has changed hands."));
                    return;
                }
                Log.Info("Proceeding with Heavy caravan creation. " + CoopInterop.DescribeSession());

                var leaderCharacter = ConversationSentence.SelectedRepeatObject as CharacterObject;
                Hero leader = leaderCharacter?.HeroObject;
                if (leader == null)
                {
                    Log.Error($"Heavy caravan creation aborted: SelectedRepeatObject was not a hero's CharacterObject (was: {ConversationSentence.SelectedRepeatObject?.GetType().FullName ?? "null"}).");
                    return;
                }

                Settlement settlement = Settlement.CurrentSettlement;
                int cost = GetCost();

                CampaignMission.Current?.FadeOutCharacter(leaderCharacter);
                LeaveSettlementAction.ApplyForCharacterOnly(leader);

                MobileParty party = HeavyCaravanService.CreateHeavyCaravan(Hero.MainHero, settlement, leader);
                if (party == null)
                {
                    Log.Error($"Heavy caravan creation failed: HeavyCaravanService.CreateHeavyCaravan returned null for leader {leader.Name} at {settlement?.Name}.");
                    return;
                }
                GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, settlement, cost);

                TextObject textObject = new TextObject("{=!}A new Heavy Caravan is created for {HERO.NAME}.");
                StringHelpers.SetCharacterProperties("HERO", Hero.MainHero.CharacterObject, textObject);
                MBTextManager.SetTextVariable("HEAVY_CARAVAN_FINAL_TEXT", new TextObject("{=!}Ok then. I will call my finest men to help you form this caravan. May it serve you well."));
                InformationManager.DisplayMessage(new InformationMessage(textObject.ToString()));
            }
            catch (System.Exception ex)
            {
                Log.Error("Heavy caravan creation threw an exception.", ex);
            }
        }

        private static bool IsTalkingToOwnHeavyCaravanLeader()
        {
            MobileParty party = MobileParty.ConversationParty;
            return party != null
                && party.IsCaravan
                && party.Party.Owner == Hero.MainHero
                && HeavyCaravanBehavior.Instance != null
                && HeavyCaravanBehavior.Instance.IsHeavyCaravan(party);
        }

        private bool OnStatusOfferCondition()
        {
            if (!IsTalkingToOwnHeavyCaravanLeader())
            {
                return false;
            }
            MobileParty party = MobileParty.ConversationParty;
            int current = party.MemberRoster.TotalManCount;
            int limit = (int)Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(party.Party).ResultNumber;
            int free = CaravanTroopTransferService.GetFreeSlots(party);
            TextObject text = new TextObject("{=!}Currently: {CURRENT}. Maximum: {MAX}. Free places: {FREE}.");
            text.SetTextVariable("CURRENT", current);
            text.SetTextVariable("MAX", limit);
            text.SetTextVariable("FREE", free);
            MBTextManager.SetTextVariable("HEAVY_CARAVAN_STATUS_TEXT", text);
            return true;
        }

        private bool OnReinforceOfferCondition()
        {
            return IsTalkingToOwnHeavyCaravanLeader()
                && CoopInterop.CanPerformAuthoritativeAction()
                && CaravanTroopTransferService.GetFreeSlots(MobileParty.ConversationParty) > 0;
        }

        private void OnReinforceOfferConsequence()
        {
            if (!CoopInterop.CanPerformAuthoritativeAction())
            {
                return;
            }
            CaravanTroopTransferService.OpenReinforceScreen(MobileParty.ConversationParty);
        }

        private static int GetCost()
        {
            return HeavyCaravanService.GetHeavyCaravanFormingCost(HeavyCaravanService.IsNavalCaravan(Settlement.CurrentSettlement));
        }

        private static List<CharacterObject> FindSuitableCompanionsToLeadCaravan()
        {
            var list = new List<CharacterObject>();
            foreach (TroopRosterElement item in MobileParty.MainParty.MemberRoster.GetTroopRoster())
            {
                Hero heroObject = item.Character.HeroObject;
                if (heroObject != null && heroObject != Hero.MainHero && heroObject.Clan == Clan.PlayerClan && heroObject.GovernorOf == null && heroObject.CanLeadParty())
                {
                    list.Add(item.Character);
                }
            }
            return list;
        }
    }
}
