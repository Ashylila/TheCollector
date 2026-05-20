using System;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;

namespace TheCollector.CollectableManager;

public unsafe class TurninWindow(AtkUnitBase* addon) : TreeListWindowBase(addon)
{
    protected override bool IsTargetNode(AtkResNode* node) => node->Type == (NodeType)1028 && node->NodeId == 28;

    protected override string ExtractLabel(AtkComponentTreeListItem* item)
    {
        var label = item->StringValues[0].Value;
        return SeString.Parse(label).TextValue;
    }

    public override int GetItemIndexOf(string label)
    {
        Log.Debug($"GetItemIndexOf({label})");
        var leafIndex = 0;
        for (var i = 0; i < Labels.Length; i++)
        {
            var item = Items[i].Value;
            if (item == null)
                continue;

            var rawType = item->UIntValues.Count > 0 ? item->UIntValues[0] : 0u;
            var itemType = (AtkComponentTreeListItemType)(rawType & 0xF);
            if (itemType == AtkComponentTreeListItemType.GroupHeader ||
                itemType == AtkComponentTreeListItemType.CollapsibleGroupHeader)
                continue;

            if (Labels[i].Contains(label, StringComparison.OrdinalIgnoreCase))
                return leafIndex;

            leafIndex++;
        }

        return -1;
    }
    
    
    
}
