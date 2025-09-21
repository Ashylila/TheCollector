using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.DalamudServices;
using TheCollector.Data.Models;

namespace TheCollector;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public CollectableShop PreferredCollectableShop { get; set; } = new CollectableShop();
    public List<ItemToPurchase> ItemsToPurchase { get; set; } = new List<ItemToPurchase>();
    public bool CollectOnAutogatherDisabled { get; set; } = false;
    public bool EnableAutogatherOnFinish { get; set; } = false;
    public bool CollectOnFinishCraftingList { get; set; } = false;
    
    public void Save()
    {
        Svc.PluginInterface.SavePluginConfig(this);
    }
    
}
