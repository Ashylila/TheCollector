using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace TheCollector.FirmamentManager;

// Drives the "HWDLottery" addon — the Kupo of Fortune scratch card you play at Lizbeth.
// Scratch a chest with the (0, chestIndex) callback, then once the reward is shown and the
// Close button (node id 36) is enabled, click it to claim and dismiss the card.
public unsafe class KupoOfFortuneWindowHandler
{
    public const string AddonName = "HWDLottery";

    // Scratch-callback chest indices (second arg of the (0, index) callback). The card has one
    // chest on the left and three on the right; the previously hard-coded index was 1 (a right
    // chest). Confirm the left/right mapping in-game and swap these if it turns out inverted.
    public const int LeftChestIndex = 0;
    public static readonly int[] RightChestIndices = { 1, 2, 3 };

    // The Close/claim button on the lottery result view (node id 36), verified in-game via
    // the addon dump. It only becomes enabled once a chest has been scratched and the reward
    // is shown, so its enabled state is our "ready to close" signal.
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

    // True once the scratched chest's reward is shown and the close button is live — i.e.
    // the card is finished and safe to claim/dismiss.
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
