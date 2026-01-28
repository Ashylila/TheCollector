

using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;


namespace TheCollector.Data.Models;

public class CollectableShop
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name {get; set;}
    public uint TerritoryId {get; set;}
    public Vector3 Location { get; set; }
    public Vector3 RetainerBellLoc {get; set;}
    public bool Disabled { get; set; } = false;
    public bool IsLifestreamRequired { get; set; } = false;
    public string LifestreamCommand { get; set; } = "";
    [JsonIgnore]
    public string DisplayName =>
        Svc.Data.GetExcelSheet<TerritoryType>()
            .GetRow(TerritoryId)
            .PlaceName.Value.Name.ExtractText();
    private Vector3? _scripShopLocation;
    
    [JsonPropertyName("ScripShopLocation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vector3 ScripShopLocation
    {
        get => _scripShopLocation ?? Location;
        set => _scripShopLocation = value;
    }
}
