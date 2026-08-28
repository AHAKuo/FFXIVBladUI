using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BladUI.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("BladUI Settings###BladUIConfig")
    {
        Size = new Vector2(380, 180);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var ctrlMove = configuration.CtrlClickInstantMove;
        if (ImGui.Checkbox("Ctrl+Click instantly moves item to other window", ref ctrlMove))
        {
            configuration.CtrlClickInstantMove = ctrlMove;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When a retainer or the saddlebag is open, ctrl+clicking an item\nmoves it there immediately. When off, ctrl+click toggles selection.");

        var showEmpty = configuration.ShowEmptySlots;
        if (ImGui.Checkbox("Show empty slots (true bag layout)", ref showEmpty))
        {
            configuration.ShowEmptySlots = showEmpty;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Off: the grid compacts to items only.\nOn: \"Slot order (game)\" shows your bags exactly as they are, gaps included.");

        if (ImGui.Button("Reset custom arrangement"))
        {
            configuration.CustomOrder.Clear();
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Forget your drag-arranged order. The Custom view rebuilds grouped by type.");

        var columns = configuration.GridColumns;
        if (ImGui.SliderInt("Grid columns", ref columns, 5, 20))
        {
            configuration.GridColumns = columns;
            configuration.Save();
        }

        var iconSize = configuration.IconSize;
        if (ImGui.SliderFloat("Icon size", ref iconSize, 24f, 64f, "%.0f px"))
        {
            configuration.IconSize = iconSize;
            configuration.Save();
        }
    }
}
