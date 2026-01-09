using System;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TheCollector.Data;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public unsafe class AutoRetainerManager : FrameRunnerPipelineBase
{
    public override string Key => "autoretainer";
    private FrameRunner? _runner;
    private string[] AddonsToClose { get; } = ["RetainerList", "SelectYesno", "SelectString", "RetainerTaskAsk"];
    private Configuration _config;
    private IObjectTable _objects;
    public event Action? OnRetainerFinish;

    public AutoRetainerManager(PlogonLog log, IFramework framework, Configuration config, IObjectTable objects)
           : base(log, framework)
    {
        _config = config;
        _objects = objects;
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        OnRetainerFinish?.Invoke();
    }
    protected override void OnStart()
    {
        base.OnStart();
        Plugin.State = PluginState.AutoRetainer;
    }
    protected override FrameRunner.Step[] BuildSteps()
    {
        var destination = _config.PreferredCollectableShop.RetainerBellLoc;

        return new[]
        {
            FrameRunner.Delay("InitDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("MoveToBell", () => MoveToBell(destination), TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("IsNearBellCheck", () => IsNearBell(destination), TimeSpan.FromSeconds(15)),
            new FrameRunner.Step("InteractWithBell", InteractWithBell, TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("EngageRetainer", EngageRetainer, TimeSpan.FromSeconds(1)),
            FrameRunner.Delay("StartDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("WaitAutoRetainerFinish", WaitAutoRetainerFinish, TimeSpan.FromSeconds(15)),
            FrameRunner.Delay("PostDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("CloseAllAddons", CloseAllAddons, TimeSpan.FromSeconds(5)),
        };
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
    private StepResult InteractWithBell()
    {
        var target = _objects.FirstOrDefault(x => x.BaseId == SummoningBellDataIds(Player.Territory.RowId));
        if(target == null) return StepResult.Fail("Could not find SummoningBell GameObject");
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
    internal static uint SummoningBellDataIds(uint territoryType)
    {
        return territoryType switch
        {
            0 => 2000403, //Inn
            1 => 196630, //Apartment
            2 => 196630, //Personal_Home
            3 => 196630, //FC_Estate
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
            _ => 0
        };
    }
}