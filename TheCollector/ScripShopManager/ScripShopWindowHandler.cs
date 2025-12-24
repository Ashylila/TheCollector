
using System;
using ECommons;
using ECommons.DalamudServices;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;
using Lumina.Data.Parsing.Uld;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using TheCollector.Utility;
using TheCollector.Automation;

namespace TheCollector.ScripShopManager;

public unsafe class ScripShopWindowHandler
{
    private IFramework _framework;
    private readonly PlogonLog _log;
    private bool _forceSearchActive;
    private bool _waitingForTabChange;

    private uint _targetItemId;
    private int _targetAmount;

    private int _forceSubPage;
    private int _forceSubPageMax;

    private DateTime _cooldownUntil;

    private const int DropdownNodeId = 9;
    private static readonly TimeSpan UiDelay = TimeSpan.FromMilliseconds(150);
    public ScripShopWindowHandler(IFramework framework, PlogonLog log)
    {
        _framework = framework;
        _log = log;
    }
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
            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
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

    public StepResult SelectItem(uint itemId, int amount)
    {
        if (DateTime.UtcNow < _cooldownUntil)
            return StepResult.Continue();

        if (!GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon) || addon == null)
        {
            ResetForceSearch();
            return StepResult.Fail("InclusionShop not open");
        }

        if (!_forceSearchActive || _targetItemId != itemId || _targetAmount != amount)
        {
            _forceSearchActive = true;
            _waitingForTabChange = false;

            _targetItemId = itemId;
            _targetAmount = amount;

            _forceSubPage = 1;
            _forceSubPageMax = 0;

            _cooldownUntil = DateTime.MinValue;
        }

        if (TrySelectItemInCurrentTab(addon, _targetItemId, _targetAmount))
        {
            ResetForceSearch();
            return StepResult.Success();
        }

        if (_forceSubPageMax == 0 && !TryGetDropdownList(addon, out _forceSubPageMax))
        {
            ResetForceSearch();
            return StepResult.Fail("Could not read subpage dropdown");
        }

        if (_forceSubPage > _forceSubPageMax)
        {
            var id = _targetItemId;
            ResetForceSearch();
            return StepResult.Fail($"Item {id} not found in any subpage");
        }

        if (!_waitingForTabChange)
        {
            SelectSubPage(_forceSubPage);
            _waitingForTabChange = true;
            _cooldownUntil = DateTime.UtcNow + UiDelay;
            return StepResult.Continue();
        }

        _forceSubPage++;
        _waitingForTabChange = false;
        _cooldownUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(50);
        return StepResult.Continue();
    }
    private void ResetForceSearch()
    {
        _forceSearchActive = false;
        _waitingForTabChange = false;

        _targetItemId = 0;
        _targetAmount = 0;

        _forceSubPage = 0;
        _forceSubPageMax = 0;

        _cooldownUntil = DateTime.MinValue;
    }


    private bool TryGetDropdownList(AtkUnitBase* addon, out int listLength)
    {
        listLength = 0;

        for (int i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node->Type != (NodeType)1015 || node->NodeId != DropdownNodeId)
                continue;

            var compNode = node->GetAsAtkComponentNode();
            if (compNode == null || compNode->Component == null)
                return false;

            var dropDown = compNode->GetAsAtkComponentDropdownList();
            if (dropDown == null || dropDown->List == null)
                return false;

            listLength = dropDown->List->ListLength;
            return true;

        }
        return false;
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
    }

    private bool TrySelectItemInCurrentTab(AtkUnitBase* addon, uint itemId, int amount)
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
            return false;

        var selectItem = stackalloc AtkValue[]
        {
        new() { Type = ValueType.Int,  Int  = 14 },
        new() { Type = ValueType.UInt, UInt = (uint)index },
        new() { Type = ValueType.UInt, UInt = (uint)amount }
    };
        addon->FireCallback(3, selectItem);
        return true;
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
            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
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
