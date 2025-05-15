using System.Linq;
using Dalamud.Game.Inventory;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace TheCollector.Utility;

public static class MethodExtensions
{
    public static bool IsCollectable(this GameInventoryItem item)
    {
        return Svc.Data.GetExcelSheet<Item>().FirstOrDefault(i => i.RowId == item.ItemId).IsCollectable;
    }
}
