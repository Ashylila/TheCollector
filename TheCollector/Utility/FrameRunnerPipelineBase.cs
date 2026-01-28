using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using TheCollector.Data;

namespace TheCollector.Utility;

public abstract class FrameRunnerPipelineBase : IPipeline
{
    public abstract string Key { get; }
    protected readonly IFramework Framework;
    protected readonly PlogonLog Log;

    protected FrameRunner? Runner;

    public bool IsRunning => Runner?.IsRunning ?? false;

    public event Action<Exception>? OnError;

    protected FrameRunnerPipelineBase(PlogonLog log, IFramework framework)
    {
        Log = log;
        Framework = framework;
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

    protected abstract FrameRunner.Step[] BuildSteps();

    protected virtual void OnStart() { }

    protected virtual void OnStepStatus(string name, StepStatus status, string? error) { }

    protected virtual void OnFinished(bool ok){}
    protected virtual void OnCanceledOrFailed(string? error){}

    protected void EnsureRunner()
    {
        var config = new FrameRunnerConfig(
            n => Log.Debug(n),
            (string name, StepStatus status, string? error) =>
            {
                if (status is StepStatus.Failed or StepStatus.Cancel)
                    OnCanceledOrFailed(error);

                Log.Debug($"{name} -> {status}{(error is null ? "" : $" ({error})")}");
                OnStepStatus(name, status, error);
            },
            e => OnError?.Invoke(new Exception(e)),
            ok => OnFinished(ok),
            TimeSpan.FromMilliseconds(50)
        );
        Runner ??= new FrameRunner(
            Framework,
            config
        );
    }
}

public interface IPipeline
{
    string Key { get; }
    bool IsRunning { get; }
    void Start();
    void Stop(string reason = "Canceled");
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
}
