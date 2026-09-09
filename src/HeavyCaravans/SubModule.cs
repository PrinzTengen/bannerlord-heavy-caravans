using System;
using System.Reflection;
using HarmonyLib;
using HeavyCaravans.Behaviors;
using HeavyCaravans.Config;
using HeavyCaravans.Coop;
using HeavyCaravans.Logging;
using HeavyCaravans.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace HeavyCaravans
{
    public class HeavyCaravansSubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                HeavyCaravanSettings.EnsureLoaded();
                _harmony = new Harmony("com.heavycaravans.mod");
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Info("HeavyCaravans loaded, Harmony patches applied.");
            }
            catch (Exception ex)
            {
                // Must never take the game down with it - report and keep the mod inert instead.
                Log.Error("HeavyCaravans failed to initialize.", ex);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddBehavior(new HeavyCaravanBehavior());
                campaignStarter.AddBehavior(new HeavyCaravanDialogBehavior());
                // A coop session (client or server) is already established before the campaign
                // loads, so this is the earliest point where the role is known.
                Log.Info(CoopInterop.DescribeSession());
                HarmonyPatchDiagnostics.LogPatchOwners();
            }
        }
    }
}
