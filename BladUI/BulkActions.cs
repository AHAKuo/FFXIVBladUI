using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace BladUI;

/// <summary>One reviewed, individually toggleable step of a bulk action plan.</summary>
public sealed class PlannedStep
{
    public required SlotInfo Slot { get; init; }
    public required string Name { get; init; }
    public required string Reason { get; init; }
    public required Func<bool> Run { get; init; }
    public bool Enabled = true;
}

/// <summary>
/// A bulk action profile: a named, growable list of instructions that plans
/// first and only executes what the user reviewed. Every step maps 1:1 to a
/// player action the game itself validates.
/// </summary>
public interface IBulkAction
{
    string Name { get; }
    string Description { get; }

    /// <summary>Profile-specific options, drawn inside the utilities window.</summary>
    void DrawOptions();

    /// <summary>Async data gathering (network etc.). Called on the main thread; may run tasks.</summary>
    Task PrepareAsync();

    /// <summary>Build the itemized plan. Main thread only. May attach a note (e.g. truncation).</summary>
    List<PlannedStep> Plan(out string? note);

    /// <summary>Called once after a run completes (e.g. to save config).</summary>
    void Finish();
}

/// <summary>
/// Pulls no-longer-useful gear out of the armoury chest into the bags.
/// Gearset items and soul crystals are always kept.
/// </summary>
public sealed class CleanArmouryAction(Plugin plugin) : IBulkAction
{
    public string Name => "Clean out armoury chest";
    public string Description => "Moves stale gear from the armoury chest to your bags. Gearset items and soul crystals are always kept.";

    public void DrawOptions()
    {
        var cfg = plugin.Configuration;

        var underlevel = cfg.CleanUnderlevel;
        if (ImGui.Checkbox("Outclassed: item level far below your best for that slot", ref underlevel))
        {
            cfg.CleanUnderlevel = underlevel;
            cfg.Save();
        }

        if (underlevel)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120f);
            var threshold = cfg.CleanUnderlevelThreshold;
            if (ImGui.SliderInt("##cleanThreshold", ref threshold, 10, 200, "≥ %d ilvls behind"))
            {
                cfg.CleanUnderlevelThreshold = threshold;
                cfg.Save();
            }
        }

        var unequip = cfg.CleanUnequippable;
        if (ImGui.Checkbox("Leveling leftovers: required level 15+ below your highest job", ref unequip))
        {
            cfg.CleanUnequippable = unequip;
            cfg.Save();
        }

        var whiteOnly = cfg.CleanWhiteOnly;
        if (ImGui.Checkbox("Only common (white) rarity gear", ref whiteOnly))
        {
            cfg.CleanWhiteOnly = whiteOnly;
            cfg.Save();
        }
    }

    public Task PrepareAsync() => Task.CompletedTask;

    public List<PlannedStep> Plan(out string? note)
    {
        note = null;
        var cfg = plugin.Configuration;
        var steps = new List<PlannedStep>();

        if (!cfg.CleanUnderlevel && !cfg.CleanUnequippable)
        {
            note = "Enable at least one rule above.";
            return steps;
        }

        var gearsets = InventoryService.GetGearsetItemKeys();
        var maxJobLevel = InventoryService.GetMaxJobLevel();

        // Best item level you own per armoury section (bags + armoury, gearset gear included).
        var best = new Dictionary<InventoryType, uint>();
        foreach (var container in InventoryService.ArmouryContainers.Concat(InventoryService.PlayerBags))
        {
            foreach (var slot in InventoryService.GetSlots(container).Where(s => s.ItemId != 0))
            {
                var meta = ItemData.Get(slot.ItemId);
                if (meta.Armoury is { } section)
                    best[section] = Math.Max(best.GetValueOrDefault(section), meta.ItemLevel);
            }
        }

        foreach (var container in InventoryService.ArmouryContainers)
        {
            if (container == InventoryType.ArmorySoulCrystal)
                continue;

            foreach (var slot in InventoryService.GetSlots(container).Where(s => s.ItemId != 0))
            {
                if (gearsets.ContainsKey(InventoryService.MakeKey(slot.ItemId, slot.IsHq)))
                    continue;

                var meta = ItemData.Get(slot.ItemId);
                if (cfg.CleanWhiteOnly && meta.Rarity != 1)
                    continue;

                string? reason = null;
                var bestIlvl = best.GetValueOrDefault(container);
                if (cfg.CleanUnderlevel && bestIlvl > meta.ItemLevel
                    && bestIlvl - meta.ItemLevel >= (uint)cfg.CleanUnderlevelThreshold)
                {
                    reason = $"ilvl {meta.ItemLevel}, your best here is {bestIlvl}";
                }
                else if (cfg.CleanUnequippable && maxJobLevel > 0 && meta.EquipLevel + 15 <= (uint)maxJobLevel)
                {
                    reason = $"level {meta.EquipLevel} gear (highest job: {maxJobLevel})";
                }

                if (reason == null)
                    continue;

                var source = new SlotKey(slot.Container, slot.Slot);
                steps.Add(new PlannedStep
                {
                    Slot = slot,
                    Name = meta.Name,
                    Reason = reason,
                    Run = () => InventoryService.MoveToFirstFree(source, InventoryService.PlayerBags),
                });
            }
        }

        var free = InventoryService.CountFreeSlots(InventoryService.PlayerBags);
        if (steps.Count > free)
        {
            note = $"Only {free} free bag slots — plan capped at {free} of {steps.Count} candidates.";
            steps = steps.Take(free).ToList();
        }

        return steps;
    }

    public void Finish() { }
}

