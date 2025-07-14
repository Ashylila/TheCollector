using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ImGuiNET;
using Lumina.Excel.Sheets;
using TheCollector.CollectableManager;
using TheCollector.Data.Models;
using TheCollector.Ipc;
using TheCollector.ScripShopManager;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly IDataManager _dataManager;
    private Configuration Configuration;
    private readonly ITargetManager _targetManager;
    private readonly ScripShopAutomationHandler _scripShopHandler;
    
    public ConfigWindow(Plugin plugin, IDataManager data, ITargetManager target, ScripShopAutomationHandler scripShop) : base("Configuration###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(400, 250);
        SizeCondition = ImGuiCond.Always;
        
        _dataManager = data;
        Configuration = plugin.Configuration;
        _targetManager = target;
        _scripShopHandler = scripShop;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
    }

    public override void Draw()
    {
        DrawOptions();
        DrawShopSelection();
    }

    private void DrawDebugStartButton()
    {

        if (ImGui.Button("Move"))
        {
            VNavmesh_IPCSubscriber.Path_MoveTo([Configuration.PreferredCollectableShop.Location], false);
        }
        

        if (ImGui.Button("Unstuck"))
        {
            _targetManager.Target = null;
        }

        if (ImGui.Button("Start"))
        {
            ServiceWrapper.Get<AutomationHandler>().Invoke();
        }
        
        
    }

    public void DrawOptions()
    {
        ImGui.TextUnformatted("Options:");
        var toggleOnAutogatherStop = Configuration.CollectOnAutogatherDisabled;
        if (ImGui.Checkbox("Collect on Autogather Stop", ref toggleOnAutogatherStop))
        {
            Configuration.CollectOnAutogatherDisabled = toggleOnAutogatherStop;
            Configuration.Save();
        }

        var toggleAutogatherOnFinish = Configuration.EnableAutogatherOnFinish;
        if (ImGui.Checkbox("Enable Autogather on Finish", ref toggleAutogatherOnFinish))
        {
            Configuration.EnableAutogatherOnFinish = toggleAutogatherOnFinish;
            Configuration.Save();
        }
        var toggleCollectOnFinishCraftingList = Configuration.CollectOnFinishCraftingList;
        if (ImGui.Checkbox("Collect on Finish Crafting an Artisan List", ref toggleCollectOnFinishCraftingList))
        {
            Configuration.CollectOnFinishCraftingList = toggleCollectOnFinishCraftingList;
            Configuration.Save();
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
