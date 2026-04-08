using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using TheCollector.Data;
using TheCollector.Ipc;

namespace TheCollector.Utility;

public class DeliverooManager : FrameRunnerPipelineBase
{
    public override string Key => "deliveroo";
    public event Action? OnDeliverooFinish;

    private const uint LimsaRootAetheryteId = 8;
    private const uint AftcastleAethernetId = 41;
    private readonly Lifestream_IPCSubscriber _lifestreamIpc;

    public DeliverooManager(PlogonLog log, IFramework framework, Lifestream_IPCSubscriber lifestreamIpc)
        : base(log, framework)
    {
        _lifestreamIpc = lifestreamIpc;
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        Plugin.State = PluginState.Idle;
        if (ok) OnDeliverooFinish?.Invoke();
    }

    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        Plugin.State = PluginState.Idle;
    }

    protected override void OnStart()
    {
        base.OnStart();
        Plugin.State = PluginState.Deliveroo;
    }

    protected override FrameRunner.Step[] BuildSteps()
    {
        var gc = PlayerHelper.GetGrandCompany();
        var gcTerritory = PlayerHelper.GetGrandCompanyTerritoryType(gc);
        var destination = GetGrandCompanyNpcLocation(gc);
        var needsTeleport = Player.Territory.RowId != gcTerritory;

        var steps = new System.Collections.Generic.List<FrameRunner.Step>
        {
            FrameRunner.Delay("InitDelay", TimeSpan.FromSeconds(1)),
        };

        if (gc == 1) // Maelstrom — Lifestream handles teleport + aethernet to Upper Decks
        {
            steps.Add(new FrameRunner.Step("LifestreamToUpperDecks", () =>
            {
                if (PlayerHelper.GetDistanceToPlayer(destination) < 40f)
                    return StepResult.Success();
                _lifestreamIpc.ExecuteCommand($"debug TaskAetheryteAethernetTeleport {LimsaRootAetheryteId} {AftcastleAethernetId}");
                return StepResult.Success();
            }, TimeSpan.FromSeconds(1)));
            steps.Add(new FrameRunner.Step("WaitForLifestream", () =>
                _lifestreamIpc.IsBusy() ? StepResult.Continue() : StepResult.Success(),
                TimeSpan.FromSeconds(30)));
            steps.Add(FrameRunner.Delay("PostLifestreamDelay", TimeSpan.FromSeconds(2)));
        }
        else if (needsTeleport)
        {
            steps.Add(new FrameRunner.Step("TeleportToGC", () => TeleportToGrandCompany(gcTerritory), TimeSpan.FromSeconds(1)));
            steps.Add(new FrameRunner.Step("WaitForTeleport", () => WaitForTeleport(gcTerritory), TimeSpan.FromSeconds(30)));
            steps.Add(FrameRunner.Delay("PostTeleportDelay", TimeSpan.FromSeconds(3)));
        }

        steps.Add(new FrameRunner.Step("MoveToPersonnelOfficer", () => MoveToNpc(destination), TimeSpan.FromSeconds(1)));
        steps.Add(new FrameRunner.Step("IsNearPersonnelOfficer", () => IsNearNpc(destination), TimeSpan.FromSeconds(20)));
        steps.Add(new FrameRunner.Step("EnableDeliveroo", EnableDeliveroo, TimeSpan.FromSeconds(1)));
        steps.Add(FrameRunner.Delay("EngageDelay", TimeSpan.FromSeconds(2)));
        steps.Add(new FrameRunner.Step("WaitDeliverooFinish", WaitDeliverooFinish, TimeSpan.FromMinutes(10)));
        steps.Add(FrameRunner.Delay("PostDelay", TimeSpan.FromSeconds(1)));

        return steps.ToArray();
    }

    private StepResult TeleportToGrandCompany(uint territoryId)
    {
        Plugin.State = PluginState.Teleporting;

        var terSheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
        var territory = terSheet.GetRow(territoryId);
        var placeName = territory.PlaceName.Value.Name.ExtractText();

        if (TeleportHelper.TryFindAetheryteByName(placeName, out var aetheryte, out _))
        {
            TeleportHelper.Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
        }
        return StepResult.Success();
    }

    private StepResult WaitForTeleport(uint territoryId)
    {
        if (Player.Territory.RowId == territoryId && PlayerHelper.CanAct)
            return StepResult.Success();
        return StepResult.Continue();
    }

    private StepResult MoveToNpc(Vector3 destination)
    {
        Plugin.State = PluginState.Deliveroo;
        VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(destination, false);
        return StepResult.Success();
    }

    private StepResult IsNearNpc(Vector3 loc)
    {
        if (PlayerHelper.GetDistanceToPlayer(loc) < 3f)
        {
            VNavmesh_IPCSubscriber.Path_Stop();
            return StepResult.Success();
        }
        return StepResult.Continue();
    }

    private StepResult EnableDeliveroo()
    {
        Chat.ExecuteCommand("/deliveroo e");
        return StepResult.Success();
    }

    private StepResult WaitDeliverooFinish()
    {
        if (Deliveroo_IPCSubscriber.IsTurnInRunning())
        {
            return StepResult.Continue();
        }
        Chat.ExecuteCommand("/deliveroo d");
        return StepResult.Success();
    }

    internal static Vector3 GetGrandCompanyNpcLocation(uint grandCompany)
    {
        return grandCompany switch
        {
            1 => new Vector3(93f, 40f, 75f),    // Maelstrom - Limsa Lominsa Upper Decks
            2 => new Vector3(-68f, -0.5f, -8f),  // Twin Adder - New Gridania
            _ => new Vector3(-141f, 4f, -106f),  // Immortal Flames - Ul'dah Steps of Nald
        };
    }
}
