using System;
using System.Reflection;
using HarmonyLib;
using HeavyCaravans.Behaviors;
using HeavyCaravans.Config;
using HeavyCaravans.Logging;
using HeavyCaravans.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
                TryPatchTaomPartySizeModel();
                Log.Info("HeavyCaravans loaded, Harmony patches applied.");
            }
            catch (Exception ex)
            {
                // Must never take the game down with it - report and keep the mod inert instead.
                Log.Error("HeavyCaravans failed to initialize.", ex);
            }
        }

        /// <summary>
        /// TaomPartySizeModel isn't a compile-time reference (TAOM stays fully optional), so it can't
        /// be targeted with a [HarmonyPatch] attribute picked up by PatchAll - patched manually here,
        /// purely via reflection, only when actually present. See HeavyCaravanPartySizeLimitPatch for
        /// why this needs to exist at all.
        /// </summary>
        private void TryPatchTaomPartySizeModel()
        {
            if (!HeavyCaravanPartySizeLimitPatch.TaomPartySizeModelPresent)
            {
                return;
            }
            try
            {
                var taomPartySizeModelType = AccessTools.TypeByName("TAOM.Features.CulturalFeats.Models.TaomPartySizeModel");
                var targetMethod = AccessTools.Method(taomPartySizeModelType, "GetPartyMemberSizeLimit");
                var postfix = new HarmonyMethod(AccessTools.Method(typeof(HeavyCaravanPartySizeLimitPatch), nameof(HeavyCaravanPartySizeLimitPatch.HeavyCaravanTaomPostfix)));
                _harmony.Patch(targetMethod, postfix: postfix);
                Log.Info("TAOM detected: patched TaomPartySizeModel.GetPartyMemberSizeLimit for Heavy Caravan capacity.");
            }
            catch (Exception ex)
            {
                Log.Warn($"TAOM detected but failed to patch TaomPartySizeModel - Heavy Caravan capacity will not account for TAOM's troop-weight penalty, which can cause desertion. {ex}");
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddBehavior(new HeavyCaravanBehavior());
                campaignStarter.AddBehavior(new HeavyCaravanDialogBehavior());
            }
        }
    }
}
