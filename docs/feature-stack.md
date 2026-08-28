# BladUI Feature Stack

Living roadmap. Add ideas at the bottom; promote them upward as they get scheduled.
One rule governs everything here: **only what the game allows** — every feature is a
click-saver routed through the game's own server-validated actions, never automation.

## Shipped

- [x] **v0.1** — Own ImGui inventory window (4 bags, saddlebag, retainer tabs),
      multiselect (click / ctrl / shift / ctrl+A / esc), ctrl+click instant move,
      bulk move via `InventoryManager.MoveItemSlot`
- [x] **v0.1.1** — Search bar, view sorts (type / name / ilvl / rarity / qty / newest),
      footer layout fix
- [x] **v0.1.2** — Compact grid (empty slots hidden by default)
- [x] **v0.1.3** — Custom view: BladUI's own persisted item arrangement
      (item-keyed, slot-independent), drag to arrange, block-drag for selections
- [x] **v0.2.0** — Baldur's Gate presentation: rarity tiles, rich hover cards,
      right-click context menu, wares (to-sell pile) with gil totals,
      dynamic category filter, capacity bar
- [x] **v0.2.1** — Server-safety gate: destinations require the real game window
      (addon visible), not cached container data
- [x] **v0.2.2** — Armoury chest tab + "→ Armoury" routing by equip slot
- [x] **v0.2.3** — Gearset badges (cyan diamond + tooltip), Use items
      (right-click / double-click, bags only), Add to hotbar (menu-based)

- [x] **v0.3.0** — Utilities: bulk action profiles (plan → preview → confirm → run);
      Clean Armoury (rule-based); Mark Garbage as Wares (universalis.app + heuristics)

## Next up

- [ ] **Sell wares**: one click sells every marked ware to an open shop.
      Route: agent/addon callback on the open shop window; study QuickTransfer's
      vendor-sell for prior art. Gate hard on the shop addon being visible.
      Confirm dialog listing items + total gil before firing.
- [ ] More utility profiles (the stack is designed to grow — new profile = one
      class implementing IBulkAction)

## Toward full inventory replacement

The end state: BladUI can do everything the native inventory windows do, so they
never need to be opened.

- [x] **Use items** from BladUI — shipped v0.2.3 (right-click Use + double-click)
- [ ] **Discard** from BladUI — with a confirm step; never bulk-discard without
      an itemized list shown first
- [ ] **Equip** from BladUI — right-click → Equip for gear usable by current job
- [x] **Hotbar assignment** (menu-based) — shipped v0.2.3
- [ ] **True drag to native hotbar** — drop BladUI items directly onto the game's
      hotbar (needs cross-UI drag handling)
- [ ] **Split stacks** — right-click → split with a quantity slider (BG3-style)
- [ ] **Auto-open/replace**: option to open BladUI whenever the native inventory
      would open (and optionally suppress the native window)

## Idea pool (suggested, not yet scheduled)

Quick wins:
- [ ] **Merge split stacks** profile — combine partial stacks of the same item
      (MoveItemSlot onto a matching stack merges natively); footer hint for slots freed
- [ ] **Spiritbond & condition surfacing** — flag 100% spiritbond (materia ready,
      + extraction list profile) and low-durability gear on tiles
- [ ] **Unopened coffers highlight** — glow/filter for coffers; double-click-use chains
- [ ] **Gearset integrity check** profile — find set pieces stranded in bags/saddlebag,
      one-click send to armoury
- [ ] **"NEW" badges** — session-diff tag on items acquired since last open

Medium:
- [ ] **Treasure detector** — badge items whose market value dwarfs vendor price
      (reuses cached Universalis data); safety net for Mark Garbage
- [ ] **Saddlebag stash profile** — rules-based bulk stash of overflow (mats yes,
      wares no, gearsets never) when saddlebag is open
- [ ] **Custom view sections** — named collapsible dividers in the Custom arrangement

Bigger:
- [ ] **Cross-storage search** — one search over bags + armoury + saddlebag +
      cached retainer data, showing item location (read-only, server-safe)
- [ ] **Consolidate with retainer** — when open, highlight bag items with existing
      retainer stacks; one-click top-up

## Backlog / ideas

- [ ] Read-only retainer view from cache (browse contents while the bell is closed;
      moves stay disabled — view only)
- [ ] Drag-rectangle (marquee) multiselect
- [ ] Wares presets / junk rules (e.g. auto-mark grey-quality drops below ilvl X)
- [ ] Crystal/currency strip
- [ ] Per-tab or per-character custom arrangements
- [x] Gear-set badges/tooltip — shipped v0.2.3
- [ ] Gear-set protection (warn before selling/discarding items in a gear set)
- [ ] Optional weight-style stats in footer (total wares gil, unique items count)

## Technical notes

- Item identity key everywhere: `itemId << 1 | hqBit` (arrangement, wares)
- Server safety: any new *action* must be gated on the relevant native addon being
  visible AND map 1:1 to something a player could click right now. Invalid requests
  disconnect the client — treat every new game-function call as suspect until
  tested in-game with a throwaway item.
- Dalamud API 15 / .NET 10; ImGui via `Dalamud.Bindings.ImGui` + ImRaii.
