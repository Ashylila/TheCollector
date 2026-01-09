using System.Collections.Generic;
using System.Numerics;
using TheCollector.Data.Models;

namespace TheCollector.CollectableManager;

public static class CollectableNpcLocations
{
    public static List<CollectableShop> CollectableShops = new()
    {
        new CollectableShop()
        {
            Name = "Solution Nine",
            Location = new Vector3(-162.17f, 0.9219f, -30.458f),
            ScripShopLocation = new Vector3(-161.84605f, 0.921f, -42.06536f),
            RetainerBellLoc = new Vector3(-151.598f, 0.59f, -15.30f),
            IsLifestreamRequired = true,
            LifestreamCommand = "Nexus Arcade"
            
        },
        new CollectableShop()
        {
            Name = "Eulmore",
            Location = new Vector3(16.94f, 82.05f, -19.177f),
            RetainerBellLoc = new Vector3(7.186f, 83.176f, 83.17f)
        },
        new CollectableShop()
        {
            Name = "Old Gridania",
            Location = new Vector3(143.62454f, 13.74769f, -105.33799f),
            IsLifestreamRequired = true,
            LifestreamCommand = "Leatherworkers",
            RetainerBellLoc = new Vector3(171f, 15.48f, -101.48f)
        }
    };
    
}
