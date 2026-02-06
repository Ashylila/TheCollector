using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
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

        public uint GetScripCount(uint curType)
    {
        if (GenericHelpers.TryGetAddonByName("CollectablesShop", out AtkUnitBase* addon))
        {
            var cur = CurrencyManager.Instance();

            var curAmount = cur->GetItemIdBySpecialId((byte)curType);
            return cur->GetItemCount(curAmount);
        }
        return uint.MinValue;
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
