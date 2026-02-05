using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;

namespace TheCollector.Utility;

public unsafe abstract class TreeListWindowBase
{
    protected readonly PlogonLog Log = new();
    protected readonly AtkUnitBase* Addon;
    protected readonly AtkComponentTreeList* TreeList;
    protected readonly StdVector<Pointer<AtkComponentTreeListItem>> Items;
    protected readonly string[] Labels;
    protected readonly int ItemCount;

    protected TreeListWindowBase(AtkUnitBase* addon)
    {
        Addon = addon;
        TreeList = FindTreeList(addon);
        ItemCount = (int)TreeList->Items.Count;
        Items = TreeList->Items;
        Labels = new string[ItemCount];
        PopulateLabels();
    }

    public virtual int GetItemIndexOf(string label)
    {
        for (var i = 0; i < Labels.Length; i++)
        {
            Log.Debug($"GetItemIndexOf({label})");
            var current = Labels[i];
            if (current.Contains(label, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    protected abstract bool IsTargetNode(AtkResNode* node);

    protected abstract string ExtractLabel(AtkComponentTreeListItem* item);

    private AtkComponentTreeList* FindTreeList(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (!IsTargetNode(node)) continue;

            var compNode = node->GetAsAtkComponentNode();
            if (compNode == null || compNode->Component == null) continue;

            return (AtkComponentTreeList*)compNode->Component;
        }

        return null;
    }

    private void PopulateLabels()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            var item = Items[i].Value;
            Labels[i] = item != null ? ExtractLabel(item) : string.Empty;
        }
    }
}
