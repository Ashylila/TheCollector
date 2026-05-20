using System.Collections.Generic;

namespace TheCollector.Utility;


public static class TerritoryRouting
{
    public readonly record struct AethernetRoute(uint RootAetheryteId, uint AethernetId);

    private static readonly Dictionary<uint, AethernetRoute> Routes = new()
    {

        [1186] = new(217, 235),
        [132]  = new(2,   26),
    };

    public static bool TryGet(uint territoryId, out AethernetRoute route)
        => Routes.TryGetValue(territoryId, out route);

    public static bool RequiresAethernet(uint territoryId) => Routes.ContainsKey(territoryId);
}
