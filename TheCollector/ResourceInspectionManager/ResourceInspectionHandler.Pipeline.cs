using System;
using System.Collections.Generic;
using ECommons.GameHelpers;
using TheCollector.Data;
using TheCollector.FirmamentManager;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.ResourceInspectionManager;

public partial class ResourceInspectionHandler
{
    private TimeSpan UiInteractDelay => TimeSpan.FromMilliseconds(_configuration.UiDelayMs);
    // How long to wait for the server to apply an inspection (scrip/inventory change) before
    // concluding the current job has no more eligible materials.
    private static readonly TimeSpan ChangeTimeout = TimeSpan.FromSeconds(5);

    private bool _firmamentRouteIssued;
    private DateTime _nextInteract;
    private int _jobCursor;
    private int _currentSelectedTab;
    private bool _jobMadeProgress;

    protected override FrameRunner.Step[] BuildSteps()
    {
        CapReached = false;
        LastEarnedCurrency = null;
        _jobCursor = 0;
        _currentSelectedTab = -1;
        _jobMadeProgress = true; // don't skip the first job before it has had a cycle
        var territoryId = _catalog.TerritoryId;

        // No cheap pre-check for "do I have inspectable materials" — just go. If there's nothing,
        // the per-job "resources available" flag (#76) makes the run a quick no-op.
        return new[]
        {
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step("CanActCheck",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(120)),
            new FrameRunner.Step("RouteToFirmament",
                () => MakeFirmamentRouteTick(territoryId),
                TimeSpan.FromSeconds(120),
                () => _firmamentRouteIssued = false),
            new FrameRunner.Step("WaitLifestreamDone",
                () => _lifestreamIpc.IsBusy() ? StepResult.Continue() : StepResult.Success(),
                TimeSpan.FromSeconds(30)),
            new FrameRunner.Step("WaitCanActAfterRoute",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(20)),
            FrameRunner.Delay("PostRouteBuffer", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step("MoveToInspector",
                MakeMoveToInspectorTick,
                TimeSpan.FromSeconds(60),
                ResetMoveThrottle),
            new FrameRunner.Step("OpenInspector",
                () =>
                {
                    if (_window.IsReady) return StepResult.Success();
                    if (DateTime.UtcNow >= _nextInteract)
                    {
                        if (!_window.ProgressTalk())
                        {
                            VNavmesh_IPCSubscriber.Path_Stop();
                            TryInteractWithNpc(InspectorBaseId);
                        }
                        _nextInteract = DateTime.UtcNow + TimeSpan.FromMilliseconds(700);
                    }
                    return StepResult.Continue();
                },
                TimeSpan.FromSeconds(15),
                () => _nextInteract = DateTime.MinValue),
            InspectionDriver(),
            FrameRunner.Delay("PostInspectBuffer", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("CloseInspector",
                () => { _window.CloseWindow(); _targetManager.Target = null; return StepResult.Success(); },
                TimeSpan.FromSeconds(5)),
            FrameRunner.Delay("FinalDelay", TimeSpan.FromSeconds(1)),
        };
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        if (ok) OnFinishedTurnIn?.Invoke();
    }

    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        VNavmesh_IPCSubscriber.Path_Stop();
        _window.CloseWindow();
    }

    private bool ScripCapReached()
        => _window.TryGetScripCount(_catalog.ScripItemId, out var cur) && cur >= (uint)_catalog.HoldingCap;

    // Walk the job tabs; for each, repeatedly auto-submit + request inspection until that job
    // stops yielding (no inventory/scrip change), then advance. Stops early if the scrip cap is
    // hit so the orchestrator can run a buy cycle and resume.
    private FrameRunner.Step InspectionDriver() => new(
        "InspectionDriver",
        ctx =>
        {
            Status.Set(PluginState.ExchangingItems);

            if (ScripCapReached())
            {
                CapReached = true;
                Log.Debug("Skybuilders' Scrip cap reached; pausing inspection for a buy cycle.");
                return StepResult.Success();
            }

            // The previous cycle made no progress => the current job is exhausted; move on.
            if (!_jobMadeProgress)
            {
                _jobCursor++;
                _currentSelectedTab = -1;
            }
            _jobMadeProgress = false;

            if (_jobCursor >= JobTabIndices.Length)
                return StepResult.Success();

            var tab = JobTabIndices[_jobCursor];
            var steps = new List<FrameRunner.Step>();
            if (_currentSelectedTab != tab)
            {
                steps.Add(SelectJobStep(tab));
                _currentSelectedTab = tab;
            }
            steps.Add(InspectJobCycleStep());
            steps.Add(InspectionDriver());
            ctx.InjectNext(steps);
            return StepResult.Success();
        },
        TimeSpan.FromSeconds(15));

    private FrameRunner.Step SelectJobStep(int tab)
        => FireAndSettle("SelectInspectionJob", () => _window.SelectJob(tab), UiInteractDelay, TimeSpan.FromSeconds(10));

    private FrameRunner.Step InspectJobCycleStep()
    {
        var phase = 0;
        uint scripBefore = 0;
        DateTime until = default;
        DateTime changeDeadline = default;
        return new FrameRunner.Step("InspectJobCycle",
            () =>
            {
                switch (phase)
                {
                    case 0:
                        // Nothing left for this job -> let the driver advance to the next one.
                        if (_window.TryReadState(out _, out _, out var available) && !available)
                        {
                            _jobMadeProgress = false;
                            return StepResult.Success();
                        }
                        _window.TryGetScripCount(_catalog.ScripItemId, out scripBefore);
                        _window.AutoSubmit();
                        until = DateTime.UtcNow + UiInteractDelay;
                        phase = 1;
                        return StepResult.Continue();

                    case 1:
                        if (DateTime.UtcNow < until) return StepResult.Continue();
                        // Auto-submit queued nothing -> job is effectively done.
                        if (_window.TryReadState(out _, out var queued, out _) && queued <= 0)
                        {
                            _jobMadeProgress = false;
                            return StepResult.Success();
                        }
                        _window.RequestInspection();
                        until = DateTime.UtcNow + UiInteractDelay;
                        changeDeadline = DateTime.UtcNow + ChangeTimeout;
                        phase = 2;
                        return StepResult.Continue();

                    default:
                        // Dismiss any confirmation, then wait for the trade to land.
                        _window.ConfirmYesNo();
                        if (DateTime.UtcNow < until) return StepResult.Continue();

                        var creditedOrCleared = false;
                        if (_window.TryGetScripCount(_catalog.ScripItemId, out var scripNow) && scripNow > scripBefore)
                        {
                            LastEarnedCurrency = _catalog.ScripItemId;
                            OnScripsEarned?.Invoke(_catalog.ScripItemId, (int)(scripNow - scripBefore));
                            creditedOrCleared = true;
                        }
                        // The queue resets to 0 once the inspection is applied.
                        if (_window.TryReadState(out _, out var queuedAfter, out _) && queuedAfter == 0)
                            creditedOrCleared = true;

                        if (creditedOrCleared)
                        {
                            // Submitted a batch; keep going. The next cycle's "resources
                            // available" check terminates the job when it runs dry.
                            _jobMadeProgress = true;
                            return StepResult.Success();
                        }

                        if (DateTime.UtcNow < changeDeadline) return StepResult.Continue();
                        // Assume the batch went through even if we couldn't observe a delta
                        // (e.g. at the scrip cap); let the next cycle re-evaluate.
                        _jobMadeProgress = true;
                        return StepResult.Success();
                }
            },
            TimeSpan.FromSeconds(30));
    }

    private StepResult MakeFirmamentRouteTick(uint territoryId)
        => FirmamentRouting.RouteTick(_clientState, _lifestreamIpc, Status, territoryId, ref _firmamentRouteIssued);

    private StepResult MakeMoveToInspectorTick()
    {
        Status.Set(PluginState.MovingToCollectableVendor, "to the Resource Inspector");
        return MoveTowardsTick(FirmamentRouting.LivePosition(new[] { InspectorBaseId }, InspectorPosition));
    }
}
