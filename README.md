# Rummage

Search and filter your backpack and stashes by name, oil or effect — and see which stash holds an item.

A BepInEx plugin for SULFUR. GUID `ryuka.sulfur.rummage`. The player-facing description lives in
`Thunderstore/README.md`; this file is for building and working on the mod.

## Layout

- `src/Search`, `src/Filter/InventoryFilter.cs`, `src/Stashes`: game-independent logic (query
  parsing, pinyin, filter state, stash keys and names), covered by the test projects.
- `src/Game`: reads the game (item index, counts, storage index, Harmony hooks).
- `src/UserInterface`: search bar, results panel, world labels, the embedded checkbox icon.
- `src/Localization`: language tracking and Rummage's own strings from `Thunderstore/lang`.
- `src/Diagnostics`: log-only probes behind the `[Debug]` switches.

Design constraints:

- Save data is read only. The mod never writes the stash table, never saves a stash and never
  changes an item.
- Level loot containers and shop inventories are not searched.
- Purely local UI; nothing is synchronised in multiplayer.

Storages with live grids (every storage in the church) are counted from those grids; the rest from
the save's stash table, which the game refreshes when a stash closes. Storage names use the game's
name for each storage's unlock item, a link verified in probe rounds; the main stash has none and
uses Rummage's own text.

## Building

1. Copy `LocalPaths.props.example` to `LocalPaths.props` and fill in your paths.
2. Build without deploying:

   ```
   dotnet build Rummage.csproj -t:Restore;Compile -v:m -nologo
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
| `ResultsPosition` | `BelowSearchBar` | Results under the search bar over the backpack, growing with the list; or `AboveSearchBar`, shorter. Applies live. |
| `HideInventoryKeyHints` | `true` | Hides the game's key hints above the backpack, which the search bar covers. Applies live. |

`[Debug]` log switches, all off by default. They never change what the mod does.

| Key | Effect |
| --- | --- |
| `LogStashProbe` | Stash identifiers, save keys, setup timing, unlock items, stamp shop list |
| `LogEnchantmentCatalog` | Every oil and enchantment item definition, once per session |
| `LogSearchFilter` | Search bar placement, match counts per filter change, world label index |
| `LogGameTerms` | Game localisation terms that could name Rummage's labels, once per session |
