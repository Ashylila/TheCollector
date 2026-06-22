using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace TheCollector.ResourceInspectionManager;

// Wrapper over the Skybuilders' "Resource Control" addon shown when talking to Flotpassant
// (the Resource Inspector). Modeled on FirmamentTurnInWindowHandler.
//
// IMPORTANT — in-game discovery required: the addon's internal name and the FireCallback
// argument layouts below are best guesses and MUST be confirmed in-game with Dalamud's addon
// inspector (the Firmament HWDSupply callbacks were found the same way). All such magic values
// are confined to this file. The pipeline deliberately detects per-job exhaustion and progress
// via inventory/scrip deltas instead of node reads, so only these callbacks need verifying.
public unsafe class ResourceInspectionWindowHandler
{
    // Confirmed in-game: the Resource Control window's addon.
    public const string AddonName = "HWDGathereInspect";

    // Confirmed in-game via /collector inspectdebug.
    private const int CaseSelectJob = 14;        // arg1 (UInt) = job tab index (0=MIN, 1=BTN, 2=FSH)
    private const int CaseAutoSubmit = 12;        // auto-select up to five eligible items
    private const int CaseRequestInspection = 11; // submit the queued items for inspection

    private readonly PlogonLog _log;

    public ResourceInspectionWindowHandler(PlogonLog log) => _log = log;

    private bool TryGetAddon(out AtkUnitBase* addon)
        => Addons.TryGetReady(AddonName, out addon);

    public bool IsReady => TryGetAddon(out _);

    // Live window state, decoded from the addon's AtkValues (see
    // images/resource-inspection-atkvalues.txt):
    //   [1]          = selected gathering job (0 = Miner, 1 = Botanist, 2 = Fisher)
    //   [count - 4]  = number of materials currently queued for inspection (0..5)
    //   [count - 1]  = "resources available" — false once the selected job has nothing left
    // The item list between them is variable-length, so the summary fields are read relative
    // to AtkValuesCount rather than at fixed indices.
    public bool TryReadState(out int selectedJob, out int queuedCount, out bool resourcesAvailable)
    {
        selectedJob = -1;
        queuedCount = 0;
        resourcesAvailable = false;
        if (!TryGetAddon(out var addon)) return false;
        var count = (int)addon->AtkValuesCount;
        var v = addon->AtkValues;
        if (v == null || count < 7) return false;

        if (v[1].Type == ValueType.UInt) selectedJob = (int)v[1].UInt;
        var queued = v[count - 4];
        if (queued.Type == ValueType.UInt) queuedCount = (int)queued.UInt;
        resourcesAvailable = v[count - 1].Byte != 0;
        return true;
    }

    public void SelectJob(int jobTabIndex)
    {
        if (!TryGetAddon(out var addon)) return;
        var values = stackalloc AtkValue[2];
        values[0] = new AtkValue { Type = ValueType.Int, Int = CaseSelectJob };
        values[1] = new AtkValue { Type = ValueType.UInt, UInt = (uint)jobTabIndex };
        addon->FireCallback(2, values, true);
    }

    public bool AutoSubmit()
    {
        if (!TryGetAddon(out var addon)) return false;
        var values = stackalloc AtkValue[1];
        values[0] = new AtkValue { Type = ValueType.Int, Int = CaseAutoSubmit };
        addon->FireCallback(1, values, true);
        return true;
    }

    public bool RequestInspection()
    {
        if (!TryGetAddon(out var addon)) return false;
        var values = stackalloc AtkValue[1];
        values[0] = new AtkValue { Type = ValueType.Int, Int = CaseRequestInspection };
        addon->FireCallback(1, values, true);
        return true;
    }

    // Advance a one-page Talk dialogue Flotpassant may show before the window opens.
    public bool ProgressTalk()
    {
        if (!Addons.TryGetReady("Talk", out var addon))
            return false;
        new ECommons.UIHelpers.AddonMasterImplementations.AddonMaster.Talk(addon).Click();
        return true;
    }

    // Confirmation popup that may appear after Request Inspection.
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
