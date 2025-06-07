using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ImGuiNET;
using Lumina.Excel.Sheets;
using TheCollector.CollectableManager;
using TheCollector.Data.Models;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;

namespace TheCollector.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly IDataManager _dataManager;
    private readonly CollectableAutomationHandler _collectableAutomationHandler;
    private Configuration Configuration;
    
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

        if (ImGui.Button("Map"))
        {
            ScripShopCache.Map();
        }

        if (ImGui.Button("Advance page"))
        {
            ScripShopCache.Page++;
            ScripShopCache.SubPage = 1;
        }
        if(ImGui.Button("Done mapping"))
        {
            ScripShopCache.SaveList();
        }
        
    }
    public void DrawShopSelection()
    {
        ImGui.TextUnformatted("Select your preferred collectable shop:");
        ImGui.SameLine();

        
        int selectedIndex = CollectableNpcLocations.CollectableShops.IndexOf(Configuration.PreferredCollectableShop);

        string currentShopName;
        if (selectedIndex != -1)
        {
            currentShopName = Configuration.PreferredCollectableShop.Name;
        }
        else
        {
            currentShopName = "Select a shop...";
        }

        if (ImGui.BeginCombo("Shop", currentShopName))
        {
            for (int i = 0; i < CollectableNpcLocations.CollectableShops.Count; i++)
            {
                
                if (ImGui.Selectable(CollectableNpcLocations.CollectableShops[i].Name))
                {
                    Configuration.PreferredCollectableShop = CollectableNpcLocations.CollectableShops[i];
                    Configuration.Save();
                }
            }

            ImGui.EndCombo();
        }
    }
}
