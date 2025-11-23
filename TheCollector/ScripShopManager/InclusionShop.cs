using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;

namespace TheCollector.ScripShopManager;

public unsafe class InclusionShop : TreeListWindowBase
{
    public InclusionShop(AtkUnitBase* addon) : base(addon) { }

    protected override bool IsTargetNode(AtkResNode* node) => node->Type == (NodeType)1024 && node->NodeId == 19;

    protected override string ExtractLabel(AtkComponentTreeListItem* item)
    {
        var label = item->StringValues[0].Value;
        return SeString.Parse(label).TextValue;
    }
}
