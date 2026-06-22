using System;
using System.Linq;
using Dalamud.Plugin.Services;
using TheCollector.Data;
using TheCollector.Data.ScripSystem;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.FirmamentManager;

public partial class FirmamentTurnInHandler : FrameRunnerPipelineBase, ITurnInPipeline
{
    public override string Key => AddonDelays.FirmamentTurnIn;

    private readonly FirmamentTurnInWindowHandler _window;
    private readonly FirmamentCatalog _catalog;
    private readonly Configuration _configuration;
    private readonly ITargetManager _targetManager;
    private readonly IClientState _clientState;
    private readonly IDataManager _dataManager;
    private readonly Lifestream_IPCSubscriber _lifestreamIpc;

    public event Action? OnFinishedTurnIn;
    public event Action<uint, int>? OnScripsEarned;

    event Action ITurnInPipeline.OnFinished
    {
        add => OnFinishedTurnIn += value;
        remove => OnFinishedTurnIn -= value;
    }

    public FirmamentTurnInHandler(
        PlogonLog log,
        FirmamentTurnInWindowHandler window,
        FirmamentCatalog catalog,
        Configuration config,
        ITargetManager targetManager,
        IFramework framework,
        IClientState clientState,
        IDataManager dataManager,
        Lifestream_IPCSubscriber lifestreamIpc,
        StatusService status) : base(log, framework, status)
    {
        _window = window;
        _catalog = catalog;
        _configuration = config;
        _targetManager = targetManager;
        _clientState = clientState;
        _dataManager = dataManager;
        _lifestreamIpc = lifestreamIpc;
    }

    public uint? LastEarnedCurrency { get; private set; }
    public bool CapReached { get; private set; }

    public bool HasCollectible =>
        ItemHelper.GetCurrentInventoryItems()
            .Any(i => i.IsCollectable && _catalog.CrafterJobByItemId.ContainsKey(i.BaseItemId));

    private bool TryInteractWithAnyAppraiser()
        => _catalog.AppraiserDataIds.Any(TryInteractWithNpc);
}
