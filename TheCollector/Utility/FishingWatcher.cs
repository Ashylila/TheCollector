using System;
using System.Linq;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using TheCollector.Data;

namespace TheCollector.Utility;

public class FishingWatcher
{
    private readonly IFramework _framework;
    
    private readonly Stopwatch UpdateWatch = new();
    private bool _wasFishing;
    private readonly ICondition _condition;

    public event Action<WatchType>? OnFishingFinished;
    public int PollInterval { get; set; } = 250;
    
    public FishingWatcher(IFramework framework, Configuration config, ICondition condition)
    {
        _framework = framework;
        _condition = condition;
        Init();
    }
    
    private void Init()
    {
        _framework.Update += OnUpdate;
        UpdateWatch.Start();
    }
    private void OnUpdate(IFramework framework)
    {
        if (UpdateWatch.ElapsedMilliseconds < PollInterval)
            return;

        UpdateWatch.Restart();
        if (PlayerHelper.IsInDuty)
            return;

        bool isFishing = _condition[ConditionFlag.Fishing];

        if (_wasFishing && !isFishing)
        {
            OnFishingFinished?.Invoke(WatchType.Fishing);
        }

        _wasFishing = isFishing;
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
    }
}
