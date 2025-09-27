using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace TheCollector.Windows;

public class StopUi : Window, IDisposable
{
    private readonly AutomationHandler _automation;

    public StopUi(AutomationHandler automation)
        : base("The Collector##CollectorStop",
               ImGuiWindowFlags.NoScrollbar
               | ImGuiWindowFlags.NoScrollWithMouse
               | ImGuiWindowFlags.NoResize
               | ImGuiWindowFlags.NoCollapse
               | ImGuiWindowFlags.NoSavedSettings
               | ImGuiWindowFlags.NoMove)
    {
        _automation = automation;
        Size = new Vector2(250, 90);
        RespectCloseHotkey = false;
        ShowCloseButton = false;
        DisableWindowSounds = true;
    }

    public override void PreOpenCheck()
    {
        if (_automation.IsRunning)
            IsOpen = true;
        else IsOpen = false;
    }

    public override void PreDraw()
    {
        var io = ImGui.GetIO();
        var center = io.DisplaySize / 2f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowFocus();
    }

    public override void Draw()
    {
        var w = ImGui.GetContentRegionAvail().X;
        var y = ImGui.GetContentRegionAvail().Y;
        if (ImGui.Button("Stop", new Vector2(w, y)))
            _automation?.ForceStop("Stopped by user");
    }

    public void Dispose() { }
}
