using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ImGuiNET;
using Lumina.Excel.Sheets;
using TheCollector.Data.Models;

namespace TheCollector.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private string comboFilter = "";
    private string GoatImagePath;
    private Configuration configuration;
    private List<ScripShopItem> ShopItems;
    private ScripShopItem? SelectedScripItem = null;
    private bool IsLoaded = false;
    
    public MainWindow(Plugin plugin, IDalamudPluginInterface pluginInterface)
        : base("The Collector##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(150, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        this.configuration = plugin.Configuration;
        this.pluginInterface = pluginInterface;
    }
    

    public void LoadScripItems()
    {
        try
        {
            var path = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName, "ScripShopItems.json");
            var text = File.ReadAllText(path);
            ShopItems = System.Text.Json.JsonSerializer.Deserialize<List<ScripShopItem>>(text) ?? new List<ScripShopItem>();
        }catch(Exception ex)
        {
            ShopItems = new List<ScripShopItem>();
            Svc.Log.Error("failed to read file", ex);
        }
    }
    public void Dispose() { }
    public override void PreDraw()
    {
        ImGui.SetNextWindowSize(new Vector2(150, 100), ImGuiCond.FirstUseEver);
    }

    public override void Draw()
    {
        if(!IsLoaded)
        {
            LoadScripItems();
            Svc.Log.Debug($"Loaded {ShopItems.Count} shop items.");
            IsLoaded = true;
        }
        ImGui.Text("Add Item...");

        
        ImGui.PushItemWidth(-ImGui.GetFrameHeightWithSpacing() * 2);
        if (ImGui.BeginCombo("##ItemCombo", SelectedScripItem?.Name ?? "Select"))
        {
            ImGui.InputTextWithHint("##ComboFilter", "Filter...", ref comboFilter, 100);
            ImGui.Separator();

            foreach (var item in ShopItems
                         .Where(i => string.IsNullOrEmpty(comboFilter) ||
                                     i.Name.Contains(comboFilter, StringComparison.OrdinalIgnoreCase)))
            {
                bool isSelected = item == SelectedScripItem;
                var iconTexture = item.IconTexture;

                ImGui.Image(iconTexture.GetWrapOrEmpty().ImGuiHandle, new Vector2(20, 20));
                ImGui.SameLine();
                if (ImGui.Selectable(item.Name, isSelected))
                {
                    SelectedScripItem = item;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
        ImGui.SameLine();

        
        if (ImGui.Button("+") && SelectedScripItem != null)
        {
            if (!configuration.ItemsToPurchase.Any(i => i.Item == SelectedScripItem))
            {
                configuration.ItemsToPurchase.Add(new ItemToPurchase()
                {
                    Item = SelectedScripItem,
                    Quantity = 1
                });
                configuration.Save();
            }
        }

        ImGui.Spacing();

        
        ImGui.Text("Items to purchase:");
        ImGui.BeginChild("##ItemList", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), true);

        for (int i = 0; i < configuration.ItemsToPurchase.Count; i++)
        {
            var item = configuration.ItemsToPurchase[i];
            if (ImGui.Button($"-##Remove{i}"))
            {
                configuration.ItemsToPurchase.RemoveAt(i);
                configuration.Save();
                i--;
            }
            ImGui.SameLine();
            var iconTexture = item.Item.IconTexture;
            ImGui.Image(iconTexture.GetWrapOrDefault().ImGuiHandle, new Vector2(20, 20));
            ImGui.SameLine();
            ImGui.Text(item.Item.Name);
            ImGui.SameLine();

            int quantity = item.Quantity;
            ImGui.PushItemWidth(100);
            if (ImGui.InputInt($"##Quantity{i}", ref quantity))
            {
                item.Quantity = quantity;
                configuration.ItemsToPurchase[i] = item; // Write back the updated item
                configuration.Save();
            }
            ImGui.PopItemWidth();
            
        }
        ImGui.EndChild();
    }

}

