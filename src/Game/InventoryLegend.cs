using System;
using BepInEx.Configuration;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.UI;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using UnityEngine;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>
    /// Hides the game's key hints above the backpack (rotate, use and so on), which the search bar
    /// covers, while the setting asks for it.
    ///
    /// The game switches between its keyboard and gamepad hint rows in InventoryUI.ToggleGamepadLegend
    /// whenever the inventory opens or input changes. The hook hides both rows after each call and
    /// remembers which row the game chose, so turning the setting off restores exactly that one.
    /// </summary>
    internal static class InventoryLegend
    {
        private static ConfigEntry<bool> hide;
        private static AccessTools.FieldRef<InventoryUI, GameObject> keyboardRow;
        private static AccessTools.FieldRef<InventoryUI, GameObject> gamepadRow;
        private static bool lastGamepadState;
        private static bool hasLastState;

        public static void Initialize(ConfigEntry<bool> hideSetting)
        {
            hide = hideSetting;

            try
            {
                keyboardRow = AccessTools.FieldRefAccess<InventoryUI, GameObject>("LegendKeyboard");
                gamepadRow = AccessTools.FieldRefAccess<InventoryUI, GameObject>("LegendGamepad");
            }
            catch (Exception ex)
            {
                keyboardRow = null;
                gamepadRow = null;
                RummagePlugin.Log.LogWarning("InventoryUI.LegendKeyboard / LegendGamepad were not found; the inventory key hints cannot be hidden. " + ex.Message);
            }

            hide.SettingChanged += OnSettingChanged;
        }

        public static void Shutdown()
        {
            if (hide != null)
            {
                hide.SettingChanged -= OnSettingChanged;
            }
        }

        public static void AfterToggle(InventoryUI ui, bool gamepadState)
        {
            lastGamepadState = gamepadState;
            hasLastState = true;

            if (hide != null && hide.Value)
            {
                HideRows(ui);
            }
        }

        private static void OnSettingChanged(object sender, EventArgs e)
        {
            UIManager manager = StaticInstance<UIManager>.Instance;
            InventoryUI ui = manager != null ? manager.InventoryUI : null;
            if (ui == null)
            {
                return;
            }

            try
            {
                if (hide.Value)
                {
                    HideRows(ui);
                }
                else if (hasLastState)
                {
                    // Let the game show the row it last chose; the hook leaves it alone now.
                    ui.ToggleGamepadLegend(lastGamepadState);
                }
            }
            catch (Exception ex)
            {
                RummagePlugin.Log.LogWarning("Applying the key hint setting failed: " + ex.Message);
            }
        }

        private static void HideRows(InventoryUI ui)
        {
            if (keyboardRow == null || gamepadRow == null || ui == null)
            {
                return;
            }

            GameObject keyboard = keyboardRow(ui);
            GameObject gamepad = gamepadRow(ui);

            if (keyboard != null && keyboard.activeSelf)
            {
                keyboard.SetActive(false);
            }

            if (gamepad != null && gamepad.activeSelf)
            {
                gamepad.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryUI), nameof(InventoryUI.ToggleGamepadLegend))]
    internal static class InventoryLegendPatch
    {
        private static void Postfix(InventoryUI __instance, bool state)
        {
            try
            {
                InventoryLegend.AfterToggle(__instance, state);
            }
            catch (Exception ex)
            {
                RummagePlugin.Log.LogWarning("Hiding the inventory key hints failed: " + ex.Message);
            }
        }
    }
}
