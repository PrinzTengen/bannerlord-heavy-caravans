using System;
using System.Collections.Generic;
using System.IO;
using HeavyCaravans.Logging;

namespace HeavyCaravans.Config
{
    /// <summary>
    /// Reads HeavyCaravans.ini from two locations, in order (later wins):
    ///
    ///   1. Modules/HeavyCaravans/ModuleData/HeavyCaravans.ini - ships with the mod, documents the
    ///      defaults. Good for quick local testing, but if HeavyCaravans is installed via Steam
    ///      Workshop, this file lives inside the Workshop content folder and gets silently
    ///      overwritten whenever the mod updates.
    ///   2. Documents/Mount and Blade II Bannerlord/Configs/ModSettings/HeavyCaravans/HeavyCaravans.ini
    ///      - the same convention other mods in this install use for user settings (see e.g.
    ///      Configs/ModSettings/ButterLib). Lives entirely outside the game/mod installation, so it
    ///      survives both mod updates and reinstalls - this is where an end player should edit
    ///      values if they want the change to stick.
    ///
    /// Deliberately a hand-rolled INI reader instead of a dependency (XML/JSON lib): the whole file
    /// is a handful of "Key = Value" lines, and this keeps HeavyCaravans free of extra module
    /// dependencies for something this small. See phases/14-config.md.
    /// </summary>
    public static class HeavyCaravanSettings
    {
        /// <summary>
        /// Uniform multiplier applied to the initial troop count of every caravan (normal, elite
        /// and heavy alike) - see phases/definition-of-done.md. 1.0 = identical to vanilla/TAOM.
        /// </summary>
        public static float GlobalCaravanTroopMultiplier { get; private set; } = 1.0f;

        /// <summary>
        /// How many extra troops a Heavy Caravan can additionally carry on top of its (doubled)
        /// base troop capacity - see phases/09-bonuskapazitaet.md.
        /// </summary>
        public static int HeavyCaravanBonusCapacity { get; private set; } = 20;

        /// <summary>
        /// How much more a Heavy Caravan costs to form, relative to the (TAOM-adjusted) elite
        /// caravan price - see phases/06-dritte-caravan-option.md.
        /// </summary>
        public static float HeavyCaravanPriceMultiplier { get; private set; } = 2.0f;

        /// <summary>
        /// How much bigger a Heavy Caravan's troops/capacity are relative to a regular elite
        /// caravan's (which itself already has GlobalCaravanTroopMultiplier applied) - see
        /// phases/08-truppenstaerke.md ("TroopMultiplier"). Independent of
        /// GlobalCaravanTroopMultiplier: the two stack (elite base x Global, then x this).
        /// </summary>
        public static float HeavyCaravanTroopMultiplier { get; private set; } = 2.0f;

        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;

            bool anyFound = false;
            anyFound |= LoadFrom(GetModuleDataConfigPath());

            string userConfigPath = GetUserConfigPath();
            EnsureUserConfigTemplateExists(userConfigPath);
            anyFound |= LoadFrom(userConfigPath);

            if (!anyFound)
            {
                Log.Info("No config file found (ModuleData or user Configs/ModSettings), using built-in defaults.");
            }
            else
            {
                Log.Info($"Effective config: GlobalCaravanTroopMultiplier={GlobalCaravanTroopMultiplier}, HeavyCaravanTroopMultiplier={HeavyCaravanTroopMultiplier}, HeavyCaravanBonusCapacity={HeavyCaravanBonusCapacity}, HeavyCaravanPriceMultiplier={HeavyCaravanPriceMultiplier}.");
            }
        }

