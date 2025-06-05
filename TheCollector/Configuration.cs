using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using TheCollector.Data.Models;

namespace TheCollector;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public CollectableShop PreferredCollectableShop { get; set; }

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
