# Release checklist — official Dalamud repo

## Screenshots to capture in-game (installer gallery + README)

The D17 submission folder accepts `images/image1.png` … `image5.png` — these show
inside `/xlplugins` when a user expands the plugin. Capture at a clean resolution,
crop tight to the window:

1. **image1** — Main window, Inventory tab, a healthy grid with a multi-selection
   active (gold borders visible), rarity colors showing.
2. **image2** — Utilities window mid-preview: "Clean out armoury chest" plan with
   the itemized reasons visible.
3. **image3** — A rich tooltip: rarity-colored name, description, gearset line,
   market/vendor line.
4. **image4** — Wares in action: coin badges + "Sell wares (N · X gil)" in the bar.
5. **image5** — Right-click context menu open (Use / Add to hotbar / Move / wares).

Also record a ~30s GIF for the README/Discord post: messy bags → select sweep →
Clean armoury preview → run → clean. Hand the raw clip to Claude for trimming.

## Submission steps

- [x] Public repo: https://github.com/AHAKuo/FFXIVBladUI (AGPL-3.0)
- [x] `packages.lock.json` committed (required by Plogon)
- [x] Icon 512×512 at `images/icon.png`
- [ ] Capture screenshots above
- [ ] Fork `goatcorp/DalamudPluginsD17`, branch `bladui`
- [ ] Add `testing/live/BladUI/manifest.toml`:
      ```toml
      [plugin]
      repository = "https://github.com/AHAKuo/FFXIVBladUI.git"
      commit = "<full sha of release commit>"
      owners = ["AHAKuo"]
      project_path = "BladUI"
      changelog = "Initial release."
      ```
- [ ] Copy `images/icon.png` (+ screenshots) into `testing/live/BladUI/images/`
- [ ] Open PR (one plugin per PR); disclose AI-assisted development per the
      Dalamud AI usage policy
- [ ] Respond to Plugin Approval Committee feedback
- [ ] After bake time on testing: promotion PR to stable

## Announcement (after testing-track merge)

- [ ] XIVLauncher/Dalamud Discord plugin showcase post with the GIF
- [ ] GitHub repo topics: `dalamud`, `dalamud-plugin`, `ffxiv`
