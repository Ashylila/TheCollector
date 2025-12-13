namespace TheCollector.Automation;

using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

public enum StepStatus { Continue, Succeeded, Failed }
public readonly struct StepResult
{
    public readonly StepStatus Status;
    public readonly string? Error;

    private StepResult(StepStatus status, string? error)
    {
        Status = status;
        Error = error;
    }

    public static StepResult Continue() => new(StepStatus.Continue, null);
    public static StepResult Success() => new(StepStatus.Succeeded, null);
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

    public bool IsRunning => _running;

    public FrameRunner(IFramework fw, Action<string> s, Action<string,StepStatus,string?> d, Action<string> e, Action<bool> f)
    { _fw = fw; _onStart = s; _onDone = d; _onError = e; _onFinished = f; }

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

        if (result.Status == StepStatus.Continue)
            return;

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
