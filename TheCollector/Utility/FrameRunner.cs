namespace TheCollector.Utility;

using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Serilog;

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

public readonly struct StepContext
{
    private readonly FrameRunner _runner;
    internal StepContext(FrameRunner runner) => _runner = runner;

    public void Enqueue(FrameRunner.Step step) => _runner.Enqueue(step);
    public void Enqueue(IEnumerable<FrameRunner.Step> steps) => _runner.Enqueue(steps);
    public void InjectNext(params FrameRunner.Step[] steps) => _runner.InjectNext(steps);
    public void InjectNext(IEnumerable<FrameRunner.Step> steps) => _runner.InjectNext(steps);
}

public sealed class FrameRunner
{
    public readonly struct Step
    {
        public readonly string Name;
        public readonly Action? Begin;
        public readonly Func<StepContext, StepResult> Tick;
        public readonly TimeSpan Timeout;

        public Step(string name, Func<StepContext, StepResult> tick, TimeSpan timeout, Action? begin = null)
        {
            Name = name;
            Tick = tick;
            Timeout = timeout;
            Begin = begin;
        }

        public Step(string name, Func<StepResult> tick, TimeSpan timeout, Action? begin = null)
            : this(name, _ => tick(), timeout, begin) { }
    }


    private readonly IFramework _fw;
    private readonly Action<string> _onStart;
    private readonly Action<string,StepStatus,string?> _onDone;
    private readonly Action<string> _onError;
    private readonly Action<bool> _onFinished;

    private readonly LinkedList<Step> _q = new();
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
        foreach (var s in steps) _q.AddLast(s);
        _running = true; _cancel = false; _err = null;
        _fw.Update += OnUpdate;
        Next();
    }

    public void Cancel(string reason = "Canceled")
    {
        if (!_running) return;
        _cancel = true;
        _err = reason;
    }

    public void Abort(string reason = "Aborted")
    {
        if (!_running) return;
        _onDone(_hasCur ? _cur.Name : "Aborted", StepStatus.Cancel, reason);
        Stop(false);
    }

    public void Enqueue(Step step) => _q.AddLast(step);

    public void Enqueue(IEnumerable<Step> steps)
    {
        foreach (var s in steps) _q.AddLast(s);
    }

    public void InjectNext(params Step[] steps) => InjectNext((IEnumerable<Step>)steps);

    public void InjectNext(IEnumerable<Step> steps)
    {
        LinkedListNode<Step>? anchor = null;
        foreach (var s in steps)
            anchor = anchor is null ? _q.AddFirst(s) : _q.AddAfter(anchor, s);
    }

    private void OnUpdate(IFramework _)
    {
        if(DateTime.UtcNow < _cooldownUntil) return;
        _cooldownUntil = DateTime.UtcNow + _updateDelay;
        if (!_running) return;

        if (_cancel)
        {
            _onDone(_hasCur ? _cur.Name : "Canceled", StepStatus.Cancel, _err);
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
            _onError($"{_cur.Name} timed out");
            Stop(false);
            return;
        }

        StepResult result;
        try
        {
            result = _cur.Tick(new StepContext(this));
        }
        catch (Exception ex)
        {
            var msg = Describe(ex);
            _onDone(_cur.Name, StepStatus.Failed, msg);
            _onError($"Unhandled exception in step {_cur.Name}: {msg}");
            Stop(false);
            return;
        }

        if (result.Status == StepStatus.Continue) return;
        _onDone(_cur.Name, result.Status, result.Error);
        if (result.Status == StepStatus.Failed)
        {
            _onError(result.Error ?? "Failed");
            Stop(false);
            return;
        }
        if (result.Status == StepStatus.Cancel)
        {
            Stop(false);
            return;
        }

        Next();
    }

    private void Next()
    {
        if (_q.Count == 0) { _hasCur = false; return; }
        _cur = _q.First!.Value;
        _q.RemoveFirst();
        _hasCur = true;
        _started = DateTime.UtcNow;
        _onStart(_cur.Name);
        try
        {
            _cur.Begin?.Invoke();
        }
        catch (Exception ex)
        {
            var msg = Describe(ex);
            _onDone(_cur.Name, StepStatus.Failed, msg);
            _onError($"Unhandled exception starting step {_cur.Name}: {msg}");
            Stop(false);
        }
    }

    private static string Describe(Exception ex)
    {
        while (ex is System.Reflection.TargetInvocationException { InnerException: not null } tie)
            ex = tie.InnerException;
        return ex.Message;
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
