using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using TheCollector.Data.Models;

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
        _log  = log;
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
        }
    }
}
