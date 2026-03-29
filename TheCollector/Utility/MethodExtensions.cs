using System.Linq;
using Dalamud.Game.Inventory;
using ECommons;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace TheCollector.Utility;

public static class MethodExtensions
{
    public static bool IsCollectable(this GameInventoryItem item)
    {
        var row = Svc.Data.GetExcelSheet<Item>().GetRow(item.ItemId);
        return row.NotNull(out _) && row.IsCollectable;
    }
}
