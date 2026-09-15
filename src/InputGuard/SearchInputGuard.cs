using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PerfectRandom.Sulfur.Core.Input;
using UnityEngine.InputSystem;

namespace Ryuka.Sulfur.Rummage.InputGuard
{
    /// <summary>
    /// Keeps the game's inventory key bindings from acting on keys typed into the search box.
    ///
    /// Only the InputReader handlers of inventory actions with a keyboard binding a player types
    /// are covered, and only while the box has focus. Mouse input, and every handler while the box
    /// is not focused, run untouched.
    /// </summary>
    internal static class SearchInputGuard
    {
        public static Func<bool> IsTyping;
        public static Action StopTyping;

        internal static bool Typing()
        {
            try
            {
                return IsTyping != null && IsTyping();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    [HarmonyPatch]
    internal static class InventoryKeyGuardPatch
    {
        // Keyboard bindings in the game's input asset (build 25257948):
        //   RotateItem: Space, R      SortGrid: P        Use: Enter
        //   DropItem: Slash           MoveItem: Backslash (handled by InventorySubmit)
        //   Navigate: arrow keys
        private static readonly string[] Handlers =
        {
            "InventoryRotateCurrentItem",
            "InventorySortCurrentGrid",
            "InventoryUse",
            "InventoryDrop",
            "InventorySubmit",
            "NavigateInventory",
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in Handlers)
            {
                MethodInfo method = AccessTools.Method(typeof(InputReader), name, new[] { typeof(InputAction.CallbackContext) });
                if (method == null)
                {
                    RummagePlugin.Log.LogWarning("InputReader." + name + " was not found; its key may act on the inventory while typing in the search box.");
                    continue;
                }

                yield return method;
            }
        }

        private static bool Prefix()
        {
            return !SearchInputGuard.Typing();
        }
    }

    /// <summary>
    /// CloseInventory (Tab, I) and CloseInventoryAlternative (Escape, Backspace) share this handler.
    /// While typing, Escape leaves the search box and the others are just text.
    /// </summary>
    [HarmonyPatch(typeof(InputReader), "InventoryCancel")]
    internal static class InventoryCancelGuardPatch
    {
        private static bool Prefix(InputAction.CallbackContext context)
        {
            if (!SearchInputGuard.Typing())
            {
                return true;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && context.control == keyboard.escapeKey)
            {
                SearchInputGuard.StopTyping?.Invoke();
            }

            return false;
        }
    }
}
