using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.CollectableManager;

public partial class CollectableAutomationHandler
{
    private readonly CollectableWindowHandler _collectibleWindowHandler;
    private readonly IDataManager _dataManager;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IClientState _clientState;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyService;
    private readonly Lifestream_IPCSubscriber _lifestreamIpc;
    public event Action<bool>? OnScripsCapped;
    public event System.Action? OnFinishCollecting;



    public CollectableAutomationHandler(
        PlogonLog log,
        CollectableWindowHandler collectibleWindowHandler,
        IDataManager data,
        Configuration config,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IFramework frameWork,
        IClientState clientState,
        GatherbuddyReborn_IPCSubscriber gatherbuddyService,
        Lifestream_IPCSubscriber lifestreamIpc,
        IPlayerState playerState): base(log, frameWork)
    {
        _collectibleWindowHandler = collectibleWindowHandler;
        _dataManager = data;
        _configuration = config;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _clientState = clientState;
        _gatherbuddyService = gatherbuddyService;
        _lifestreamIpc = lifestreamIpc;
        _player = playerState;
        
        Init();
    }

    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);

    private void Init()
    {
        foreach (var row in _dataManager.GetSubrowExcelSheet<CollectablesShopItem>())
        foreach (var sub in row)
            _collectableByItemId[sub.Item.RowId] = sub;
    }

    private unsafe void OpenShop()
    {
        var gameObj = _objectTable.FirstOrDefault(a =>
            a.Name.TextValue.Contains("collectable", StringComparison.OrdinalIgnoreCase));

        if (gameObj == null) return;

        VNavmesh_IPCSubscriber.Path_Stop();
        TargetSystem.Instance()->Target = (GameObject*)gameObj.Address;
        TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);
    }

    private static List<Item> GetCollectablesInInventory()
    {
        return ItemHelper.GetLuminaItemsFromInventory()
            .Where(i => i.IsCollectable)
            .OrderBy(i => i.Name.ExtractText())
            .ToList();
    }
}
