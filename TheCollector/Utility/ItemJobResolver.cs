using System;
using System.Linq;
using System.Reflection;
using Lumina.Excel.Sheets;
using Dalamud.Plugin.Services;
using ECommons.Logging;
using Lumina.Excel;

namespace TheCollector.Utility;
public static class ItemJobResolver
{
    /// <summary>
    /// Returns job id (0-10) for the class needed to obtain an item (craftable/gatherable/fishable).
    /// Returns -1 if not found.
    /// </summary>
    public static int GetJobIdForItem(string itemName, IDataManager data)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return -1;

        itemName = itemName.Replace(" \uE03D", "").ToLowerInvariant();

        // Find item row
        var item = data.GetExcelSheet<Item>()?
            .FirstOrDefault(i => i.Name.ToString().ToLowerInvariant() == itemName);
        if (item == null)
            return -1;

        uint itemId = item.Value.RowId;

        // Craftable
        var recipeSheet = data.GetExcelSheet<Recipe>();
        if (recipeSheet != null)
        {
            var recipe = recipeSheet.FirstOrDefault(r => r.ItemResult.RowId == itemId);
            if (((Lumina.Excel.IExcelRow<Recipe>)recipe).RowId != 0)
                return (int)recipe.CraftType.RowId;
        }


        // Gatherable
        var fishSheet = data.GetExcelSheet<FishParameter>();
        if (fishSheet?.Any(f => f.Item.RowId == itemId) == true)
            return 10; // Fisher
        var spearSheet = data.GetExcelSheet<SpearfishingItem>();
        if (spearSheet?.Any(f => f.Item.RowId == itemId) == true)
            return 10; // Fisher
        
        var giSheet  = data.GetExcelSheet<GatheringItem>();
        var gpbSheet = data.GetExcelSheet<GatheringPointBase>();
        if (giSheet == null || gpbSheet == null)
            return -1;

        var gi = giSheet.FirstOrDefault(g => g.Item.RowId == itemId);

        var gatherId = gi.RowId;

        foreach (var b in gpbSheet)
        {
            for (int i = 0; i < b.Item.Count; i++)
            {
                if (b.Item[i].RowId == gatherId)
                    return MapTypeToJob(b);
            }
        }

        var gipSheet = data.GetSubrowExcelSheet<GatheringItemPoint>();
        if (gipSheet != null)
        {
            foreach (var gip in gipSheet.SelectMany(s => s))
            {
                if (gip.RowId != gatherId)
                    continue;

                var gp = gip.GatheringPoint.ValueNullable;
                if (gp == null) continue;

                var baseRow = gp.Value.GatheringPointBase.ValueNullable;
                if (baseRow == null) continue;

                return MapTypeToJob(baseRow.Value);
            }
        }

        return -1;
    }
    static int MapTypeToJob(GatheringPointBase b)
    {
        var type = b.GatheringType.ValueNullable;
        if (type == null) return -1;
        var id = type.Value.RowId;
        return id switch
        {
            0 or 1 or 6 => 8,
            2 or 3 or 5 => 9,
            4 or 7     => 10,
            _          => -1,
        };
    }
}
