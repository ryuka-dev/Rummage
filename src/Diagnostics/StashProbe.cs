using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.UI;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using PerfectRandom.Sulfur.Core.World;
using Ryuka.Sulfur.Rummage.Stashes;
using UnityEngine;

namespace Ryuka.Sulfur.Rummage.Diagnostics
{
    /// <summary>
    /// Development probe that records how the game names, sets up, saves and changes its stashes:
    ///
    /// - every key in the save's STASH table when a level spawns the player;
    /// - each PlayerStash.DoSetup: identifier, object, position, and whether its grid already existed;
    /// - on the first inventory open of each level: every PlayerStash in the scene, which of them a
    ///   weapon rack owns, and how scene stashes line up with save keys;
    /// - stash open and close: live grid contents against the saved copy;
    /// - how often each open grid raised onContentsChanged while the inventory was open.
    ///
    /// Read only: it never writes the save, the grids or any item.
    /// </summary>
    internal static class StashProbe
    {
        private const string Tag = "[StashProbe] ";

        private static readonly Dictionary<ItemGrid, GridWatch> Watches = new Dictionary<ItemGrid, GridWatch>();

        private static DiagnosticsConfig config;
        private static FieldInfo weaponRackStashField;
        private static int levelSerial;
        private static bool sceneDumpPending;

        private static bool Enabled
        {
            get { return config != null && config.LogStashProbe.Value; }
        }

        internal struct SetupSnapshot
        {
            public bool Captured;
            public string RegistryBefore;
            public int Frame;
        }

        private sealed class GridWatch
        {
            public string Label;
            public int Events;
            public ItemGrid.OnContentsChanged Handler;
        }

