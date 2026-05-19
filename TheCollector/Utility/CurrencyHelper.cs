using System.Collections.Generic;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using TheCollector.Data;

namespace TheCollector.Utility;

public static class CurrencyHelper
{
    private static Dictionary<uint, RunSource>? _runSourceByCurrency;

    public static unsafe uint SpecialIdToItemId(uint specialId)
    {
        var cur = CurrencyManager.Instance();
        if (cur == null) return 0;
        return cur->GetItemIdBySpecialId((byte)specialId);
    }

    public static string GetCurrencyName(uint specialId)
    {
        var itemId = SpecialIdToItemId(specialId);
        if (itemId == 0) return $"Scrip Type {specialId}";
        var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        return item?.Name.ExtractText() ?? $"Scrip Type {specialId}";
    }

    public static uint GetCurrencyIdForItem(uint shopItemId)
    {
        foreach (var s in ScripShopItemManager.ShopItems)
        {
            if (s.ItemId == shopItemId)
                return s.CurrencyId;
        }
        return 0;
    }

    public static RunSource RunSourceFromJobIndex(int jobIndex)
        => jobIndex >= 0 && jobIndex <= 7 ? RunSource.Crafting : RunSource.Gathering;

    public static RunSource GetRunSource(uint currencyId)
    {
        EnsureRunSourceMap();
        return _runSourceByCurrency != null && _runSourceByCurrency.TryGetValue(currencyId, out var rs)
            ? rs
            : RunSource.Gathering;
    }

    public static void InvalidateRunSourceMap() => _runSourceByCurrency = null;

    private static void EnsureRunSourceMap()
    {
        if (_runSourceByCurrency != null) return;

        var map        = new Dictionary<uint, RunSource>();
        var roleCounts = new Dictionary<uint, (int crafter, int gatherer)>();

        var recipeSheet     = Svc.Data.GetExcelSheet<Recipe>();
        var gatheringSheet  = Svc.Data.GetExcelSheet<GatheringItem>();
        var fishSheet       = Svc.Data.GetExcelSheet<FishParameter>();
        var spearSheet      = Svc.Data.GetExcelSheet<SpearfishingItem>();
        var collectableShop = Svc.Data.GetSubrowExcelSheet<CollectablesShopItem>();
        if (collectableShop == null)
        {
            _runSourceByCurrency = map;
            return;
        }

        var crafterItemIds  = new HashSet<uint>();
        var gathererItemIds = new HashSet<uint>();

        if (recipeSheet != null)
            foreach (var r in recipeSheet)
                if (r.ItemResult.RowId != 0) crafterItemIds.Add(r.ItemResult.RowId);

        if (gatheringSheet != null)
            foreach (var g in gatheringSheet)
                if (g.Item.RowId != 0) gathererItemIds.Add(g.Item.RowId);

        if (fishSheet != null)
            foreach (var f in fishSheet)
                if (f.Item.RowId != 0) gathererItemIds.Add(f.Item.RowId);

        if (spearSheet != null)
            foreach (var s in spearSheet)
                if (s.Item.RowId != 0) gathererItemIds.Add(s.Item.RowId);

        foreach (var row in collectableShop)
        foreach (var sub in row)
        {
            var rewardScrip = sub.CollectablesShopRewardScrip.ValueNullable;
            if (rewardScrip == null) continue;

            var specialId = rewardScrip.Value.Currency;
            if (specialId == 0) continue;

            var currencyItemId = SpecialIdToItemId(specialId);
            if (currencyItemId == 0) continue;

            var itemId = sub.Item.RowId;
            if (itemId == 0) continue;

            roleCounts.TryGetValue(currencyItemId, out var counts);
            if (crafterItemIds.Contains(itemId))
                counts.crafter++;
            else if (gathererItemIds.Contains(itemId))
                counts.gatherer++;
            roleCounts[currencyItemId] = counts;
        }

        foreach (var (currency, counts) in roleCounts)
            map[currency] = counts.crafter > counts.gatherer ? RunSource.Crafting : RunSource.Gathering;

        _runSourceByCurrency = map;
    }
}
