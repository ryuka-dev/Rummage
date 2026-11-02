using System;
using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.UI;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using Ryuka.Sulfur.Rummage.Config;
using Ryuka.Sulfur.Rummage.Diagnostics;
using Ryuka.Sulfur.Rummage.Game;
using Ryuka.Sulfur.Rummage.InputGuard;
using Ryuka.Sulfur.Rummage.UserInterface;
using UnityEngine.InputSystem;

namespace Ryuka.Sulfur.Rummage.Filter
{
    /// <summary>
    /// Connects the filter, its presentation on the grids, the search bar, the world markers and the
    /// typing guard to the inventory's and the level's lifecycle.
    /// </summary>
    internal sealed class RummageController : IDisposable
    {
        private readonly InventoryFilter filter = new InventoryFilter();
        private readonly InventoryFilterPresenter presenter;
        private readonly SearchBar searchBar;
        private readonly WorldMarkers worldMarkers;
        private bool countsStale;
        private bool tickFailureReported;

        public RummageController(DiagnosticsConfig diagnostics, DisplayConfig display)
        {
            presenter = new InventoryFilterPresenter(filter, diagnostics);
            searchBar = new SearchBar(filter, diagnostics, display);
            worldMarkers = new WorldMarkers(diagnostics);
            presenter.SummaryChanged += PublishCounts;

            SearchInputGuard.IsTyping = () => searchBar.IsFocused;
            SearchInputGuard.StopTyping = searchBar.Unfocus;
        }

        public void OnInventoryStateSet(InventoryUI ui, UIState state)
        {
            if (ui == null)
            {
                return;
            }

            if (state == UIState.Shown)
            {
                if (ui.IsOpened)
                {
                    presenter.OnInventoryShown(ui);
                }
            }
            else
            {
                presenter.OnInventoryHidden();
                searchBar.OnInventoryHidden();
            }
        }

        /// <summary>
        /// A level was built. Its storages are new objects, and the counts are refreshed even with
        /// the inventory closed so the world markers are right from the start.
        /// </summary>
        public void OnPlayerSpawned()
        {
            worldMarkers.OnPlayerSpawned();
            countsStale = true;
        }

        public void OnLanguageChanged()
        {
            // State first, so a failure in the UI refresh cannot leave stale names behind.
            ItemSearchCatalog.Invalidate();
            worldMarkers.OnLanguageChanged();
            presenter.MarkDirty();
            countsStale = true;
            searchBar.RefreshTexts();
        }

        public void Tick()
        {
            UIManager manager = StaticInstance<UIManager>.Instance;
            InventoryUI ui = manager != null ? manager.InventoryUI : null;
            if (ui == null)
            {
                return;
            }

            try
            {
                if (countsStale)
                {
                    countsStale = false;
                    PublishCounts(filter.IsActive ? StashInventoryCounter.Count(ui, filter) : null);
                }

                if (!ui.IsOpened)
                {
                    return;
                }

                searchBar.Tick(ui);

                if (!searchBar.IsFocused && searchBar.IsVisible && FocusShortcutPressed())
                {
                    searchBar.FocusInput();
                }

                if (!searchBar.IsFocused && GamepadCategoryPressed(manager))
                {
                    filter.CycleCategory();
                }

                presenter.Tick(ui);
            }
            catch (Exception ex)
            {
                if (!tickFailureReported)
                {
                    tickFailureReported = true;
                    RummagePlugin.Log.LogError("Search and filter update failed; further failures are not logged. " + ex);
                }
            }
        }

        public void LateTick()
        {
            worldMarkers.LateTick();
        }

        public void Dispose()
        {
            SearchInputGuard.IsTyping = null;
            SearchInputGuard.StopTyping = null;
            presenter.SummaryChanged -= PublishCounts;
            presenter.Dispose();
            searchBar.Dispose();
            worldMarkers.Dispose();
        }

        private void PublishCounts(List<LocationCount> counts)
        {
            searchBar.SetSummary(counts);
            worldMarkers.SetCounts(counts);
        }

        /// <summary>
        /// Ctrl+F. F has no binding in the game's inventory action map (build 26327540) and Ctrl
        /// is only the CtrlModifier click modifier there; this is only checked while the search bar
        /// is on screen.
        /// </summary>
        private static bool FocusShortcutPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && keyboard.fKey.wasPressedThisFrame
                && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        /// <summary>
        /// A gamepad cannot type, so the View/Select button steps through the categories. That
        /// button has no binding in the game's inventory action map (build 26327540).
        /// </summary>
        private static bool GamepadCategoryPressed(UIManager manager)
        {
            Gamepad gamepad = Gamepad.current;
            return gamepad != null
                && gamepad.selectButton.wasPressedThisFrame
                && manager.informationDialogsActive.Count == 0;
        }
    }
}
