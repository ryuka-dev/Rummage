# Rummage

Search and filter your backpack and stashes by name, oil or effect — and see which stash holds an item.

A BepInEx plugin for SULFUR. GUID `ryuka.sulfur.rummage`.

## Status

Early development. The current build (phase 0) adds no gameplay-visible features yet: it loads,
and writes diagnostic logs about how the game names, sets up and saves its stashes and which items
count as enchantments.

Planned phases:

1. Search box in the inventory (name, localised name, pinyin) plus category buttons such as
   "oils only". Non-matching items are dimmed; the open stash is filtered the same way.
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
dotnet run --project Tests/StashSaveKeyTests
```

## Configuration

Only log switches, under `[Debug]`. They never change what the mod does.

| Key | Default | Effect |
| --- | --- | --- |
| `LogStashProbe` | `true` | Stash identifiers, save keys, setup timing and item change counts |
| `LogEnchantmentCatalog` | `true` | Every oil and enchantment item definition, once per session |
