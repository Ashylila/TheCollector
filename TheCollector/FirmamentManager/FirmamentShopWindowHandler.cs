using System;
using System.Collections.Generic;
using System.Linq;
using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Data;
using TheCollector.Utility;
using AddonMaster = ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace TheCollector.FirmamentManager;

public unsafe class FirmamentShopWindowHandler
{
    public const string ShopAddon = "ShopExchangeCurrency";
    public const string ShopDialogAddon = "ShopExchangeCurrencyDialog";
    public const string MenuAddon = "SelectString";
    public const string YesNoAddon = "SelectYesno";

    private const int MaxTabSweep = 8;

    private readonly PlogonLog _log;
    private readonly Configuration _configuration;
    private readonly FirmamentCatalog _catalog;

    private uint _navItemId;
    private readonly HashSet<int> _triedEntries = new();
    private DateTime _cooldownUntil;

    private bool _directTabTried; 
    private bool _shopTabsInitialized; 
    private int _tabIndex;
    private readonly HashSet<uint> _seenInShop = new();

    private static readonly TimeSpan TransientGrace = TimeSpan.FromSeconds(3);
    private DateTime _lastSessionSeen = DateTime.MinValue;

    public FirmamentShopWindowHandler(PlogonLog log, Configuration configuration, FirmamentCatalog catalog)
    {
        _log = log;
        _configuration = configuration;
        _catalog = catalog;
    }

    private TimeSpan UiDelay => TimeSpan.FromMilliseconds(_configuration.GetUiDelayMs(AddonDelays.FirmamentShop));

    public bool IsReady => Addons.Ready(MenuAddon) || Addons.Ready(ShopAddon);

    public void OpenShop() => ResetNavigation();

    public StepResult SelectItem(uint itemId, int amount)
    {
        if (_navItemId != itemId)
        {
            _navItemId = itemId;
            _triedEntries.Clear();
            ResetShopTabs();
            _cooldownUntil = DateTime.MinValue;
        }

        if (IsReady) _lastSessionSeen = DateTime.UtcNow;

        if (DateTime.UtcNow < _cooldownUntil)
            return StepResult.Continue();

        var havePlacement = _catalog.TryGetPlacement(itemId, out var targetShop, out var targetTab);

        if (Addons.TryGetReady(ShopAddon, out var shopAddon))
        {
            var items = new AddonMaster.ShopExchangeCurrency(shopAddon).BasicShopItems;

            var ware = items.FirstOrDefault(w => w.ItemId == itemId);
            if (ware != null)
            {
                ware.Select(amount);
                _cooldownUntil = DateTime.UtcNow + UiDelay;
                return StepResult.Success();
            }

            if (havePlacement)
            {
                var openShop = _catalog.IdentifyShop(items.Select(w => w.ItemId));
                if (openShop != 0 && openShop != targetShop)
                {
                    _log.Debug($"[FirmamentShop] open shop {openShop} != target {targetShop} for item {itemId}; backing out.");
                    return CloseShopAndContinue(shopAddon);
                }

                if (!_directTabTried && targetTab >= 1)
                {
                    _directTabTried = true;
                    SwitchCategoryTab(shopAddon, targetTab);
                    _cooldownUntil = DateTime.UtcNow + UiDelay;
                    return StepResult.Continue();
                }
            }

            return SweepTabsOrAdvance(shopAddon, items);
        }

        if (Addons.TryGetReady(MenuAddon, out var menuAddon))
        {
            var entries = new AddonMaster.SelectString(menuAddon).Entries;
            var lastSelectable = entries.Length - 1;

            var predicted = havePlacement ? _catalog.PredictEntryIndex(targetShop) : -1;
            var entry = predicted >= 0 && predicted < lastSelectable && !_triedEntries.Contains(predicted)
                ? predicted
                : NextUntriedEntry(lastSelectable);

            if (entry < 0)
                return StepResult.Fail($"Item {itemId} is not stocked by any Skybuilders' Scrip shop.");

            _triedEntries.Add(entry);
            _log.Debug($"[FirmamentShop] opening menu entry {entry} (predicted={predicted}) for item {itemId} → shop {targetShop}, tab {targetTab}.");
            entries[entry].Select();
            ResetShopTabs();
            _cooldownUntil = DateTime.UtcNow + UiDelay;
            return StepResult.Continue();
        }

        if (DateTime.UtcNow - _lastSessionSeen < TransientGrace)
            return StepResult.Continue();
        return StepResult.Fail("Enie shop closed while selecting an item.");
    }

    private StepResult SweepTabsOrAdvance(AtkUnitBase* shopAddon, AddonMaster.ShopExchangeCurrency.ShopItemInfo[] items)
    {
        if (!_shopTabsInitialized)
        {
            _shopTabsInitialized = true;
            _tabIndex = 0;
            _seenInShop.Clear();
            SwitchCategoryTab(shopAddon, 0);
            _cooldownUntil = DateTime.UtcNow + UiDelay;
            return StepResult.Continue();
        }

        var addedNew = false;
        foreach (var w in items) addedNew |= _seenInShop.Add(w.ItemId);

        if (addedNew && _tabIndex + 1 < MaxTabSweep)
        {
            _tabIndex++;
            SwitchCategoryTab(shopAddon, _tabIndex);
            _cooldownUntil = DateTime.UtcNow + UiDelay;
            return StepResult.Continue();
        }

        return CloseShopAndContinue(shopAddon);
    }

    private StepResult CloseShopAndContinue(AtkUnitBase* shopAddon)
    {
        shopAddon->Close(true);
        ResetShopTabs();
        _cooldownUntil = DateTime.UtcNow + UiDelay;
        return StepResult.Continue();
    }

    public void ConfirmPurchaseDialog()
    {
        if (Addons.TryGetReady(ShopDialogAddon, out var addon))
            new AddonMaster.ShopExchangeCurrencyDialog(addon).Exchange();
    }

    public bool ConfirmYesNo()
    {
        if (Addons.TryGetReady(YesNoAddon, out var addon))
        {
            new AddonMaster.SelectYesno(addon).Yes();
            return true;
        }
        return false;
    }

    public bool TryGetScripCount(uint scripItemId, out uint count)
    {
        count = 0;
        if (!IsReady) return false;
        var cur = CurrencyManager.Instance();
        if (cur == null) return false;
        count = cur->GetItemCount(scripItemId);
        return true;
    }

    public void CloseShop()
    {
        ResetNavigation();
        if (Addons.TryGetReady(ShopAddon, out var shopAddon))
            shopAddon->Close(true);
        if (Addons.TryGetReady(MenuAddon, out var menuAddon))
            menuAddon->Close(true);
    }

    private static void SwitchCategoryTab(AtkUnitBase* addon, int tab)
        => Callback.Fire(addon, true, 4, -1, 1, (uint)tab);

    private int NextUntriedEntry(int lastSelectable)
    {
        for (var i = 0; i < lastSelectable; i++)
            if (!_triedEntries.Contains(i)) return i;
        return -1;
    }

    private void ResetNavigation()
    {
        _navItemId = 0;
        _triedEntries.Clear();
        _cooldownUntil = DateTime.MinValue;
        _lastSessionSeen = DateTime.UtcNow;
        ResetShopTabs();
    }

    private void ResetShopTabs()
    {
        _directTabTried = false;
        _shopTabsInitialized = false;
        _tabIndex = 0;
        _seenInShop.Clear();
    }

}
