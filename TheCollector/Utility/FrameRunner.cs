namespace TheCollector.Utility;

using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

public enum StepStatus { Continue, Succeeded, Failed, Cancel }
public readonly struct StepResult
{
    public readonly StepStatus Status;
    public readonly string? Error;

    private StepResult(StepStatus status, string? error)
    {
        Status = status;
        Error = error;
    }
    public static StepResult Cancel(string reason) => new(StepStatus.Cancel, reason);
    public static StepResult Continue(string? message = null) => new(StepStatus.Continue, message);
    public static StepResult Success(string? message = null) => new(StepStatus.Succeeded, message);
    public static StepResult Fail(string error) => new(StepStatus.Failed, error);
}


public sealed class FrameRunner
{
    public readonly struct Step
    {
        public readonly string Name;
        public readonly Action? Begin;
        public readonly Func<StepResult> Tick;
        public readonly TimeSpan Timeout;

        public Step(string name, Func<StepResult> tick, TimeSpan timeout, Action? begin = null)
        {
            Name = name;
            Tick = tick;
            Timeout = timeout;
            Begin = begin;
        }
    }


    private readonly IFramework _fw;
    private readonly Action<string> _onStart;
    private readonly Action<string,StepStatus,string?> _onDone;
    private readonly Action<string> _onError;
    private readonly Action<bool> _onFinished;

    private readonly Queue<Step> _q = new();
    private Step _cur;
    private bool _hasCur;
    private DateTime _started;
    private bool _running;
    private bool _cancel;
    private string? _err;
    private DateTime _cooldownUntil = DateTime.MinValue;
    private TimeSpan _updateDelay;

    public bool IsRunning => _running;

    public FrameRunner(IFramework fw, FrameRunnerConfig configuration)
    { _fw = fw; _onStart = configuration.OnStart; _onDone = configuration.OnDone; _onError = configuration.OnError; _onFinished = configuration.OnFinish; _updateDelay = configuration.UpdateDelay; }

    public void Start(IEnumerable<Step> steps)
    {
        if (_running) return;
        _q.Clear();
        foreach (var s in steps) _q.Enqueue(s);
        _running = true; _cancel = false; _err = null;
        _fw.Update += OnUpdate;
        Next();
    }

    public void Cancel(string reason = "Canceled")
    {
        if (!_running) return;
        _cancel = true;
        _err = reason;
        _onError(reason);
    }

    private void OnUpdate(IFramework _)
    {
        if(DateTime.UtcNow < _cooldownUntil) return;
        _cooldownUntil = DateTime.UtcNow + _updateDelay;
        if (!_running) return;

        if (_cancel)
        {
            Stop(false);
            return;
        }

        if (!_hasCur)
        {
            Stop(true);
            return;
        }

        if (_cur.Timeout > TimeSpan.Zero && DateTime.UtcNow - _started > _cur.Timeout)
        {
            _onDone(_cur.Name, StepStatus.Failed, "Timeout");
            Next();
            return;
        }

        var result = _cur.Tick();

        if (result.Status == StepStatus.Continue) return;

        _onDone(_cur.Name, result.Status, result.Error);
        Next();
    }

    private void Next()
    {
        if (_q.Count == 0) { _hasCur = false; return; }
        _cur = _q.Dequeue();
        _hasCur = true;
        _started = DateTime.UtcNow;
        _onStart(_cur.Name);
        _cur.Begin?.Invoke();
    }

    private void Stop(bool ok)
    {
        _fw.Update -= OnUpdate;
        _q.Clear();
        _hasCur = false;
        _running = false;
        _onFinished(ok && !_cancel);
    }
    public static Step Delay(string name, TimeSpan duration)
    {
        DateTime until = default;
        return new FrameRunner.Step(
            name,
            () => DateTime.UtcNow >= until ? StepResult.Success() : StepResult.Continue(),
            duration + TimeSpan.FromSeconds(2),
            () => until = DateTime.UtcNow + duration
        );
    }
}
public class FrameRunnerConfig
{
    public Action<string> OnStart {get; set;}
    public Action<string, StepStatus, string?> OnDone {get; set;}
    public Action<string> OnError {get; set;}
    public Action<bool> OnFinish {get; set;}
    public TimeSpan UpdateDelay {get; set;}

    public FrameRunnerConfig(Action<string> onstart, Action<string, StepStatus, string?> ondone, Action<string> onerror, Action<bool> onfinish, TimeSpan updateDelay)
    {
        OnStart = onstart;
        OnDone = ondone;
        OnError = onerror;
        OnFinish = onfinish;
        UpdateDelay = updateDelay;
    }
}
