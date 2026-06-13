using System.Linq;

namespace TheCollector.Utility;

public sealed class FirmamentPlannerService
{
    private readonly Configuration _config;

    public FirmamentPlannerService(Configuration config) => _config = config;

    public bool IsGoalComplete()
    {
        var items = _config.FirmamentGoal.ItemsToPurchase;
        if (items.Count == 0) return false;
        return items.All(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity);
    }
}
