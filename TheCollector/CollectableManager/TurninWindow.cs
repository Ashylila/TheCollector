using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;

namespace TheCollector.CollectableManager;

public unsafe class TurninWindow : TreeListWindowBase
{
    public record TurninEntry(int Index, string Label);
    
    public TurninWindow(AtkUnitBase* addon) : base(addon){}

    protected override bool IsTargetNode(AtkResNode* node) => node->Type == (NodeType)1028 && node->NodeId == 28;

    protected override string ExtractLabel(AtkComponentTreeListItem* item)
    {
        var label = item->StringValues[0].Value;
        return SeString.Parse(label).TextValue;
    }

    public int GetItemIndexOf(string label)
    {
        var trimmedLabels = Labels.Where(l => l.Contains("Rarefied", StringComparison.OrdinalIgnoreCase)).ToArray();
        for (var i = 0; i < trimmedLabels.Length; i++)
        {
            Log.Debug($"GetItemIndexOf({label})");
            var current = trimmedLabels[i];
            if (current.Contains(label, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
    
    
    
}
