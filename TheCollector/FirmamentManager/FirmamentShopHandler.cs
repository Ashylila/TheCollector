using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using TheCollector.Data;
using TheCollector.Data.ScripSystem;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.FirmamentManager;

public partial class FirmamentShopHandler : FrameRunnerPipelineBase, IBuyPipeline
{
    public override string Key => "firmament-shop";

    private readonly Configuration _configuration;
    private readonly FirmamentShopWindowHandler _window;
    private readonly FirmamentCatalog _catalog;
    private readonly IClientState _clientState;
    private readonly Lifestream_IPCSubscriber _lifestreamIpc;

    public event Action<Dictionary<uint, int>>? OnFinishedTrading;

    public FirmamentShopHandler(
        PlogonLog log,
        IFramework framework,
        Configuration configuration,
        FirmamentShopWindowHandler window,
        FirmamentCatalog catalog,
        IClientState clientState,
        Lifestream_IPCSubscriber lifestreamIpc,
        StatusService status) : base(log, framework, status)
    {
        _configuration = configuration;
        _window = window;
        _catalog = catalog;
        _clientState = clientState;
        _lifestreamIpc = lifestreamIpc;
    }
}
