using System;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Data;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public unsafe class AutoRetainerManager : FrameRunnerPipelineBase
{
    public override string Key => "autoretainer";
    private string[] AddonsToClose { get; } = ["RetainerList", "SelectYesno", "SelectString", "RetainerTaskAsk"];
    private Configuration _config;
    private IObjectTable _objects;
    public event Action? OnRetainerFinish;

    public AutoRetainerManager(PlogonLog log, IFramework framework, StatusService status, Configuration config, IObjectTable objects)
           : base(log, framework, status)
    {
        _config = config;
        _objects = objects;
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        if (ok) OnRetainerFinish?.Invoke();
    }
    protected override void OnStart()
    {
        base.OnStart();
        Status.Set(PluginState.AutoRetainer);
    }
    protected override FrameRunner.Step[] BuildSteps()
    {

        if (SummoningBellDataIds(Player.Territory.RowId) == uint.MaxValue)
        {
            Log.Debug($"No summoning bell mapped for territory {Player.Territory.RowId}; skipping AutoRetainer.");
            return new[]
            {
                new FrameRunner.Step("SkipAutoRetainer", () => StepResult.Success(), TimeSpan.FromSeconds(1)),
            };
        }

        return new[]
        {
            FrameRunner.Delay("InitDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("MoveToBell", MoveToBellTick, TimeSpan.FromSeconds(60), ResetMoveThrottle),
            new FrameRunner.Step("InteractWithBell", InteractWithBell, TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("EngageRetainer", EngageRetainer, TimeSpan.FromSeconds(1)),
            FrameRunner.Delay("StartDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("WaitAutoRetainerFinish", WaitAutoRetainerFinish, TimeSpan.FromMinutes(10)),
            FrameRunner.Delay("PostDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("CloseAllAddons", CloseAllAddons, TimeSpan.FromSeconds(5)),
        };
    }

    // Bell location used to be hand-coded per shop in Configuration. Now we
    // discover the nearest summoning bell at runtime via the DataId table
    // below — same identity used for InteractWithBell, so the move always
    // ends up next to the bell we're about to ring.
    private Vector3? GetNearestBellPosition()
    {
        var bellId = SummoningBellDataIds(Player.Territory.RowId);
        if (bellId == uint.MaxValue) return null;
        var bell = _objects
            .Where(o => o.BaseId == bellId)
            .OrderBy(o => Vector3.DistanceSquared(Player.Position, o.Position))
            .FirstOrDefault();
        return bell?.Position;
    }

    private StepResult MoveToBellTick()
    {
        // The object table can still be populating right after a zone change;
        // keep waiting for the bell to appear and let the step timeout backstop it.
        if (GetNearestBellPosition() is not { } dest)
            return StepResult.Continue();
        return MoveTowardsTick(dest, 1f);
    }
    private StepResult InteractWithBell()
    {
        var bellId = SummoningBellDataIds(Player.Territory.RowId);
        if (bellId == uint.MaxValue) return StepResult.Fail($"Unsupported territory for summoning bell: {Player.Territory.RowId}");

        var target = _objects
            .Where(o => o.BaseId == bellId)
            .OrderBy(o => Vector3.DistanceSquared(Player.Position, o.Position))
            .FirstOrDefault();
        if (target == null) return StepResult.Fail("Could not find SummoningBell GameObject");
        TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address, false);
        return StepResult.Success();
    }
    private StepResult EngageRetainer()
    {
        Chat.ExecuteCommand("/autoretainer e");
        return StepResult.Success();
    }
    private StepResult WaitAutoRetainerFinish()
    {
        if (Autoretainer_IPCSubscriber.IsBusy())
        {
            return StepResult.Continue();
        }
        Chat.ExecuteCommand("/autoretainer d");
        return StepResult.Success();
    }

    private StepResult CloseAllAddons()
    {
        for (int i = 0; i < AddonsToClose.Length; i++)
        {
            if (GenericHelpers.TryGetAddonByName(AddonsToClose[i], out AtkUnitBase* atkUnitBase) && atkUnitBase->IsReady())
            {
                Log.Debug("Closing Addon " + AddonsToClose[i]);
                atkUnitBase->FireCallbackInt(-1);
                return StepResult.Continue();
            }
        }
        return StepResult.Success();
    }
    // Callers always pass the *current* territory; the intended-use fallback below
    // reads the current territory too, so the two stay consistent.
    internal static uint SummoningBellDataIds(uint territoryType)
    {
        uint bellId = territoryType switch
        {
            129 => 2000401, //Limsa_Lominsa_Lower_Decks
            133 => 2000401, //Old_Gridania
            131 => 2000401, //Uldah_Steps_of_Thal
            419 => 2000401, //The_Pillars
            635 => 2000441, //Rhalgrs_Reach
            628 => 2000441, //Kugane
            759 => 2006565, //The_Doman_Enclave
            819 => 2010284, //The_Crystarium
            820 => 2010284, //Eulmore
            962 => 2000441, //Old_Sharlayan
            963 => 2000441, //Radz_at_Han
            1185 => 2000441, //Tuliyollal
            1186 => 2000441, //Nexus_Arcade
            _ => uint.MaxValue
        };
        if (bellId != uint.MaxValue) return bellId;

        return Player.TerritoryIntendedUseEnum switch
        {
            TerritoryIntendedUseEnum.Inn => 2000403,
            TerritoryIntendedUseEnum.Housing_Instances or TerritoryIntendedUseEnum.Residential_Area => 196630,
            _ => uint.MaxValue,
        };
    }
}