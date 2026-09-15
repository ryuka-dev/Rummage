using BepInEx.Configuration;

namespace Ryuka.Sulfur.Rummage.Diagnostics
{
    /// <summary>
    /// Log switches. Nothing here changes what the mod does; each entry only decides whether a
    /// probe writes to the BepInEx log.
    /// </summary>
    internal sealed class DiagnosticsConfig
    {
        public const string Section = "Debug";

        public DiagnosticsConfig(ConfigFile config)
        {
            LogStashProbe = config.Bind(
                Section,
                "LogStashProbe",
                true,
                "Writes stash identifiers, save keys, setup timing and item change counts to the BepInEx log. Does not change how the mod works.");

            LogEnchantmentCatalog = config.Bind(
                Section,
                "LogEnchantmentCatalog",
                true,
                "Writes every oil and other enchantment item the game defines to the BepInEx log once per session. Does not change how the mod works.");

            LogSearchFilter = config.Bind(
                Section,
                "LogSearchFilter",
                true,
                "Writes where the search bar is placed and how many items match each filter change to the BepInEx log. Does not change how the mod works.");

            LogGameTerms = config.Bind(
                Section,
                "LogGameTerms",
                true,
                "Once per session, writes the game's localization terms that could name Rummage's buttons and labels to the BepInEx log. Does not change how the mod works.");
        }

        public ConfigEntry<bool> LogSearchFilter { get; }

        public ConfigEntry<bool> LogGameTerms { get; }

        public ConfigEntry<bool> LogStashProbe { get; }

        public ConfigEntry<bool> LogEnchantmentCatalog { get; }
    }
}
