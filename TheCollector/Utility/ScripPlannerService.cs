using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TheCollector.Data.Models;

namespace TheCollector.Utility;

public class ScripPlannerService
{
    private readonly IDataManager _dataManager;
    private readonly Configuration _config;
    private Dictionary<uint, List<CollectableInfo>>? _collectablesByCurrency;

    public ScripPlannerService(IDataManager dataManager, Configuration config)
    {
        _dataManager = dataManager;
        _config = config;
    }

    public PlanSummary Calculate()
    {
        EnsureCollectablesLoaded();

        var byCurrency = new Dictionary<uint, CurrencySummary>();
        var itemBreakdowns = new List<ItemBreakdown>();

        foreach (var item in _config.ItemsToPurchase)
        {
            var remaining = Math.Max(0, item.Quantity - item.AmountPurchased);
            var scripCost = remaining * (int)item.Item.ItemCost;
            var currencyId = CurrencyHelper.GetCurrencyIdForItem(item.Item.ItemId);

            itemBreakdowns.Add(new ItemBreakdown
            {
                Name = item.Name,
                UnitCost = (int)item.Item.ItemCost,
                QuantityRemaining = remaining,
                TotalCost = scripCost,
                CurrencyId = currencyId
            });

            if (remaining <= 0) continue;

            if (!byCurrency.TryGetValue(currencyId, out var summary))
            {
                summary = new CurrencySummary { CurrencyId = currencyId };
                byCurrency[currencyId] = summary;
            }

            summary.TotalScripsNeeded += scripCost;
        }

        foreach (var summary in byCurrency.Values)
        {
            if (_collectablesByCurrency != null &&
                _collectablesByCurrency.TryGetValue(summary.CurrencyId, out var collectables))
            {
                summary.Collectables = collectables;
                var filtered = _config.Goal.HideFishingCollectables
                    ? collectables.Where(c => !c.IsFish)
                    : collectables;
                var best = filtered.OrderByDescending(c => c.HighReward).FirstOrDefault();
                if (best != null && best.HighReward > 0)
                {
                    summary.BestCollectable = best;
                    summary.EstimatedTurnIns = (int)Math.Ceiling((double)summary.TotalScripsNeeded / best.HighReward);
                }
            }
        }

        return new PlanSummary
        {
            CurrencySummaries = byCurrency.Values.ToList(),
            ItemBreakdowns = itemBreakdowns,
            IsListComplete = _config.ItemsToPurchase.Count > 0 &&
                             _config.ItemsToPurchase.All(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity)
        };
    }

    public bool IsGoalComplete()
    {
        if (_config.ItemsToPurchase.Count == 0) return false;
        return _config.ItemsToPurchase.All(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity);
    }

    private void EnsureCollectablesLoaded()
    {
        if (_collectablesByCurrency != null) return;
        _collectablesByCurrency = new Dictionary<uint, List<CollectableInfo>>();

        var classLevelByItemId = BuildClassLevelMap();

        var sheet = _dataManager.GetSubrowExcelSheet<CollectablesShopItem>();
        if (sheet == null) return;

        foreach (var row in sheet)
        foreach (var sub in row)
            {
                var reward = sub.CollectablesShopRewardScrip.ValueNullable;
                if (reward == null) continue;

                var highReward = reward.Value.HighReward;
                if (highReward <= 0) continue;

                var specialId = reward.Value.Currency;
                var itemName = sub.Item.Value.Name.ExtractText();
                if (string.IsNullOrEmpty(itemName)) continue;

                // Only include items with a resolved crafting/gathering level
                if (!classLevelByItemId.TryGetValue(sub.Item.RowId, out var classLevel) || classLevel == 0)
                    continue;

                if (!_collectablesByCurrency.TryGetValue(specialId, out var list))
                {
                    list = new List<CollectableInfo>();
                    _collectablesByCurrency[specialId] = list;
                }

                // Skip duplicates — keep the entry with the highest reward
                var existing = list.FindIndex(c => c.ItemId == sub.Item.RowId);
                if (existing >= 0)
                {
                    if (highReward > list[existing].HighReward)
                    {
                        list[existing].HighReward = highReward;
                        list[existing].MidReward = reward.Value.MidReward;
                        list[existing].LowReward = reward.Value.LowReward;
                    }
                    continue;
                }

                var itemData = sub.Item.Value;
                var uiCategory = itemData.ItemUICategory.RowId;
                // ItemUICategory 47 = Fish, 49 = Spearfishing
                bool isFish = uiCategory is 47 or 49;

                list.Add(new CollectableInfo
                {
                    ItemId = sub.Item.RowId,
                    Name = itemName,
                    HighReward = highReward,
                    MidReward = reward.Value.MidReward,
                    LowReward = reward.Value.LowReward,
                    CurrencyType = specialId,
                    IsFish = isFish,
                    Level = classLevel
                });
            }
    }

