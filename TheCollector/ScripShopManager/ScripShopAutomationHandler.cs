using System;
using System.Collections.Generic;
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
    private readonly ITargetManager _targetManager;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ScripShopWindowHandler _scripShopWindowHandler;
    private readonly IDataManager _data;

    public event Action<Dictionary<uint, int>>? OnFinishedTrading;

    public ScripShopAutomationHandler(
        PlogonLog log,
        ITargetManager targetManager,
        IFramework framework,
        Configuration configuration,
        IObjectTable objectTable,
        ScripShopWindowHandler handler,
        IDataManager data) : base(log, framework)
    {
        _targetManager = targetManager;
        _configuration = configuration;
        _objectTable = objectTable;
        _scripShopWindowHandler = handler;
        _data = data;
    }

}
