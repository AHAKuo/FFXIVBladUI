using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace BladUI.Windows;

/// <summary>
/// Bulk action profiles: pick a utility, review its itemized plan, run it.
/// Nothing executes without the preview confirm.
/// </summary>
public class UtilitiesWindow : Window, IDisposable
{
    private readonly List<IBulkAction> actions;

    private IBulkAction? active;
    private Task? preparing;
    private List<PlannedStep>? plan;
    private string? planNote;
    private string? error;
    private string report = string.Empty;

    public UtilitiesWindow(Plugin plugin) : base("BladUI — Utilities###BladUIUtilities")
    {
        Size = new Vector2(520, 420);
        SizeCondition = ImGuiCond.FirstUseEver;

        actions =
        [
            new CleanArmouryAction(plugin),
            new MarkGarbageAction(plugin),
        ];
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (active == null)
            DrawActionList();
        else
            DrawActiveAction();

        if (report.Length > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled(report);
        }
    }

    private void DrawActionList()
    {
        ImGui.TextWrapped("Bulk action profiles. Each one plans first — you review the exact list before anything runs.");
        ImGui.Spacing();

        foreach (var action in actions)
        {
            using var id = ImRaii.PushId(action.Name);
            ImGui.Separator();
            ImGui.Text(action.Name);
            ImGui.TextWrapped(action.Description);
            action.DrawOptions();

            if (ImGui.Button($"Plan: {action.Name}"))
            {
                active = action;
                plan = null;
                planNote = null;
                error = null;
                report = string.Empty;
                try
                {
                    preparing = action.PrepareAsync();
                }
                catch (Exception e)
                {
                    error = e.Message;
                    preparing = null;
                }
            }
        }
    }

    private void DrawActiveAction()
    {
        ImGui.Text(active!.Name);
        ImGui.Separator();

        if (error != null)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.4f, 0.35f, 1f), $"Failed: {error}");
            if (ImGui.Button("Back"))
                Reset();
            return;
        }

        if (preparing is { IsCompleted: false })
        {
            ImGui.Text("Preparing… (fetching market prices)");
            if (ImGui.Button("Cancel"))
                Reset();
            return;
        }

        if (preparing is { IsFaulted: true })
        {
            error = preparing.Exception?.GetBaseException().Message ?? "unknown error";
            return;
        }

        // Plan on the main thread once preparation is done.
        if (plan == null)
        {
            try
            {
                plan = active.Plan(out planNote);
            }
            catch (Exception e)
            {
                error = e.Message;
                return;
            }
        }

        if (planNote != null)
            ImGui.TextDisabled(planNote);

        if (plan.Count == 0)
        {
            ImGui.Text("Nothing to do — no items matched.");
            if (ImGui.Button("Back"))
                Reset();
            return;
        }

        var enabled = plan.Count(s => s.Enabled);
        ImGui.Text($"{plan.Count} step(s) planned, {enabled} enabled:");

        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        using (var child = ImRaii.Child("planList", new Vector2(0, -footer), true))
        {
            if (child.Success)
            {
                for (var i = 0; i < plan.Count; i++)
                {
                    using var id = ImRaii.PushId(i);
                    var step = plan[i];
                    ImGui.Checkbox("##on", ref step.Enabled);
                    ImGui.SameLine();
                    ImGui.Text($"{step.Name}{(step.Slot.Quantity > 1 ? $" ×{step.Slot.Quantity}" : string.Empty)}");
                    ImGui.SameLine();
                    ImGui.TextDisabled($"— {step.Reason}");
                }
            }
        }

        using (ImRaii.Disabled(enabled == 0))
        {
            if (ImGui.Button($"Run {enabled} step(s)"))
                RunPlan();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            Reset();
    }

    private void RunPlan()
    {
        var ok = 0;
        var failed = 0;
        foreach (var step in plan!.Where(s => s.Enabled))
        {
            if (step.Run())
                ok++;
            else
                failed++;
        }

        active!.Finish();
        report = failed == 0
            ? $"{active.Name}: {ok} step(s) done."
            : $"{active.Name}: {ok} done, {failed} failed (bags full?).";
        Plugin.Log.Information(report);
        Reset();
    }

    private void Reset()
    {
        active = null;
        preparing = null;
        plan = null;
        planNote = null;
        error = null;
    }
}
