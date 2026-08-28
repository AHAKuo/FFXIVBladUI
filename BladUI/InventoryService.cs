using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

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
