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
            Location = new Vector3(-162f, 0.92f, -33)
        },
        new CollectableShop()
        {
            Name = "Eulmore",
            Location = new Vector3(16.94f, 82.05f, -19.177f)
        }
    };
    public static Vector3 CollectableNpcLocationVectors(int territoryId)
    {
        return territoryId switch
        {
            1186 => new Vector3(-162f, 0.92f, -33),
            _ => new Vector3(162f, 0.92f, -33),
        };
    }
    
}
