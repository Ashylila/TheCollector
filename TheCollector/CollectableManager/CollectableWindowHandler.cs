using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Dalamud.Memory;
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
}
