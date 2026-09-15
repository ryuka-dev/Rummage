using BepInEx.Configuration;

namespace Ryuka.Sulfur.Rummage.Config
{
    /// <summary>Where the search results panel sits relative to the search bar.</summary>
    internal enum ResultsPosition
    {
        /// <summary>Under the search bar, over the top of the backpack; grows with its list.</summary>
        BelowSearchBar,

        /// <summary>Above the search bar, in a shorter scrolling panel.</summary>
        AboveSearchBar,
    }

    /// <summary>Player-facing layout choices. Read live, so a change applies without restarting.</summary>
    internal sealed class DisplayConfig
    {
        public const string Section = "Display";

        public DisplayConfig(ConfigFile config)
        {
            ResultsPlacement = config.Bind(
                Section,
                "ResultsPosition",
                ResultsPosition.BelowSearchBar,
                "Where the search results sit. BelowSearchBar: over the top of the backpack, growing with the list up to the bottom of the screen. AboveSearchBar: above the search bar, in a shorter scrolling panel.");
        }

        public ConfigEntry<ResultsPosition> ResultsPlacement { get; }
    }
}
