using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace TheCollector.Utility;

public static class CurrencyHelper
{
    public static unsafe uint SpecialIdToItemId(uint specialId)
    {
        var cur = CurrencyManager.Instance();
        if (cur == null) return 0;
        return cur->GetItemIdBySpecialId((byte)specialId);
    }

    public static string GetCurrencyName(uint specialId)
    {
        var itemId = SpecialIdToItemId(specialId);
        if (itemId == 0) return $"Scrip Type {specialId}";
        var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        return item?.Name.ExtractText() ?? $"Scrip Type {specialId}";
    }

    public static uint GetCurrencyIdForItem(uint shopItemId)
    {
        foreach (var s in ScripShopItemManager.ShopItems)
        {
            if (s.ItemId == shopItemId)
                return s.CurrencyId;
        }
        return 0;
    }
}
