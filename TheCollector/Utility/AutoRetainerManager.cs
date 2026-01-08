using System;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Utility;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public unsafe class AutoRetainerManager
{
    private readonly PlogonLog _log;
    private FrameRunner? _runner;
    private readonly IFramework _framework;
    private string[] AddonsToClose { get; } = ["RetainerList", "SelectYesno", "SelectString", "RetainerTaskAsk"];
    private DateTime _cooldownUntil = DateTime.MinValue;
    private TimeSpan _updateDelay = TimeSpan.FromMilliseconds(250);
    private Configuration _config;
    public event Action<string>? OnError;
    public event Action? OnRetainerFinish;
    public bool IsRunning { get; private set; }

    public AutoRetainerManager(PlogonLog log, IFramework framework, Configuration config)
    {
        _log = log;
        _framework = framework;
        _config = config;
    }

    public void Start()
    {
        _runner ??= new FrameRunner(_framework,
            n => _log.Debug(n),
            (string name, StepStatus status, string? error) =>
            {
                if (StepStatus.Failed == status)
                {
                    _runner?.Cancel(error);
                }
                _log.Debug($"{name} -> {status}{(error is null ? "" : $" ({error})")}");
            },
            e => OnError?.Invoke(e),
            ok =>
            {
                IsRunning = false;
                if (ok) OnRetainerFinish.Invoke();
            });

        var destination = _config.PreferredCollectableShop.RetainerBellLoc;
        var steps = new[]
        {
                FrameRunner.Delay("InitDelay", TimeSpan.FromSeconds(1)),
                new FrameRunner.Step
                (
                    "MoveToBell",
                    () => MoveToBell(destination),
                    TimeSpan.FromSeconds(1)
                ),
                new FrameRunner.Step(
                    "IsNearBellCheck",
                    () => IsNearBell(destination),
                    TimeSpan.FromSeconds(15)
                ),
                new FrameRunner.Step(
                    "EngageRetainer",
                    EngageRetainer,
                    TimeSpan.FromSeconds(1)
                ),
                new FrameRunner.Step(
                    "WaitAutoRetainerFinish",
                    WaitAutoRetainerFinish,
                    TimeSpan.FromSeconds(15)
                ),
                new FrameRunner.Step(
                    "CloseAllAddons",
                    CloseAllAddons,
                    TimeSpan.FromSeconds(5)
                )
            };
            _runner.Start(steps);
    }
    private StepResult MoveToBell(Vector3 destination)
    {
        VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destination, false);
        return StepResult.Success();
    }

    private StepResult IsNearBell(Vector3 loc)
    {
        if (PlayerHelper.GetDistanceToPlayer(loc) < 1f)
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            return StepResult.Success();
        }
        return StepResult.Continue();
    }

    private StepResult EngageRetainer()
    {
        Chat.ExecuteCommand("/autoretainer e");
        return StepResult.Success();
    }
    private StepResult WaitAutoRetainerFinish()
    {
        if (DateTime.UtcNow < _cooldownUntil) return StepResult.Continue();
        if (Autoretainer_IPCSubscriber.IsBusy())
        {
            _cooldownUntil = DateTime.UtcNow + _updateDelay;
            return StepResult.Continue();
        }
        return StepResult.Success();
    }

    private StepResult CloseAllAddons()
    {
        for (int i = 0; i < AddonsToClose.Length; i++)
        {
            if (GenericHelpers.TryGetAddonByName(AddonsToClose[i], out AtkUnitBase* atkUnitBase) && atkUnitBase->IsReady())
            {
                _log.Debug("Closing Addon " + AddonsToClose[i]);
                atkUnitBase->FireCallbackInt(-1);
                return StepResult.Continue();
            }
        }
        return StepResult.Success();
    }
}