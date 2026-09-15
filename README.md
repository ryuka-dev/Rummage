# Rummage

Search and filter your backpack and stashes by name, oil or effect — and see which stash holds an item.

A BepInEx plugin for SULFUR. GUID `ryuka.sulfur.rummage`.

## Status

In development (phase 1).

- A search bar sits above the backpack whenever the inventory is open. Typing matches an item's
  name in the current language, its English name, its oil or scroll name, and the effect lines its
  description shows. Names written in Chinese also match pinyin (`moyou`, `my`).
  Several words must all match.
- Category buttons: All, Oils, Scrolls, Weapons, Attachments, Consumables. Clicking the selected
  one again returns to All. On a gamepad the View/Select button steps through them.
- Items that do not match are faded, matching items are framed. An open stash (including weapon
  racks) is filtered the same way as the backpack.
- While the search box has focus, the inventory's keyboard shortcuts do not fire; Escape leaves the
  box instead of closing the inventory.
- The filter stays set until cleared, for the rest of the session. It is not saved.
- While a filter is active, a line next to the search bar counts the matches per place, for example
  `Backpack ×1 · Stash ×2 · Weapon rack 1 ×1`. The square button at its end expands a scrollable
  list of the matching items in each place. By default it hangs under the search bar over the top
  of the backpack and grows with the list down to the bottom of the screen; with
  `ResultsPosition = AboveSearchBar` it sits above the search bar, capped shorter. Stashes whose grid the game has set up (all of them
  in the church) are counted live; the rest from the save's stash table, which the game refreshes
  when a stash closes. Item frames and wall mounts count as places too. Storage names use the
  game's own name for the storage's unlock item, where probe rounds showed the storage carrying
  that UnlockableStorage (every storage except the main stash, which has none and uses
  Rummage's own table). An identifier Rummage does not know shows as "Stash #n".

- While a filter is active, every storage in the scene that holds matches gets a floating label
  above it (for example `Wardrobe ×5`), facing the camera and drawn over walls. A storage that
  stands in several places (the main stash) is labelled only at the one nearest the player,
  re-chosen every frame. Counts are refreshed when a level loads, so the labels are right with the
  inventory closed.

Constraints the design keeps:

- Save data is read only. The mod never writes the stash table, never saves a stash and never
  changes an item.
- Level loot containers and shop inventories are not searched.
- Purely local UI; nothing is synchronised in multiplayer.

## Building

1. Copy `LocalPaths.props.example` to `LocalPaths.props` and fill in your paths.
2. Build without deploying:

   ```
   dotnet build -t:Restore;Compile -v:m -nologo
   ```

3. To copy the plugin and language files into a profile, build with
   `-p:DeployToSulfurProfile=true` (off by default).

Tests for the game-independent code:

```
dotnet run --project Tests/SearchTests
dotnet run --project Tests/StashSaveKeyTests
```

## Configuration

`[Display]`

| Key | Default | Effect |
| --- | --- | --- |
| `ResultsPosition` | `BelowSearchBar` | `BelowSearchBar`: results under the search bar, over the backpack, growing with the list. `AboveSearchBar`: above the search bar, shorter. Applies live. |

`[Debug]` log switches. They never change what the mod does.

| Key | Default | Effect |
| --- | --- | --- |
| `LogStashProbe` | `true` | Stash identifiers, save keys, setup timing and item change counts |
| `LogEnchantmentCatalog` | `true` | Every oil and enchantment item definition, once per session |
| `LogSearchFilter` | `true` | Search bar placement, match counts per filter change, world marker index |
| `LogGameTerms` | `true` | Game localisation terms that could name Rummage's labels, once per session |
