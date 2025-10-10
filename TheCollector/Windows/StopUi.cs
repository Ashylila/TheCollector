using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using TheCollector.CollectableManager;
using TheCollector.Data;

namespace TheCollector.Windows;

public class StopUi : Window, IDisposable
{
    private readonly AutomationHandler _automation;
    private readonly CollectableAutomationHandler _collectableHandler;

    public StopUi(AutomationHandler automation, CollectableAutomationHandler collectableHandler)
        : base("The Collector##CollectorStop",
               ImGuiWindowFlags.NoScrollbar
               | ImGuiWindowFlags.NoScrollWithMouse
               | ImGuiWindowFlags.NoResize
               | ImGuiWindowFlags.NoCollapse
               | ImGuiWindowFlags.NoSavedSettings
               | ImGuiWindowFlags.NoMove
               | ImGuiWindowFlags.AlwaysAutoResize)
    {
        _automation = automation;
        _collectableHandler = collectableHandler;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 140),
            MaximumSize = new Vector2(350, 800)
        };

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
        Panel("StatusInfo", DrawStatusInfo);
        DrawStopButton();
    }

    private void DrawStopButton()
    {
        if (ImGui.Button("Stop", new Vector2(ImGui.GetContentRegionAvail().X, 100)))
            _automation?.ForceStop("Stopped by user");

    }
    private void DrawStatusInfo()
    {
        switch (Plugin.State)
        {
            case PluginState.Idle:
                ImGui.TextUnformatted("Idle...");
                break;
            case PluginState.Teleporting:
                ImGui.TextUnformatted("Teleporting...");
                break;
            case PluginState.MovingToCollectableVendor:
                ImGui.TextUnformatted("Moving to vendor...");
                break;
            case PluginState.ExchangingItems:
                ImGui.TextUnformatted("Turning in items:");
                if (_collectableHandler.TurnInQueue.Length != 0)
                {
                    for (int i = 0; i < _collectableHandler.TurnInQueue.Length; i++)
                    {
                        var item = _collectableHandler.TurnInQueue[i];
                        bool pushedColor = false;
                        if (_collectableHandler.CurrentItemName is not null && _collectableHandler.CurrentItemName == item.name)
                        {
                            ImGui.TextUnformatted("Current item: ");
                            ImGui.SameLine();
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0f, 1f, 0f, 1f)); //green
                            pushedColor = true;
                        }

                        ImGui.TextUnformatted($"{item.name} : {item.left}");
                        if(pushedColor)
                            ImGui.PopStyleColor();
                    }
                }
                break;
            case PluginState.SpendingScrip:
                ImGui.TextUnformatted("Spending scrip...");
                break;
        }
    }
    private void Panel(string id, Action body)
    {
        var style = ImGui.GetStyle();
        var pad   = style.FramePadding;
        
        var startScreen = ImGui.GetCursorScreenPos();
        var availW      = ImGui.GetContentRegionAvail().X;

        ImGui.PushID(id);
        
        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);      
        dl.ChannelsSetCurrent(1); 

        ImGui.BeginGroup();
        body();                       
        ImGui.EndGroup();

        var endY = ImGui.GetItemRectMax().Y; 
        
        var bgMin = new Vector2(startScreen.X - pad.X, startScreen.Y - pad.Y);
        var bgMax = new Vector2(startScreen.X + availW + pad.X, endY + pad.Y);
        
        dl.ChannelsSetCurrent(0);
        var bgCol  = ImGui.GetColorU32(ImGuiCol.ChildBg);
        var brdCol = ImGui.GetColorU32(ImGuiCol.Border);
        var round  = style.FrameRounding;

        dl.AddRectFilled(bgMin, bgMax, bgCol, round);
        dl.AddRect(bgMin, bgMax, brdCol, round);

        dl.ChannelsMerge();

        ImGui.PopID();
        
        ImGui.Dummy(new Vector2(0, style.ItemSpacing.Y));
    }

    public void Dispose() { }
}
