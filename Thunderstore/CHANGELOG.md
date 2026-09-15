# Changelog

## 0.0.6

* Ctrl+F puts the caret in the search box while it is on screen.
* New checkbox next to the search box: when ticked, weapons are also found by the oils and scrolls applied to them, in the backpack, open stashes, the counts and the world labels. Words may split between the weapon and its oil ("shotgun crit"). Hover it for a short explanation.

## 0.0.5

* The search results now sit under the search bar, over the top of the backpack, by default. Expanded, the item list grows with its content down to the bottom of the screen, never taller than the list itself.
* New setting Display > Results Position: `BelowSearchBar` (default) or `AboveSearchBar` for the previous layout. It applies without restarting.
* The counts line no longer blocks clicks on the backpack beneath it; only its expand button takes clicks.

## 0.0.4

* While a search or category is active, storages that hold matches show a floating label in the world, such as `Wardrobe ×5`, visible through walls. When one storage stands in several places, only the nearest is labelled, updated as you move.
* Search now also matches what an item's description says it does: healing, buffs, statuses it adds or removes, repair, the stats it gives when equipped, and its own description. Searching "health" finds food.
* Pinyin search no longer matches a character's rare readings by initial, so "hp" no longer finds 汉堡.
* The Consumable and Attachments buttons use the game's own translations.
* Fixed an error when changing the game language with a search active.

## 0.0.3

* While a search or category is active, a line above the search bar shows where the matching items are, for example `Backpack ×1 · Stash ×2 · Weapon rack 1 ×1`. Stashes, weapon racks, item frames and wall mounts are all counted, including the ones not currently open.
* The square button at the end of that line expands a scrollable list of which items each place holds.
* Storages are named with the game's own translated names (Refrigerator, Chest of Drawers, Weapon Rack, the wall frames and wall mounts, and so on).
* Fixed the search box: typed text and the caret were invisible in Chinese, Japanese and Korean, and Backspace left the box instead of deleting.

## 0.0.2

* Added a search bar above the backpack. It matches item names in the current language and in English, oil and scroll names, and effect text; Chinese names also match pinyin.
* Added category buttons for oils, scrolls, weapons, attachments and consumables. The gamepad View/Select button cycles them.
* Items that do not match are faded and matches are framed, in the backpack and in an open stash.
* Inventory keyboard shortcuts no longer fire while typing in the search box; Escape leaves the box.

## 0.0.1

* Project setup. The plugin loads and writes diagnostic logs about stashes and enchantment items; no visible features yet.
