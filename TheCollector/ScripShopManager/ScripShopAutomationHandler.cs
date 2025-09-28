using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.ScripShopManager;

public partial class ScripShopAutomationHandler
{
    private readonly PlogonLog _log;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ScripShopWindowHandler _scripShopWindowHandler;

    public bool IsRunning { get; private set; } = false;

    internal static ScripShopAutomationHandler? Instance { get; private set; }
    public event Action? OnFinishedTrading;
    public event Action<string>? OnError;

    public ScripShopAutomationHandler(
        PlogonLog log,
        ITargetManager targetManager,
        IFramework framework,
        IClientState clientState,
        Configuration configuration,
        IObjectTable objectTable,
        ScripShopWindowHandler handler)
    {
        _log = log;
        _targetManager = targetManager;
        _framework = framework;
        _clientState = clientState;
        _configuration = configuration;
        _objectTable = objectTable;
        _scripShopWindowHandler = handler;
        Instance = this;
    }

    public unsafe void Start()
    {
        if (IsRunning) return;
        IsRunning = true;

        StartPipeline();
    }

    public void ForceStop(string reason)
    {
        VNavmesh_IPCSubscriber.Path_Stop();
        _scripShopWindowHandler.CloseShop();
        StopPipeline();
        IsRunning = false;
        Plugin.State = PluginState.Idle;
        _log.Error(new Exception(reason), "TheCollector has stopped unexpectedly.");
        OnError?.Invoke(reason);
    }
}
