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

        if (Version < 2)
        {
            changed |= Migrate_AddBellLocation();
            Version = 2;
        }
        Save();
        return changed;
    }

    private bool Migrate_AddBellLocation()
    {
        var changed = false;

        if (PreferredCollectableShop != null)
            changed |= EnsureBellLocation(PreferredCollectableShop);


        return changed;
    }

    private bool EnsureBellLocation(CollectableShop loc)
    {
        if (loc == null) return false;

        if (loc.RetainerBellLoc == default)
        {
            var def = CollectableNpcLocations.CollectableShops
                .FirstOrDefault(s => string.Equals(s.Name, loc.Name, StringComparison.Ordinal));

            if (def == null) return false;

            loc.RetainerBellLoc = def.RetainerBellLoc;
            return true;
        }

        return false;
    }

}
