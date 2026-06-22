using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;


namespace TheCollector.CollectableManager;

 public unsafe class CollectableWindowHandler
 {
     public bool IsReady => Addons.Ready("CollectablesShop");
     private readonly PlogonLog _log;

     public CollectableWindowHandler(PlogonLog log)
     {
         _log = log;
     }
     public unsafe void SelectJob(uint id)
     {
         if (Addons.TryGetReady("CollectablesShop", out var addon))
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
        if (Addons.TryGetReady("CollectablesShop", out var addon))
        {
            var turnIn = new TurninWindow(addon);
            var index = turnIn.GetItemIndexOf(itemName);
            if (index == -1)
            {
                return false;
            }
            var selectItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 12 },
                new(){Type = ValueType.UInt, UInt = (uint)index}
            };
            addon->FireCallback(2, selectItem);
            _log.Debug(index.ToString());
            return true;
        }
        return false;
    }
    
    public unsafe void SubmitItem()
    {
        if (Addons.TryGetReady("CollectablesShop", out var addon))
        {
            var submitItem = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 15 },
                new(){Type = ValueType.UInt, UInt = 0}
            };
            addon->FireCallback(2, submitItem, true);
        }
    }

    public bool TryGetScripCount(uint curType, out uint count)
    {
        count = 0;
        if (!Addons.TryGet("CollectablesShop", out var addon))
            return false;
        var cur = CurrencyManager.Instance();
        var itemId = cur->GetItemIdBySpecialId((byte)curType);
        count = cur->GetItemCount(itemId);
        return true;
    }

    public unsafe void CloseWindow()
    {
        if (Addons.TryGetReady("CollectablesShop", out var addon))
        {
            addon->Close(true);
        }
    }
 }
