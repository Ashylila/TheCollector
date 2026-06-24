using ECommons;
using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;

namespace TheCollector.FirmamentManager;

// Drives the "HWDLottery" addon — the Kupo of Fortune scratch card you play at Lizbeth.
public unsafe class KupoOfFortuneWindowHandler
{
    public const string AddonName = "HWDLottery";

    // Hexagon index: 0 is the lone left hexagon, 1-3 the three on the right.
    public const int LeftChestIndex = 0;
    public static readonly int[] RightChestIndices = { 1, 2, 3 };

    // Node id of the result view's Close button; only enabled once the reward is shown.
    private const uint CloseButtonNodeId = 36;

    public bool IsLotteryOpen => Addons.Ready(AddonName);

    public bool IsTalkOpen => Addons.Ready("Talk");

    public bool IsYesNoOpen => Addons.Ready("SelectYesno");

    // The hexagons are top-level nodes id 17 (left) and 18-20 (right), so chestIndex maps to 17+index.
    private const uint FirstHexagonNodeId = 17;

    // Scratches the chosen hexagon by clicking its node.
    // Returns false if the hexagon isn't present yet, so the caller can retry.
    // chestIndex: 0 = left, 1-3 = right.
    public bool Scratch(int chestIndex)
    {
        if (!Addons.TryGetReady(AddonName, out var addon))
            return false;

        var hexNode = FindNodeById(&addon->UldManager, FirstHexagonNodeId + (uint)chestIndex);
        var button = hexNode == null ? null : FindButtonChild(hexNode);
        if (button == null)
            return false;

        button->ClickAddonButton(addon);
        return true;
    }

    private static AtkResNode* FindNodeById(AtkUldManager* uld, uint id)
    {
        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node != null && node->NodeId == id)
                return node;
        }
        return null;
    }

    private static AtkComponentButton* FindButtonChild(AtkResNode* node)
    {
        var component = node->GetAsAtkComponentNode();
        if (component == null || component->Component == null)
            return null;
        var uld = component->Component->UldManager;
        for (var i = 0; i < uld.NodeListCount; i++)
        {
            var child = uld.NodeList[i];
            var button = child == null ? null : child->GetAsAtkComponentButton();
            if (button != null)
                return button;
        }
        return null;
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

        closeButton->ClickAddonButton(addon);
        return true;
    }

    private static AtkComponentButton* GetCloseButton(AtkUnitBase* addon)
    {
        var node = FindNodeById(&addon->UldManager, CloseButtonNodeId);
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
