using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TheCollector.ScripShopManager;

public unsafe class ScripShopWIndowHandler
{
    public void SelectPage(int page)
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            var selectPage = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 12 },
                new() { Type = ValueType.UInt, UInt = (uint)page }
            };
            addon->FireCallback(2, selectPage);
        }
    }
    public void SelectSubPage(int subPage)
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            var selectSubPage = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 13 },
                new() { Type = ValueType.UInt, UInt = (uint)subPage }
            };
            addon->FireCallback(2, selectSubPage);
        }
    } public void SelectItem(int index)
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            var selectItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 14 },
                new() { Type = ValueType.UInt, UInt = (uint)index }
            };
            addon->FireCallback(2, selectItem);
        }
    }

    public void PurchaseItem()
    {
        if (GenericHelpers.TryGetAddonByName("ShopExchangeItemDialog", out AtkUnitBase* addon))
        {
            var purchaseItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 0 }
            };
            addon->FireCallback(1, purchaseItem);
        }
    }
}
