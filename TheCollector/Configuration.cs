using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ECommons.DalamudServices;
using OtterGui.Widgets;
using TheCollector.CollectableManager;
using TheCollector.Data.Models;
using TheCollector.Windows;

namespace TheCollector;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public List<ItemToPurchase> ItemsToPurchase { get; set; } = new List<ItemToPurchase>();
    public bool EnableAutogatherOnFinish { get; set; } = false;
    public bool CollectOnFinishCraftingList { get; set; } = false;
    public bool ShouldCraftOnAutogatherChanged { get; set; } = false;
    public bool BuyAfterEachCollect { get; set; } = false;
    public bool ResetEachQuantityAfterCompletingList { get; set; } = false;
    public bool CollectOnFinishedFishing { get; set; } = false;
    public int ArtisanListId { get; set; } = 0;
    public int LastSeenVersion { get; set; } = ChangelogUi.LastChangelogVersion;
    public bool CheckForVenturesBetweenRuns { get; set; } = false;
    public ChangeLogDisplayType ChangeLogDisplayType { get; set; } = ChangeLogDisplayType.New;
    public CollectableShop PreferredCollectableShop { get; set; } = new();

    public void Save()
    {
        Svc.PluginInterface.SavePluginConfig(this);
    }
    public bool Migrate()
    {
        var changed = false;

        if (Version < 3)
        {
            changed |= Migrate_TerritoryId(PreferredCollectableShop);
            Version = 3;
        }
        Save();
        return changed;
    }

    private bool Migrate_TerritoryId(CollectableShop shop)
    {
        if (shop == null) return false;

        if(!string.IsNullOrEmpty(shop.Name) && shop.TerritoryId == default )
        {
            var newShop = CollectableNpcLocations.CollectableShops.FirstOrDefault(x => x.RetainerBellLoc == shop.RetainerBellLoc);
            if(newShop == null) return false;

            shop.TerritoryId = newShop.TerritoryId;
            shop.Name = null;
            return true;
        }
        return false;
    }
    

}
