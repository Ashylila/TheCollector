using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using TheCollector.CollectableManager;

namespace TheCollector.CollectableManager;

public unsafe class TurninWindow
{
    public readonly string[] _labels;
    private readonly StdVector<Pointer<AtkComponentTreeListItem>> _items;
    private readonly int _itemCount;
    private readonly AtkUnitBase* _addon;
    private AtkComponentTreeList* _treeList;
    public record TurninEntry(int Index, string Label);

    public Dictionary<LevelRange, List<TurninEntry>> ItemsByLevelRange = new();

    private static readonly Dictionary<string, LevelRange> HeaderMap = new()
    {
        ["91-100"] = LevelRange.NintyOneToHundred,
        ["81-90"] = LevelRange.EightyOneToNinty,
        ["71-80"] = LevelRange.SeventyOneToEighty,
        ["61-70"] = LevelRange.SixtyOneToSeventy,
        ["50-60"] = LevelRange.FiftyToSixty
    };

    public int GetItemIndexOf(string label)
    {
        var trimmedLabels = _labels.Where(l => l.Contains("Rarefied", StringComparison.OrdinalIgnoreCase)).ToArray();
        for (int i = 0; i < trimmedLabels.Length; i++)
        {
            PluginLog.Debug($"GetItemIndexOf({label})");
            var l = trimmedLabels[i];
            if (l.Contains(label, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
    public List<TurninEntry> GetItemsInLevelRange(LevelRange levelRange)
    {
        if (!ItemsByLevelRange.TryGetValue(levelRange, out var indices))
            return new();

        return indices;
    }
    private void GroupItemsByLevelRange()
    {
        LevelRange? currentRange = null;

        for (int i = 0; i < _itemCount; i++)
        {
            var label = _labels[i];
            if (string.IsNullOrWhiteSpace(label))
                continue;

            
            foreach (var kvp in HeaderMap)
            {
                if (label.Contains(kvp.Key))
                {
                    currentRange = kvp.Value;
                    if (!ItemsByLevelRange.ContainsKey(currentRange.Value))
                        ItemsByLevelRange[currentRange.Value] = new List<TurninEntry>();
                    goto Next;
                }
            }

            // If it's a Rarefied item under a level header
            if (currentRange.HasValue && label.Contains("Rarefied"))
            {
                ItemsByLevelRange[currentRange.Value].Add(new TurninEntry(i, label));
            }

            Next: ;
        }
    }
    public TurninWindow(AtkUnitBase* addon)
    {
        Init(addon);
        _addon = addon;
        _itemCount = (int)_treeList->Items.Count;
        _items = _treeList->Items;
        _labels = new string[_itemCount];
        GetLabels();
        GroupItemsByLevelRange();
    }

    private void Init(AtkUnitBase* addon)
    {
        for (int i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node->Type != (NodeType)1028 || node->NodeId != 28) continue;
            var compNode = node->GetAsAtkComponentNode();
            if (compNode == null || compNode->Component == null) continue;

             _treeList = (AtkComponentTreeList*)compNode->Component;
        }
    }
    public int GetIndexOfLevelRange(LevelRange levelRange)
    {
        return levelRange switch
        {
            LevelRange.NintyOneToHundred => Array.FindIndex(_labels, s => s.ToLowerInvariant().Contains("91-100")),
            LevelRange.EightyOneToNinty => Array.FindIndex(_labels, s => s.ToLowerInvariant().Contains("81-90")),
            LevelRange.SeventyOneToEighty => Array.FindIndex(_labels, s => s.ToLowerInvariant().Contains("71-80")),
            LevelRange.SixtyOneToSeventy => Array.FindIndex(_labels, s => s.ToLowerInvariant().Contains("61-70")),
            LevelRange.FiftyToSixty => Array.FindIndex(_labels, s => s.ToLowerInvariant().Contains("50-60")),
            _ => -1
            
        };
    }
    public AtkComponentTreeListItem* GetItemOfLevelRange(LevelRange levelRange)
    {
        var index = GetIndexOfLevelRange(levelRange);
        if (index < 0 || index >= _itemCount)
            return null;

        var ptr = _items[index];
        return ptr != null ? ptr.Value : null;
    }

    private void GetLabels()
    {
        for (int i = 0; i < _itemCount; i++)
        {
            var item = _items[i].Value;
            _labels[i] = item != null ? item->StringValues[0].ToString() : string.Empty;
        }
    }
    
    
}
