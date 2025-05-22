using System.Numerics;

namespace TheCollector.CollectableManager;

public static class CollectableNpcLocations
{
    public static Vector3 CollectableNpcLocationVectors(int territoryId)
    {
        return territoryId switch
        {
            1186 => new Vector3(-162f, 0.92f, -33),
            _ => new Vector3(162f, 0.92f, -33),
        };
    }
    
}
