using System.Collections.Frozen;
using System.Collections.Generic;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using TheCollector.Data.Models;

namespace TheCollector.Utility;


public class ScripShopItemManager
{
    public static IReadOnlyList<ScripShopItem> ShopItems { get; private set; } = new List<ScripShopItem>();
    public static FrozenDictionary<uint, ScripShopItem> ByItemId { get; private set; } =
        FrozenDictionary<uint, ScripShopItem>.Empty;


    public static bool IsLoading => false;

    private readonly PlogonLog _log;

    public ScripShopItemManager(PlogonLog log)
    {
        _log = log;
        BuildFromLumina();
    }

    private void BuildFromLumina()
    {
        var inclusionSheet = Svc.Data.GetExcelSheet<InclusionShop>();
        var categorySheet  = Svc.Data.GetExcelSheet<InclusionShopCategory>();
        var seriesSheet    = Svc.Data.GetSubrowExcelSheet<InclusionShopSeries>();
        var specialSheet   = Svc.Data.GetExcelSheet<SpecialShop>();
        if (inclusionSheet == null || categorySheet == null || seriesSheet == null || specialSheet == null)
        {
            _log.Error("ScripShopCatalog: required Lumina sheets unavailable; catalog will be empty.");
            return;
        }

        InclusionShop? canonical = null;
        var canonicalScripItemCount = 0;
        foreach (var inclusion in inclusionSheet)
        {
            var count = CountScripItems(inclusion, seriesSheet, specialSheet);
            if (count > canonicalScripItemCount)
            {
                canonicalScripItemCount = count;
                canonical = inclusion;
            }
        }

        if (canonical is not { } canonicalShop || canonicalScripItemCount == 0)
        {
            _log.Error("ScripShopCatalog: no InclusionShop with scrip-cost items found.");
            return;
        }

        var byItemId = new Dictionary<uint, ScripShopItem>();
        var ordered  = new List<ScripShopItem>();

        for (var pageIndex = 0; pageIndex < canonicalShop.Category.Count; pageIndex++)
        {
            var categoryRef = canonicalShop.Category[pageIndex];
            if (categoryRef.RowId == 0) continue;
            if (categoryRef.ValueNullable is not { } category) continue;

            var seriesId = category.InclusionShopSeries.RowId;
            if (seriesId == 0) continue;
            if (!seriesSheet.TryGetRow(seriesId, out var seriesRow)) continue;

            for (var subRowIndex = 0; subRowIndex < seriesRow.Count; subRowIndex++)
            {
                var sub = seriesRow[subRowIndex];
                var subPage = subRowIndex + 1;

                var specialShopRowId = sub.SpecialShop.RowId;
                if (specialShopRowId == 0) continue;
                if (!specialSheet.TryGetRow(specialShopRowId, out var shop)) continue;

                foreach (var entry in shop.Item)
                {
                    uint scripCurrency = 0;
                    uint scripCost     = 0;
                    foreach (var cost in entry.ItemCosts)
                    {
                        if (cost.ItemCost.RowId == 0 || cost.CurrencyCost == 0) continue;
                        var normalized = CurrencyHelper.NormalizeScripCurrencyId(cost.ItemCost.RowId);
                        if (normalized == 0) continue;
                        scripCurrency = normalized;
                        scripCost     = (uint)cost.CurrencyCost;
                        break;
                    }
                    if (scripCurrency == 0 || scripCost == 0) continue;

                    foreach (var receive in entry.ReceiveItems)
                    {
                        var itemId = receive.Item.RowId;
                        if (itemId == 0) continue;
                        if (byItemId.ContainsKey(itemId)) continue;

                        var item = new ScripShopItem
                        {
                            ItemId     = itemId,
                            ItemCost   = scripCost,
                            CurrencyId = scripCurrency,
                            Page       = pageIndex,
                            SubPage    = subPage,
                            Index      = ordered.Count,
                        };
                        byItemId[itemId] = item;
                        ordered.Add(item);
                    }
                }
            }
        }

        ShopItems = ordered;
        ByItemId  = byItemId.ToFrozenDictionary();
        _log.Debug($"ScripShopCatalog: built {ordered.Count} items from InclusionShop #{canonicalShop.RowId}.");
    }

    private static int CountScripItems(
        InclusionShop inclusion,
        Lumina.Excel.SubrowExcelSheet<InclusionShopSeries> seriesSheet,
        Lumina.Excel.ExcelSheet<SpecialShop> specialSheet)
    {
        var count = 0;
        for (var pageIndex = 0; pageIndex < inclusion.Category.Count; pageIndex++)
        {
            var categoryRef = inclusion.Category[pageIndex];
            if (categoryRef.RowId == 0) continue;
            if (categoryRef.ValueNullable is not { } category) continue;

            var seriesId = category.InclusionShopSeries.RowId;
            if (seriesId == 0) continue;
            if (!seriesSheet.TryGetRow(seriesId, out var seriesRow)) continue;

            for (var subRowIndex = 0; subRowIndex < seriesRow.Count; subRowIndex++)
            {
                var specialShopRowId = seriesRow[subRowIndex].SpecialShop.RowId;
                if (specialShopRowId == 0) continue;
                if (!specialSheet.TryGetRow(specialShopRowId, out var shop)) continue;

                foreach (var entry in shop.Item)
                {
                    var hasScripCost = false;
                    foreach (var cost in entry.ItemCosts)
                    {
                        if (cost.ItemCost.RowId == 0 || cost.CurrencyCost == 0) continue;
                        if (CurrencyHelper.NormalizeScripCurrencyId(cost.ItemCost.RowId) != 0)
                        {
                            hasScripCost = true;
                            break;
                        }
                    }
                    if (!hasScripCost) continue;
                    foreach (var receive in entry.ReceiveItems)
                        if (receive.Item.RowId != 0)
                            count++;
                }
            }
        }
        return count;
    }
}
