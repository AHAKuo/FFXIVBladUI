using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace BladUI;

public readonly record struct ItemMeta(
    string Name,
    string NameLower,
    uint IconId,
    int CategoryMajor,
    int CategoryMinor,
    int CategoryId,
    string CategoryName,
    uint ItemLevel,
    uint EquipLevel,
    int Rarity,
    uint SellPrice,
    bool Marketable,
    string Description,
    InventoryType? Armoury,
    bool Usable);

/// <summary>Cached static item metadata from the game's Excel sheets.</summary>
public static class ItemData
{
    private static readonly Dictionary<uint, ItemMeta> Cache = [];

    public static ItemMeta Get(uint itemId)
    {
        if (Cache.TryGetValue(itemId, out var cached))
            return cached;

        ItemMeta meta;
        if (Plugin.DataManager.GetExcelSheet<Item>().TryGetRow(itemId, out var row))
        {
            var catMajor = 999;
            var catMinor = 999;
            var catName = "Miscellany";
            if (row.ItemUICategory.IsValid && row.ItemUICategory.RowId != 0)
            {
                catMajor = row.ItemUICategory.Value.OrderMajor;
                catMinor = row.ItemUICategory.Value.OrderMinor;
                catName = row.ItemUICategory.Value.Name.ExtractText();
            }

            // Which armoury chest container this gear belongs in, if any.
            // EquipSlotCategory fields: 1 = occupies the slot, -1 = blocks it.
            InventoryType? armoury = null;
            if (row.EquipSlotCategory.IsValid && row.EquipSlotCategory.RowId != 0)
            {
                var e = row.EquipSlotCategory.Value;
                armoury = e.MainHand == 1 ? InventoryType.ArmoryMainHand
                    : e.OffHand == 1 ? InventoryType.ArmoryOffHand
                    : e.Head == 1 ? InventoryType.ArmoryHead
                    : e.Body == 1 ? InventoryType.ArmoryBody
                    : e.Gloves == 1 ? InventoryType.ArmoryHands
                    : e.Legs == 1 ? InventoryType.ArmoryLegs
                    : e.Feet == 1 ? InventoryType.ArmoryFeets
                    : e.Ears == 1 ? InventoryType.ArmoryEar
                    : e.Neck == 1 ? InventoryType.ArmoryNeck
                    : e.Wrists == 1 ? InventoryType.ArmoryWrist
                    : e.FingerL == 1 || e.FingerR == 1 ? InventoryType.ArmoryRings
                    : e.SoulCrystal == 1 ? InventoryType.ArmorySoulCrystal
                    : null;
            }

            var name = row.Name.ExtractText();
            meta = new ItemMeta(
                name,
                name.ToLowerInvariant(),
                row.Icon,
                catMajor,
                catMinor,
                (int)row.ItemUICategory.RowId,
                catName,
                row.LevelItem.RowId,
                row.LevelEquip,
                row.Rarity,
                row.PriceLow,
                row.ItemSearchCategory.RowId != 0,
                row.Description.ExtractText(),
                armoury,
                row.ItemAction.RowId != 0);
        }
        else
        {
            meta = new ItemMeta($"Unknown item #{itemId}", $"unknown item #{itemId}", 0, 999, 999, 0, "Unknown", 0, 0, 0, 0, false, string.Empty, null, false);
        }

        Cache[itemId] = meta;
        return meta;
    }
}
