using System;
using Dalamud.Plugin.Services;


namespace TheCollector.Utility;

public abstract class FrameRunnerPipelineBase
{
    protected readonly IFramework Framework;
    protected readonly PlogonLog Log;

    protected FrameRunner? Runner;

    public bool IsRunning { get; protected set; }

    public event Action<Exception>? OnError;

    protected FrameRunnerPipelineBase(PlogonLog log, IFramework framework)
    {
        Log = log;
        Framework = framework;
    }

    public void Start()
    {
        if (IsRunning) return;

        IsRunning = true;
        EnsureRunner();
        OnStart();
        Runner!.Start(BuildSteps());
    }

    public void Stop(string reason = "Canceled")
    {
        Runner?.Cancel(reason);
    }

    protected abstract FrameRunner.Step[] BuildSteps();

    protected virtual void OnStart() { }

    protected virtual void OnStepStatus(string name, StepStatus status, string? error) { }

    protected virtual void OnFinished(bool ok)
    {
        IsRunning = false;
    }

    protected virtual void OnCanceledOrFailed(string? error)
    {
        Runner?.Cancel(error ?? "Failed");
    }

    protected void EnsureRunner()
    {
        Runner ??= new FrameRunner(
            Framework,
            n => Log.Debug(n),
            (string name, StepStatus status, string? error) =>
            {
                if (status == StepStatus.Failed)
                    OnCanceledOrFailed(error);

                Log.Debug($"{name} -> {status}{(error is null ? "" : $" ({error})")}");
                OnStepStatus(name, status, error);
            },
            e => OnError?.Invoke(new Exception(e)),
            ok => OnFinished(ok)
        );
    }
}
