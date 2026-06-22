using System.Text.RegularExpressions;
using Dalamud.Memory;
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

    public bool IsReady => Addons.Ready(AddonName);

    // Held Kupo of Fortune vouchers as last read from this window's "Kupo Vouchers N/10"
    // counter, or -1 if not seen yet. Sampled during turn-in and used to gate the minigame.
    public int LastVoucherCount { get; private set; } = -1;

    // Reads the Kupo voucher count from the window's "N/10" counter. The Skybuilders' Scrip
    // total ("6,120/10,000") is the only other "n/m" string and carries separators, so an
    // unseparated "(digits)/(digits)" match uniquely identifies the voucher counter.
    public bool TryGetVoucherCount(out int current)
    {
        current = -1;
        if (!Addons.TryGetReady(AddonName, out var addon))
            return false;

        for (var i = 0; i < addon->AtkValuesCount; i++)
        {
            ref var v = ref addon->AtkValues[i];
            if (v.Type != ValueType.String || v.String.Value == null) continue;
            var text = MemoryHelper.ReadSeStringNullTerminated((nint)v.String.Value).TextValue;
            var match = Regex.Match(text, @"^(\d+)/(\d+)$");
            if (!match.Success) continue;
            current = int.Parse(match.Groups[1].Value);
            LastVoucherCount = current;
            return true;
        }
        return false;
    }

    public void SelectJob(int jobIndex)
    {
        if (!Addons.TryGetReady(AddonName, out var addon))
            return;

        var values = stackalloc AtkValue[2];
        values[0] = new AtkValue { Type = ValueType.Int, Int = 0 };
        values[1] = new AtkValue { Type = ValueType.Int, Int = jobIndex };
        addon->FireCallback(2, values, true);
    }

    public int FindRowIndex(uint itemId)
    {
        if (!Addons.TryGet(AddonName, out var addon))
            return -1;

        var target = itemId + CollectableOffset;

        const int indexOffset = 18;
        for (var i = 0; i + indexOffset < addon->AtkValuesCount; i++)
        {
            ref var v = ref addon->AtkValues[i];
            if (v.Type != ValueType.UInt || v.UInt != target)
                continue;

            ref var idx = ref addon->AtkValues[i + indexOffset];
            return idx.Type == ValueType.UInt ? (int)idx.UInt : -1;
        }

        return -1;
    }

    public bool SelectItem(uint itemId)
    {
        if (!Addons.TryGetReady(AddonName, out var addon))
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

    public bool IsRequestOpen => Addons.Ready(RequestAddon);

    public bool HandOverEnabled
    {
        get
        {
            if (!Addons.TryGetReady(RequestAddon, out var addon))
                return false;
            return new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.Request(addon).IsHandOverEnabled;
        }
    }

    public bool HandOver()
    {
        if (!Addons.TryGetReady(RequestAddon, out var addon))
            return false;

        var master = new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.Request(addon);
        if (!master.IsHandOverEnabled) return false;
        master.HandOver();
        return true;
    }

    public bool IsPickerOpen => Addons.Ready("ContextIconMenu");

    public void OpenCollectablePicker()
    {
        if (!Addons.TryGetReady(RequestAddon, out var addon))
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
        if (!Addons.TryGetReady("ContextIconMenu", out var addon))
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
        if (!Addons.TryGetReady("Talk", out var addon))
            return false;
        new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.Talk(addon).Click();
        return true;
    }

    public bool ConfirmYesNo()
    {
        if (!Addons.TryGetReady("SelectYesno", out var addon))
            return false;
        new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.SelectYesno(addon).Yes();
        return true;
    }

    public bool TryGetScripCount(uint scripItemId, out uint count)
    {
        count = 0;
        if (!Addons.TryGet(AddonName, out var addon))
            return false;
        var cur = CurrencyManager.Instance();
        if (cur == null) return false;
        count = cur->GetItemCount(scripItemId);
        return true;
    }

    public void CloseWindow()
    {
        if (Addons.TryGetReady(AddonName, out var addon))
            addon->Close(true);
    }
}
