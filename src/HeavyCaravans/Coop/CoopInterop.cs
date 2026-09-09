using System;
using System.Linq;
using System.Reflection;
using HeavyCaravans.Logging;

namespace HeavyCaravans.Coop
{
    /// <summary>
    /// Single choke point for "is it this simulation's job to perform this state-changing action"
    /// (phases/12-coop-autoritaet.md), now backed by real Bannerlord Coop detection.
    ///
    /// The coop mod actually in use here is Bannerlord Coop (Steam Workshop module id "CoopNightly",
    /// formerly "Coop"; assemblies Coop.dll / Coop.Core.dll / GameInterface.dll / Common.dll) - NOT
    /// "Bannerlord Online", which phases/03-coop-analyse.md looked at. Bannerlord Coop is strictly
    /// server-authoritative: even the hosting player's game joins its own separately spawned server
    /// process as a *client* (Coop.Core.CoopartiveMultiplayerExperience: ServerProcessManager.Start
    /// + StartAsClient(JoinIntent.HostLoopback)), so in a live session every player's game has
    /// Common.ModInformation.IsServer == false.
    ///
    /// On such a client, Coop's Harmony prefix on the CaravanPartyComponent constructor
    /// (GameInterface.Services.PartyComponents.Patches.Lifetime.CaravanPartyComponentLifetimePatches)
    /// logs "Client created managed CaravanPartyComponent" and returns false, i.e. the constructor
    /// body is skipped: Owner/Settlement/_initializationArgs stay null, and MobileParty.CreateParty
    /// then dies with a NullReferenceException in CaravanPartyComponent.OnMobilePartySetOnCreation
    /// (`Owner.Clan`). Vanilla's own caravan dialog only survives this because Coop replaces its
    /// consequence with a prefix that sends a FormPlayerClanCaravan network message and lets the
    /// server call CreateCaravanParty. Anything that wants to create a party locally on a coop client
    /// is therefore doomed - which is exactly what CanPerformAuthoritativeAction() now reports.
    ///
    /// Detection mirrors TAOM's own TAOM.Features.CoopInterop.CoopSessionProvider (proven against
    /// this Coop build): reflection only, no compile-time reference, so HeavyCaravans keeps working
    /// without Coop installed.
    /// </summary>
    public static class CoopInterop
    {
        private const string GameInterfaceAssemblyName = "GameInterface";
        private const string CommonAssemblyName = "Common";
        private const string ContainerProviderTypeName = "GameInterface.ContainerProvider";
        private const string ModInformationTypeName = "Common.ModInformation";

        private static readonly object BindLock = new object();
        private static MethodInfo _tryGetContainer;
        private static PropertyInfo _isServer;
        private static bool _probeFailureLogged;

        /// <summary>True once Bannerlord Coop's GameInterface assembly is loaded in this process.</summary>
        public static bool IsCoopModuleLoaded
        {
            get
            {
                EnsureBound();
                return _tryGetContainer != null;
            }
        }

        /// <summary>True while a Bannerlord Coop session (client or server) is running.</summary>
        public static bool IsCoopSessionActive => Probe().sessionActive;

        /// <summary>True when this process is a Bannerlord Coop client (including the hosting player).</summary>
        public static bool IsCoopClient
        {
            get
            {
                var (sessionActive, isServer) = Probe();
                return sessionActive && !isServer;
            }
        }

        /// <summary>
        /// False on a Bannerlord Coop client: campaign state there is owned by the coop server, and
        /// local party creation is actively blocked by Coop's patches (see class summary). True in
        /// singleplayer, with the Coop module merely installed, and on the coop server itself.
        /// </summary>
        public static bool CanPerformAuthoritativeAction()
        {
            return !IsCoopClient;
        }

        /// <summary>One-line human-readable state for the log.</summary>
        public static string DescribeSession()
        {
            if (!IsCoopModuleLoaded)
            {
                return "Bannerlord Coop: not loaded.";
            }
            var (sessionActive, isServer) = Probe();
            if (!sessionActive)
            {
                return "Bannerlord Coop: module loaded, no session active (singleplayer rules apply).";
            }
            return isServer
                ? "Bannerlord Coop: session active, this process is the SERVER (authoritative)."
                : "Bannerlord Coop: session active, this process is a CLIENT - Heavy Caravan creation is not possible here (see CoopInterop).";
        }

        private static (bool sessionActive, bool isServer) Probe()
        {
            EnsureBound();
            if (_tryGetContainer == null)
            {
                return (false, false);
            }
            try
            {
                // public static bool TryGetContainer(out ILifetimeScope lifetimeScope)
                object[] args = new object[1];
                object returned = _tryGetContainer.Invoke(null, args);
                bool sessionActive = returned is bool b ? b : args[0] != null;
                if (!sessionActive)
                {
                    return (false, false);
                }
                bool isServer = _isServer?.GetValue(null) is bool s && s;
                return (true, isServer);
            }
            catch (Exception ex)
            {
                if (!_probeFailureLogged)
                {
                    _probeFailureLogged = true;
                    Log.Warn($"Bannerlord Coop session probe failed, treating as no session: {ex.GetType().Name}: {ex.Message}");
                }
                return (false, false);
            }
        }

        private static void EnsureBound()
        {
            if (_tryGetContainer != null)
            {
                return;
            }
            lock (BindLock)
            {
                if (_tryGetContainer != null)
                {
                    return;
                }
                // Cheap assembly-name lookup (no type scan) so this stays free when Coop isn't
                // installed, and re-checks each call until the assembly shows up - Coop's
                // GameInterface.dll may not be loaded yet at our OnSubModuleLoad.
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Assembly gameInterface = assemblies.FirstOrDefault(a => a.GetName().Name == GameInterfaceAssemblyName);
                Assembly common = assemblies.FirstOrDefault(a => a.GetName().Name == CommonAssemblyName);
                if (gameInterface == null || common == null)
                {
                    return;
                }
                try
                {
                    MethodInfo tryGetContainer = gameInterface.GetType(ContainerProviderTypeName, false)
                        ?.GetMethod("TryGetContainer", BindingFlags.Static | BindingFlags.Public);
                    PropertyInfo isServer = common.GetType(ModInformationTypeName, false)
                        ?.GetProperty("IsServer", BindingFlags.Static | BindingFlags.Public);
                    if (tryGetContainer == null || isServer == null)
                    {
                        Log.Warn("Bannerlord Coop assemblies found but GameInterface.ContainerProvider.TryGetContainer / Common.ModInformation.IsServer are missing - its API changed; HeavyCaravans will behave as if no coop session is active.");
                        return;
                    }
                    _isServer = isServer;
                    _tryGetContainer = tryGetContainer;
                    Log.Info("Bannerlord Coop detected (GameInterface.dll loaded); coop session role will be checked before Heavy Caravan actions.");
                }
                catch (Exception ex)
                {
                    Log.Warn($"Bannerlord Coop binding failed, treating as no session: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }
}
