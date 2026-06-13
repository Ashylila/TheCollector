using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using TheCollector.Data;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public abstract class FrameRunnerPipelineBase : IPipeline
{
    public abstract string Key { get; }
    protected readonly IFramework Framework;
    protected readonly PlogonLog Log;
    protected readonly StatusService Status;

    protected FrameRunner? Runner;

    public bool IsRunning => Runner?.IsRunning ?? false;

    public event Action<Exception>? OnError;

    protected FrameRunnerPipelineBase(PlogonLog log, IFramework framework, StatusService status)
    {
        Log = log;
        Framework = framework;
        Status = status;
    }

    public void Start()
    {
        if (IsRunning) return;
        EnsureRunner();
        OnStart();
        Runner!.Start(BuildSteps());
    }

    public void Stop(string reason = "Canceled")
    {
        if (!IsRunning) return;
        Runner?.Cancel(reason);
    }

    public void Abort(string reason = "Aborted")
    {
        if (!IsRunning) return;
        Runner?.Abort(reason);
    }

    protected abstract FrameRunner.Step[] BuildSteps();

    protected virtual void OnStart() { }

    protected virtual void OnStepStatus(string name, StepStatus status, string? error) { }

    protected virtual void OnFinished(bool ok) => Status.SetIdle();
    protected virtual void OnCanceledOrFailed(string? error) => Status.SetIdle();

    protected void EnsureRunner()
    {
        Runner ??= new FrameRunner(Framework, new FrameRunnerConfig(
            n => Log.Debug(n),
            (string name, StepStatus status, string? error) =>
            {
                if (status is StepStatus.Failed or StepStatus.Cancel)
                {
                    Status.ReportError($"{Key}/{name}", $"{status}: {error ?? "<no detail>"}");
                    OnCanceledOrFailed(error);
                }

                Log.Debug($"{name} -> {status}{(error is null ? "" : $" ({error})")}");
                OnStepStatus(name, status, error);
            },
            e => OnError?.Invoke(new Exception(e)),
            ok => OnFinished(ok),
            TimeSpan.FromMilliseconds(50)
        ));
    }

    private DateTime _lastMoveIssued;

    protected void ResetMoveThrottle() => _lastMoveIssued = DateTime.MinValue;

    protected StepResult MoveTowardsTick(Vector3 destination, float arriveDistance = 3.5f)
    {
        if (!VNavmesh_IPCSubscriber.IsEnabled)
            return StepResult.Fail("vnavmesh is not installed or not ready.");

        if (Player.DistanceTo(destination) <= arriveDistance)
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            return StepResult.Success();
        }

        if (!VNavmesh_IPCSubscriber.Nav_IsReady())
            return StepResult.Continue();

        if ((DateTime.UtcNow - _lastMoveIssued).TotalMilliseconds >= 200)
        {
            if (!VNavmesh_IPCSubscriber.Path_IsRunning())
                VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destination, false);
            _lastMoveIssued = DateTime.UtcNow;
        }
        return StepResult.Continue();
    }

    protected unsafe bool TryInteractWithNpc(uint baseId)
    {
        var gameObj = Svc.Objects.FirstOrDefault(o => o.BaseId == baseId);
        if (gameObj == null) return false;

        TargetSystem.Instance()->Target = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObj.Address;
        TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);
        return true;
    }

    protected static FrameRunner.Step FireAndSettle(string name, Action act, TimeSpan settle, TimeSpan timeout)
    {
        var fired = false;
        DateTime until = default;
        return new FrameRunner.Step(
            name,
            () =>
            {
                if (!fired)
                {
                    act();
                    until = DateTime.UtcNow + settle;
                    fired = true;
                }
                return DateTime.UtcNow >= until ? StepResult.Success() : StepResult.Continue();
            },
            timeout);
    }
}

public interface IPipeline
{
    string Key { get; }
    bool IsRunning { get; }
    void Start();
    void Stop(string reason = "Canceled");
    void Abort(string reason = "Aborted");
}
public sealed class PipelineRegistry
{
    private readonly Dictionary<string, IPipeline> _pipelines;

    public PipelineRegistry(IEnumerable<IPipeline> pipelines)
        => _pipelines = pipelines.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IPipeline> All => _pipelines.Values;

    public IPipeline Get(string key) => _pipelines[key];

    public void StopAll(string reason = "StopAll")
    {
        foreach (var p in _pipelines.Values)
            if (p.IsRunning) p.Stop(reason);
    }

    public void AbortAll(string reason = "AbortAll")
    {
        foreach (var p in _pipelines.Values)
            if (p.IsRunning) p.Abort(reason);
    }
}
