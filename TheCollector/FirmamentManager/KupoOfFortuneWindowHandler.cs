using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;
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

    // NodeList index of the Close button; only enabled once the reward is shown.
    private const int CloseButtonNodeIndex = 7;

    private const uint PickPromptNodeId = 21;

    private const int RevealStateValueIndex = 32;
    private const uint RevealStateComplete = 3;

    private readonly PlogonLog _log;

    public KupoOfFortuneWindowHandler(PlogonLog log) => _log = log;

    public bool IsLotteryOpen => Addons.Ready(AddonName);

    public bool IsLotteryPresent => Addons.TryGet(AddonName, out _);

    public bool IsTalkOpen => Addons.Ready("Talk");

    public bool IsYesNoOpen => Addons.Ready("SelectYesno");

    public bool IsCardLive
    {
        get
        {
            if (!Addons.TryGetReady(AddonName, out var addon))
                return false;
            var prompt = FindNodeById(&addon->UldManager, PickPromptNodeId);
            return prompt != null && prompt->IsVisible();
        }
    }

    public bool Scratch(int chestIndex)
    {
        if (!IsCardLive || !Addons.TryGetReady(AddonName, out var addon))
            return false;

        Callback.Fire(addon, true, 0, chestIndex);

        var lottery = (AddonHWDLottery*)addon;
        _log.Debug($"HWDLottery: fired scratch callback (0, {chestIndex}); stage={lottery->Stage} selected={lottery->SelectedIndex}.");
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

    public bool IsRevealComplete
    {
        get
        {
            if (!Addons.TryGetReady(AddonName, out var addon))
                return false;
            if (addon->AtkValues == null || addon->AtkValuesCount <= RevealStateValueIndex)
                return false;
            ref var state = ref addon->AtkValues[RevealStateValueIndex];
            if (state.Type != AtkValueType.UInt || state.UInt < RevealStateComplete)
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

        Callback.Fire(addon, true, 2);
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
        addon->FireCallback(0, null, true);
        return true;
    }

    public bool ConfirmYesNo()
    {
        if (!Addons.TryGetReady("SelectYesno", out var addon))
            return false;
        Callback.Fire(addon, true, 0);
        return true;
    }
}
