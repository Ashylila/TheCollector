using System;
using System.Linq;
using Dalamud.Plugin.Services;
using TheCollector.Data;
using TheCollector.Utility;

namespace TheCollector.FirmamentManager;

// Plays the Kupo of Fortune scratch-card minigame at Lizbeth in The Firmament to drain
// held cards before they cap (and start wasting vouchers). Firmament-only; the orchestrator
// gates it on Configuration.KupoOfFortuneEnabled and the active scrip system.
public partial class KupoOfFortuneHandler : FrameRunnerPipelineBase
{
    public override string Key => "kupo-of-fortune";

    private readonly KupoOfFortuneWindowHandler _window;
    private readonly FirmamentCatalog _catalog;
    private readonly Configuration _configuration;
    private readonly IClientState _clientState;
    private readonly ITargetManager _targetManager;

    // Raised on successful completion (cards drained or none to play). Failures/timeouts
    // surface through the inherited OnError event instead; the orchestrator listens to both
    // so the post-turn-in flow always resumes.
    public event Action? OnFinishedPlaying;

    public KupoOfFortuneHandler(
        PlogonLog log,
        KupoOfFortuneWindowHandler window,
        FirmamentCatalog catalog,
        Configuration config,
        IClientState clientState,
        ITargetManager targetManager,
        IFramework framework,
        StatusService status) : base(log, framework, status)
    {
        _window = window;
        _catalog = catalog;
        _configuration = config;
        _clientState = clientState;
        _targetManager = targetManager;
    }

    private bool TryInteractWithLizbeth()
        => _catalog.LizbethDataIds.Any(TryInteractWithNpc);

    // Chest index to scratch for the next card, per the configured preference.
    private int PickChestIndex()
        => _configuration.KupoChestPick == KupoChestPick.RandomRight
            ? KupoOfFortuneWindowHandler.RightChestIndices[Random.Shared.Next(KupoOfFortuneWindowHandler.RightChestIndices.Length)]
            : KupoOfFortuneWindowHandler.LeftChestIndex;
}
