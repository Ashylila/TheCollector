

using System.Numerics;
using System.Text.Json.Serialization;
using ECommons.Automation.NeoTaskManager;


namespace TheCollector.Data.Models;

public class CollectableShop
{
    public string Name { get; set; }
    public Vector3 Location { get; set; }
    public Vector3 RetainerBellLoc {get; set;}
    public bool Disabled { get; set; } = false;
    public bool IsLifestreamRequired { get; set; } = false;
    public string LifestreamCommand { get; set; } = "";
    private Vector3? _scripShopLocation;
    
    [JsonPropertyName("ScripShopLocation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vector3 ScripShopLocation
    {
        get => _scripShopLocation ?? Location;
        set => _scripShopLocation = value;
    }
}
