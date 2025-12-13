
using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public class CraftingHandler : IDisposable
{

    private readonly TaskManager _taskManager;
    private readonly Configuration _configuration;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyService;
    private readonly IChatGui _chat;
    private readonly IPluginLog _log;
    private readonly IDataManager _data;
    private readonly ICondition _condition;



    public CraftingHandler(
        Configuration configuration,
        IChatGui chat,
        IPluginLog log,
        IDataManager data,
        TaskManager taskManager,
        GatherbuddyReborn_IPCSubscriber gatherBuddyService,
        ICondition condition)
    {
        _taskManager = taskManager;
        _configuration = configuration;
        _chat = chat;
        _log = log;
        _data = data;
        _gatherbuddyService = gatherBuddyService;
        _condition = condition;
    }
    

    public void Dispose()
    {
        _taskManager.Dispose();
    }
    

    public void ShouldStartCrafting()
    {
            _taskManager.Enqueue(StartCrafting);
    }

    private void StartCrafting()
    {

        if (Player.TerritoryIntendedUseEnum == TerritoryIntendedUseEnum.Open_World &&
            Player.Available)
        {
            _taskManager.Enqueue(TeleportToSafeArea);
            _taskManager.EnqueueDelay(7000);
            _taskManager.Enqueue(() => PlayerHelper.CanAct);
            _taskManager.Enqueue(MountCheck);
            _taskManager.Enqueue(Invoke);
        }
        else if (Player.Available)
        {
            _taskManager.Enqueue(Invoke);
        }
        else
        {
            _log.Debug("Player is not available for crafting.");
        }


    }

    private unsafe void MountCheck()
    {
        if (_condition[ConditionFlag.Mounted] || _condition[ConditionFlag.RidingPillion])
        {
            var am = ActionManager.Instance();
            am->UseAction(ActionType.Mount, 0);
        }
    }

    public void Invoke()
    {
        Chat.ExecuteCommand($"/artisan lists {_configuration.ArtisanListId} start");
        _log.Debug("Artisan.Invoke");
    }

    private void TeleportToSafeArea()
    {
        var nearestAetheryte = _data.GetExcelSheet<Aetheryte>()
                                    .FirstOrDefault(a => a.Territory.RowId == Player.Territory.RowId).PlaceName.Value.Name
                                    .ExtractText();
        if (TeleportHelper.TryFindAetheryteByName(nearestAetheryte, out var info, out var aetherName))
        {
            TeleportHelper.Teleport(info.AetheryteId, info.SubIndex);
            _log.Debug($"Teleporting to {aetherName}...");
        }
        else
        {
            _log.Error("Failed to find teleport location.");
        }
    }
}
