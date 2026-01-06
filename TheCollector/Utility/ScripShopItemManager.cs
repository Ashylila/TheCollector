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
        var itemIds = items.Select(x => x.ItemId).ToHashSet();

        var map = new Dictionary<uint, uint>(itemIds.Count);

        var shops = Svc.Data.GetExcelSheet<SpecialShop>();
        if (shops == null) return;

        foreach (var shop in shops)
        {
            foreach (var entry in shop.Item)
            {
                uint currency = 0;
                foreach (var cost in entry.ItemCosts)
                {
                    var id = cost.ItemCost.RowId;
                    if (id == 0) continue;
                    currency = id;
                    break;
                }

                if (currency == 0 || currency > ushort.MaxValue)
                    continue;

                foreach (var receive in entry.ReceiveItems)
                {
                    var reward = receive.Item.RowId;
                    if (reward == 0) continue;
                    if (!itemIds.Contains(reward)) continue;

                    map[reward] = (ushort)currency;
                }
            }
        }

        foreach (var it in items)
        {
            if (it.CurrencyId == 0 && map.TryGetValue(it.ItemId, out var cid))
                it.CurrencyId = cid;
        }

        var missing = items.Count(x => x.CurrencyId == 0);
        if (missing > 0)
            _log.Error($"CurrencyId resolve: {missing}/{items.Count} items unresolved.");
    }


}
