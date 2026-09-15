using System;
using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using Ryuka.Sulfur.Rummage.Filter;
using Ryuka.Sulfur.Rummage.Stashes;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>How many matching items one place holds.</summary>
    internal sealed class LocationCount
    {
        public LocationCount(string identifier, string label, int quantity, bool live)
        {
            Identifier = identifier;
            Label = label;
            Quantity = quantity;
            Live = live;
        }

        /// <summary>The stash identifier, or null for the backpack.</summary>
        public string Identifier { get; }

        public string Label { get; }

        public int Quantity { get; }

        /// <summary>Counted from a live grid rather than from the save. Diagnostics only.</summary>
        public bool Live { get; }
    }

    /// <summary>
    /// Counts the items matching the filter in the backpack and in every stash. Read only.
    ///
    /// A stash whose grid the game has set up (all of them while in the church, as the probe rounds
    /// showed) is counted from that live grid, which is what the player sees and is current even
    /// while the stash is open. Any other stash is counted from the save's STASH table, which the
    /// game refreshes when a stash closes and on level changes.
    /// </summary>
    internal static class StashInventoryCounter
    {
        private static readonly HashSet<InventoryItem> SeenItems = new HashSet<InventoryItem>();
        private static readonly HashSet<string> Identifiers = new HashSet<string>(StringComparer.Ordinal);

        public static List<LocationCount> Count(InventoryUI ui, InventoryFilter filter)
        {
            List<LocationCount> result = new List<LocationCount>();

            int backpack = CountGrid(ui.PlayerBackpackGrid, filter);
            if (backpack > 0)
            {
                result.Add(new LocationCount(null, StashLabels.Backpack(), backpack, true));
            }

            SulfurSaveState save = SulfurSaveState.active;
            CollectIdentifiers(ui, save);

            List<KeyValuePair<StashName, LocationCount>> stashes = new List<KeyValuePair<StashName, LocationCount>>();
            foreach (string identifier in Identifiers)
            {
                ItemGrid grid = ui.GetStashItemGrid(identifier);
                bool live = grid != null && grid.slots != null;
                int quantity = live ? CountGrid(grid, filter) : CountSaved(save, identifier, filter);

                if (quantity > 0)
                {
                    stashes.Add(new KeyValuePair<StashName, LocationCount>(
                        StashNaming.Resolve(identifier),
                        new LocationCount(identifier, StashLabels.Get(identifier), quantity, live)));
                }
            }

            stashes.Sort((a, b) =>
            {
                int byKind = a.Key.Kind.CompareTo(b.Key.Kind);
                if (byKind != 0)
                {
                    return byKind;
                }

                int byNumber = a.Key.Number.CompareTo(b.Key.Number);
                return byNumber != 0 ? byNumber : string.CompareOrdinal(a.Value.Identifier, b.Value.Identifier);
            });

            foreach (KeyValuePair<StashName, LocationCount> stash in stashes)
            {
                result.Add(stash.Value);
            }

            return result;
        }

        /// <summary>Every stash the save knows about plus every one the game registered this level.</summary>
        private static void CollectIdentifiers(InventoryUI ui, SulfurSaveState save)
        {
            Identifiers.Clear();

            if (save != null && save.STASH != null)
            {
                foreach (string key in save.STASH.Keys)
                {
                    string identifier;
                    if (StashSaveKey.TryGetIdentifier(key, out identifier))
                    {
                        Identifiers.Add(identifier);
                    }
                }
            }

            if (ui.Stashes != null)
            {
                foreach (string identifier in ui.Stashes.Keys)
                {
                    Identifiers.Add(identifier ?? string.Empty);
                }
            }
        }

        private static int CountGrid(ItemGrid grid, InventoryFilter filter)
        {
            if (grid == null || grid.slots == null)
            {
                return 0;
            }

            SeenItems.Clear();
            int quantity = 0;

            foreach (GridSlot slot in grid.slots)
            {
                InventoryItem item = slot != null ? slot.ItemInSlot : null;
                if (item == null || !SeenItems.Add(item))
                {
                    continue;
                }

                ItemSearchCatalog.Entry entry = ItemSearchCatalog.Get(item.itemDefinition);
                if (filter.Accepts(entry.Category, entry.Text))
                {
                    quantity += Math.Max(1, item.quantity);
                }
            }

            return quantity;
        }

        private static int CountSaved(SulfurSaveState save, string identifier, InventoryFilter filter)
        {
            if (save == null || save.STASH == null)
            {
                return 0;
            }

            SerializableList<InventoryData> entries;
            if (!save.STASH.TryGetValue(StashSaveKey.FromIdentifier(identifier), out entries) || entries == null || entries.list == null)
            {
                return 0;
            }

            int quantity = 0;
            foreach (InventoryData data in entries.list)
            {
                if (data == null || !data.id.IsValid)
                {
                    continue;
                }

                ItemDefinition definition = data.id.GetAsset();
                if (definition == null)
                {
                    continue;
                }

                ItemSearchCatalog.Entry entry = ItemSearchCatalog.Get(definition);
                if (filter.Accepts(entry.Category, entry.Text))
                {
                    quantity += Math.Max(1, data.quantity);
                }
            }

            return quantity;
        }
    }
}
