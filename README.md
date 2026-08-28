<p align="center"><img src="images/icon.png" width="128" alt="BladUI"></p>

# BladUI

**A Baldur's Gate-style inventory for FFXIV.** BladUI is a Dalamud plugin that renders
your inventory as its own view — arranged your way, with the multiselect, quick-move,
and bulk operations the native UI doesn't offer. The name is a nod to Baldur's Gate,
whose inventory UX this project chases.

> Clean out your armoury chest, saddlebag, and bags in under a minute.

## Screenshots

<!-- TODO: capture in-game (see docs/release-checklist.md)
![Main window](images/image1.png)
![Utilities preview](images/image2.png)
-->
*Coming soon — main window, Utilities plan preview, wares marking.*

## Features

### Your inventory, your layout
- **Custom arrangement** (default view): BladUI keeps its own persisted item order,
  independent of the game's physical bag slots. Drag items to place them; drag a
  selected item to move the whole selection as a block. New items append automatically
  and hold their position forever.
- Quick sort lenses: Type, Name, Item level, Rarity, Quantity, Newest (ID ≈ patch
  order) — plus the raw game slot layout if you want it.
- **Search** and a **category filter** built dynamically from what you're carrying.

### Baldur's Gate-style presentation
- Rarity-colored tiles (gradient + border): white / green / blue / purple / aetherial pink.
- Rich hover cards: rarity-colored name, category, item level, description, vendor value.
- HQ glyph, stack counts, slim capacity bar in the footer.

### Multiselect & bulk operations
- Click to select, **Ctrl+Click** to toggle, **Shift+Click** for ranges, **Ctrl+A**
  select all (respects search/filter), **Esc** to clear.
- **Ctrl+Click instant move** to the open retainer or saddlebag (configurable).
- **Move selected** — bulk move to the open retainer/saddlebag via the game's own
  server-validated `MoveItemSlot`.
- **Right-click context menu**: move, mark as wares, select.

### Wares (to-sell pile)
Mark items as **wares** (coin badge, persisted) — your standing "sell this junk" list,
straight out of Baldur's Gate. The action bar shows the count and total vendor gil.
One-click **Sell wares** at any shop arrives in v0.3.

## Design principle

BladUI only does what the game already lets you do — it is a **click-saver, not an
automation tool**. Every action maps to a player action routed through the game's own
functions (`MoveItemSlot`, item use, discard, vendor sell) and is validated by the
server exactly as if you'd clicked through the native UI. No item duping, no acting
on closed windows, no bypassing game rules: if the game would refuse it, BladUI can't
do it either.

## Roadmap: full inventory replacement

The end goal is for BladUI to *replace* the native inventory windows:

- **v0.3 — Sell wares**: one click sells every marked ware to an open shop
  (agent/addon callback route; see QuickTransfer for prior art).
- **Use items** from BladUI (`AgentInventoryContext` / `UseItem`) — food, potions,
  materia, minions.
- **Discard** from BladUI, with a confirm step.
- **Drag to hotbar** — assign usable items to hotbar slots (`RaptureHotbarModule`).
- Equip from BladUI; open native context menu as a fallback for anything exotic.
- Optionally auto-open BladUI when the game inventory would open, and hide the native
  windows entirely.

## Commands

| Command | Effect |
|---|---|
| `/bladui` | Toggle the inventory window |
| `/bladui config` | Open settings |

## Building

Requires the .NET 10 SDK and XIVLauncher (Dalamud dev libs at
`%APPDATA%\XIVLauncher\addon\Hooks\dev`, API 15).

```
dotnet build BladUI\BladUI.csproj -c Release
```

Output: `BladUI\bin\Release\BladUI.dll` (plus the generated manifest and `latest.zip`).

## Installing

Official Dalamud repository submission is in progress — once accepted, BladUI will
appear in `/xlplugins` (testing track first). Until then, build from source below.

## Installing (dev plugin)

1. In game: `/xlsettings` → **Experimental** tab.
2. Add the full path of `BladUI.dll` under **Dev Plugin Locations**, save.
3. `/xlplugins` → **Dev Tools** → **Installed Dev Plugins** → enable **BladUI**.
4. `/bladui`.

## Architecture

- **UI**: ImGui overlay via Dalamud's `WindowSystem` (`Dalamud.Bindings.ImGui` + ImRaii).
  The native game UI can't be extended with new input modes, so BladUI draws its own.
- **Data/moves**: `FFXIVClientStructs.InventoryManager` — container reads and
  `MoveItemSlot`, the same code path the game uses, so every move is server-validated.
- **Item info**: Lumina Excel sheets (`Item`, `ItemUICategory`) + `ITextureProvider`
  for real game icons; metadata cached per item ID.
- **Identity**: custom order and wares are keyed by item identity
  (`itemId << 1 | hqBit`), not bag slots — the game can shuffle stacks freely without
  disturbing your view.

## License

[AGPL-3.0](LICENSE). Developed with AI assistance (Claude Code).
