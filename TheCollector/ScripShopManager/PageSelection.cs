using System.Reflection.Metadata.Ecma335;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;

namespace TheCollector.ScripShopManager;

public unsafe class PageSelection
{
    public static void SelectPage(int page)
    {
        if (GenericHelpers.TryGetAddonByName("InclusionShop", out AtkUnitBase* addon))
        {
            var selectPage = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 12 },
                new() { Type = ValueType.UInt, UInt = (uint)page }
            };
            for(int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node->Type == (NodeType)1015 && node->NodeId == 9)
                {
                    var compNode = node->GetAsAtkComponentNode();
                    if (compNode == null || compNode->Component == null) continue;
                    var listComponentNote = node->GetAsAtkComponentDropdownList();
                    for (int c = 0; c < listComponentNote->List->UldManager.Nodes.Length; c++)
                    {
                        var resNode = listComponentNote->List->UldManager.Nodes[c];
                        if (resNode.Value->Type == (NodeType)1022)
                        {
                            var listRenderNode =  listComponentNote->List->UldManager.Nodes[c].Value->GetAsAtkComponentListItemRenderer();
                            if (listRenderNode == null) continue;
                            Svc.Log.Debug(listRenderNode->ButtonTextNode->NodeText.GetText());
                        }
                    };
                }
            }
        }
    }
}
