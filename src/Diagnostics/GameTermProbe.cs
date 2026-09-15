using System;
using System.Collections.Generic;
using System.Text;
using I2.Loc;
using PerfectRandom.Sulfur.Core.UI;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using Ryuka.Sulfur.Rummage.Localization;

namespace Ryuka.Sulfur.Rummage.Diagnostics
{
    /// <summary>
    /// Development probe that searches the game's own localisation terms for words Rummage shows
    /// (Backpack, Weapons, Oils, Clear, ...), once per session after the language has loaded. The
    /// term keys it finds let those labels come from the game's translations instead of Rummage's.
    /// </summary>
    internal static class GameTermProbe
    {
        private const string Tag = "[GameTerms] ";
        private const int MaximumLines = 400;

        private static readonly HashSet<string> EnglishValues = new HashSet<string>(StringComparer.Ordinal)
        {
            "all", "clear", "clear all", "search", "filter", "sort",
            "oil", "oils", "scroll", "scrolls", "enchantment", "enchantments",
            "weapon", "weapons", "attachment", "attachments", "consumable", "consumables",
            "item", "items", "backpack", "bag", "inventory", "storage", "stash", "chest", "priest's chest",
            "no results", "no matches", "none", "empty", "category", "categories",
        };

        private static readonly string[] KeyFragments =
        {
            "stash", "backpack", "inventory", "itemtype", "category", "priest", "search", "filter", "storage",
        };

        private static DiagnosticsConfig config;
        private static bool done;

        public static void Initialize(DiagnosticsConfig diagnostics)
        {
            config = diagnostics;
        }

        public static void OnInventoryStateSet(InventoryUI ui, UIState state)
        {
            if (done || state != UIState.Shown || config == null || !config.LogGameTerms.Value || GameLanguage.CurrentCode() == null)
            {
                return;
            }

            done = true;

            try
            {
                Dump();
            }
            catch (Exception ex)
            {
                RummagePlugin.Log.LogWarning(Tag + "term search failed: " + ex);
            }
        }

        private static void Dump()
        {
            List<LanguageSourceData> sources = LocalizationManager.Sources;
            string currentCode = LocalizationManager.CurrentLanguageCode;
            RummagePlugin.Log.LogInfo(Tag + (sources != null ? sources.Count : 0) + " localisation source(s); current language " + currentCode);
            if (sources == null)
            {
                return;
            }

            int lines = 0;
            int matches = 0;

            foreach (LanguageSourceData source in sources)
            {
                if (source == null || source.mTerms == null)
                {
                    continue;
                }

                int english = source.GetLanguageIndexFromCode("en", true, false);
                int current = source.GetLanguageIndexFromCode(currentCode, true, false);
                RummagePlugin.Log.LogInfo(Tag + "source with " + source.mTerms.Count + " terms, English column " + english + ", current column " + current);

                foreach (TermData term in source.mTerms)
                {
                    if (term == null || string.IsNullOrEmpty(term.Term))
                    {
                        continue;
                    }

                    string englishValue = Column(term, english);
                    if (!IsCandidate(term.Term, englishValue))
                    {
                        continue;
                    }

                    matches++;
                    if (lines >= MaximumLines)
                    {
                        continue;
                    }

                    lines++;
                    RummagePlugin.Log.LogInfo(Tag + "'" + term.Term + "' en='" + OneLine(englishValue) + "' " + currentCode + "='" + OneLine(Column(term, current)) + "'");
                }
            }

            RummagePlugin.Log.LogInfo(Tag + matches + " candidate term(s)" + (matches > lines ? ", first " + lines + " listed" : string.Empty) + ".");
        }

        private static bool IsCandidate(string key, string englishValue)
        {
            string lowerKey = key.ToLowerInvariant();
            foreach (string fragment in KeyFragments)
            {
                if (lowerKey.Contains(fragment))
                {
                    return true;
                }
            }

            return englishValue != null && EnglishValues.Contains(Normalise(englishValue));
        }

        private static string Column(TermData term, int index)
        {
            return term.Languages != null && index >= 0 && index < term.Languages.Length ? term.Languages[index] : null;
        }

        private static string Normalise(string value)
        {
            StringBuilder plain = new StringBuilder(value.Length);
            bool inTag = false;
            foreach (char character in value)
            {
                if (character == '<')
                {
                    inTag = true;
                }
                else if (character == '>' && inTag)
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    plain.Append(character);
                }
            }

            return plain.ToString().Trim().TrimEnd(':').ToLowerInvariant();
        }

        private static string OneLine(string value)
        {
            return value == null ? "<null>" : value.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
