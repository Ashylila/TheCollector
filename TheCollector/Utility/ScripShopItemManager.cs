using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using TheCollector.Data.Models;

namespace TheCollector.Utility;

public class ScripShopItemManager
{

    private static readonly HttpClient _http = new();
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

            var text = await _http.GetStringAsync(_scripFileLink);
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

        foreach (var id in specialShopIds)
        {
            var ss = specialShopSheet.GetRow(id);
            if (ss.RowId == 0) continue;

            foreach (var entry in ss.Item)
            {
                uint scripCurrency = 0;
                foreach (var cost in entry.ItemCosts)
                {
                    var normalized = CurrencyHelper.NormalizeScripCurrencyId(cost.ItemCost.RowId);
                    if (normalized != 0)
                    {
                        scripCurrency = normalized;
                        break;
                    }
                }

                if (scripCurrency == 0) continue;

                foreach (var received in entry.ReceiveItems)
                {
                    var rewardId = received.Item.RowId;
                    if (rewardId == 0 || !targets.Contains(rewardId)) continue;
                    resolved.TryAdd(rewardId, scripCurrency);
                }
            }
        }
    }

}
