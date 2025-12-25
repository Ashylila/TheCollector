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
        BuildScripTypeIndex();
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
        }
    }
    static Dictionary<uint, ScripType> _itemToScripType;

    public static void BuildScripTypeIndex()
    {
        _itemToScripType = new Dictionary<uint, ScripType>();
        var shops = Svc.Data.GetExcelSheet<SpecialShop>();

        foreach (var shop in shops)
        {
            var name = shop.Name.ToString();
            if (!name.Contains("Scrip Exchange", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!name.Contains("Purple", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("Orange", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var entry in shop.Item)
                foreach (var receive in entry.ReceiveItems)
                {
                    var rewardItemId = receive.Item.RowId;
                    if (rewardItemId == 0)
                        continue;

                    foreach (var cost in entry.ItemCosts)
                    {
                        var tag = cost.ItemCost.RowId;
                        if (tag is not (2u or 4u or 6u or 7u))
                            continue;

                        _itemToScripType[rewardItemId] = (ScripType)tag;
                    }
                }
        }

        Svc.Log.Info($"ScripType index built: {_itemToScripType.Count} items");
    }
    public static bool TryGetScripType(uint itemId, out ScripType type)
{
    if (_itemToScripType == null)
    {
        type = default;
        return false;
    }

    return _itemToScripType.TryGetValue(itemId, out type);
}


}
