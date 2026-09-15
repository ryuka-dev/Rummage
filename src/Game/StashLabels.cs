using System;
using System.Collections.Generic;
using Ryuka.Sulfur.Rummage.Localization;
using Ryuka.Sulfur.Rummage.Stashes;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>
    /// Player-facing names for stashes, from Rummage's own table. Identifiers the table does not
    /// know are shown as "Stash #n", numbered in the order this session first met them.
    /// </summary>
    internal static class StashLabels
    {
        private static readonly List<string> UnknownIdentifiers = new List<string>();

        public static string Backpack()
        {
            return RummageText.Get("summary.backpack", "Backpack");
        }

        public static string Get(string identifier)
        {
            StashName name = StashNaming.Resolve(identifier);

            switch (name.Kind)
            {
                case StashKind.Main:
                    return RummageText.Get("stash.main", "Stash");
                case StashKind.Wardrobe:
                    return RummageText.Get("stash.wardrobe", "Wardrobe");
                case StashKind.Suitcase:
                    return RummageText.Get("stash.suitcase", "Suitcase");
                case StashKind.Safe:
                    return RummageText.Get("stash.safe", "Safe");
                case StashKind.Sideboard:
                    return RummageText.Get("stash.sideboard", "Sideboard");
                case StashKind.Fridge:
                    return RummageText.Get("stash.fridge", "Fridge");
                case StashKind.SnakeBasket:
                    return RummageText.Get("stash.snakeBasket", "Snake basket");
                case StashKind.GunCabinet:
                    return RummageText.Get("stash.gunCabinet", "Gun cabinet");
                case StashKind.Coffin:
                    return RummageText.Get("stash.coffin", "Coffin");
                case StashKind.Toilet:
                    return RummageText.Get("stash.toilet", "Toilet");
                case StashKind.WeaponRack:
                    return Numbered(RummageText.Get("stash.weaponRack", "Weapon rack {0}"), "Weapon rack {0}", name.Number);
                case StashKind.ItemFrame:
                    return Numbered(RummageText.Get("stash.itemFrame", "Item frame {0}"), "Item frame {0}", name.Number);
                case StashKind.WallMount:
                    return Numbered(RummageText.Get("stash.wallMount", "Wall mount {0}"), "Wall mount {0}", name.Number);
                default:
                    return Numbered(RummageText.Get("stash.unknown", "Stash #{0}"), "Stash #{0}", UnknownNumber(identifier));
            }
        }

        private static int UnknownNumber(string identifier)
        {
            int index = UnknownIdentifiers.IndexOf(identifier ?? string.Empty);
            if (index < 0)
            {
                UnknownIdentifiers.Add(identifier ?? string.Empty);
                index = UnknownIdentifiers.Count - 1;
            }

            return index + 1;
        }

        /// <summary>A translation whose placeholder did not survive falls back to the English pattern.</summary>
        private static string Numbered(string pattern, string englishPattern, int number)
        {
            try
            {
                return string.Format(pattern, number);
            }
            catch (FormatException)
            {
                return string.Format(englishPattern, number);
            }
        }
    }
}
