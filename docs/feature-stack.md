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

## Next up

- [ ] **v0.3 — Sell wares**: one click sells every marked ware to an open shop.
      Route: agent/addon callback on the open shop window; study QuickTransfer's
      vendor-sell for prior art. Gate hard on the shop addon being visible.
      Confirm dialog listing items + total gil before firing.

## Toward full inventory replacement

The end state: BladUI can do everything the native inventory windows do, so they
never need to be opened.

- [ ] **Use items** from BladUI (food, potions, materia, minions, orchestrion rolls)
      — `AgentInventoryContext.UseItem`; respect cooldowns/combat lockouts (the game
      rejects those anyway; grey the action out when known-invalid)
- [ ] **Discard** from BladUI — with a confirm step; never bulk-discard without
      an itemized list shown first
- [ ] **Equip** from BladUI — right-click → Equip for gear usable by current job
- [ ] **Drag to hotbar** — assign usable items to hotbar slots
      (`RaptureHotbarModule` slot assignment)
- [ ] **Split stacks** — right-click → split with a quantity slider (BG3-style)
- [ ] **Auto-open/replace**: option to open BladUI whenever the native inventory
      would open (and optionally suppress the native window)

## Backlog / ideas

- [ ] Read-only retainer view from cache (browse contents while the bell is closed;
      moves stay disabled — view only)
- [ ] Drag-rectangle (marquee) multiselect
- [ ] Wares presets / junk rules (e.g. auto-mark grey-quality drops below ilvl X)
- [ ] Crystal/currency strip
- [ ] Per-tab or per-character custom arrangements
- [ ] Gear-set awareness (warn before selling/discarding items in a gear set)
- [ ] Optional weight-style stats in footer (total wares gil, unique items count)

## Technical notes

- Item identity key everywhere: `itemId << 1 | hqBit` (arrangement, wares)
- Server safety: any new *action* must be gated on the relevant native addon being
  visible AND map 1:1 to something a player could click right now. Invalid requests
  disconnect the client — treat every new game-function call as suspect until
  tested in-game with a throwaway item.
- Dalamud API 15 / .NET 10; ImGui via `Dalamud.Bindings.ImGui` + ImRaii.
