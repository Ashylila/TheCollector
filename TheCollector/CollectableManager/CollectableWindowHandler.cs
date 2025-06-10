using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Dalamud.Memory;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Data.Parsing.Uld;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;


namespace TheCollector.CollectableManager;

 public unsafe class CollectableWindowHandler
 {
     public unsafe bool IsReady => GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
                                   GenericHelpers.IsAddonReady(addon);

     public unsafe void SelectJob(uint id)
     {
         if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
             GenericHelpers.IsAddonReady(addon))
         {
             var selectJob = stackalloc AtkValue[]
             {
                 new() {Type = ValueType.Int, Int = 14},
                 new(){Type = ValueType.UInt, UInt = id }
             };
             addon->FireCallback(2, selectJob); 
             
         }
     }

    public unsafe void SelectItem(string itemName)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
            GenericHelpers.IsAddonReady(addon))
        {
            var turnIn = new TurninWindow(addon);
            var selectItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 12 },
                new(){Type = ValueType.UInt, UInt = (uint)turnIn.GetItemIndexOf(itemName)}
            };
            addon->FireCallback(2, selectItem);
            PluginLog.Debug(turnIn.GetItemIndexOf(itemName).ToString());
        }
    }
    
    public unsafe void SubmitItem()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
            GenericHelpers.IsAddonReady(addon))
        {
            var submitItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 15 },
                new(){Type = ValueType.UInt, UInt = 0}
            };
            addon->FireCallback(2, submitItem, true);
        }
    }
    public unsafe void CloseWindow()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
            GenericHelpers.IsAddonReady(addon))
        {
            addon->Close(true);
        }
    }
    public unsafe int PurpleScripCount()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
            GenericHelpers.IsAddonReady(addon))
        {
            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node->Type == NodeType.Res && node->NodeId == 14)
                {
                    var child = node->ChildNode;
                    if (child->NodeId == 16)
                    {
                        var componentNode = child->GetAsAtkComponentNode();
                        return int.Parse(componentNode->Component->GetTextNodeById(4)->GetAsAtkTextNode()->NodeText.StringPtr
                                             .ExtractText().Split('/')[0].Replace(",", ""));

                    }
                    else
                    {
                        child = child->NextSiblingNode;
                        var componentNode = child->GetAsAtkComponentNode();
                        return int.Parse(componentNode->Component->GetTextNodeById(4)->GetAsAtkTextNode()->NodeText.StringPtr
                                             .ExtractText().Split('/')[0].Replace(",", ""));
                    }
                }
            }
        }
        return -1;
    }
    public unsafe int OrangeScripCount()
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
            GenericHelpers.IsAddonReady(addon))
        {
            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                
                if (node->Type == NodeType.Res && node->NodeId == 14)
                {
                    var child = node->ChildNode;
                    if (child->NodeId == 15)
                    {
                        var componentNode = child->GetAsAtkComponentNode();
                        return int.Parse(componentNode->Component->GetTextNodeById(4)->GetAsAtkTextNode()->NodeText.StringPtr
                                             .ExtractText().Split('/')[0]);

                    }
                    else
                    {
                        var nextChild = child->PrevSiblingNode;
                        var componentNode = nextChild->GetAsAtkComponentNode();
                        return int.Parse(componentNode->Component->GetTextNodeById(4)->GetAsAtkTextNode()->NodeText.StringPtr
                                             .ExtractText().Split('/')[0].Replace(",", ""));
                    }
                }
            }
        }
        return -1;
    }
}
