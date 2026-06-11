using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TheCollector.Data;
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

        var inventory = ItemHelper.GetCollectableInventoryCounts();
        var byCurrency = new Dictionary<uint, CurrencySummary>();
        var itemBreakdowns = new List<ItemBreakdown>();

        foreach (var item in _config.Goal.ItemsToPurchase)
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

                // Inventory holdings claimable for this currency, valued at each collectable's HighReward
                int holdingsValue = 0;
                foreach (var c in collectables)
                {
                    if (!inventory.TryGetValue(c.ItemId, out var count) || count <= 0) continue;
                    holdingsValue += count * c.HighReward;
                }
                summary.InventoryScripsValue = holdingsValue;

                IEnumerable<CollectableInfo> filtered = collectables;
                if (_config.Goal.HideFishingCollectables)
                    filtered = filtered.Where(c => !c.IsFish);
                if (_config.Goal.HideUnobtainableCollectables)
                    filtered = filtered.Where(IsObtainable);
                var best = filtered.OrderByDescending(c => c.HighReward).FirstOrDefault();
                if (best != null && best.HighReward > 0)
                {
                    summary.BestCollectable = best;
                    var remaining = Math.Max(0, summary.TotalScripsNeeded - holdingsValue);
                    summary.EstimatedTurnIns = (int)Math.Ceiling((double)remaining / best.HighReward);
                }
            }
        }

        return new PlanSummary
        {
            CurrencySummaries = byCurrency.Values.ToList(),
            ItemBreakdowns = itemBreakdowns,
            InventoryByItemId = inventory,
            IsListComplete = _config.Goal.ItemsToPurchase.Count > 0 &&
                             _config.Goal.ItemsToPurchase.All(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity)
        };
    }

    public bool IsGoalComplete()
        => IsGoalComplete(_config.ActiveRunSource);

    public bool IsGoalComplete(RunSource source)
    {
        var items = _config.Goal.ItemsToPurchase
            .Where(i => CurrencyHelper.GetRunSource(CurrencyHelper.GetCurrencyIdForItem(i.Item.ItemId)) == source)
            .ToList();
        if (items.Count == 0) return false;
        return items.All(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity);
    }

    private void EnsureCollectablesLoaded()
    {
        if (_collectablesByCurrency != null) return;
        _collectablesByCurrency = new Dictionary<uint, List<CollectableInfo>>();

        var classMap = BuildClassMap();

        var sheet = _dataManager.GetSubrowExcelSheet<CollectablesShopItem>();
        var shopSheet = _dataManager.GetExcelSheet<CollectablesShop>();
        if (sheet == null || shopSheet == null) return;

        uint canonicalShopRowId = 0;
        int maxCurrencyCount = 0;
        foreach (var s in shopSheet)
        {
            var currencies = new HashSet<uint>();
            foreach (var refr in s.ShopItems)
            {
                if (refr.RowId == 0) continue;
                if (!sheet.TryGetRow(refr.RowId, out var refRow)) continue;
                foreach (var sub in refRow)
                {
                    var r = sub.CollectablesShopRewardScrip.ValueNullable;
                    if (r == null) continue;
                    if (r.Value.Currency is 2 or 4 or 6 or 7)
                        currencies.Add(r.Value.Currency);
                }
            }
            if (currencies.Count > maxCurrencyCount)
            {
                maxCurrencyCount = currencies.Count;
                canonicalShopRowId = s.RowId;
            }
        }
        if (canonicalShopRowId == 0) return;
        if (!shopSheet.TryGetRow(canonicalShopRowId, out var canonical)) return;

        foreach (var refr in canonical.ShopItems)
        {
            if (refr.RowId == 0) continue;
            if (!sheet.TryGetRow(refr.RowId, out var refRow)) continue;
            foreach (var sub in refRow)
            {
                var reward = sub.CollectablesShopRewardScrip.ValueNullable;
                if (reward == null) continue;

                var highReward = reward.Value.HighReward;
                if (highReward <= 0) continue;

                var itemName = sub.Item.Value.Name.ExtractText();
                if (string.IsNullOrEmpty(itemName)) continue;

                if (!classMap.TryGetValue(sub.Item.RowId, out var classEntry) || classEntry.Level == 0)
                    continue;
                if (classEntry.JobId < 0) continue;

                var currencyItemId = CurrencyHelper.NormalizeScripCurrencyId(reward.Value.Currency);
                if (currencyItemId == 0) continue;

                if (!_collectablesByCurrency.TryGetValue(currencyItemId, out var list))
                {
                    list = new List<CollectableInfo>();
                    _collectablesByCurrency[currencyItemId] = list;
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
                    CurrencyType = currencyItemId,
                    IsFish = isFish,
                    Level = classEntry.Level,
                    JobId = classEntry.JobId
                });
            }
        }
    }

    private Dictionary<uint, ClassMapEntry> BuildClassMap()
    {
        var map = new Dictionary<uint, ClassMapEntry>();

        // Pre-resolve MIN/BTN job for each GatheringItem via GatheringPointBase → GatheringType
        var gpbSheet = _dataManager.GetExcelSheet<GatheringPointBase>();
        var gatherJobByGI = new Dictionary<uint, sbyte>();
        if (gpbSheet != null)
        {
            foreach (var gpb in gpbSheet)
            {
                var typeId = gpb.GatheringType.RowId;
                sbyte job = typeId switch
                {
                    0 or 1 or 6 => 8,  // MIN
                    2 or 3 or 5 => 9,  // BTN
                    4 or 7      => 10, // FSH
                    _           => (sbyte)-1,
                };
                if (job < 0) continue;
                for (int i = 0; i < gpb.Item.Count; i++)
                {
                    var giRowId = (uint)gpb.Item[i].RowId;
                    if (giRowId == 0) continue;
                    gatherJobByGI.TryAdd(giRowId, job);
                }
            }
        }

        // Crafted items: Recipe → RecipeLevelTable.ClassJobLevel + Recipe.CraftType
        var recipeSheet = _dataManager.GetExcelSheet<Recipe>();
        if (recipeSheet != null)
        {
            foreach (var recipe in recipeSheet)
            {
                var resultId = recipe.ItemResult.RowId;
                if (resultId == 0 || map.ContainsKey(resultId)) continue;
                var lvl = recipe.RecipeLevelTable.Value.ClassJobLevel;
                if (lvl > 0)
                    map[resultId] = new ClassMapEntry(lvl, (sbyte)recipe.CraftType.RowId);
            }
        }

        // Gathered items: GatheringItem → GatheringItemLevel + job via gatherJobByGI
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
                if (lvl <= 0) continue;
                var job = gatherJobByGI.TryGetValue(gi.RowId, out var j) ? j : (sbyte)-1;
                map[itemId] = new ClassMapEntry((ushort)lvl, job);
            }
        }

        // Fish: FishParameter → GatheringItemLevel (FSH = 10)
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
                if (lvl > 0) map[itemId] = new ClassMapEntry((ushort)lvl, 10);
            }
        }

        // Spearfishing: SpearfishingItem → GatheringItemLevel (FSH = 10)
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
                if (lvl > 0) map[itemId] = new ClassMapEntry((ushort)lvl, 10);
            }
        }

        return map;
    }

    public static bool IsObtainable(CollectableInfo c)
    {
        if (c.JobId < 0) return true; // unknown job → don't gate
        var playerLevel = PlayerEx.GetLevelForCollectableJob(c.JobId);
        if (playerLevel <= 0) return false;
        return playerLevel >= c.Level;
    }

    public void InvalidateCache() => _collectablesByCurrency = null;

    private readonly struct ClassMapEntry
    {
        public readonly ushort Level;
        public readonly sbyte JobId;
        public ClassMapEntry(ushort level, sbyte jobId)
        {
            Level = level;
            JobId = jobId;
        }
    }
}

public class PlanSummary
{
    public List<CurrencySummary> CurrencySummaries { get; set; } = new();
    public List<ItemBreakdown> ItemBreakdowns { get; set; } = new();
    public Dictionary<uint, int> InventoryByItemId { get; set; } = new();
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
    public int InventoryScripsValue { get; set; }
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
    // 0-7 = DoH CraftType (CRP..CUL), 8 = MIN, 9 = BTN, 10 = FSH, -1 = unknown
    public sbyte JobId { get; set; } = -1;
}
