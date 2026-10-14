using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.DalamudServices;
using TheCollector.Data;
using TheCollector.Data.Models;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public sealed class CommandHookRunner : FrameRunnerPipelineBase
{
    public override string Key => "command-hooks";

    private static readonly TimeSpan SettleGrace = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromMinutes(3);

    private readonly Configuration _config;
    private readonly Lifestream_IPCSubscriber _lifestream;

    private CommandHookTrigger _trigger;
    private List<CommandHook> _pending = new();
    private Action? _continuation;

    public CommandHookRunner(PlogonLog log, IFramework framework, StatusService status, Configuration config, Lifestream_IPCSubscriber lifestream)
        : base(log, framework, status)
    {
        _config = config;
        _lifestream = lifestream;
    }

    public IEnumerable<CommandHook> HooksFor(CommandHookTrigger trigger)
        => _config.CommandHooks.Where(h => h.Enabled && h.Trigger == trigger && !string.IsNullOrWhiteSpace(h.Command));

    public bool Run(CommandHookTrigger trigger, Action? then = null)
    {
        var hooks = HooksFor(trigger).ToList();
        if (hooks.Count == 0)
        {
            then?.Invoke();
            return false;
        }
        if (IsRunning)
        {
            Log.Error($"Command hooks for {trigger} requested while {_trigger} hooks are still running; skipping the new batch.");
            then?.Invoke();
            return false;
        }
        _trigger = trigger;
        _pending = hooks;
        _continuation = then;
        Start();
        return true;
    }

    protected override void OnStart()
    {
        base.OnStart();
        Status.Set(PluginState.RunningCommandHooks, _trigger.Label());
    }

    protected override FrameRunner.Step[] BuildSteps()
    {
        var steps = new List<FrameRunner.Step>();
        foreach (var hook in _pending)
        {
            var command = hook.Command.Trim();
            steps.Add(new FrameRunner.Step($"Hook:{command}",
                () =>
                {
                    Log.Information($"Command hook ({_trigger}): {command}");
                    Chat.ExecuteCommand(command);
                    return StepResult.Success();
                },
                TimeSpan.FromSeconds(5)));
            if (hook.WaitUntilSettled)
                steps.Add(SettleStep(command));
        }
        return steps.ToArray();
    }

    private FrameRunner.Step SettleStep(string command)
    {
        DateTime earliest = default;
        return new FrameRunner.Step("HookSettle",
            () =>
            {
                if (earliest == default) earliest = DateTime.UtcNow + SettleGrace;
                if (DateTime.UtcNow < earliest) return StepResult.Continue();
                if (IPCSubscriber_Common.IsReady("Lifestream") && _lifestream.IsBusy()) return StepResult.Continue();
                if (Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51]) return StepResult.Continue();
                if (!PlayerEx.CanAct) return StepResult.Continue();
                return StepResult.Success();
            },
            SettleTimeout);
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        var then = _continuation;
        _continuation = null;
        _pending = new();
        if (ok) then?.Invoke();
    }

    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        _continuation = null;
        _pending = new();
    }
}
