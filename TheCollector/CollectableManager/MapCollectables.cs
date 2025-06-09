using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Utility;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Data;
using TheCollector.Data.Models;

namespace TheCollector.CollectableManager;

public unsafe class MapCollectables
{
    private static CollectableWindowHandler collectableWindowHandler = new();
    static List<CollectableShopItem> items = new List<CollectableShopItem>();
    private static int currentJob = 0;

    public static void Map()
    {
        AtkComponentTreeList* _treeList = null;
        if (GenericHelpers.TryGetAddonByName("CollectablesShop", out AtkUnitBase* addon))
        {
            
                if(GenericHelpers.TryGetAddonByName("CollectablesShop", out AtkUnitBase* addonUpdated))
                {


                    for (int i = 0; i < addonUpdated->UldManager.NodeListCount; i++)
                    {
                        var node = addonUpdated->UldManager.NodeList[i];
                        if (node->Type != (NodeType)1028 || node->NodeId != 28) continue;
                        var compNode = node->GetAsAtkComponentNode();
                        if (compNode == null || compNode->Component == null) continue;

                        _treeList = (AtkComponentTreeList*)compNode->Component;
                    }

                    if (_treeList == null) return;
                    var treeItems = _treeList->Items;
                    for (int b = 0; b < treeItems.Count; b++)
                    {
                        var item = treeItems[b].Value;
                        if (item->StringValues[0].ToString().Contains("-")) continue;
                        var collectableItem = new CollectableShopItem()
                        {
                            Name = item->StringValues[0].ExtractText(),
                            ScripType = item->StringValues[1].ExtractText()
                                                             .Contains("100", StringComparison.OrdinalIgnoreCase)
                                            ? ScripType.Orange
                                            : ScripType.Purple,
                            Class = (CollectableClasses)currentJob,
                            Amount = (int)((int)item->UIntValues[5] *
                                           1.2f) // The base value is for minimum collectability, 1.2 is the multiplier the game gives it for the maximum collectability
                        };
                        if (items.Any(i => i.Name == collectableItem.Name)) continue;
                        Svc.Log.Debug(item->StringValues[0].ExtractText());
                        items.Add(collectableItem);
                    }
                }
                currentJob++;
        };
    }

    public static void Values()
    {
        AtkComponentTreeList* _treeList = null;
        int currentJob = 0;
        if (GenericHelpers.TryGetAddonByName("CollectablesShop", out AtkUnitBase* addon))
        {
            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node->Type != (NodeType)1028 || node->NodeId != 28) continue;
                var compNode = node->GetAsAtkComponentNode();
                if (compNode == null || compNode->Component == null) continue;

                _treeList = (AtkComponentTreeList*)compNode->Component;
            }

            if (_treeList == null) return;
            var treeItems = _treeList->Items;
            for (int b = 0; b < treeItems.Count; b++)
            {
                var item = treeItems[b].Value;
                if (item->StringValues[0].ToString().Contains("-")) continue;
                for (int c = 0; c < item->StringValues.Count; c++)
                {
                    Svc.Log.Debug(item->StringValues[c].ExtractText() + " String, Index: " + c);
                }

                for (int c = 0; c < item->UIntValues.Count; c++)
                {
                    Svc.Log.Debug(item->UIntValues[c].ToString() + " Int, Index: " + c);
                }
            }
        }
    }
    public static void SaveList()
    {
        var path = Svc.PluginInterface.AssemblyLocation.DirectoryName;
        var fileName = "CollectableShopItems.json";
        var fullPath = System.IO.Path.Combine(path, fileName);
        if (items.Count == 0)
        {
            Svc.Log.Error("No items found in the Scrip Shop cache.");
            return;
        }
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize<List<CollectableShopItem>>(items);
            System.IO.File.WriteAllText(fullPath, json);
            Svc.Log.Information($"Scrip Shop items saved to {fullPath}");
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Failed to save Scrip Shop items: {ex.Message}");
        }
    }
}
