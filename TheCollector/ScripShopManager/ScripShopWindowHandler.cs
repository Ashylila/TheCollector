using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Data.Parsing.Uld;

namespace TheCollector.ScripShopManager;

public unsafe class ScripShopWindowHandler
{
    public void OpenShop()
    {
        if (GenericHelpers.TryGetAddonByName("SelectIconString", out AtkUnitBase* addon))
        {
            var openShop = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 0 }
            };
            addon->FireCallback(1, openShop);
        }
    }
    public void SelectPage(int page)
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            var selectPage = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 12 },
                new() { Type = ValueType.UInt, UInt = (uint)page }
            };
            for(int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node->Type == (NodeType)1015 && node->NodeId == 7)
                {
                    var compNode = node->GetAsAtkComponentNode();
                    if (compNode == null || compNode->Component == null) continue;

                    var dropDown = compNode->GetAsAtkComponentDropdownList();
                    dropDown->SelectItem(page);
                    addon->FireCallback(2, selectPage);
                }
            }
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
    } public bool SelectItem(uint itemId, int amount)
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            var shop = new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.InclusionShop(addon);
            var shopItems = shop.ShopItems;
            var index = -1;
            
            for (int i = 0; i < shopItems.Length; i++)
            {
                if (shopItems[i].ItemId == itemId)
                {
                    index = i;
                    break;
                }
            }
            
            if (index == -1)
            {
                Svc.Log.Error($"[ScripShopWindowHandler] Item ID {itemId} not found in current scrip shop tab");
                return false;
            }
            
            var selectItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 14 },
                new() { Type = ValueType.UInt, UInt = (uint)index },
                new() { Type = ValueType.UInt, UInt = (uint)amount }
            };
            addon->FireCallback(3, selectItem);
            return true;
        }
        
        return false;
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
            addon->Close(true);
        }
    }

    public int ScripCount()
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            for(int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node->Type == NodeType.Text && node->NodeId == 4)
                {
                    var textNode = node->GetAsAtkTextNode();
                    if (textNode != null)
                    {
                        return int.Parse(textNode->NodeText.ToString().Replace(",", ""));
                    }
                }
            }
        }
        return -1;
    }
    public void CloseShop()
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            addon->Close(true);
        }
    }
}
