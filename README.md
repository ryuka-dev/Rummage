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

Planned:

2. Per-stash counts for the current search, for example `Backpack ×1 · Stash ×2 · Weapon rack ×1`.
3. Later: world markers on the matching stashes in the church.

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

Only log switches, under `[Debug]`. They never change what the mod does.

| Key | Default | Effect |
| --- | --- | --- |
| `LogStashProbe` | `true` | Stash identifiers, save keys, setup timing and item change counts |
| `LogEnchantmentCatalog` | `true` | Every oil and enchantment item definition, once per session |
| `LogSearchFilter` | `true` | Search bar placement and match counts for each filter change |
