using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace TheCollector.FirmamentManager;

// Drives the "HWDLottery" addon — the Kupo of Fortune scratch card you play at Lizbeth.
public unsafe class KupoOfFortuneWindowHandler
{
    public const string AddonName = "HWDLottery";

    // Chest indices for the scratch (0, index) callback: one chest left, three right.
    public const int LeftChestIndex = 0;
    public static readonly int[] RightChestIndices = { 1, 2, 3 };

    // NodeList index of the Close button; only enabled once the reward is shown.
    private const int CloseButtonNodeIndex = 7;

    public bool IsLotteryOpen => Addons.Ready(AddonName);

    public bool IsTalkOpen => Addons.Ready("Talk");

    public bool IsYesNoOpen => Addons.Ready("SelectYesno");

    public void Scratch(int chestIndex)
    {
        if (!Addons.TryGetReady(AddonName, out var addon))
            return;

        var values = stackalloc AtkValue[2];
        values[0] = new AtkValue { Type = ValueType.Int, Int = 0 };
        values[1] = new AtkValue { Type = ValueType.Int, Int = chestIndex };
        addon->FireCallback(2, values, true);
    }

    // True once the reward is shown and the Close button is live.
    public bool IsRevealComplete
    {
        get
        {
            if (!Addons.TryGetReady(AddonName, out var addon))
                return false;
            var closeButton = GetCloseButton(addon);
            return closeButton != null && closeButton->IsEnabled;
        }
    }

    public bool CloseLottery()
    {
        if (!Addons.TryGetReady(AddonName, out var addon))
            return false;

        var closeButton = GetCloseButton(addon);
        if (closeButton == null || !closeButton->IsEnabled)
            return false;

        var eventData = new AtkEvent();
        addon->ReceiveEvent(AtkEventType.ButtonClick, 0, &eventData);
        return true;
    }

    private static AtkComponentButton* GetCloseButton(AtkUnitBase* addon)
    {
        if (addon->UldManager.NodeListCount <= CloseButtonNodeIndex)
            return null;
        var node = addon->UldManager.NodeList[CloseButtonNodeIndex];
        return node == null ? null : node->GetAsAtkComponentButton();
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
}
