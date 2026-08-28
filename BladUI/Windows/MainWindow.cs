using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace BladUI.Windows;

public class MainWindow : Window, IDisposable
{
    private const int SortCustom = 0;
    private const int SortSlotOrder = 7;

    private const int FilterAll = -1;
    private const int FilterWares = -2;

    private static readonly string[] SortModeNames =
    [
        "Custom (yours)",
        "Type",
        "Name",
        "Item level",
        "Rarity",
        "Quantity",
        "Newest (ID)",
        "Slot order (game)",
    ];

    private readonly record struct ItemMeta(
        string Name,
        string NameLower,
        uint IconId,
        int CategoryMajor,
        int CategoryMinor,
        int CategoryId,
        string CategoryName,
        uint ItemLevel,
        int Rarity,
        uint SellPrice,
        string Description);

    private readonly Plugin plugin;
    private readonly Dictionary<uint, ItemMeta> metaCache = [];

    private readonly HashSet<SlotKey> selection = [];
    private SlotKey? selectionAnchor;
    private string statusMessage = string.Empty;
    private string searchText = string.Empty;
    private int categoryFilter = FilterAll;
    private List<uint>? dragKeys;

    public MainWindow(Plugin plugin)
        : base("BladUI — Inventory###BladUIMain")
    {
        Size = new Vector2(560, 440);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 240),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            ImGui.Text("Log in to a character to use BladUI.");
            return;
        }

        var saddlebagOpen = InventoryService.AnyLoaded(InventoryService.SaddlebagPages);
        var retainerOpen = InventoryService.AnyLoaded(InventoryService.RetainerPages);

        using var tabBar = ImRaii.TabBar("BladUITabs");
        if (!tabBar.Success)
            return;

        using (var tab = ImRaii.TabItem("Inventory"))
        {
            if (tab.Success)
            {
                var (dest, destName) = retainerOpen
                    ? (InventoryService.RetainerPages, "Retainer")
                    : saddlebagOpen
                        ? (InventoryService.SaddlebagPages, "Saddlebag")
                        : (null, string.Empty);
                DrawContainerView("inv", InventoryService.PlayerBags, dest, destName);
            }
        }

        if (saddlebagOpen)
        {
            using var tab = ImRaii.TabItem("Saddlebag");
            if (tab.Success)
                DrawContainerView("saddle", InventoryService.SaddlebagPages, InventoryService.PlayerBags, "Inventory");
        }

        if (retainerOpen)
        {
            using var tab = ImRaii.TabItem("Retainer");
            if (tab.Success)
                DrawContainerView("retainer", InventoryService.RetainerPages, InventoryService.PlayerBags, "Inventory");
        }
    }

    private void DrawContainerView(string id, InventoryType[] sources, InventoryType[]? destination, string destinationName)
    {
        // Snapshot all slots of this view in physical slot order.
        var slots = new List<SlotInfo>();
        foreach (var container in sources)
            slots.AddRange(InventoryService.GetSlots(container));

        // Drop stale selection entries for slots (in this view) that emptied out.
        var occupied = slots.Where(s => s.ItemId != 0).Select(s => new SlotKey(s.Container, s.Slot)).ToHashSet();
        selection.RemoveWhere(key => sources.Contains(key.Container) && !occupied.Contains(key));

        var view = BuildView(slots);

        DrawActionBar(slots, view, destination, destinationName);
        DrawToolBar(slots);
        ImGui.Separator();

        // Explicit positive child height so the grid always fills the window and
        // the footer stays pinned to the bottom as a single compact line.
        var footerHeight = ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y + 2f;
        var childHeight = Math.Max(50f, ImGui.GetContentRegionAvail().Y - footerHeight);
        using (var child = ImRaii.Child($"{id}Grid", new Vector2(0, childHeight), false))
        {
            if (child.Success)
                DrawGrid(id, view, destination, destinationName);
        }

        DrawFooter(slots);
        HandleShortcuts(view);
    }

    #region View building

    private List<SlotInfo> BuildView(List<SlotInfo> slots)
    {
        var sortMode = plugin.Configuration.SortMode;
        var search = searchText.Trim().ToLowerInvariant();
        var noFilters = search.Length == 0 && categoryFilter == FilterAll;

        // True bag layout (gaps included) only when explicitly enabled.
        if (sortMode == SortSlotOrder && noFilters && plugin.Configuration.ShowEmptySlots)
            return slots;

        IEnumerable<SlotInfo> items = slots.Where(s => s.ItemId != 0);

        if (categoryFilter == FilterWares)
            items = items.Where(s => plugin.Configuration.Wares.Contains(OrderKey(s)));
        else if (categoryFilter != FilterAll)
            items = items.Where(s => GetMeta(s.ItemId).CategoryId == categoryFilter);

        if (search.Length > 0)
            items = items.Where(s => GetMeta(s.ItemId).NameLower.Contains(search));

        items = sortMode switch
        {
            SortCustom => OrderCustom(items),
            // Type: game-like category buckets, best gear first inside each.
            1 => items.OrderBy(s => GetMeta(s.ItemId).CategoryMajor)
                      .ThenBy(s => GetMeta(s.ItemId).CategoryMinor)
                      .ThenByDescending(s => GetMeta(s.ItemId).ItemLevel)
                      .ThenBy(s => GetMeta(s.ItemId).Name),
            2 => items.OrderBy(s => GetMeta(s.ItemId).Name),
            3 => items.OrderByDescending(s => GetMeta(s.ItemId).ItemLevel)
                      .ThenBy(s => GetMeta(s.ItemId).Name),
            4 => items.OrderByDescending(s => GetMeta(s.ItemId).Rarity)
                      .ThenBy(s => GetMeta(s.ItemId).CategoryMajor)
                      .ThenBy(s => GetMeta(s.ItemId).Name),
            5 => items.OrderByDescending(s => s.Quantity)
                      .ThenBy(s => GetMeta(s.ItemId).Name),
            6 => items.OrderByDescending(s => s.ItemId),
            _ => items, // Slot order: already in physical order.
        };

        return items.ToList();
    }

    /// <summary>Stable identity of an item for arrangement/wares (HQ counts as its own entry).</summary>
    private static uint OrderKey(SlotInfo s) => (s.ItemId << 1) | (s.IsHq ? 1u : 0u);

    /// <summary>
    /// BladUI's own view: items appear in the user's persisted arrangement.
    /// Items never seen before are appended (grouped by type) and remembered.
    /// </summary>
    private IEnumerable<SlotInfo> OrderCustom(IEnumerable<SlotInfo> items)
    {
        var list = items.ToList();
        var order = plugin.Configuration.CustomOrder;

        var known = new HashSet<uint>(order);
        var newKeys = list
            .Where(s => !known.Contains(OrderKey(s)))
            .OrderBy(s => GetMeta(s.ItemId).CategoryMajor)
            .ThenBy(s => GetMeta(s.ItemId).CategoryMinor)
            .ThenBy(s => GetMeta(s.ItemId).Name)
            .Select(OrderKey)
            .Distinct()
            .ToList();
        if (newKeys.Count > 0)
        {
            order.AddRange(newKeys);
            plugin.Configuration.Save();
        }

        var position = new Dictionary<uint, int>(order.Count);
        for (var i = 0; i < order.Count; i++)
            position.TryAdd(order[i], i);

        return list.OrderBy(s => position.GetValueOrDefault(OrderKey(s), int.MaxValue));
    }

    /// <summary>Move the dragged keys so they sit immediately before the drop target.</summary>
    private void ReorderCustom(List<uint> movingKeys, uint targetKey)
    {
        if (movingKeys.Contains(targetKey))
            return;

        var order = plugin.Configuration.CustomOrder;
        order.RemoveAll(movingKeys.Contains);

        var insertAt = order.IndexOf(targetKey);
        if (insertAt < 0)
            insertAt = order.Count;
        order.InsertRange(insertAt, movingKeys);

        plugin.Configuration.Save();
    }

    #endregion

    #region Toolbars

    private void DrawActionBar(List<SlotInfo> slots, List<SlotInfo> view, InventoryType[]? destination, string destinationName)
    {
        var selectedHere = slots.Where(s => s.ItemId != 0 && selection.Contains(new SlotKey(s.Container, s.Slot))).ToList();

        using (ImRaii.Disabled(destination == null || selectedHere.Count == 0))
        {
            var label = destination == null
                ? "Move selected"
                : $"Move {selectedHere.Count} selected → {destinationName}";
            if (ImGui.Button(label))
                MoveSelected(selectedHere, destination!, destinationName);
        }

        if (destination == null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Open a retainer or the saddlebag to move items.");

        ImGui.SameLine();
        if (ImGui.Button("Select all"))
            SelectAll(view);
        if (ImGui.IsItemHovered() && (searchText.Trim().Length > 0 || categoryFilter != FilterAll))
            ImGui.SetTooltip("Selects only the items matching your search/filter.");

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            selection.Clear();
            selectionAnchor = null;
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(selectedHere.Count == 0))
        {
            var anyUnmarked = selectedHere.Any(s => !plugin.Configuration.Wares.Contains(OrderKey(s)));
            if (ImGui.Button(anyUnmarked ? "Mark wares" : "Unmark wares"))
                ToggleWares(selectedHere.Select(OrderKey).Distinct().ToList(), anyUnmarked);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Wares are your to-sell pile (like Baldur's Gate).\nMarked items get a coin badge and are summed below.");

        ImGui.SameLine();
        var (waresCount, waresGil) = WaresTotals(slots);
        using (ImRaii.Disabled(true))
            ImGui.Button(waresCount > 0 ? $"Sell wares ({waresCount} · {waresGil:N0} gil)" : "Sell wares");
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Coming in v0.3 — sell every marked ware to an open shop in one click.");
    }

    private void DrawToolBar(List<SlotInfo> slots)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var sortWidth = 130f * scale;
        var catWidth = 150f * scale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        ImGui.SetNextItemWidth(Math.Max(100f, ImGui.GetContentRegionAvail().X - sortWidth - catWidth - spacing * 2));
        ImGui.InputTextWithHint("##bladSearch", "Search items…", ref searchText, 128);

        // Category filter, built from what you are actually carrying.
        ImGui.SameLine();
        ImGui.SetNextItemWidth(catWidth);
        var filterLabel = categoryFilter switch
        {
            FilterAll => "All categories",
            FilterWares => "Wares",
            _ => slots.Where(s => s.ItemId != 0)
                      .Select(s => GetMeta(s.ItemId))
                      .FirstOrDefault(m => m.CategoryId == categoryFilter).CategoryName ?? "Category",
        };
        if (ImGui.BeginCombo("##bladFilter", filterLabel))
        {
            if (ImGui.Selectable("All categories", categoryFilter == FilterAll))
                categoryFilter = FilterAll;
            if (ImGui.Selectable($"Wares ({plugin.Configuration.Wares.Count} marked)", categoryFilter == FilterWares))
                categoryFilter = FilterWares;
            ImGui.Separator();

            var categories = slots
                .Where(s => s.ItemId != 0)
                .Select(s => GetMeta(s.ItemId))
                .GroupBy(m => m.CategoryId)
                .Select(g => g.First())
                .OrderBy(m => m.CategoryMajor)
                .ThenBy(m => m.CategoryMinor);
            foreach (var cat in categories)
            {
                if (ImGui.Selectable(cat.CategoryName, categoryFilter == cat.CategoryId))
                    categoryFilter = cat.CategoryId;
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(sortWidth);
        var sortMode = plugin.Configuration.SortMode;
        if (ImGui.Combo("##bladSort", ref sortMode, SortModeNames, SortModeNames.Length))
        {
            plugin.Configuration.SortMode = sortMode;
            plugin.Configuration.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("View order only — your real inventory slots are untouched.\n\"Custom\" is BladUI's own arrangement: drag items to place them; new items append at the end.\n\"Newest (ID)\" approximates patch order (higher item ID = added later).");
    }

    #endregion

    #region Grid

    private static Vector4 RarityColor(int rarity) => rarity switch
    {
        1 => new Vector4(0.78f, 0.78f, 0.80f, 1f), // common — pale steel
        2 => new Vector4(0.38f, 0.82f, 0.38f, 1f), // uncommon — green
        3 => new Vector4(0.38f, 0.62f, 0.98f, 1f), // rare — blue
        4 => new Vector4(0.72f, 0.48f, 0.98f, 1f), // relic — purple
        7 => new Vector4(0.95f, 0.52f, 0.76f, 1f), // aetherial — pink
        _ => new Vector4(0.60f, 0.60f, 0.62f, 1f),
    };

    private void DrawGrid(string id, List<SlotInfo> view, InventoryType[]? destination, string destinationName)
    {
        if (view.Count == 0)
        {
            ImGui.TextDisabled(searchText.Trim().Length > 0 || categoryFilter != FilterAll
                ? "No items match your search/filter."
                : "Nothing here.");
            return;
        }

        var cfg = plugin.Configuration;
        var cellSize = new Vector2(cfg.IconSize) * ImGuiHelpers.GlobalScale;
        var columns = Math.Max(1, cfg.GridColumns);
        var drawList = ImGui.GetWindowDrawList();
        var io = ImGui.GetIO();

        for (var i = 0; i < view.Count; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            var slot = view[i];
            var key = new SlotKey(slot.Container, slot.Slot);
            var isSelected = selection.Contains(key);

            using var idScope = ImRaii.PushId($"{id}-{i}");
            var pos = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton("slot", cellSize);
            var hovered = ImGui.IsItemHovered();
            var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);

            if (slot.ItemId != 0)
            {
                var meta = GetMeta(slot.ItemId);
                var rarity = RarityColor(meta.Rarity);

                // BG3-style tile: dark slate base with a rarity-tinted gradient.
                drawList.AddRectFilled(pos, pos + cellSize, ImGui.GetColorU32(new Vector4(0.09f, 0.09f, 0.11f, 0.92f)), 4f);
                var tintTop = ImGui.GetColorU32(new Vector4(rarity.X, rarity.Y, rarity.Z, 0.05f));
                var tintBottom = ImGui.GetColorU32(new Vector4(rarity.X, rarity.Y, rarity.Z, 0.28f));
                drawList.AddRectFilledMultiColor(pos + new Vector2(1f, 1f), pos + cellSize - new Vector2(1f, 1f), tintTop, tintTop, tintBottom, tintBottom);

                var customMode = cfg.SortMode == SortCustom;
                if (customMode)
                {
                    if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                    {
                        dragKeys = selection.Contains(key)
                            ? view.Where(s => s.ItemId != 0 && selection.Contains(new SlotKey(s.Container, s.Slot)))
                                  .Select(OrderKey).Distinct().ToList()
                            : [OrderKey(slot)];
                        ImGui.SetDragDropPayload("BLADUI_ARRANGE", ReadOnlySpan<byte>.Empty);
                        ImGui.Text(dragKeys.Count > 1 ? $"Arrange {dragKeys.Count} items" : meta.Name);
                        ImGui.EndDragDropSource();
                    }

                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("BLADUI_ARRANGE");
                        unsafe
                        {
                            if (payload.Handle != null && dragKeys != null)
                            {
                                ReorderCustom(dragKeys, OrderKey(slot));
                                dragKeys = null;
                            }
                        }

                        ImGui.EndDragDropTarget();
                    }
                }

                var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(meta.IconId, slot.IsHq)).GetWrapOrEmpty();
                drawList.AddImage(wrap.Handle, pos + new Vector2(2f, 2f), pos + cellSize - new Vector2(2f, 2f));

                if (slot.IsHq)
                {
                    drawList.AddText(pos + new Vector2(3f, 1f), ImGui.GetColorU32(new Vector4(1f, 0.9f, 0.4f, 1f)),
                        SeIconChar.HighQuality.ToIconString());
                }

                if (plugin.Configuration.Wares.Contains(OrderKey(slot)))
                {
                    var badgeCenter = pos + new Vector2(cellSize.X - 7f, 7f);
                    drawList.AddCircleFilled(badgeCenter, 5f, ImGui.GetColorU32(new Vector4(0.95f, 0.78f, 0.22f, 1f)));
                    drawList.AddCircle(badgeCenter, 5f, ImGui.GetColorU32(new Vector4(0.25f, 0.18f, 0.02f, 1f)), 0, 1.5f);
                }

                if (slot.Quantity > 1)
                {
                    var qty = slot.Quantity.ToString();
                    var textSize = ImGui.CalcTextSize(qty);
                    var textPos = pos + cellSize - textSize - new Vector2(3f, 1f);
                    drawList.AddText(textPos + new Vector2(1f, 1f), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)), qty);
                    drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), qty);
                }

                if (hovered)
                    DrawItemTooltip(slot, meta, destination, destinationName);

                DrawItemContextMenu(slot, key, meta, destination, destinationName);

                if (clicked)
                    HandleItemClick(view, i, key, io, destination, destinationName);

                // Border: selection gold beats rarity; hover brightens.
                if (isSelected)
                {
                    drawList.AddRect(pos, pos + cellSize, ImGui.GetColorU32(new Vector4(1f, 0.82f, 0.26f, 1f)), 4f, ImDrawFlags.None, 2.5f);
                }
                else
                {
                    var alpha = hovered ? 1f : 0.55f;
                    drawList.AddRect(pos, pos + cellSize, ImGui.GetColorU32(new Vector4(rarity.X, rarity.Y, rarity.Z, alpha)), 4f, ImDrawFlags.None, hovered ? 2f : 1.2f);
                }
            }
            else
            {
                drawList.AddRectFilled(pos, pos + cellSize, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 4f);
                drawList.AddRect(pos, pos + cellSize, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 4f);
                if (clicked && !io.KeyCtrl && !io.KeyShift)
                {
                    selection.Clear();
                    selectionAnchor = null;
                }
            }
        }
    }

    private void DrawItemTooltip(SlotInfo slot, ItemMeta meta, InventoryType[]? destination, string destinationName)
    {
        using var tooltip = ImRaii.Tooltip();

        ImGui.TextColored(RarityColor(meta.Rarity), $"{meta.Name}{(slot.IsHq ? " " + SeIconChar.HighQuality.ToIconString() : string.Empty)}");
        ImGui.TextDisabled(meta.ItemLevel > 1 ? $"{meta.CategoryName} — item level {meta.ItemLevel}" : meta.CategoryName);

        if (meta.Description.Length > 0)
        {
            ImGui.PushTextWrapPos(320f * ImGuiHelpers.GlobalScale);
            ImGui.TextWrapped(meta.Description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        ImGui.Text(meta.SellPrice > 0
            ? $"Sells for {meta.SellPrice:N0} gil{(slot.Quantity > 1 ? $" ({meta.SellPrice * (uint)slot.Quantity:N0} for the stack of {slot.Quantity})" : string.Empty)}"
            : "Cannot be sold to vendors");

        ImGui.Separator();
        if (destination != null)
            ImGui.TextDisabled(plugin.Configuration.CtrlClickInstantMove
                ? $"Ctrl+Click: move to {destinationName}"
                : "Ctrl+Click: toggle selection");
        ImGui.TextDisabled("Shift+Click: range · Right-click: menu");
        if (plugin.Configuration.SortMode == SortCustom)
            ImGui.TextDisabled("Drag: arrange your view");
    }

    private void DrawItemContextMenu(SlotInfo slot, SlotKey key, ItemMeta meta, InventoryType[]? destination, string destinationName)
    {
        if (!ImGui.BeginPopupContextItem("ctx"))
            return;

        // Right-clicking a selected item acts on the whole selection.
        var actOnSelection = selection.Contains(key) && selection.Count > 1;
        var scopeLabel = actOnSelection ? $"{selection.Count} selected items" : meta.Name;

        ImGui.TextColored(RarityColor(meta.Rarity), scopeLabel);
        ImGui.Separator();

        if (destination != null && ImGui.MenuItem($"Move to {destinationName}"))
        {
            if (actOnSelection)
            {
                var items = CurrentSelectionSlots(slot);
                MoveSelected(items, destination, destinationName);
            }
            else
            {
                statusMessage = InventoryService.MoveToFirstFree(key, destination)
                    ? $"Moved {meta.Name} to {destinationName}."
                    : $"Could not move {meta.Name} — {destinationName} full?";
            }
        }

        var keys = actOnSelection
            ? CurrentSelectionSlots(slot).Select(OrderKey).Distinct().ToList()
            : [OrderKey(slot)];
        var anyUnmarked = keys.Any(k => !plugin.Configuration.Wares.Contains(k));
        if (ImGui.MenuItem(anyUnmarked ? "Mark as wares" : "Unmark wares"))
            ToggleWares(keys, anyUnmarked);

        ImGui.Separator();
        if (ImGui.MenuItem("Select only this"))
        {
            selection.Clear();
            selection.Add(key);
            selectionAnchor = key;
        }

        ImGui.EndPopup();
    }

    /// <summary>Occupied slots of the current selection, re-read from the live containers the given slot belongs to.</summary>
    private List<SlotInfo> CurrentSelectionSlots(SlotInfo reference)
    {
        var sources = InventoryService.PlayerBags.Contains(reference.Container)
            ? InventoryService.PlayerBags
            : InventoryService.SaddlebagPages.Contains(reference.Container)
                ? InventoryService.SaddlebagPages
                : InventoryService.RetainerPages;

        var result = new List<SlotInfo>();
        foreach (var container in sources)
            result.AddRange(InventoryService.GetSlots(container).Where(s => s.ItemId != 0 && selection.Contains(new SlotKey(s.Container, s.Slot))));
        return result;
    }

    private void HandleItemClick(List<SlotInfo> view, int index, SlotKey key, ImGuiIOPtr io, InventoryType[]? destination, string destinationName)
    {
        if (io.KeyCtrl)
        {
            if (plugin.Configuration.CtrlClickInstantMove && destination != null)
            {
                statusMessage = InventoryService.MoveToFirstFree(key, destination)
                    ? $"Moved item to {destinationName}."
                    : $"Could not move item to {destinationName} (no free slot?).";
                selection.Remove(key);
            }
            else
            {
                if (!selection.Remove(key))
                    selection.Add(key);
                selectionAnchor = key;
            }

            return;
        }

        if (io.KeyShift && selectionAnchor != null)
        {
            var anchorIndex = view.FindIndex(s => new SlotKey(s.Container, s.Slot) == selectionAnchor.Value);
            if (anchorIndex >= 0)
            {
                var from = Math.Min(anchorIndex, index);
                var to = Math.Max(anchorIndex, index);
                for (var i = from; i <= to; i++)
                {
                    if (view[i].ItemId != 0)
                        selection.Add(new SlotKey(view[i].Container, view[i].Slot));
                }

                return;
            }
        }

        selection.Clear();
        selection.Add(key);
        selectionAnchor = key;
    }

    #endregion

    #region Footer, wares, actions

    private void DrawFooter(List<SlotInfo> slots)
    {
        ImGui.Separator();

        var selected = slots.Where(s => s.ItemId != 0 && selection.Contains(new SlotKey(s.Container, s.Slot))).ToList();
        var used = slots.Count(s => s.ItemId != 0);
        var fraction = slots.Count == 0 ? 0f : (float)used / slots.Count;

        // Slim BG3-style capacity bar.
        var barColor = fraction > 0.9f
            ? new Vector4(0.85f, 0.35f, 0.25f, 1f)
            : fraction > 0.75f
                ? new Vector4(0.9f, 0.7f, 0.25f, 1f)
                : new Vector4(0.45f, 0.55f, 0.65f, 1f);
        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, barColor))
            ImGui.ProgressBar(fraction, new Vector2(90f * ImGuiHelpers.GlobalScale, ImGui.GetTextLineHeight() * 0.8f), string.Empty);

        ImGui.SameLine();
        var line = $"{used}/{slots.Count} — {selected.Count} selected (qty {selected.Sum(s => s.Quantity)})";
        if (!string.IsNullOrEmpty(statusMessage))
            line += $"  •  {statusMessage}";
        ImGui.TextDisabled(line);
    }

    private (int Count, long Gil) WaresTotals(List<SlotInfo> slots)
    {
        var count = 0;
        var gil = 0L;
        foreach (var s in slots.Where(s => s.ItemId != 0 && plugin.Configuration.Wares.Contains(OrderKey(s))))
        {
            count++;
            gil += GetMeta(s.ItemId).SellPrice * (long)s.Quantity;
        }

        return (count, gil);
    }

    private void ToggleWares(List<uint> keys, bool mark)
    {
        var wares = plugin.Configuration.Wares;
        if (mark)
        {
            foreach (var k in keys.Where(k => !wares.Contains(k)))
                wares.Add(k);
        }
        else
        {
            wares.RemoveAll(keys.Contains);
        }

        plugin.Configuration.Save();
    }

    private void SelectAll(List<SlotInfo> view)
    {
        foreach (var s in view.Where(s => s.ItemId != 0))
            selection.Add(new SlotKey(s.Container, s.Slot));
    }

    private void HandleShortcuts(List<SlotInfo> view)
    {
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
            return;

        var io = ImGui.GetIO();
        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.A) && !ImGui.IsAnyItemActive())
            SelectAll(view);

        if (ImGui.IsKeyPressed(ImGuiKey.Escape) && !ImGui.IsAnyItemActive())
        {
            selection.Clear();
            selectionAnchor = null;
        }
    }

    private void MoveSelected(List<SlotInfo> items, InventoryType[] destination, string destinationName)
    {
        var moved = 0;
        foreach (var item in items)
        {
            if (InventoryService.MoveToFirstFree(new SlotKey(item.Container, item.Slot), destination))
            {
                selection.Remove(new SlotKey(item.Container, item.Slot));
                moved++;
            }
            else
            {
                break; // Destination full (or move rejected) — stop instead of spamming.
            }
        }

        statusMessage = moved == items.Count
            ? $"Moved {moved} item(s) to {destinationName}."
            : $"Moved {moved}/{items.Count} item(s) to {destinationName} — destination full?";
        Plugin.Log.Information(statusMessage);
    }

    #endregion

    private ItemMeta GetMeta(uint itemId)
    {
        if (metaCache.TryGetValue(itemId, out var cached))
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
                row.Rarity,
                row.PriceLow,
                row.Description.ExtractText());
        }
        else
        {
            meta = new ItemMeta($"Unknown item #{itemId}", $"unknown item #{itemId}", 0, 999, 999, 0, "Unknown", 0, 0, 0, string.Empty);
        }

        metaCache[itemId] = meta;
        return meta;
    }
}
