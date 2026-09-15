using System;
using HarmonyLib;
using PerfectRandom.Sulfur.Core.UI;
using PerfectRandom.Sulfur.Core.UI.Inventory;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>
    /// Relays InventoryUI.SetState, the game's single entry point for opening and closing the
    /// inventory, including when a stash or other station opens it.
    /// </summary>
    [HarmonyPatch(typeof(InventoryUI), nameof(InventoryUI.SetState))]
    internal static class InventoryStatePatch
    {
        internal static event Action<InventoryUI, UIState> StateSet;

        private static void Postfix(InventoryUI __instance, UIState state)
        {
            Action<InventoryUI, UIState> listeners = StateSet;
            if (listeners == null)
            {
                return;
            }

            try
            {
                listeners(__instance, state);
            }
            catch (Exception ex)
            {
                RummagePlugin.Log.LogError("Inventory open/close handling failed: " + ex);
            }
        }
    }
}
