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
    /// is not focused, run untouched; the mouse scroll wheel is the exception, as it shares the
    /// rotate handler with Space and R.
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
        // Keyboard bindings of the Inventory action map in the game's input asset (build 26327540),
        // and the InputReader handler each one reaches:
        //   RotateItem: Space, R, mouse scroll    -> InventoryRotateCurrentItem
        //   SortGrid: P                           -> InventorySortCurrentGrid
        //   Use: Enter                            -> InventoryUse
        //   DropItem: Slash                       -> InventoryDropOrAlternativeUse
        //     (presses a service station's button when one is available, otherwise drops the
        //     hovered item; replaced InventoryDrop and InventoryAlternativeUse)
        //   MoveItem: Backslash                   -> InventorySubmit
        //   Navigate: arrow keys                  -> NavigateInventory
        //   CloseInventory: Tab, I; CloseInventoryAlternative: Escape, Backspace
        //                                         -> InventoryCancel (InventoryCancelGuardPatch)
        // Left alone: CtrlModifier (Ctrl, held as a click modifier) and the mouse pointer actions.
        private static readonly string[] Handlers =
        {
            "InventoryRotateCurrentItem",
            "InventorySortCurrentGrid",
            "InventoryUse",
            "InventoryDropOrAlternativeUse",
            "InventorySubmit",
            "NavigateInventory",
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodBase> found = new List<MethodBase>(Handlers.Length);
            List<string> missing = new List<string>();
            foreach (string name in Handlers)
            {
                MethodInfo method = AccessTools.Method(typeof(InputReader), name, new[] { typeof(InputAction.CallbackContext) });
                if (method == null)
                {
                    missing.Add(name);
                    continue;
                }

                found.Add(method);
            }

            // One line either way, so a handler renamed by a game update shows up in the startup log.
            string summary = "Typing guard covers " + found.Count + " of " + Handlers.Length + " InputReader inventory key handlers";
            if (missing.Count == 0)
            {
                RummagePlugin.Log.LogInfo(summary + ".");
            }
            else
            {
                RummagePlugin.Log.LogWarning(summary + "; not found: " + string.Join(", ", missing.ToArray())
                    + ". Their keys may act on the inventory while typing in the search box.");
            }

            return found;
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
