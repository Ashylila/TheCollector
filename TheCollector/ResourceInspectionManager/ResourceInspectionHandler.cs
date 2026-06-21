using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using TheCollector.Data;
using TheCollector.Data.ScripSystem;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.ResourceInspectionManager;

// Turn-in pipeline for the Skybuilders' Resource Inspection at Flotpassant the Resource
// Inspector. Implements ITurnInPipeline so it slots into AutomationHandler's existing
// turn-in -> (scrip cap) -> buy -> turn-in loop, paired with the Firmament shop buy.
public partial class ResourceInspectionHandler : FrameRunnerPipelineBase, ITurnInPipeline
{
    public override string Key => "resource-inspection";

    private readonly ResourceInspectionWindowHandler _window;
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

    public ResourceInspectionHandler(
        PlogonLog log,
        ResourceInspectionWindowHandler window,
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

    // There's no cheap way to know which gathered items are inspectable without opening the
    // window (the HWDGathererInspection sheet only lists Heavensward-era items). So we always
    // consider it worth a run; the window's per-job "resources available" flag (#76) makes the
    // run a fast no-op when nothing is left, and stops the post-buy resume loop.
    public bool HasCollectible => true;

    // Flotpassant the Resource Inspector, in The Firmament.
    private const uint InspectorBaseId = 1031693;
    private static readonly Vector3 InspectorPosition = new(-21.25586f, -16f, 138.93335f);

    // In-window job tabs (Miner / Botanist / Fisher).
    private static readonly int[] JobTabIndices = { 0, 1, 2 };
}
