using System.Collections.Generic;

namespace TheCollector.Data.Models;

public class ScripGoal
{
    public bool StopGatheringWhenComplete { get; set; } = true;
    public bool HideFishingCollectables { get; set; } = false;
    public bool HideUnobtainableCollectables { get; set; } = true;

    public List<ItemToPurchase> ItemsToPurchase { get; set; } = new();
}