/// <summary>
/// Marks vendor-trash as wares: items whose vendor price beats the current
/// market board price (universalis.app), plus unmarketable common junk.
/// Gearset items are never marked.
/// </summary>
public sealed class MarkGarbageAction(Plugin plugin) : IBulkAction
{
    private const double MarketTax = 0.05;

    private Dictionary<uint, (long Nq, long Hq)> prices = [];
    private string world = string.Empty;

    public string Name => "Mark garbage as wares";
    public string Description => "Marks bag items better sold to an NPC: vendor price ≥ market price (universalis.app, minus 5% tax), plus unmarketable common junk. Gearset items are never marked.";

    public void DrawOptions()
    {
        ImGui.TextDisabled("Market prices: universalis.app for your current world (cached 15 min).");
    }

    public Task PrepareAsync()
    {
        // Gather candidates on the main thread, then fetch prices off-thread.
        world = Plugin.PlayerState.IsLoaded && Plugin.PlayerState.CurrentWorld.IsValid
            ? Plugin.PlayerState.CurrentWorld.Value.Name.ExtractText()
            : string.Empty;
        if (world.Length == 0)
            throw new InvalidOperationException("Not logged in — no world to price against.");

        var marketableIds = new HashSet<uint>();
        foreach (var container in InventoryService.PlayerBags)
        {
            foreach (var slot in InventoryService.GetSlots(container).Where(s => s.ItemId != 0))
            {
                var meta = ItemData.Get(slot.ItemId);
                if (meta.Marketable && meta.SellPrice > 0)
                    marketableIds.Add(slot.ItemId);
            }
        }

        return Task.Run(async () => prices = await UniversalisClient.GetMinPricesAsync(world, marketableIds));
    }

    public List<PlannedStep> Plan(out string? note)
    {
        note = $"Prices from universalis.app ({world}).";
        var steps = new List<PlannedStep>();
        var gearsets = InventoryService.GetGearsetItemKeys();
        var wares = plugin.Configuration.Wares;
        var seenKeys = new HashSet<uint>();

        foreach (var container in InventoryService.PlayerBags)
        {
            foreach (var slot in InventoryService.GetSlots(container).Where(s => s.ItemId != 0))
            {
                var key = InventoryService.MakeKey(slot.ItemId, slot.IsHq);
                if (wares.Contains(key) || gearsets.ContainsKey(key) || !seenKeys.Add(key))
                    continue;

                var meta = ItemData.Get(slot.ItemId);
                if (meta.SellPrice == 0)
                    continue; // Can't be vendored at all.

                string? reason = null;
                if (meta.Marketable)
                {
                    var (nq, hq) = prices.GetValueOrDefault(slot.ItemId);
                    var market = slot.IsHq ? (hq > 0 ? hq : nq) : nq;
                    if (market <= 0)
                        continue; // No listings — could be valuable; leave alone.

                    var proceeds = (long)(market * (1 - MarketTax));
                    if (meta.SellPrice >= proceeds)
                        reason = $"vendor {meta.SellPrice:N0} ≥ market {market:N0} −5% tax";
                }
                else if (meta.Rarity == 1 && !meta.Usable && meta.Armoury == null)
                {
                    reason = $"unmarketable junk — vendors for {meta.SellPrice:N0} gil";
                }

                if (reason == null)
                    continue;

                steps.Add(new PlannedStep
                {
                    Slot = slot,
                    Name = meta.Name + (slot.IsHq ? " (HQ)" : string.Empty),
                    Reason = reason,
                    Run = () =>
                    {
                        if (!wares.Contains(key))
                            wares.Add(key);
                        return true;
                    },
                });
            }
        }

        return steps;
    }

    public void Finish() => plugin.Configuration.Save();
}
