using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.Sheets;
using TheCollector.CollectableManager;

namespace TheCollector.Utility;

public static class ItemHelper
{
    public static List<GameInventoryItem> GetCurrentInventoryItems()
    {
        var inventoriesToFetch = new GameInventoryType[]
        {
            GameInventoryType.Inventory1, GameInventoryType.Inventory2, GameInventoryType.Inventory3,
            GameInventoryType.Inventory4
        };
        var inventoryItems = new List<GameInventoryItem>();
        for (int i = 0; i < inventoriesToFetch.Length; i++)
        {
            inventoryItems.AddRange(Svc.GameInventory.GetInventoryItems(inventoriesToFetch[i]));
        }
        return inventoryItems;
    }
    public static List<Item> GetLuminaItemsFromInventory()
    {
        List<Item>? luminaItems = new List<Item>();
        var inventoryItems = GetCurrentInventoryItems();
    
        foreach (var invItem in inventoryItems)
        {
            var luminaItem = Svc.Data.GetExcelSheet<Item>().FirstOrDefault(i => i.RowId == invItem.BaseItemId);
            if (luminaItem.NotNull(out var t))
                luminaItems.Add(luminaItem);
        }
        return luminaItems;
    }
    
}
