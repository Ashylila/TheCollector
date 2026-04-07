using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.Sheets;
using Serilog;
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
    public static int GetFreeInventorySlots()
    {
        const int SlotsPerPage = 35;
        const int TotalSlots = SlotsPerPage * 4;
        var items = GetCurrentInventoryItems();
        var count = items.Count(i => !i.IsEmpty);
        return TotalSlots - count;
    }

    public static List<Item> GetLuminaItemsFromInventory()
    {
        List<Item>? luminaItems = new List<Item>();
        var inventoryItems = GetCurrentInventoryItems();

        var itemSheet = Svc.Data.GetExcelSheet<Item>();
        foreach (var invItem in inventoryItems)
        {
            var luminaItem = itemSheet.GetRow(invItem.BaseItemId);
            if (luminaItem.NotNull(out var t))
                luminaItems.Add(luminaItem);
        }
        return luminaItems;
    }

}