        /// <summary>Returns true if the file existed and was (at least attempted to be) read.</summary>
        private static bool LoadFrom(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                foreach (var (key, value) in ParseIniLines(File.ReadAllLines(path)))
                {
                    Apply(key, value);
                }
                Log.Info($"Loaded config from '{path}'.");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to read config file '{path}', ignoring it.", ex);
            }
            return true;
        }

        private static void Apply(string key, string value)
        {
            switch (key)
            {
                case "GlobalCaravanTroopMultiplier":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float multiplier) && multiplier > 0f)
                    {
                        GlobalCaravanTroopMultiplier = multiplier;
                    }
                    else
                    {
                        Log.Warn($"Ignoring invalid GlobalCaravanTroopMultiplier value '{value}' (must be a positive number).");
                    }
                    break;
                case "HeavyCaravanBonusCapacity":
                    if (int.TryParse(value, out int bonusCapacity) && bonusCapacity >= 0)
                    {
                        HeavyCaravanBonusCapacity = bonusCapacity;
                    }
                    else
                    {
                        Log.Warn($"Ignoring invalid HeavyCaravanBonusCapacity value '{value}' (must be a non-negative integer).");
                    }
                    break;
                case "HeavyCaravanPriceMultiplier":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float priceMultiplier) && priceMultiplier > 0f)
                    {
                        HeavyCaravanPriceMultiplier = priceMultiplier;
                    }
                    else
                    {
                        Log.Warn($"Ignoring invalid HeavyCaravanPriceMultiplier value '{value}' (must be a positive number).");
                    }
                    break;
                case "HeavyCaravanTroopMultiplier":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float troopMultiplier) && troopMultiplier > 0f)
                    {
                        HeavyCaravanTroopMultiplier = troopMultiplier;
                    }
                    else
                    {
                        Log.Warn($"Ignoring invalid HeavyCaravanTroopMultiplier value '{value}' (must be a positive number).");
                    }
                    break;
                default:
                    Log.Warn($"Ignoring unknown config key '{key}'.");
                    break;
            }
        }

        private static IEnumerable<(string Key, string Value)> ParseIniLines(IEnumerable<string> lines)
        {
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("["))
                {
                    continue;
                }
                int separatorIndex = line.IndexOf('=');
                if (separatorIndex < 0)
                {
                    continue;
                }
                yield return (line.Substring(0, separatorIndex).Trim(), line.Substring(separatorIndex + 1).Trim());
            }
        }

        private static string GetModuleDataConfigPath()
        {
            // HeavyCaravans.dll runs from Modules/HeavyCaravans/bin/Win64_Shipping_Client/ - relative
            // to the assembly's actual location, not a hard-coded absolute path, so this resolves
            // correctly whether the module folder is a normal install, a dev symlink/junction, or a
            // Steam Workshop content folder.
            string binDir = Path.GetDirectoryName(typeof(HeavyCaravanSettings).Assembly.Location) ?? ".";
            return Path.GetFullPath(Path.Combine(binDir, "..", "..", "ModuleData", "HeavyCaravans.ini"));
        }

        private static string GetUserConfigPath()
        {
            string configsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "Configs", "ModSettings", "HeavyCaravans");
            return Path.Combine(configsDir, "HeavyCaravans.ini");
        }

        /// <summary>
        /// Writes a starter file (current effective values, i.e. ModuleData's - or the built-in
        /// defaults - already applied) the first time a player looks here, so there is always
        /// something ready to edit without having to know the exact keys/format up front.
        /// </summary>
        private static void EnsureUserConfigTemplateExists(string path)
        {
            if (File.Exists(path))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllLines(path, new[]
                {
                    "# HeavyCaravans user settings.",
                    "# This file lives outside the mod's own folder, so it survives mod updates/reinstalls",
                    "# (unlike Modules/HeavyCaravans/ModuleData/HeavyCaravans.ini) - edit it here to keep your changes.",
                    "",
                    $"GlobalCaravanTroopMultiplier = {GlobalCaravanTroopMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    $"HeavyCaravanTroopMultiplier = {HeavyCaravanTroopMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    $"HeavyCaravanBonusCapacity = {HeavyCaravanBonusCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    $"HeavyCaravanPriceMultiplier = {HeavyCaravanPriceMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                });
                Log.Info($"Created user config template at '{path}'.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not create user config template at '{path}': {ex.Message}");
            }
        }
    }
}
