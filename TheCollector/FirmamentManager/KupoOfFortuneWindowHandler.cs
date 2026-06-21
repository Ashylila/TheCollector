using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace TheCollector.FirmamentManager;

// Drives the "HWDLottery" addon — the Kupo of Fortune scratch card you play at Lizbeth.
// Scratch a chest with the (0, 1) callback, then once the reward is shown and the Close
// button (node id 36) is enabled, click it to claim and dismiss the card.
public unsafe class KupoOfFortuneWindowHandler
{
    public const string AddonName = "HWDLottery";

    // The Close/claim button on the lottery result view (node id 36), verified in-game via
    // the addon dump. It only becomes enabled once a chest has been scratched and the reward
    // is shown, so its enabled state is our "ready to close" signal.
    private const int CloseButtonNodeIndex = 7;

    public bool IsLotteryOpen =>
        GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) &&
        GenericHelpers.IsAddonReady(addon);

    public bool IsTalkOpen =>
        GenericHelpers.TryGetAddonByName<AtkUnitBase>("Talk", out var addon) &&
        GenericHelpers.IsAddonReady(addon);

    public bool IsYesNoOpen =>
        GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) &&
        GenericHelpers.IsAddonReady(addon);

    public void Scratch()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return;

        var values = stackalloc AtkValue[2];
        values[0] = new AtkValue { Type = ValueType.Int, Int = 0 };
        values[1] = new AtkValue { Type = ValueType.Int, Int = 1 };
        addon->FireCallback(2, values, true);
    }

    // True once the scratched chest's reward is shown and the close button is live — i.e.
    // the card is finished and safe to claim/dismiss.
    public bool IsRevealComplete
    {
        get
        {
            if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) ||
                !GenericHelpers.IsAddonReady(addon))
                return false;
            var closeButton = GetCloseButton(addon);
            return closeButton != null && closeButton->IsEnabled;
        }
    }

    public bool CloseLottery()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return false;

        var closeButton = GetCloseButton(addon);
        if (closeButton == null || !closeButton->IsEnabled)
            return false;

        // Click the result window's Close button (node id 36). The reward is already granted
        // on scratch, so this just dismisses the display.
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

    // Lizbeth shows a one-page Talk dialogue and a yes/no confirmation before the lottery
    // opens; reuse the same advance helpers the appraiser flow uses.
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

}
