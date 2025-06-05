using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Lumina.Excel.Sheets;
using TheCollector.CollectableManager;
using TheCollector.Data.Models;
using TheCollector.Ipc;

namespace TheCollector.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly IDataManager _dataManager;
    private readonly CollectableAutomationHandler _collectableAutomationHandler;
    private Configuration Configuration;
    private string[] _collectableShops = new []
    {
        "Eulmore",
        "Collectable Appraiser (Radz-at-Han)",
        "Collectable Appraiser (Old Sharlayan)",
        "Collectable Appraiser (Thavnair)",
        "Collectable Appraiser (Rhalgr's Reach)",
        "Collectable Appraiser (Mor Dhona)",
        "Collectable Appraiser (Ishgard)"
    };
    
    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin, CollectableAutomationHandler collectableAutomationHandler, IDataManager data) : base("A Wonderful Configuration Window###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(400, 250);
        SizeCondition = ImGuiCond.Always;
    
        _collectableAutomationHandler = collectableAutomationHandler;
        _dataManager = data;
        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
    }

    public override void Draw()
    {
        DrawDebugStartButton();
        DrawShopSelection();
    }

    private void DrawDebugStartButton()
    {
        if (ImGui.Button("Start"))
        {
            _collectableAutomationHandler.Start();
        }

        if (ImGui.Button("Move"))
        {
            VNavmesh_IPCSubscriber.Path_MoveTo([Configuration.PreferredCollectableShop.Location], false);
        }
    }
    public void DrawShopSelection()
    {
        ImGui.TextUnformatted("Select your preferred collectable shop:");
        ImGui.SameLine();

        int selectedIndex = 0;
        if (selectedIndex < 0) selectedIndex = 0;

        if (ImGui.BeginCombo("Shop", _collectableShops[selectedIndex]))
        {
            for (int i = 0; i < _collectableShops.Length; i++)
            {
                bool isSelected = selectedIndex == i;
                if (ImGui.Selectable(_collectableShops[i], isSelected))
                {
                    Configuration.PreferredCollectableShop = CollectableNpcLocations.CollectableShops.FirstOrDefault(s => s.Name == _collectableShops[i]) ?? new CollectableShop();
                    Configuration.Save();
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }
}
