using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using TheCollector.Data.Models;
using TheCollector.Data;

namespace TheCollector.Utility;

public class ScripShopItemManager
{

    public static List<ScripShopItem> ShopItems = new();
    public static bool IsLoading { get; private set; }
    private readonly PlogonLog _log;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly string _scripFileLink = "https://raw.githubusercontent.com/Ashylila/TheCollector/master/Data/ScripShopItems.json";

    public ScripShopItemManager(PlogonLog log, IDalamudPluginInterface pluginInterface)
    {
        _log = log;
        _pluginInterface = pluginInterface;
        _ = LoadScripItemsAsync();
    }
    public async Task LoadScripItemsAsync()
    {
        IsLoading = true;
        try
        {
            _log.Debug($"Loading {_scripFileLink}");
            using var http = new HttpClient();

            var text = await http.GetStringAsync(_scripFileLink);
            ShopItems = JsonSerializer.Deserialize<List<ScripShopItem>>(text) ?? new();
        }
        catch (Exception ex)
        {
            ShopItems = new();
            Svc.Log.Error("Failed to fetch file", ex);
        }
        finally
        {
            IsLoading = false;
            _log.Debug($"Loaded {ShopItems.Count} items from {_scripFileLink}.");
            ResolveCurrencyIdsForItems(ShopItems);
        }
    }
    private void ResolveCurrencyIdsForItems(IReadOnlyCollection<ScripShopItem> items)
    {
        var targets = items.Select(x => x.ItemId).ToHashSet();
        if (targets.Count == 0) return;

        var resolved = new Dictionary<uint, uint>(targets.Count);

        ResolveFromInclusionShopSpecialShops(targets, resolved);

        foreach (var it in items)
        {
            if (it.CurrencyId == 0 && resolved.TryGetValue(it.ItemId, out var cid))
                it.CurrencyId = cid;
        }

        var missing = items.Count(x => x.CurrencyId == 0);
        if (missing > 0)
            _log.Error($"CurrencyId resolve: {missing}/{items.Count} items unresolved.");
        

    }

    private void ResolveFromInclusionShopSpecialShops(HashSet<uint> targets, Dictionary<uint, uint> resolved)
    {
        var inclusion = Svc.Data.GetExcelSheet<InclusionShop>();
        if (inclusion == null) return;

        var specialShopSheet = Svc.Data.GetExcelSheet<SpecialShop>();
        if (specialShopSheet == null) return;

        var specialShopIds = new HashSet<uint>();

        foreach (var shop in inclusion)
        {
            for (var c = 0; c < shop.Category.Count; c++)
            {
                if (shop.Category[c].RowId == 0) continue;

                var cat = shop.Category[c].Value;
                for (var s = 0; s < cat.InclusionShopSeries.Value.Count; s++)
                {
                    if (cat.InclusionShopSeries.Value[s].RowId == 0) continue;

                    var specialShopId = cat.InclusionShopSeries.Value[s].SpecialShop.RowId;
                    if (specialShopId != 0) specialShopIds.Add(specialShopId);
                }
            }
        }

        if (specialShopIds.Count == 0) return;

        var counts = new Dictionary<uint, Dictionary<uint, int>>();

        foreach (var id in specialShopIds)
        {
            var ss = specialShopSheet.GetRow(id);
            if (ss.RowId == 0) continue;

            foreach (var entry in ss.Item)
            {
                var currencies = entry.ItemCosts
                    .Select(x => x.ItemCost.RowId)
                    .Where(x => x != 0)
                    .Distinct()
                    .ToArray();

                if (currencies.Length == 0) continue;

                var rewards = entry.ReceiveItems
                    .Select(x => x.Item.RowId)
                    .Where(x => x != 0 && targets.Contains(x))
                    .Distinct()
                    .ToArray();

                if (rewards.Length == 0) continue;

                foreach (var reward in rewards)
                {
                    if (!counts.TryGetValue(reward, out var byCur))
                        counts[reward] = byCur = new Dictionary<uint, int>();

                    foreach (var cur in currencies)
                    {
                        byCur.TryGetValue(cur, out var n);
                        byCur[cur] = n + 1;
                    }
                }
            }
        }

        foreach (var kv in counts)
        {
            var reward = kv.Key;
            var byCur = kv.Value;

            uint bestCur = 0;
            var bestHits = -1;

            foreach (var c in byCur)
            {
                if (c.Value > bestHits || (c.Value == bestHits && c.Key < bestCur))
                {
                    bestCur = c.Key;
                    bestHits = c.Value;
                }
            }

            if (bestCur != 0)
                resolved[reward] = bestCur;
        }
    }

}