    private Dictionary<uint, ushort> BuildClassLevelMap()
    {
        var map = new Dictionary<uint, ushort>();

        // Crafted items: Recipe → RecipeLevelTable.ClassJobLevel
        var recipeSheet = _dataManager.GetExcelSheet<Recipe>();
        if (recipeSheet != null)
        {
            foreach (var recipe in recipeSheet)
            {
                var resultId = recipe.ItemResult.RowId;
                if (resultId == 0 || map.ContainsKey(resultId)) continue;
                var lvl = recipe.RecipeLevelTable.Value.ClassJobLevel;
                if (lvl > 0) map[resultId] = lvl;
            }
        }

        // Gathered items: GatheringItem → GatheringItemLevel
        var gatheringSheet = _dataManager.GetExcelSheet<GatheringItem>();
        if (gatheringSheet != null)
        {
            foreach (var gi in gatheringSheet)
            {
                var itemId = gi.Item.RowId;
                if (itemId == 0 || map.ContainsKey(itemId)) continue;
                var levelData = gi.GatheringItemLevel.ValueNullable;
                if (levelData == null) continue;
                var lvl = levelData.Value.GatheringItemLevel;
                if (lvl > 0) map[itemId] = (ushort)lvl;
            }
        }

        // Fish: FishParameter → GatheringItemLevel
        var fishSheet = _dataManager.GetExcelSheet<FishParameter>();
        if (fishSheet != null)
        {
            foreach (var fish in fishSheet)
            {
                var itemId = fish.Item.RowId;
                if (itemId == 0 || map.ContainsKey(itemId)) continue;
                var levelData = fish.GatheringItemLevel.ValueNullable;
                if (levelData == null) continue;
                var lvl = levelData.Value.GatheringItemLevel;
                if (lvl > 0) map[itemId] = (ushort)lvl;
            }
        }

        // Spearfishing: SpearfishingItem → GatheringItemLevel
        var spearSheet = _dataManager.GetExcelSheet<SpearfishingItem>();
        if (spearSheet != null)
        {
            foreach (var sf in spearSheet)
            {
                var itemId = sf.Item.RowId;
                if (itemId == 0 || map.ContainsKey(itemId)) continue;
                var levelData = sf.GatheringItemLevel.ValueNullable;
                if (levelData == null) continue;
                var lvl = levelData.Value.GatheringItemLevel;
                if (lvl > 0) map[itemId] = (ushort)lvl;
            }
        }

        return map;
    }

    public void InvalidateCache() => _collectablesByCurrency = null;
}

public class PlanSummary
{
    public List<CurrencySummary> CurrencySummaries { get; set; } = new();
    public List<ItemBreakdown> ItemBreakdowns { get; set; } = new();
    public bool IsListComplete { get; set; }

    public int TotalScripsNeeded => CurrencySummaries.Sum(c => c.TotalScripsNeeded);
}

public class ItemBreakdown
{
    public string Name { get; set; } = "";
    public int UnitCost { get; set; }
    public int QuantityRemaining { get; set; }
    public int TotalCost { get; set; }
    public uint CurrencyId { get; set; }
}

public class CurrencySummary
{
    public uint CurrencyId { get; set; }
    public int TotalScripsNeeded { get; set; }
    public int EstimatedTurnIns { get; set; }
    public CollectableInfo? BestCollectable { get; set; }
    public List<CollectableInfo> Collectables { get; set; } = new();
}

public class CollectableInfo
{
    public uint ItemId { get; set; }
    public string Name { get; set; } = "";
    public ushort HighReward { get; set; }
    public ushort MidReward { get; set; }
    public ushort LowReward { get; set; }
    public uint CurrencyType { get; set; }
    public bool IsFish { get; set; }
    public ushort Level { get; set; }
}
