using System;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.UI;
using PerfectRandom.Sulfur.Core.UI.Inventory;

namespace Ryuka.Sulfur.Rummage.Diagnostics
{
    // Every hook here only observes. A probe failure is caught and logged; it never reaches the game.

    [HarmonyPatch(typeof(PlayerStash), nameof(PlayerStash.DoSetup))]
    internal static class PlayerStashDoSetupProbePatch
    {
        private static void Prefix(PlayerStash __instance, out StashProbe.SetupSnapshot __state)
        {
            __state = default;
            try
            {
                __state = StashProbe.BeforeStashSetup(__instance);
            }
            catch (Exception ex)
            {
                StashProbe.ReportFailure("before stash setup", ex);
            }
        }

        private static void Postfix(PlayerStash __instance, StashProbe.SetupSnapshot __state)
        {
            try
            {
                StashProbe.AfterStashSetup(__instance, __state);
            }
            catch (Exception ex)
            {
                StashProbe.ReportFailure("after stash setup", ex);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerStash), nameof(PlayerStash.Enter), new Type[0])]
    internal static class PlayerStashEnterProbePatch
    {
        private static void Postfix(PlayerStash __instance)
        {
            try
            {
                StashProbe.AfterStashOpened(__instance);
            }
            catch (Exception ex)
            {
                StashProbe.ReportFailure("stash opened", ex);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerStash), nameof(PlayerStash.Exit))]
    internal static class PlayerStashExitProbePatch
    {
        private static void Prefix(PlayerStash __instance)
        {
            try
            {
                StashProbe.BeforeStashClosed(__instance);
            }
            catch (Exception ex)
            {
                StashProbe.ReportFailure("before stash closed", ex);
            }
        }

        private static void Postfix(PlayerStash __instance)
        {
            try
            {
                StashProbe.AfterStashClosed(__instance);
            }
            catch (Exception ex)
            {
                StashProbe.ReportFailure("after stash closed", ex);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryUI), nameof(InventoryUI.SetState))]
    internal static class InventoryUISetStateProbePatch
    {
        private static void Postfix(InventoryUI __instance, UIState state)
        {
            try
            {
                StashProbe.AfterInventoryStateSet(__instance, state);
            }
            catch (Exception ex)
            {
                StashProbe.ReportFailure("inventory state set", ex);
            }
        }
    }
}
