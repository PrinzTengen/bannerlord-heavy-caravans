using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HeavyCaravans.Logging;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace HeavyCaravans.Patches
{
    /// <summary>
    /// One-shot log of every Harmony owner patching the methods HeavyCaravans depends on, so a
    /// "_Patch2" suffix in a stack trace can be attributed without decompiling anything. Called at
    /// campaign start (not module load): Bannerlord Coop applies its patches when a session starts,
    /// after every module's OnSubModuleLoad.
    /// </summary>
    internal static class HarmonyPatchDiagnostics
    {
        public static void LogPatchOwners()
        {
            try
            {
                var targets = new List<MethodBase>();
                targets.AddRange(AccessTools.GetDeclaredConstructors(typeof(CaravanPartyComponent)));
                targets.Add(AccessTools.Method(typeof(CaravanPartyComponent), "OnMobilePartySetOnCreation"));
                targets.Add(AccessTools.Method(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.CreateCaravanParty)));
                targets.Add(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.CreateParty)));
                targets.Add(AccessTools.Method(typeof(DefaultPartySizeLimitModel), nameof(DefaultPartySizeLimitModel.GetPartyMemberSizeLimit)));
                targets.Add(AccessTools.Method(typeof(DefaultPartySizeLimitModel), "CalculateMobilePartyMemberSizeLimit"));

                foreach (MethodBase target in targets.Where(t => t != null))
                {
                    HarmonyLib.Patches info = Harmony.GetPatchInfo(target);
                    string name = $"{target.DeclaringType?.Name}.{target.Name}";
                    if (info == null)
                    {
                        Log.Info($"Harmony patches on {name}: none.");
                        continue;
                    }
                    Log.Info($"Harmony patches on {name}: prefixes[{Describe(info.Prefixes)}] postfixes[{Describe(info.Postfixes)}] transpilers[{Describe(info.Transpilers)}] finalizers[{Describe(info.Finalizers)}]");
                }
            }
            catch (Exception ex)
            {
                Log.Info($"Harmony patch diagnostics failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string Describe(IEnumerable<Patch> patches)
        {
            return string.Join(", ", patches.Select(p => $"{p.owner}:{p.PatchMethod?.DeclaringType?.Name}.{p.PatchMethod?.Name}"));
        }
    }
}
