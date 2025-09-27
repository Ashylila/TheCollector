

using System.Numerics;
using System.Text.Json.Serialization;
using ECommons.Automation.NeoTaskManager;


namespace TheCollector.Data.Models;

public class CollectableShop
{
    public string Name { get; set; }
    public Vector3 Location { get; set; }
}
