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
    private readonly StatusService _status;

    public StopUi(AutomationHandler automation, CollectableAutomationHandler collectableHandler, StatusService status)
        : base("The Collector##CollectorStop",
               ImGuiWindowFlags.NoScrollbar
               | ImGuiWindowFlags.NoScrollWithMouse
               | ImGuiWindowFlags.NoResize
               | ImGuiWindowFlags.NoSavedSettings
               | ImGuiWindowFlags.AlwaysAutoResize)
    {
        _automation = automation;
        _collectableHandler = collectableHandler;
        _status = status;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 0),
            MaximumSize = new Vector2(360, 800)
        };
    }

    public override void PreOpenCheck()
    {
        IsOpen = _automation.IsRunning;
    }

    public override void PreDraw()
    {
        UiTheme.Push();

        var io = ImGui.GetIO();
        var center = io.DisplaySize / 2f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
    }

    public override void PostDraw()
    {
        UiTheme.Pop();
    }

    public override void Draw()
    {
        ImGuiHelper.Panel("StatusInfo", DrawStatusInfo);
        DrawStopButton();
    }

    private void DrawStatusInfo()
    {
        var (color, label, isActive) = PluginStatePresentation.Describe(_status.Current, _status.Detail);

        ImGuiHelper.StatusDot(color, pulse: isActive);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();

        if (_status.Current == PluginState.ExchangingItems)
        {
            var q = _collectableHandler.TurnInQueue;
            if (q != null && q.Count != 0)
            {
                ImGui.Spacing();
                ImGuiHelper.SectionHeader("Turn-in queue");

                for (int i = 0; i < q.Count; i++)
                {
                    var (_, name, left, _) = q[i];
                    bool isCurrent = _collectableHandler.CurrentItemName is not null &&
                                     _collectableHandler.CurrentItemName == name;

                    if (isCurrent)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Success);
                        ImGui.TextUnformatted(">");
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.TextDisabled(" ");
                    }

                    ImGui.SameLine();

                    if (isCurrent)
                        ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Success);

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
        if (ImGuiHelper.DangerButton("Stop", new Vector2(ImGui.GetContentRegionAvail().X, 50)))
            _automation?.ForceStop("Stopped by user");
    }
}
