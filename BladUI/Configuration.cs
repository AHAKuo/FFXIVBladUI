using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace BladUI;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    /// <summary>
    /// When true, ctrl+clicking an item instantly moves it to the open
    /// "other window" (retainer or saddlebag). When false, ctrl+click
    /// toggles selection instead.
    /// </summary>
    public bool CtrlClickInstantMove { get; set; } = true;

    /// <summary>Columns per bag row in the grid.</summary>
    public int GridColumns { get; set; } = 10;

    /// <summary>Icon size in pixels (before UI scaling).</summary>
    public float IconSize { get; set; } = 40f;

    /// <summary>Last used view sort mode (index into MainWindow.SortModeNames).</summary>
    public int SortMode { get; set; } = 0;

    /// <summary>
    /// Show empty slots in slot-order view (true bag layout with gaps).
    /// When false the grid compacts to occupied slots only.
    /// </summary>
    public bool ShowEmptySlots { get; set; } = false;

    /// <summary>
    /// User-defined item arrangement for the Custom view. Each entry is an
    /// item key (itemId &lt;&lt; 1 | hqBit); list index is display position.
    /// New items are appended automatically as they are first seen.
    /// </summary>
    public List<uint> CustomOrder { get; set; } = [];

    /// <summary>
    /// Items marked as wares (to-sell pile), keyed like CustomOrder
    /// (itemId &lt;&lt; 1 | hqBit).
    /// </summary>
    public List<uint> Wares { get; set; } = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
