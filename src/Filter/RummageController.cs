using System;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.UI;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using Ryuka.Sulfur.Rummage.Diagnostics;
using Ryuka.Sulfur.Rummage.Game;
using Ryuka.Sulfur.Rummage.InputGuard;
using Ryuka.Sulfur.Rummage.UserInterface;
using UnityEngine.InputSystem;

namespace Ryuka.Sulfur.Rummage.Filter
{
    /// <summary>
    /// Connects the filter, its presentation on the grids, the search bar and the typing guard to
    /// the inventory's lifecycle.
    /// </summary>
    internal sealed class RummageController : IDisposable
    {
        private readonly InventoryFilter filter = new InventoryFilter();
        private readonly InventoryFilterPresenter presenter;
        private readonly SearchBar searchBar;
        private bool tickFailureReported;

        public RummageController(DiagnosticsConfig diagnostics)
        {
            presenter = new InventoryFilterPresenter(filter, diagnostics);
            searchBar = new SearchBar(filter, diagnostics);

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

        public void OnLanguageChanged()
        {
            ItemSearchCatalog.Invalidate();
            searchBar.RefreshTexts();
            presenter.MarkDirty();
        }

        public void Tick()
        {
            UIManager manager = StaticInstance<UIManager>.Instance;
            InventoryUI ui = manager != null ? manager.InventoryUI : null;
            if (ui == null || !ui.IsOpened)
            {
                return;
            }

            try
            {
                searchBar.Tick(ui);

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

        public void Dispose()
        {
            SearchInputGuard.IsTyping = null;
            SearchInputGuard.StopTyping = null;
            presenter.Dispose();
            searchBar.Dispose();
        }

        /// <summary>
        /// A gamepad cannot type, so the View/Select button steps through the categories. That
        /// button has no binding in the game's inventory action map (build 25257948).
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
