using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using TheCollector.Data.Models;

namespace TheCollector;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public CollectableShop PreferredCollectableShop { get; set; } = new CollectableShop();
    public List<ItemToPurchase> ItemsToPurchase { get; set; } = new List<ItemToPurchase>();

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
