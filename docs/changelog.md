# BladUI Changelog

Feature update log. Newest first. See `feature-stack.md` for what's planned.

## v0.3.0 — 2026-08-29

- **Utilities window** (action bar → "Utilities…"): bulk action *profiles* — a
  growable list of instruction sets behind a shared plan → preview → confirm →
  run pipeline. Every step is itemized with its reason and individually
  toggleable; nothing executes without the review.
- **Clean out armoury chest**: moves stale gear to bags. Gearset items and soul
  crystals always kept. Rules (toggleable): outclassed (item level ≥N behind
  your best for that slot, default 30), leveling leftovers (required level 15+
  below your highest job), restrict to common (white) rarity. Plan capped to
  free bag space.
- **Mark garbage as wares**: marks bag items better sold to an NPC — vendor
  price ≥ current market minimum (live from universalis.app for your world,
  minus 5% tax, cached 15 min), plus unmarketable common junk. Gearset items
  and existing wares are never touched; items with no market listings are left
  alone.
- Item metadata moved to a shared `ItemData` service (adds equip level +
  marketability).

## v0.2.3 — 2026-08-29

- **Gearset awareness**: items belonging to any of your gear sets show a cyan
  diamond badge (bottom-left), and the hover card lists the gear set names.
  Matched by item + HQ against `RaptureGearsetModule` (all 100 sets), so it works
  in bags and the armoury alike.
- **Use items**: right-click → Use, or BG3-style **double-click**, for usable
  items in your bags (food, potions, minions, materia…). Routed through
  `AgentInventoryContext.UseItem` — the native Use path, so cooldowns, combat
  locks, and all other rules stay enforced by the game/server.
- **Hotbarring**: right-click → Add to hotbar → Bar 1–10 → Slot 1–12
  (occupied slots marked with •). Uses `RaptureHotbarModule.SetAndSaveSlot`.

## v0.2.2 — 2026-08-29

- **Armoury chest tab**: permanent tab aggregating all 12 armoury containers;
  full multiselect/search/sort/wares support; ctrl+click pulls items back to bags.
- **→ Armoury action**: routes each selected gear piece to its correct armoury
  container via `EquipSlotCategory` (non-gear skipped; full sections reported).

## v0.2.1 — 2026-08-29

- **Server-safety gate**: move destinations now require the real game window to
  be open (addon visible), not just cached container data. Prevents invalid
  moves into closed retainer/saddlebag containers — the server answers those
  with a disconnect.

## v0.2.0 — 2026-08-29

- **Baldur's Gate presentation**: rarity-colored gradient tiles, rich hover cards
  (description, category, ilvl, vendor value), HQ glyph.
- **Right-click context menu**: move, wares, select (selection-aware).
- **Wares**: mark items as a persistent to-sell pile (coin badge); action bar
  shows count + total gil. Selling ships in v0.3.
- **Category filter** built dynamically from carried items; capacity bar.

## v0.1.3 — 2026-08-29

- **Custom view**: BladUI's own persisted arrangement (item-keyed, independent of
  physical slots), drag to arrange, block-drag for selections, reset in settings.

## v0.1.2 — 2026-08-29

- Grid compacts to occupied slots; "show empty slots" toggle for true bag layout.

## v0.1.1 — 2026-08-29

- Search bar; view sorts (type/name/ilvl/rarity/qty/newest); footer layout fix;
  metadata caching.

## v0.1.0 — 2026-08-29

- First version: own ImGui inventory window (bags/saddlebag/retainer),
  multiselect, ctrl+click instant move, bulk move via `MoveItemSlot`.
