using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using TheCollector.CollectableManager;
using TheCollector.Data;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class StopUi : Window
{
    private readonly AutomationHandler _automation;
    private readonly CollectableAutomationHandler _collectableHandler;

    public StopUi(AutomationHandler automation, CollectableAutomationHandler collectableHandler)
        : base("The Collector##CollectorStop",
               ImGuiWindowFlags.NoScrollbar
               | ImGuiWindowFlags.NoScrollWithMouse
               | ImGuiWindowFlags.NoResize
               | ImGuiWindowFlags.NoSavedSettings
               | ImGuiWindowFlags.AlwaysAutoResize)
    {
        _automation = automation;
        _collectableHandler = collectableHandler;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 0),
            MaximumSize = new Vector2(350, 800)
        };
    }

    public override void PreOpenCheck()
    {
        IsOpen = _automation.IsRunning;
    }

    public override void PreDraw()
    {
        var io = ImGui.GetIO();
        var center = io.DisplaySize / 2f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
    }

    public override void Draw()
    {
        ImGuiHelper.Panel("StatusInfo", DrawStatusInfo);
        DrawStopButton();
    }

    private void DrawStatusInfo()
    {
        var (color, label) = Plugin.State switch
        {
            PluginState.Teleporting               => (new Vector4(0.95f, 0.75f, 0.10f, 1f), "Teleporting"),
            PluginState.MovingToCollectableVendor => (new Vector4(0.95f, 0.75f, 0.10f, 1f), "Moving to vendor"),
            PluginState.ExchangingItems           => (new Vector4(0.30f, 0.85f, 0.30f, 1f), "Exchanging items"),
            PluginState.SpendingScrip             => (new Vector4(0.30f, 0.85f, 0.30f, 1f), "Spending scrip"),
            PluginState.AutoRetainer              => (new Vector4(0.40f, 0.65f, 1.00f, 1f), "AutoRetainer running"),
            _                                     => (new Vector4(0.55f, 0.55f, 0.55f, 1f), "Idle")
        };

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted($"● {label}");
        ImGui.PopStyleColor();

        if (Plugin.State == PluginState.ExchangingItems)
        {
            var q = _collectableHandler.TurnInQueue;
            if (q != null && q.Count != 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Turn-in queue:");
                ImGui.Separator();
                ImGui.Spacing();

                for (int i = 0; i < q.Count; i++)
                {
                    var (_, name, left, _) = q[i];
                    bool isCurrent = _collectableHandler.CurrentItemName is not null &&
                                     _collectableHandler.CurrentItemName == name;

                    if (isCurrent)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.30f, 0.90f, 0.30f, 1f));
                        ImGui.TextUnformatted("▶");
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.TextDisabled(" ");
                    }

                    ImGui.SameLine();

                    if (isCurrent)
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.30f, 0.90f, 0.30f, 1f));

                    ImGui.TextUnformatted(name);

                    if (isCurrent)
                        ImGui.PopStyleColor();

                    ImGui.SameLine();
                    ImGui.TextDisabled($"({left} left)");
                }
            }
        }
    }

    private void DrawStopButton()
    {
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.65f, 0.10f, 0.10f, 0.90f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.85f, 0.15f, 0.15f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(1.00f, 0.20f, 0.20f, 1.00f));

        if (ImGui.Button("Stop", new Vector2(ImGui.GetContentRegionAvail().X, 50)))
            _automation?.ForceStop("Stopped by user");

        ImGui.PopStyleColor(3);
    }
}
