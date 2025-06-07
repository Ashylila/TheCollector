using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Data.Models;

namespace TheCollector.ScripShopManager;

public unsafe class ScripShopCache
{
    private static AtkComponentTreeList* treeList;
    public static int SubPage = 1;
    public static int Page = 0;
    public static int index = 0;
    private static List<ScripShopItem> _items = new();
    public static unsafe void Map()
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node->Type == (NodeType)1024 && node->NodeId == 19)
                {
                    var compNode = node->GetAsAtkComponentNode();
                    if (compNode == null || compNode->Component == null) continue;

                    treeList = (AtkComponentTreeList*)compNode->Component;
                    break;
                }
            }

            if (treeList == null) return;
            for (int b = 0; b < treeList->Items.Count; b++)
            {
                
                var item = treeList->Items[b].Value;// UintValue 13(index 12) is the price
                var scripItem = new ScripShopItem()
                {
                    ItemCost = item->UIntValues[12],
                    Index = b,
                    Name = new string(item->StringValues[0].ToString().Where(c => c >= 32 && c <= 126).ToArray()).Replace("H%I&", "").Replace("IH", "").Trim(),
                    Page = ScripShopCache.Page,
                    SubPage = ScripShopCache.SubPage,
                    ScripType = item->StringValues[1].ToString().Contains("purple", StringComparison.OrdinalIgnoreCase)
                                    ? ScripType.Purple
                                    : ScripType.Orange,

                };
                _items.Add(scripItem);
            }
            SubPage++;
        }
        
    }

    public static void SaveList()
    {
        var path = Svc.PluginInterface.AssemblyLocation.DirectoryName;
        var fileName = "ScripShopItems.json";
        var fullPath = System.IO.Path.Combine(path, fileName);
        if (_items.Count == 0)
        {
            Svc.Log.Error("No items found in the Scrip Shop cache.");
            return;
        }
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize<List<ScripShopItem>>(_items);
            System.IO.File.WriteAllText(fullPath, json);
            Svc.Log.Information($"Scrip Shop items saved to {fullPath}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to save Scrip Shop items: {ex.Message}");
        }
    }
}

