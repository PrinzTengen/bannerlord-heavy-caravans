using System.Collections.Generic;
using HeavyCaravans.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace HeavyCaravans.Behaviors
{
    /// <summary>
    /// Tracks which caravans are "Heavy" (see phases/05-heavy-caravan-markierung.md).
    ///
    /// CaravanPartyComponent is a sealed TaleWorlds type with only a single isElite flag - there is
    /// no room in the vanilla data model for a third caravan tier. Rather than reflection-hacking a
    /// new field onto it, HeavyCaravans keeps its own save-game-persisted list of MobileParty
    /// references. This is the same pattern TaleWorlds itself uses for many campaign-wide trackers
    /// and plays well with the save system (MobileParty is an MBObjectBase, so plain object
    /// references round-trip through SyncData without needing string IDs).
    /// </summary>
    public class HeavyCaravanBehavior : CampaignBehaviorBase
    {
        public static HeavyCaravanBehavior Instance { get; private set; }

        [SaveableField(1)]
        private List<MobileParty> _heavyCaravanParties = new List<MobileParty>();

        public HeavyCaravanBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_heavyCaravanParties", ref _heavyCaravanParties);
            _heavyCaravanParties ??= new List<MobileParty>();
            // Drop stale/null entries that could accumulate across saves (e.g. from an older mod version).
            _heavyCaravanParties.RemoveAll(party => party == null);
            if (dataStore.IsLoading)
            {
                Log.Info($"Save data loaded: {_heavyCaravanParties.Count} Heavy Caravan(s) restored.");
            }
        }

        public bool IsHeavyCaravan(MobileParty party)
        {
            return party != null && _heavyCaravanParties.Contains(party);
        }

        public void MarkAsHeavyCaravan(MobileParty party)
        {
            if (party != null && !_heavyCaravanParties.Contains(party))
            {
                _heavyCaravanParties.Add(party);
                Log.Info($"Heavy caravan registered: {party.Name}.");
            }
        }

        public IEnumerable<MobileParty> HeavyCaravans => _heavyCaravanParties.AsReadOnly();

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyerParty)
        {
            if (_heavyCaravanParties.Remove(party))
            {
                Log.Info($"Heavy caravan removed: {party.Name}.");
            }
        }
    }
}
