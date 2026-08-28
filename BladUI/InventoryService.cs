using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace BladUI;

/// <summary>A single occupied (or empty) inventory slot snapshot.</summary>
public readonly record struct SlotInfo(
    InventoryType Container,
    short Slot,
    uint ItemId,
    int Quantity,
    bool IsHq);

/// <summary>Identifies a slot for selection purposes.</summary>
public readonly record struct SlotKey(InventoryType Container, short Slot);

/// <summary>
/// Unsafe wrapper over FFXIVClientStructs.InventoryManager. All calls must
/// happen on the game main thread (Dalamud's UiBuilder.Draw runs there).
/// </summary>
public static unsafe class InventoryService
{
    public static readonly InventoryType[] PlayerBags =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    public static readonly InventoryType[] SaddlebagPages =
    [
        InventoryType.SaddleBag1,
        InventoryType.SaddleBag2,
    ];

    public static readonly InventoryType[] ArmouryContainers =
    [
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
        InventoryType.ArmorySoulCrystal,
    ];

    public static readonly InventoryType[] RetainerPages =
    [
        InventoryType.RetainerPage1,
        InventoryType.RetainerPage2,
        InventoryType.RetainerPage3,
        InventoryType.RetainerPage4,
        InventoryType.RetainerPage5,
        InventoryType.RetainerPage6,
        InventoryType.RetainerPage7,
    ];

    /// <summary>Number of empty slots across the given containers.</summary>
    public static int CountFreeSlots(InventoryType[] containers)
    {
        var free = 0;
        foreach (var type in containers)
            free += GetSlots(type).Count(s => s.ItemId == 0);
        return free;
    }

    /// <summary>Highest level among all the player's jobs (0 if unavailable).</summary>
    public static short GetMaxJobLevel()
    {
        var ps = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
        if (ps == null)
            return 0;

        short max = 0;
        foreach (var level in ps->ClassJobLevels)
            max = Math.Max(max, level);
        return max;
    }

    /// <summary>Identity key used for arrangement/wares/gearset matching (HQ is its own entry).</summary>
    public static uint MakeKey(uint itemId, bool hq) => (itemId << 1) | (hq ? 1u : 0u);

    /// <summary>
    /// Item keys referenced by the player's gear sets, mapped to the set names
    /// using them. Gear sets store itemId + 1,000,000 for HQ items.
    /// </summary>
    public static Dictionary<uint, List<string>> GetGearsetItemKeys()
    {
        var result = new Dictionary<uint, List<string>>();
        var module = RaptureGearsetModule.Instance();
        if (module == null)
            return result;

        for (var i = 0; i < 100; i++)
        {
            var gearset = module->GetGearset(i);
            if (gearset == null || !gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;

            var name = gearset->NameString;
            if (string.IsNullOrWhiteSpace(name))
                name = $"Gear set {i + 1}";

            for (var j = 0; j < 14; j++)
            {
                var rawId = gearset->Items[j].ItemId;
                if (rawId == 0)
                    continue;

                var hq = rawId >= 1_000_000;
                var key = MakeKey(hq ? rawId - 1_000_000 : rawId, hq);
                if (!result.TryGetValue(key, out var names))
                    result[key] = names = [];
                if (!names.Contains(name))
                    names.Add(name);
            }
        }

        return result;
    }

    /// <summary>
    /// Use an item from the player's bags — same code path as the native
    /// context menu's Use, so the game/server enforce all usage rules.
    /// </summary>
    public static bool UseItem(uint itemId, bool hq)
    {
        var agent = AgentInventoryContext.Instance();
        if (agent == null)
            return false;

        agent->UseItem(hq ? itemId + 1_000_000 : itemId);
        return true;
    }

    /// <summary>Assign an item to a hotbar slot (bar 0-9, slot 0-11).</summary>
    public static bool SetHotbarSlot(uint bar, uint slot, uint itemId, bool hq)
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null)
            return false;

        module->SetAndSaveSlot(bar, slot, RaptureHotbarModule.HotbarSlotType.Item,
            hq ? itemId + 1_000_000 : itemId);
        return true;
    }

    /// <summary>True if the given hotbar slot currently has something on it.</summary>
    public static bool IsHotbarSlotOccupied(uint bar, uint slot)
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null)
            return false;

        var s = module->GetSlotById(bar, slot);
        return s != null && s->CommandType != RaptureHotbarModule.HotbarSlotType.Empty;
    }

    /// <summary>
    /// True while the given native addon (game window) exists and is visible.
    /// Container data alone is NOT proof a window is open — the game caches
    /// retainer/saddlebag contents after closing, and moving into a closed
    /// container is an invalid request the server answers with a disconnect.
    /// </summary>
    public static bool IsAddonVisible(string name)
    {
        var addon = Plugin.GameGui.GetAddonByName(name);
        return addon.Address != nint.Zero && addon.IsVisible;
    }

    /// <summary>Chocobo saddlebag window is actually open.</summary>
    public static bool IsSaddlebagOpen()
        => IsAddonVisible("InventoryBuddy") && AnyLoaded(SaddlebagPages);

    /// <summary>A retainer's inventory window is actually open.</summary>
    public static bool IsRetainerOpen()
        => (IsAddonVisible("InventoryRetainer") || IsAddonVisible("InventoryRetainerLarge")) && AnyLoaded(RetainerPages);

    public static bool IsContainerLoaded(InventoryType type)
    {
        var im = InventoryManager.Instance();
        if (im == null)
            return false;

        var container = im->GetInventoryContainer(type);
        return container != null && container->IsLoaded && container->Size > 0;
    }

    public static bool AnyLoaded(InventoryType[] types)
    {
        foreach (var t in types)
        {
            if (IsContainerLoaded(t))
                return true;
        }

        return false;
    }

    /// <summary>Snapshot every slot (including empty ones) of a container.</summary>
    public static List<SlotInfo> GetSlots(InventoryType type)
    {
        var result = new List<SlotInfo>();
        var im = InventoryManager.Instance();
        if (im == null)
            return result;

        var container = im->GetInventoryContainer(type);
        if (container == null || !container->IsLoaded)
            return result;

        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item == null)
            {
                result.Add(new SlotInfo(type, (short)i, 0, 0, false));
                continue;
            }

            result.Add(new SlotInfo(
                type,
                (short)i,
                item->ItemId,
                item->Quantity,
                item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality)));
        }

        return result;
    }

    /// <summary>Find the first empty slot across the given containers.</summary>
    public static SlotKey? FindFreeSlot(InventoryType[] containers)
    {
        var im = InventoryManager.Instance();
        if (im == null)
            return null;

        foreach (var type in containers)
        {
            var container = im->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded)
                continue;

            for (var i = 0; i < container->Size; i++)
            {
                var item = container->GetInventorySlot(i);
                if (item == null || item->ItemId == 0)
                    return new SlotKey(type, (short)i);
            }
        }

        return null;
    }

    /// <summary>
    /// Move one stack to the first free slot of the destination containers.
    /// Returns true if the game accepted the move request.
    /// </summary>
    public static bool MoveToFirstFree(SlotKey source, InventoryType[] destination)
    {
        var free = FindFreeSlot(destination);
        if (free == null)
            return false;

        var im = InventoryManager.Instance();
        if (im == null)
            return false;

        var result = im->MoveItemSlot(
            source.Container,
            (ushort)source.Slot,
            free.Value.Container,
            (ushort)free.Value.Slot,
            true);
        return result == 0;
    }
}
