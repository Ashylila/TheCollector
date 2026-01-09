using System;
using System.Text.RegularExpressions;
using Dalamud.Utility;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;


namespace TheCollector.CollectableManager;

 public unsafe class CollectableWindowHandler
 {
     public unsafe bool IsReady => GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
                                   GenericHelpers.IsAddonReady(addon);
     private readonly PlogonLog _log = new();
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

    public unsafe bool SelectItem(string itemName)
    {
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) &&
            GenericHelpers.IsAddonReady(addon))
        {
            var turnIn = new TurninWindow(addon);
            var index = turnIn.GetItemIndexOf(itemName);
            if (turnIn.GetItemIndexOf(itemName) == -1)
            {
                return false;
            }
            var selectItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 12 },
                new(){Type = ValueType.UInt, UInt = (uint)index}
            };
            addon->FireCallback(2, selectItem);
            _log.Debug(turnIn.GetItemIndexOf(itemName).ToString());
            return true;
        }
        return false;
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
        try
        {
            if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) ||
                !GenericHelpers.IsAddonReady(addon))
                return -1;

            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node == null || node->Type != NodeType.Res || node->NodeId != 14) continue;

                var child = node->ChildNode;
                if (child == null) return -1;

                if (child->NodeId != 16) child = child->NextSiblingNode;
                if (child == null) return -1;

                var comp = child->GetAsAtkComponentNode();
                if (comp == null || comp->Component == null) return -1;

                var textNode = comp->Component->GetTextNodeById(4)->GetAsAtkTextNode();
                if (textNode == null || textNode->NodeText.StringPtr.Value == null) return -1;

                var raw = textNode->NodeText.StringPtr.ExtractText();
                var left = raw?.Split('/')?[0];
                if (string.IsNullOrEmpty(left)) return -1;

                left = Regex.Replace(left, @"[^\d]", "");
                if (left.Length == 0) return -1;
                _log.Debug(left);
                if (int.TryParse(left, out var val)) return val;
                return -1;
            }

            return -1;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error getting purple scrip count");
            return -1;
        }
    }

    public unsafe int OrangeScripCount()
{
    try
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("CollectablesShop", out var addon) ||
            !GenericHelpers.IsAddonReady(addon))
            return -1;

        for (int i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Res || node->NodeId != 14) continue;

            var child = node->ChildNode;
            if (child == null) return -1;

            if (child->NodeId == 15)
            {
                var comp = child->GetAsAtkComponentNode();
                if (comp == null || comp->Component == null) return -1;

                var tn = comp->Component->GetTextNodeById(4);
                if (tn == null) return -1;

                var txt = tn->GetAsAtkTextNode();
                if (txt == null || txt->NodeText.StringPtr.Value == null) return -1;

                var raw = txt->NodeText.StringPtr.ExtractText();
                var left = raw?.Split('/')?[0];
                if (string.IsNullOrWhiteSpace(left)) return -1;

                left = Regex.Replace(left, @"[^\d]", "");
                if (left.Length == 0) return -1;

                _log.Debug(left);
                if (int.TryParse(left, out var val)) return val;
                return -1;
            }
            else
            {
                var prev = child->PrevSiblingNode;
                if (prev == null) return -1;

                var comp = prev->GetAsAtkComponentNode();
                if (comp == null || comp->Component == null) return -1;

                var tn = comp->Component->GetTextNodeById(4);
                if (tn == null) return -1;

                var txt = tn->GetAsAtkTextNode();
                if (txt == null || txt->NodeText.StringPtr.Value == null) return -1;

                var raw = txt->NodeText.StringPtr.ExtractText();
                var left = raw?.Split('/')?[0];
                if (string.IsNullOrWhiteSpace(left)) return -1;

                left = Regex.Replace(left, @"[^\d]", "");
                if (left.Length == 0) return -1;

                _log.Debug(left);
                if (int.TryParse(left, out var val)) return val;
                return -1;
            }
        }

        return -1;
    }
    catch (Exception ex)
    {
        _log.Error(ex, "Error getting orange scrip count");
        return -1;
    }
}
 }
