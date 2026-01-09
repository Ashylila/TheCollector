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
    public override string Key => "scripshop";
    private readonly PlogonLog _log;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ScripShopWindowHandler _scripShopWindowHandler;


    public event Action? OnFinishedTrading;

    public ScripShopAutomationHandler(
        PlogonLog log,
        ITargetManager targetManager,
        IFramework framework,
        IClientState clientState,
        Configuration configuration,
        IObjectTable objectTable,
        ScripShopWindowHandler handler) : base(log, framework)
    {
        _log = log;
        _targetManager = targetManager;
        _framework = framework;
        _clientState = clientState;
        _configuration = configuration;
        _objectTable = objectTable;
        _scripShopWindowHandler = handler;
    }

}
