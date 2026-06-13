using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.CollectableManager;

public partial class CollectableAutomationHandler : TheCollector.Data.ScripSystem.ITurnInPipeline
{
    event System.Action TheCollector.Data.ScripSystem.ITurnInPipeline.OnFinished
    {
        add => OnFinishCollecting += value;
        remove => OnFinishCollecting -= value;
    }

    bool TheCollector.Data.ScripSystem.ITurnInPipeline.CapReached => ScripCapReached;

    private readonly CollectableWindowHandler _collectibleWindowHandler;
    private readonly IDataManager _dataManager;
    private readonly Configuration _configuration;
    private readonly ITargetManager _targetManager;
    private readonly IClientState _clientState;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyService;
    private readonly Lifestream_IPCSubscriber _lifestreamIpc;
    private readonly VendorCatalog _vendorCatalog;
    public event System.Action? OnFinishCollecting;
    public event Action<uint, int>? OnScripsEarned;



    public CollectableAutomationHandler(
        PlogonLog log,
        CollectableWindowHandler collectibleWindowHandler,
        IDataManager data,
        Configuration config,
        ITargetManager targetManager,
        IFramework frameWork,
        IClientState clientState,
        GatherbuddyReborn_IPCSubscriber gatherbuddyService,
        Lifestream_IPCSubscriber lifestreamIpc,
        VendorCatalog vendorCatalog,
        StatusService status): base(log, frameWork, status)
    {
        _collectibleWindowHandler = collectibleWindowHandler;
        _dataManager = data;
        _configuration = config;
        _targetManager = targetManager;
        _clientState = clientState;
        _gatherbuddyService = gatherbuddyService;
        _lifestreamIpc = lifestreamIpc;
        _vendorCatalog = vendorCatalog;

        Init();
    }

    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);

    private void Init()
    {
        foreach (var row in _dataManager.GetSubrowExcelSheet<CollectablesShopItem>())
        foreach (var sub in row)
            _collectableByItemId[sub.Item.RowId] = sub;
    }

    private void OpenShop()
    {
        var vendor = _vendorCatalog.GetCollectableVendor(_configuration.PreferredTerritoryId);
        if (vendor == null) return;

        VNavmesh_IPCSubscriber.Path_Stop();
        TryInteractWithNpc(vendor.DataId);
    }
}
