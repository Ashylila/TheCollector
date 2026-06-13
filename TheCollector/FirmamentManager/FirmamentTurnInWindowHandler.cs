using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace TheCollector.FirmamentManager;

public unsafe class FirmamentTurnInWindowHandler
{
    public const string AddonName = "HWDSupply";
    private const string RequestAddon = "Request";
    private const uint CollectableOffset = 500000;

    private readonly PlogonLog _log;

    public FirmamentTurnInWindowHandler(PlogonLog log) => _log = log;

    public bool IsReady =>
        GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) &&
        GenericHelpers.IsAddonReady(addon);

    public void SelectJob(int jobIndex)
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return;

        var values = stackalloc AtkValue[2];
        values[0] = new AtkValue { Type = ValueType.Int, Int = 0 };
        values[1] = new AtkValue { Type = ValueType.Int, Int = jobIndex };
        addon->FireCallback(2, values, true);
    }

    public int FindRowIndex(uint itemId)
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon))
            return -1;

        var target = itemId + CollectableOffset;
        var ordinal = 0;
        for (var i = 0; i < addon->AtkValuesCount; i++)
        {
            ref var v = ref addon->AtkValues[i];
            if (v.Type != ValueType.UInt) continue;
            var u = v.UInt;
            if (u < CollectableOffset || u >= 600000) continue;
            if (u == target) return ordinal;
            ordinal++;
        }
        return -1;
    }

    public bool SelectItem(uint itemId)
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return false;

        var row = FindRowIndex(itemId);
        if (row < 0)
        {
            _log.Debug($"HWDSupply: item {itemId} not present in the current job list.");
            return false;
        }

        var values = stackalloc AtkValue[2];
        values[0] = new AtkValue { Type = ValueType.Int, Int = 1 };
        values[1] = new AtkValue { Type = ValueType.Int, Int = row };
        addon->FireCallback(2, values, true);
        return true;
    }

    public bool IsRequestOpen =>
        GenericHelpers.TryGetAddonByName<AtkUnitBase>(RequestAddon, out var addon) &&
        GenericHelpers.IsAddonReady(addon);

    public bool HandOverEnabled
    {
        get
        {
            if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(RequestAddon, out var addon) ||
                !GenericHelpers.IsAddonReady(addon))
                return false;
            return new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.Request(addon).IsHandOverEnabled;
        }
    }

    public bool HandOver()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(RequestAddon, out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return false;

        var master = new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.Request(addon);
        if (!master.IsHandOverEnabled) return false;
        master.HandOver();
        return true;
    }

    public bool IsPickerOpen =>
        GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var addon) &&
        GenericHelpers.IsAddonReady(addon);

    public void OpenCollectablePicker()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(RequestAddon, out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return;
        var values = stackalloc AtkValue[4];
        values[0] = new AtkValue { Type = ValueType.Int, Int = 2 };
        values[1] = new AtkValue { Type = ValueType.UInt, UInt = 0 };
        values[2] = new AtkValue { Type = ValueType.UInt, UInt = 0 };
        values[3] = new AtkValue { Type = ValueType.UInt, UInt = 0 };
        addon->FireCallback(4, values, true);
    }

    public void SelectFirstPickerEntry()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return;
        var icon = addon->AtkValuesCount > 11 && addon->AtkValues[11].Type == ValueType.UInt
            ? addon->AtkValues[11].UInt
            : 0u;
        var values = stackalloc AtkValue[4];
        values[0] = new AtkValue { Type = ValueType.Int, Int = 0 };
        values[1] = new AtkValue { Type = ValueType.Int, Int = 0 };
        values[2] = new AtkValue { Type = ValueType.UInt, UInt = icon };
        values[3] = new AtkValue { Type = ValueType.UInt, UInt = 0 };
        addon->FireCallback(4, values, true);
    }

    public bool ProgressTalk()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("Talk", out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return false;
        new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.Talk(addon).Click();
        return true;
    }

    public bool ConfirmYesNo()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return false;
        new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.SelectYesno(addon).Yes();
        return true;
    }

    public bool TryGetScripCount(uint scripItemId, out uint count)
    {
        count = 0;
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon))
            return false;
        var cur = CurrencyManager.Instance();
        if (cur == null) return false;
        count = cur->GetItemCount(scripItemId);
        return true;
    }

    public void CloseWindow()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) &&
            GenericHelpers.IsAddonReady(addon))
            addon->Close(true);
    }
}
