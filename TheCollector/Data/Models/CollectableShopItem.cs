using TheCollector.CollectableManager;

namespace TheCollector.Data.Models;

public class CollectableShopItem
{
    public string Name { get; set; }
    public CollectableClasses Class { get; set; }
    public ScripType ScripType { get; set; }
    public int Amount { get; set; }
}
