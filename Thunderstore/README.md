# Rummage

Search and filter your backpack and stashes by name, oil or effect — and see which stash holds an item.

## Features

- **Search bar** above the backpack whenever the inventory is open. Type an item name in your language or in English, an oil or scroll name, or what the item does ("health", "reload", "crit"). Chinese names also match pinyin, full or as initials. Several words must all match.
- **Category buttons**: All, Oils, Scrolls, Weapons, Attachments, Consumables. On a gamepad, the View/Select button cycles them.
- **Highlighting**: items that do not match fade out and matches get a frame, in the backpack and in the open stash or weapon rack.
- **Where is it?** A line under the search bar counts the matches in the backpack and in every storage, open or not: the stash, wardrobe, refrigerator, weapon racks, wall frames and so on. The square button at its end expands a list of exactly which items each place holds.
- **Labels in the church**: storages holding matches show a floating label such as "Wardrobe ×5", visible through walls. When a storage stands in several places, only the nearest one is labelled.
- **Oiled weapons**: tick the gun checkbox beside the search box to also find weapons by the oils and scrolls applied to them. Hover it for a reminder.
- **Keyboard**: Ctrl+F jumps to the search box. While you type, inventory shortcuts are paused and Escape leaves the box.
- Storage, consumable and attachment names come from the game's own translations, and Rummage's text is translated into all 14 game languages.

## Configuration

Settings live in `BepInEx/config/ryuka.sulfur.rummage.cfg`, or in game through SULFUR Config. Both apply without restarting.

### Results Position

Where the search results sit. `BelowSearchBar` hangs them over the top of the backpack and lets the item list grow down the screen. `AboveSearchBar` places them above the search bar in a shorter panel.

```ini
[Display]
ResultsPosition = BelowSearchBar
```

### Hide Inventory Key Hints

Hides the game's key hints above the backpack (rotate, use and so on), which the search bar covers.

```ini
[Display]
HideInventoryKeyHints = true
```

The `[Debug]` section only writes extra log output for troubleshooting and is off by default.

## Compatibility

- Optional: SULFUR Config shows these settings in the game's options, with translated names.
- Save data is only read, never written.
- Level loot containers and shop inventories are not searched.
- Works locally only; nothing is synchronised in multiplayer, and each player searches their own inventory and stashes.
