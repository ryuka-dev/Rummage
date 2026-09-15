using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Ryuka.Sulfur.Rummage.Diagnostics;
using Ryuka.Sulfur.Rummage.Game;

namespace Ryuka.Sulfur.Rummage
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class RummagePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ryuka.sulfur.rummage";
        public const string PluginName = "Rummage";
        public const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        private Harmony harmony;
        private PlayerSpawnSource playerSpawnSource;

        private void Awake()
        {
            Log = Logger;

            DiagnosticsConfig diagnostics = new DiagnosticsConfig(Config);
            StashProbe.Initialize(diagnostics);
            EnchantmentCatalogProbe.Initialize(diagnostics);

            playerSpawnSource = new PlayerSpawnSource();
            playerSpawnSource.PlayerSpawned += StashProbe.OnPlayerSpawned;
            playerSpawnSource.PlayerSpawned += EnchantmentCatalogProbe.OnPlayerSpawned;

            harmony = new Harmony(PluginGuid);
            int failed = 0;
            failed += Patch(typeof(PlayerStashDoSetupProbePatch), "stash setup probe");
            failed += Patch(typeof(PlayerStashEnterProbePatch), "stash open probe");
            failed += Patch(typeof(PlayerStashExitProbePatch), "stash close probe");
            failed += Patch(typeof(InventoryUISetStateProbePatch), "inventory open/close probe");

            Log.LogInfo(PluginName + " " + PluginVersion + " loaded" + (failed == 0 ? "." : " with " + failed + " patch failure(s), see above."));
        }

        private void Update()
        {
            playerSpawnSource?.Tick();
        }

        private void OnDestroy()
        {
            if (playerSpawnSource != null)
            {
                playerSpawnSource.Dispose();
                playerSpawnSource = null;
            }

            StashProbe.Shutdown();

            if (harmony != null)
            {
                harmony.UnpatchSelf();
                harmony = null;
            }
        }

        /// <summary>Applies one attribute-declared patch class; returns 1 when it failed.</summary>
        private int Patch(Type patchClass, string capability)
        {
            try
            {
                harmony.CreateClassProcessor(patchClass).Patch();
                return 0;
            }
            catch (Exception ex)
            {
                Log.LogError("Could not install " + patchClass.Name + ", the " + capability + " is disabled. "
                    + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }
    }
}
