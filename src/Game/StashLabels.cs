using System;
using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using Ryuka.Sulfur.Rummage.Localization;
using Ryuka.Sulfur.Rummage.Stashes;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>
    /// Player-facing names for stashes: the game's own name for the storage's unlock item where
    /// that link is verified, otherwise Rummage's own table. Identifiers the table does not know are
    /// shown as "Stash #n", numbered in the order this session first met them.
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
                    return Official(ItemIds.Item_Wardrobe) ?? RummageText.Get("stash.wardrobe", "Wardrobe");
                case StashKind.Suitcase:
                    return Official(ItemIds.Item_Suitcase) ?? RummageText.Get("stash.suitcase", "Suitcase");
                case StashKind.Safe:
                    return Official(ItemIds.Item_Safe) ?? RummageText.Get("stash.safe", "Safe");
                case StashKind.Sideboard:
                    return Official(ItemIds.Item_ChestOfDrawers) ?? RummageText.Get("stash.sideboard", "Sideboard");
                case StashKind.Fridge:
                    return Official(ItemIds.Item_Refrigerator) ?? RummageText.Get("stash.fridge", "Fridge");
                case StashKind.SnakeBasket:
                    return Official(ItemIds.Item_SnakeBasket) ?? RummageText.Get("stash.snakeBasket", "Snake basket");
                case StashKind.GunCabinet:
                    return Official(ItemIds.Item_GunCabinet) ?? RummageText.Get("stash.gunCabinet", "Gun cabinet");
                case StashKind.Coffin:
                    return Official(ItemIds.Item_Coffin) ?? RummageText.Get("stash.coffin", "Coffin");
                case StashKind.Toilet:
                    return Official(ItemIds.Item_Toilet) ?? RummageText.Get("stash.toilet", "Toilet");
                case StashKind.WeaponRack:
                    string rack = Official(WeaponRackItem(name.Number));
                    return rack != null
                        ? rack + " " + name.Number
                        : Numbered(RummageText.Get("stash.weaponRack", "Weapon rack {0}"), "Weapon rack {0}", name.Number);
                case StashKind.ItemFrame:
                    return Numbered(RummageText.Get("stash.itemFrame", "Item frame {0}"), "Item frame {0}", name.Number);
                case StashKind.WallMount:
                    return Numbered(RummageText.Get("stash.wallMount", "Wall mount {0}"), "Wall mount {0}", name.Number);
                default:
                    return Numbered(RummageText.Get("stash.unknown", "Stash #{0}"), "Stash #{0}", UnknownNumber(identifier));
            }
        }

        /// <summary>
        /// The game's own name for the item that unlocks a storage, in the current language. Only
        /// used where probe round Misc/27 showed that item on the stash's UnlockableStorage.
        /// Null when the item database is not available, so the table name is used instead.
        /// </summary>
        private static string Official(ItemId id)
        {
            if (!id.IsValid)
            {
                return null;
            }

            try
            {
                ItemDefinition item = id.GetAsset();
                string name = item != null ? item.LocalizedDisplayName : null;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>WeaponRack0..2 are unlocked by Item_WeaponRack0..2; all three share one name.</summary>
        private static ItemId WeaponRackItem(int number)
        {
            switch (number)
            {
                case 1:
                    return ItemIds.Item_WeaponRack0;
                case 2:
                    return ItemIds.Item_WeaponRack1;
                case 3:
                    return ItemIds.Item_WeaponRack2;
                default:
                    return ItemId.None;
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
