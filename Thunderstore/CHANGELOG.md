# Changelog

## 0.0.4

* While a search or category is active, storages that hold matches show a floating label in the world, such as `Wardrobe ×5`, visible through walls. When one storage stands in several places, only the nearest is labelled, updated as you move.

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
