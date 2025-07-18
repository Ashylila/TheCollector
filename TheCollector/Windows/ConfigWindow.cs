﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(400, 350);
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
        DrawInstalledPlugins();
        DrawOptions();
    }

    private void DrawInstalledPlugins()
    {
        ImGui.BeginChild("##InstalledPlugs", new Vector2(0, 130), true);
        
        ImGui.TextUnformatted("Installed required/optional Plugins:");
        
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, IPCSubscriber_Common.IsReady("vnavmesh") ? new Vector4(0,1,0,1) : new Vector4(1,0,0,1));
        ImGui.TextUnformatted("vnavmesh(required)");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        
        ImGui.PushStyleColor(ImGuiCol.Text, IPCSubscriber_Common.IsReady("GatherbuddyReborn") ? new Vector4(0,1,0,1) : new Vector4(1,0,0,1));
        ImGui.TextUnformatted("GatherbuddyReborn(optional)");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        
        ImGui.PushStyleColor(ImGuiCol.Text, IPCSubscriber_Common.IsReady("Artisan") ? new Vector4(0,1,0,1) : new Vector4(1,0,0,1));
        ImGui.TextUnformatted("Artisan(optional)");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        
        ImGui.PushStyleColor(ImGuiCol.Text, IPCSubscriber_Common.IsReady("ArtisanBuddy") ? new Vector4(0,1,0,1) : new Vector4(1,0,0,1));
        ImGui.TextUnformatted("ArtisanBuddy(optional)");
        ImGui.PopStyleColor();
        ImGui.EndChild();
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
        ImGui.BeginChild("##Options", new Vector2(0, 157), true);
        
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
        ImGui.Spacing();
        if (ImGui.BeginCombo("##shopselection", currentShopName))
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
        ImGui.EndChild();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.60f, 0.86f, 1.00f));        
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.30f, 0.70f, 0.96f, 1.00f)); 
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.10f, 0.50f, 0.76f, 1.00f));  

        float buttonWidth = ImGui.CalcTextSize("Support Me").X + ImGui.GetStyle().FramePadding.X * 2;
        float windowWidth = ImGui.GetWindowContentRegionMax().X;
        float cursorX = windowWidth - buttonWidth;

        ImGui.SetCursorPosX(cursorX);
        if (ImGui.Button("Support Me"))
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/Ashylila",
                UseShellExecute = true
            });
        }


        ImGui.PopStyleColor(3);

        
    }
}
