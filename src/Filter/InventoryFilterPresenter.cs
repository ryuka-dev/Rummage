using System;
using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using Ryuka.Sulfur.Rummage.Diagnostics;
using Ryuka.Sulfur.Rummage.Game;

namespace Ryuka.Sulfur.Rummage.Filter
{
    /// <summary>
    /// Shows the filter on the open grids: the backpack and, when one is open, the stash.
    ///
    /// Work is only marked due by the real triggers - the filter changing, the inventory opening,
    /// or an open grid reporting its contents changed - and done at most once per frame, because a
    /// grid can report hundreds of changes in one frame while it is sorted.
    /// </summary>
    internal sealed class InventoryFilterPresenter : IDisposable
    {
        private readonly InventoryFilter filter;
        private readonly DiagnosticsConfig diagnostics;
        private readonly ItemGrid.OnContentsChanged onContentsChanged;
        private readonly HashSet<InventoryItem> seenItems = new HashSet<InventoryItem>();

        /// <summary>Items left marked by the previous pass; any not reached again are reset.</summary>
        private HashSet<ItemFilterVisual> marked = new HashSet<ItemFilterVisual>();
        private HashSet<ItemFilterVisual> markedThisPass = new HashSet<ItemFilterVisual>();

        private ItemGrid watchedBackpack;
        private ItemGrid watchedStash;
        private bool dirty;
        private bool failureReported;

        public InventoryFilterPresenter(InventoryFilter filter, DiagnosticsConfig diagnostics)
        {
            this.filter = filter;
            this.diagnostics = diagnostics;
            onContentsChanged = MarkDirty;
            filter.Changed += MarkDirty;
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        public void OnInventoryShown(InventoryUI ui)
        {
            Watch(ui.PlayerBackpackGrid, ref watchedBackpack);
            Watch(StashGrid(ui), ref watchedStash);
            dirty = true;
        }

        public void OnInventoryHidden()
        {
            Unwatch(ref watchedBackpack);
            Unwatch(ref watchedStash);
        }

        public void Tick(InventoryUI ui)
        {
            if (!dirty)
            {
                return;
            }

            dirty = false;

            try
            {
                Apply(ui);
            }
            catch (Exception ex)
            {
                if (!failureReported)
                {
                    failureReported = true;
                    RummagePlugin.Log.LogError("Applying the inventory filter failed; items may keep a stale highlight until the filter changes. " + ex);
                }
            }
        }

        public void Dispose()
        {
            filter.Changed -= MarkDirty;
            OnInventoryHidden();

            foreach (ItemFilterVisual visual in marked)
            {
                if (visual != null)
                {
                    visual.Show(FilterMark.Neutral);
                }
            }

            marked.Clear();
        }

        private void Apply(InventoryUI ui)
        {
            seenItems.Clear();
            markedThisPass.Clear();

            ItemGrid stash = StashGrid(ui);
            Watch(ui.PlayerBackpackGrid, ref watchedBackpack);
            Watch(stash, ref watchedStash);

            int backpackItems;
            int backpackMatches = ApplyGrid(ui.PlayerBackpackGrid, out backpackItems);
            int stashItems;
            int stashMatches = ApplyGrid(stash, out stashItems);

            // Items that left the open grids (equipped, dropped, or in a stash that closed) since
            // the last pass go back to looking untouched.
            foreach (ItemFilterVisual visual in marked)
            {
                if (visual != null && !markedThisPass.Contains(visual))
                {
                    visual.Show(FilterMark.Neutral);
                }
            }

            HashSet<ItemFilterVisual> previous = marked;
            marked = markedThisPass;
            markedThisPass = previous;

            if (diagnostics.LogSearchFilter.Value)
            {
                PlayerStash station = ui.CurrentServiceStation as PlayerStash;
                RummagePlugin.Log.LogInfo("[SearchFilter] text='" + filter.Text + "', category=" + filter.Category
                    + ", active=" + filter.IsActive
                    + ": backpack " + backpackMatches + "/" + backpackItems
                    + (station != null ? ", stash '" + station.StashIdentifier + "' " + stashMatches + "/" + stashItems : string.Empty));
            }
        }

        /// <summary>Marks every item in the grid; returns how many match.</summary>
        private int ApplyGrid(ItemGrid grid, out int items)
        {
            items = 0;
            if (grid == null || grid.slots == null)
            {
                return 0;
            }

            int matches = 0;
            bool active = filter.IsActive;

            foreach (GridSlot slot in grid.slots)
            {
                InventoryItem item = slot != null ? slot.ItemInSlot : null;
                if (item == null || !seenItems.Add(item))
                {
                    continue;
                }

                items++;

                FilterMark mark = FilterMark.Neutral;
                if (active)
                {
                    ItemSearchCatalog.Entry entry = ItemSearchCatalog.Get(item.itemDefinition);
                    bool match = filter.Accepts(entry.Category, entry.Text);
                    mark = match ? FilterMark.Match : FilterMark.Dimmed;
                    if (match)
                    {
                        matches++;
                    }
                }

                if (mark == FilterMark.Neutral)
                {
                    // Never add anything to an item while no filter is active.
                    ItemFilterVisual existing = item.GetComponent<ItemFilterVisual>();
                    if (existing != null && existing.Mark != FilterMark.Neutral)
                    {
                        existing.Show(FilterMark.Neutral);
                    }

                    continue;
                }

                ItemFilterVisual visual = ItemFilterVisual.Attach(item);
                visual.Show(mark);
                markedThisPass.Add(visual);
            }

            return matches;
        }

        private static ItemGrid StashGrid(InventoryUI ui)
        {
            PlayerStash stash = ui.CurrentServiceStation as PlayerStash;
            return stash != null ? stash.Grid : null;
        }

        private void Watch(ItemGrid grid, ref ItemGrid watched)
        {
            if (ReferenceEquals(grid, watched))
            {
                return;
            }

            Unwatch(ref watched);

            if (grid != null)
            {
                grid.onContentsChanged += onContentsChanged;
                watched = grid;
            }
        }

        private void Unwatch(ref ItemGrid watched)
        {
            if (!ReferenceEquals(watched, null))
            {
                watched.onContentsChanged -= onContentsChanged;
                watched = null;
            }
        }
    }
}
