using System.Collections.Generic;
using HeavyCaravans.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace HeavyCaravans.Behaviors
{
    /// <summary>
    /// Tracks which caravans are "Heavy" (see phases/05-heavy-caravan-markierung.md), plus the exact
    /// troop count each one started with once fully created (see phases/09-bonuskapazitaet.md and
    /// HeavyCaravanPartySizeLimitPatch): capacity is derived as
    /// "that frozen count + HeavyCaravanBonusCapacity" instead of being recomputed independently from
    /// the base game's size-limit formula, so it can never end up short of the roster due to the two
    /// numbers being rounded separately (GlobalCaravanTroopMultiplier/HeavyCaravanTroopMultiplier are
    /// applied per-troop-stack when scaling the roster, but as a single multiply when scaling
    /// capacity - those two roundings don't have to land on the same integer).
    ///
    /// CaravanPartyComponent is a sealed TaleWorlds type with only a single isElite flag - there is
    /// no room in the vanilla data model for a third caravan tier. Rather than reflection-hacking a
    /// new field onto it, HeavyCaravans keeps its own save-game-persisted lists of MobileParty
    /// references (index-aligned with the base troop count list). This is the same pattern TaleWorlds
    /// itself uses for many campaign-wide trackers and plays well with the save system (MobileParty is
    /// an MBObjectBase, so plain object references round-trip through SyncData without needing string
    /// IDs).
    /// </summary>
    public class HeavyCaravanBehavior : CampaignBehaviorBase
    {
        public static HeavyCaravanBehavior Instance { get; private set; }

        [SaveableField(1)]
        private List<MobileParty> _heavyCaravanParties = new List<MobileParty>();

        /// <summary>Index-aligned with _heavyCaravanParties.</summary>
        [SaveableField(2)]
        private List<int> _heavyCaravanBaseTroopCounts = new List<int>();

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
            dataStore.SyncData("_heavyCaravanBaseTroopCounts", ref _heavyCaravanBaseTroopCounts);
            _heavyCaravanParties ??= new List<MobileParty>();
            _heavyCaravanBaseTroopCounts ??= new List<int>();
            // Old saves (before _heavyCaravanBaseTroopCounts existed) load a shorter/empty second
            // list - pad it out so the two stay index-aligned. 0 falls back to the live troop count
            // at the time capacity is queried (see HeavyCaravanPartySizeLimitPatch), same as before.
            while (_heavyCaravanBaseTroopCounts.Count < _heavyCaravanParties.Count)
            {
                _heavyCaravanBaseTroopCounts.Add(0);
            }
            // Drop stale/null entries that could accumulate across saves (e.g. from an older mod version).
            for (int i = _heavyCaravanParties.Count - 1; i >= 0; i--)
            {
                if (_heavyCaravanParties[i] == null)
                {
                    _heavyCaravanParties.RemoveAt(i);
                    _heavyCaravanBaseTroopCounts.RemoveAt(i);
                }
            }
            if (dataStore.IsLoading)
            {
                Log.Info($"Save data loaded: {_heavyCaravanParties.Count} Heavy Caravan(s) restored.");
            }
        }

        public bool IsHeavyCaravan(MobileParty party)
        {
            return party != null && _heavyCaravanParties.Contains(party);
        }

        /// <summary>
        /// Marks a party as Heavy and freezes its current troop count as the capacity baseline - call
        /// this once, after the party's initial roster is fully scaled (HeavyCaravanService).
        /// </summary>
        public void MarkAsHeavyCaravan(MobileParty party, int baseTroopCount)
        {
            if (party == null || _heavyCaravanParties.Contains(party))
            {
                return;
            }
            _heavyCaravanParties.Add(party);
            _heavyCaravanBaseTroopCounts.Add(baseTroopCount);
            Log.Info($"Heavy caravan registered: {party.Name} (base troop count {baseTroopCount}).");
        }

        /// <summary>The troop count frozen at MarkAsHeavyCaravan time, or 0 if the party isn't Heavy.</summary>
        public int GetBaseTroopCount(MobileParty party)
        {
            int index = party == null ? -1 : _heavyCaravanParties.IndexOf(party);
            return index >= 0 ? _heavyCaravanBaseTroopCounts[index] : 0;
        }

        public IEnumerable<MobileParty> HeavyCaravans => _heavyCaravanParties.AsReadOnly();

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyerParty)
        {
            int index = _heavyCaravanParties.IndexOf(party);
            if (index >= 0)
            {
                _heavyCaravanParties.RemoveAt(index);
                _heavyCaravanBaseTroopCounts.RemoveAt(index);
                Log.Info($"Heavy caravan removed: {party.Name}.");
            }
        }
    }
}
