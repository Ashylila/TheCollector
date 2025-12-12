using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using TheCollector.Data.Models;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private string comboFilter = "";
    private Configuration configuration;

    private readonly PlogonLog _log;
    private ScripShopItem? SelectedScripItem = null;
    
    public MainWindow(Plugin plugin, IDalamudPluginInterface pluginInterface, PlogonLog log)
        : base("The Collector##CollectorMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(150, 0),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _log  = log;
        this.configuration = plugin.Configuration;
        this.pluginInterface = pluginInterface;
    }
    
    public void Dispose() { }
    public override void PreDraw()
    {
    }

    public override void Draw()
    {
        if (ScripShopItemManager.IsLoading)
        {
            ImGui.Text("Loading items...");
            return;
        }
        ImGui.Text("Add Item...");
        
        ImGui.PushItemWidth(-ImGui.GetFrameHeightWithSpacing() * 2);
        if (ImGui.BeginCombo("##ItemCombo", SelectedScripItem?.Name ?? "Select"))
        {
            ImGui.InputTextWithHint("##ComboFilter", "Filter...", ref comboFilter, 100);
            ImGui.Separator();

            foreach (var item in ScripShopItemManager.ShopItems
                         .Where(i => string.IsNullOrEmpty(comboFilter) ||
                                     i.Name.Contains(comboFilter, StringComparison.OrdinalIgnoreCase)))
            {
                bool isSelected = item == SelectedScripItem;
                var iconTexture = item.IconTexture;

                ImGui.Image(iconTexture.GetWrapOrEmpty().Handle, new Vector2(20, 20));
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
        ImGui.Spacing();
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
            ImGui.Image(iconTexture.GetWrapOrEmpty().Handle, new Vector2(20, 20));
            ImGui.SameLine();
            ImGui.Text(item.Item.Name);
            ImGui.SameLine();
            ImGui.TextUnformatted($"{item.AmountPurchased}/");
            ImGui.SameLine();
            int quantity = item.Quantity;
            ImGui.PushItemWidth(35);
            if (ImGui.InputInt($"##Quantity{i}", ref quantity))
            {
                item.Quantity = quantity;
                configuration.ItemsToPurchase[i] = item;
                configuration.Save();
            }

            ImGui.PopItemWidth();
            ImGui.SameLine();
            if (item.Quantity == item.AmountPurchased)
            {
                ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Completed");
            }

            ImGui.SameLine();
            float buttonWidth = ImGui.CalcTextSize("Refresh").X + ImGui.GetStyle().FramePadding.X * 2;
            float windowWidth = ImGui.GetWindowContentRegionMax().X;
            float cursorX = windowWidth - buttonWidth;
            ImGui.SetCursorPosX(cursorX);
            if (ImGui.Button("Refresh"))
            {
                item.ResetQuantity();
                configuration.Save();
            }
            
        }
        
    }
    


}

