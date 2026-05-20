using System.Numerics;

namespace TheCollector.Data.Models;

public class VendorNpc
{
    public uint DataId { get; init; }
    public string Name { get; init; } = "";
    public uint TerritoryId { get; init; }
    public uint MapId { get; init; }
    public Vector3 Position { get; init; }
    public bool IsScripVendor { get; init; }
    public bool IsCollectableVendor { get; init; }
}