        public static void Initialize(DiagnosticsConfig diagnostics)
        {
            config = diagnostics;

            weaponRackStashField = typeof(WeaponRack).GetField("rackStash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (weaponRackStashField == null || weaponRackStashField.FieldType != typeof(PlayerStash))
            {
                weaponRackStashField = null;
                RummagePlugin.Log.LogWarning(Tag + "WeaponRack.rackStash was not found as a PlayerStash field; the stash probe cannot tell which stash belongs to a weapon rack.");
            }
        }

        public static void Shutdown()
        {
            foreach (KeyValuePair<ItemGrid, GridWatch> pair in Watches)
            {
                if (!ReferenceEquals(pair.Key, null))
                {
                    pair.Key.onContentsChanged -= pair.Value.Handler;
                }
            }

            Watches.Clear();
        }

        public static void ReportFailure(string hook, Exception ex)
        {
            RummagePlugin.Log.LogWarning(Tag + "probe failed in " + hook + ": " + ex);
        }

        // ---- player spawned -------------------------------------------------------------------

        public static void OnPlayerSpawned()
        {
            levelSerial++;
            sceneDumpPending = true;

            if (!Enabled)
            {
                return;
            }

            GameManager gameManager = StaticInstance<GameManager>.Instance;
            string environment = gameManager != null && gameManager.currentEnvironment != null
                ? gameManager.currentEnvironment.id.ToString()
                : "<none>";

            Info("level #" + levelSerial + " player spawned: environment=" + environment + ", at church=" + AtChurch());
            LogSaveKeys();
        }

        private static void LogSaveKeys()
        {
            SulfurSaveState save = SulfurSaveState.active;
            if (save == null)
            {
                Info("SulfurSaveState.active is null; no stash contents to list.");
                return;
            }

            if (save.STASH == null)
            {
                Info("SulfurSaveState.active.STASH is null.");
                return;
            }

            List<string> keys = new List<string>(save.STASH.Keys);
            keys.Sort(StringComparer.Ordinal);

            Info("save STASH table (save object #" + RuntimeHelpers.GetHashCode(save) + "): " + keys.Count + " key(s)");
            foreach (string key in keys)
            {
                SerializableList<InventoryData> entries = save.STASH[key];
                string identifier;
                string parsed = StashSaveKey.TryGetIdentifier(key, out identifier)
                    ? "identifier '" + identifier + "'"
                    : "unrecognised key shape";

                Info("  key '" + key + "' -> " + parsed + ", " + DescribeSaved(entries));
            }
        }

        // ---- stash setup ----------------------------------------------------------------------

        public static SetupSnapshot BeforeStashSetup(PlayerStash stash)
        {
            if (!Enabled)
            {
                return default;
            }

            InventoryUI ui = InventoryUi();
            ItemGrid registered = ui != null ? ui.GetStashItemGrid(stash.StashIdentifier ?? string.Empty) : null;

            return new SetupSnapshot
            {
                Captured = true,
                RegistryBefore = DescribeObject(registered),
                Frame = Time.frameCount,
            };
        }

        public static void AfterStashSetup(PlayerStash stash, SetupSnapshot before)
        {
            if (!Enabled || !before.Captured)
            {
                return;
            }

            InventoryUI ui = InventoryUi();
            string identifier = stash.StashIdentifier ?? string.Empty;
            ItemGrid registered = ui != null ? ui.GetStashItemGrid(identifier) : null;
            ItemGrid own = stash.Grid;

            Info("stash setup: identifier='" + identifier + "', object='" + PathOf(stash.transform) + "'"
                + ", position=" + Vector(stash.transform.position)
                + ", level #" + levelSerial + ", frame " + before.Frame
                + ", at church=" + AtChurch()
                + ", registered grid before=" + before.RegistryBefore
                + ", station grid is registered grid=" + ReferenceEquals(own, registered)
                + ", grid items=" + CountItems(own));
        }

        // ---- stash open / close ---------------------------------------------------------------

        public static void AfterStashOpened(PlayerStash stash)
        {
            if (!Enabled)
            {
                return;
            }

            string identifier = stash.StashIdentifier ?? string.Empty;
            Info("stash opened: identifier='" + identifier + "', live grid items=" + CountItems(stash.Grid)
                + ", saved copy: " + DescribeSaved(SavedEntries(identifier)));
        }

        public static void BeforeStashClosed(PlayerStash stash)
        {
            if (!Enabled)
            {
                return;
            }

            string identifier = stash.StashIdentifier ?? string.Empty;
            Info("stash closing: identifier='" + identifier + "', live grid items=" + CountItems(stash.Grid)
                + ", saved copy before save: " + DescribeSaved(SavedEntries(identifier)));
        }

        public static void AfterStashClosed(PlayerStash stash)
        {
            if (!Enabled)
            {
                return;
            }

            string identifier = stash.StashIdentifier ?? string.Empty;
            Info("stash closed: identifier='" + identifier + "', saved copy after save: " + DescribeSaved(SavedEntries(identifier)));
        }

        // ---- inventory open / close -----------------------------------------------------------

        public static void AfterInventoryStateSet(InventoryUI ui, UIState state)
        {
            if (!Enabled || ui == null)
            {
                return;
            }

            if (state == UIState.Shown && ui.IsOpened)
            {
                ServiceStation station = ui.CurrentServiceStation;
                Info("inventory opened: station=" + (station != null ? station.GetType().Name : "<none>")
                    + ", current stash identifier='" + ui.CurrentStashIdentifier + "'");

                if (sceneDumpPending)
                {
                    sceneDumpPending = false;
                    DumpScene(ui);
                }

                Watch(ui.PlayerBackpackGrid, "backpack");
                if (station != null && station.Grid != null)
                {
                    PlayerStash stash = station as PlayerStash;
                    Watch(station.Grid, stash != null ? "stash '" + stash.StashIdentifier + "'" : station.GetType().Name);
                }
            }
            else if (state != UIState.Shown)
            {
                FlushWatches();
            }
        }

        private static void DumpScene(InventoryUI ui)
        {
            PlayerStash[] stashes = UnityEngine.Object.FindObjectsByType<PlayerStash>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            WeaponRack[] racks = UnityEngine.Object.FindObjectsByType<WeaponRack>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Dictionary<PlayerStash, WeaponRack> rackOwners = new Dictionary<PlayerStash, WeaponRack>();
            if (weaponRackStashField != null)
            {
                foreach (WeaponRack rack in racks)
                {
                    PlayerStash owned = weaponRackStashField.GetValue(rack) as PlayerStash;
                    if (owned != null && !rackOwners.ContainsKey(owned))
                    {
                        rackOwners.Add(owned, rack);
                    }
                }
            }

            Array.Sort(stashes, (a, b) => string.CompareOrdinal(a.StashIdentifier, b.StashIdentifier));

            Info("scene dump for level #" + levelSerial + ", at church=" + AtChurch() + ": "
                + stashes.Length + " PlayerStash, " + racks.Length + " WeaponRack");

            HashSet<string> sceneKeys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> identifierUses = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (PlayerStash stash in stashes)
            {
                string identifier = stash.StashIdentifier ?? string.Empty;
                string key = StashSaveKey.FromIdentifier(identifier);
                sceneKeys.Add(key);

                int uses;
                identifierUses.TryGetValue(identifier, out uses);
                identifierUses[identifier] = uses + 1;

                WeaponRack rack;
                string rackText = weaponRackStashField == null
                    ? "unknown"
                    : rackOwners.TryGetValue(stash, out rack) ? "'" + PathOf(rack.transform) + "'" : "no";

                Info("  identifier='" + identifier + "', object='" + PathOf(stash.transform) + "'"
                    + ", position=" + Vector(stash.transform.position)
                    + ", active=" + stash.gameObject.activeInHierarchy
                    + ", weapon rack=" + rackText
                    + ", unlock item=" + DescribeUnlockItem(stash)
                    + ", size field=" + DescribeGridSize(stash.Grid)
                    + ", grid items=" + CountItems(stash.Grid)
                    + ", in InventoryUI.Stashes=" + (ui.Stashes != null && ui.Stashes.ContainsKey(identifier))
                    + ", save key '" + key + "': " + DescribeSaved(SavedEntries(identifier)));
            }

            foreach (KeyValuePair<string, int> pair in identifierUses)
            {
                if (pair.Value > 1)
                {
                    Info("  identifier '" + pair.Key + "' is shared by " + pair.Value + " PlayerStash objects in this scene.");
                }
            }

            SulfurSaveState save = SulfurSaveState.active;
            if (save != null && save.STASH != null)
            {
                foreach (string key in save.STASH.Keys)
                {
                    if (!sceneKeys.Contains(key))
                    {
                        Info("  save key '" + key + "' has no PlayerStash in this scene.");
                    }
                }
            }

            DumpUnlockables();
            DumpStampShop();

            if (ui.Stashes != null)
            {
                StringBuilder registered = new StringBuilder();
                foreach (KeyValuePair<string, ServiceStation> pair in ui.Stashes)
                {
                    if (registered.Length > 0)
                    {
                        registered.Append(", ");
                    }

                    registered.Append('\'').Append(pair.Key).Append("'=").Append(DescribeObject(pair.Value));
                }

                Info("  InventoryUI.Stashes: " + (registered.Length > 0 ? registered.ToString() : "<empty>"));
            }
        }

        /// <summary>
        /// Every UnlockableStorage in the scene with the service stations under it, so storages that
        /// are not a PlayerStash (item frames, wall mounts) can be tied to their unlock item too.
        /// </summary>
        private static void DumpUnlockables()
        {
            UnlockableStorage[] unlocks = UnityEngine.Object.FindObjectsByType<UnlockableStorage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Info("  UnlockableStorage objects: " + unlocks.Length);

            foreach (UnlockableStorage unlock in unlocks)
            {
                ItemDefinition item = unlock.itemBroughtToChurch;
                StringBuilder stations = new StringBuilder();
                foreach (ServiceStation station in unlock.GetComponentsInChildren<ServiceStation>(true))
                {
                    if (stations.Length > 0)
                    {
                        stations.Append(", ");
                    }

                    stations.Append(station.GetType().Name).Append(" '").Append(StationIdentifier(station)).Append('\'');
                }

                Info("  unlock '" + PathOf(unlock.transform) + "': item="
                    + (item != null ? "'" + item.name + "' ('" + item.LocalizedDisplayName + "')" : "unset")
                    + ", shown when unlocked=" + unlock.value
                    + ", stations=[" + stations + "]");
            }
        }

        /// <summary>The storage identifier of a station: a PlayerStash's own, or an item frame's frameIdentifier.</summary>
        private static string StationIdentifier(ServiceStation station)
        {
            PlayerStash stash = station as PlayerStash;
            if (stash != null)
            {
                return stash.StashIdentifier;
            }

            FieldInfo field = station.GetType().GetField("frameIdentifier", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field != null ? field.GetValue(station) as string : "<no identifier field>";
        }

        /// <summary>What the stamp shop sells, with the game's names, as evidence for storage names.</summary>
        private static void DumpStampShop()
        {
            FieldInfo itemsField = typeof(StampPurchaseStation).GetField("purchasableItems", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (itemsField == null)
            {
                Info("  StampPurchaseStation.purchasableItems was not found; the stamp shop list is not logged.");
                return;
            }

            foreach (StampPurchaseStation shop in UnityEngine.Object.FindObjectsByType<StampPurchaseStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                List<ItemDefinition> items = itemsField.GetValue(shop) as List<ItemDefinition>;
                StringBuilder list = new StringBuilder();
                if (items != null)
                {
                    foreach (ItemDefinition item in items)
                    {
                        if (list.Length > 0)
                        {
                            list.Append(", ");
                        }

                        list.Append(item != null ? "'" + item.name + "' ('" + item.LocalizedDisplayName + "')" : "<null>");
                    }
                }

                Info("  stamp shop '" + PathOf(shop.transform) + "': " + (items != null ? items.Count + " items: " + list : "no item list"));
            }
        }

        // ---- contents changed -----------------------------------------------------------------

        private static void Watch(ItemGrid grid, string label)
        {
            if (grid == null)
            {
                return;
            }

            GridWatch watch;
            if (Watches.TryGetValue(grid, out watch))
            {
                watch.Label = label;
                return;
            }

            watch = new GridWatch { Label = label };
            GridWatch captured = watch;
            watch.Handler = () => captured.Events++;
            grid.onContentsChanged += watch.Handler;
            Watches.Add(grid, watch);
        }

        private static void FlushWatches()
        {
            if (Watches.Count == 0)
            {
                return;
            }

            StringBuilder line = new StringBuilder();
            List<ItemGrid> destroyed = null;

            foreach (KeyValuePair<ItemGrid, GridWatch> pair in Watches)
            {
                if (pair.Key == null)
                {
                    (destroyed ??= new List<ItemGrid>()).Add(pair.Key);
                    continue;
                }

                if (pair.Value.Events == 0)
                {
                    continue;
                }

                if (line.Length > 0)
                {
                    line.Append(", ");
                }

                line.Append(pair.Value.Label).Append('=').Append(pair.Value.Events);
                pair.Value.Events = 0;
            }

            if (destroyed != null)
            {
                foreach (ItemGrid grid in destroyed)
                {
                    Watches.Remove(grid);
                }
            }

            if (line.Length > 0)
            {
                Info("onContentsChanged events since inventory opened: " + line);
            }
        }

        // ---- helpers --------------------------------------------------------------------------

        private static InventoryUI InventoryUi()
        {
            UIManager manager = StaticInstance<UIManager>.Instance;
            return manager != null ? manager.InventoryUI : null;
        }

        private static bool AtChurch()
        {
            GameManager gameManager = StaticInstance<GameManager>.Instance;
            return gameManager != null && gameManager.IsPlayerAtChurch;
        }

        private static SerializableList<InventoryData> SavedEntries(string identifier)
        {
            SulfurSaveState save = SulfurSaveState.active;
            if (save == null || save.STASH == null)
            {
                return null;
            }

            SerializableList<InventoryData> entries;
            return save.STASH.TryGetValue(StashSaveKey.FromIdentifier(identifier), out entries) ? entries : null;
        }

        private static string DescribeSaved(SerializableList<InventoryData> entries)
        {
            if (entries == null || entries.list == null)
            {
                return "absent";
            }

            int quantity = 0;
            foreach (InventoryData data in entries.list)
            {
                if (data != null)
                {
                    quantity += data.quantity;
                }
            }

            return entries.list.Count + " entries, total quantity " + quantity;
        }

        /// <summary>Distinct items occupying the grid's slots; a multi-cell item counts once.</summary>
        private static string CountItems(ItemGrid grid)
        {
            if (grid == null)
            {
                return "no grid";
            }

            GridSlot[,] slots = grid.slots;
            if (slots == null)
            {
                return "slots not built";
            }

            HashSet<InventoryItem> items = new HashSet<InventoryItem>();
            foreach (GridSlot slot in slots)
            {
                if (slot != null && slot.ItemInSlot != null)
                {
                    items.Add(slot.ItemInSlot);
                }
            }

            return items.Count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The item that unlocks this storage when brought to the church, if an UnlockableStorage
        /// sits on the stash or above it. Evidence for whether its name can label the stash.
        /// </summary>
        private static string DescribeUnlockItem(PlayerStash stash)
        {
            UnlockableStorage unlock = stash.GetComponentInParent<UnlockableStorage>(true);
            if (unlock == null)
            {
                return "none";
            }

            ItemDefinition item = unlock.itemBroughtToChurch;
            return item == null
                ? "unset"
                : "'" + item.name + "' ('" + item.LocalizedDisplayName + "', shown when unlocked=" + unlock.value + ")";
        }

        private static string DescribeGridSize(ItemGrid grid)
        {
            return grid == null ? "no grid" : grid.gridSize.x + "x" + grid.gridSize.y;
        }

        /// <summary>Tells a missing reference apart from one Unity has already destroyed.</summary>
        private static string DescribeObject(UnityEngine.Object value)
        {
            if (ReferenceEquals(value, null))
            {
                return "missing";
            }

            return value == null ? "destroyed" : "alive";
        }

        private static string PathOf(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            StringBuilder path = new StringBuilder(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path.Insert(0, parent.name + "/");
            }

            return path.ToString();
        }

        private static string Vector(Vector3 value)
        {
            return "(" + value.x.ToString("0.00", CultureInfo.InvariantCulture)
                + ", " + value.y.ToString("0.00", CultureInfo.InvariantCulture)
                + ", " + value.z.ToString("0.00", CultureInfo.InvariantCulture) + ")";
        }

        private static void Info(string message)
        {
            RummagePlugin.Log.LogInfo(Tag + message);
        }
    }
}
