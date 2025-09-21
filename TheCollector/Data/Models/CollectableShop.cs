

using System.Numerics;
using System.Text.Json.Serialization;


namespace TheCollector.Data.Models;

public class CollectableShop
{
    public string Name { get; set; }
    [JsonIgnore]
    public Vector3 Location { get; set; }
}
