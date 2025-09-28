﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Data.Models;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.CollectableManager;

public partial class CollectableAutomationHandler
{
    private readonly PlogonLog _log;
    private readonly CollectableWindowHandler _collectibleWindowHandler;
    private readonly IDataManager _dataManager;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyService;
    private List<CollectableShopItem> _collectableShopItems = new();

    public event Action<string>? OnError;
    public event Action<bool>? OnScripsCapped;
    public event System.Action? OnFinishCollecting;

    public bool IsRunning = false;

    internal static CollectableAutomationHandler? Instance { get; private set; }

    public CollectableAutomationHandler(
        PlogonLog log,
        CollectableWindowHandler collectibleWindowHandler,
        IDataManager data,
        Configuration config,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IFramework frameWork,
        IClientState clientState,
        GatherbuddyReborn_IPCSubscriber gatherbuddyService)
    {
        _log = log;
        _collectibleWindowHandler = collectibleWindowHandler;
        _dataManager = data;
        _configuration = config;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _framework = frameWork;
        _clientState = clientState;
        _gatherbuddyService = gatherbuddyService;
        Instance = this;
        LoadItems();
    }

    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);

    public void Start()
    {
        if (IsRunning) return;
        if (!HasCollectible)
        {
            _log.Debug("No collectables found in inventory, cancelling");
            IsRunning = false;
            return;
        }
        StartPipeline();
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

    public void ForceStop(string reason)
    {
        _collectibleWindowHandler.CloseWindow();
        StopPipeline();
        VNavmesh_IPCSubscriber.Path_Stop();
        OnError?.Invoke(reason);
        IsRunning = false;
        Plugin.State = PluginState.Idle;
        _log.Error(new Exception("TheCollector has stopped unexpectedly."), reason);
    }

    private static List<Item> GetCollectablesInInventory()
    {
        return ItemHelper.GetLuminaItemsFromInventory()
            .Where(i => i.IsCollectable)
            .OrderBy(i => i.Name.ExtractText())
            .ToList();
    }
    private void LoadItems()
    {
        var fileName = "CollectableShopItems.json";
        var path = Svc.PluginInterface.AssemblyLocation.DirectoryName;
        var fullPath = Path.Combine(path, fileName);
        try
        {
            var text = File.ReadAllText(fullPath);
            _collectableShopItems = JsonSerializer.Deserialize<List<CollectableShopItem>>(text) ?? new List<CollectableShopItem>();
            _log.Debug($"Loaded {_collectableShopItems.Count} items");
        }
        catch (Exception e)
        {
            _log.Error(e.Message);
        }
    }
}
