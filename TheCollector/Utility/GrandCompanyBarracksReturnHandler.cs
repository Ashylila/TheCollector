using System;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.GameHelpers;
using TheCollector.Data;
using TheCollector.Ipc;
using AddonMaster = ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace TheCollector.Utility;

public sealed unsafe class GrandCompanyBarracksReturnHandler : FrameRunnerPipelineBase
{
    public override string Key => "gc-barracks-return";

    private readonly Lifestream_IPCSubscriber _lifestreamIpc;
    private readonly IClientState _clientState;

    private bool _routeIssued;
    private bool _usedLifestreamForRoute;
    private DateTime _nextInteract;

    public event Action? OnFinishedReturning;

    // Limsa main aetheryte (8) → Aftcastle aethernet shard (41) → Upper Decks (territory 128).
    // Maelstrom HQ is in Upper Decks which has no direct aetheryte, so a Lifestream aethernet
    // task is the only IPC-friendly way to get there.
    // Twin Adder and Immortal Flames have their HQ aetheryte directly in the target territory,
    // so the game's native Telepo is used — locale-independent and requires no Lifestream.
    private const string MaelstromRouteCmd = "debug TaskAetheryteAethernetTeleport 8 41";

    // Old Gridania (133) is where the Gridania aetheryte deposits us; vnavmesh can path
    // seamlessly across to New Gridania (132) where the Adders' Nest actually is.
    private const uint OldGridaniaTerritoryId = 133u;

    private static readonly string[] BarracksEntryKeywords =
    {
        "Adventurer Squadrons",
        "冒険者小隊",
        "冒險者小隊",
        "Abenteurerkommando",
        "escouades d'aventuriers",
    };

    public GrandCompanyBarracksReturnHandler(
        PlogonLog log,
        IFramework framework,
        StatusService status,
        Lifestream_IPCSubscriber lifestreamIpc,
        IClientState clientState)
        : base(log, framework, status)
    {
        _lifestreamIpc = lifestreamIpc;
        _clientState = clientState;
    }

    protected override void OnStart()
    {
        base.OnStart();
        Status.Set(PluginState.ReturningToBarracks);
    }

    protected override void OnFinished(bool ok)
    {
        base.OnFinished(ok);
        if (ok) OnFinishedReturning?.Invoke();
    }

    protected override void OnCanceledOrFailed(string? error)
    {
        base.OnCanceledOrFailed(error);
        VNavmesh_IPCSubscriber.Path_Stop();
    }

    protected override FrameRunner.Step[] BuildSteps()
    {
        if ((uint)Player.GrandCompany == 0)
        {
            return new[]
            {
                new FrameRunner.Step("MissingGrandCompany", () => StepResult.Fail("Cannot return to barracks without joining a Grand Company."), TimeSpan.FromSeconds(1)),
            };
        }

        if (Player.Territory.RowId == GetBarracksTerritoryType((uint)Player.GrandCompany))
        {
            return new[]
            {
                new FrameRunner.Step("AlreadyInBarracks", () => StepResult.Success(), TimeSpan.FromSeconds(1)),
            };
        }

        return new[]
        {
            FrameRunner.Delay("InitialDelay", TimeSpan.FromSeconds(1)),
            new FrameRunner.Step("CanActCheck",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(120)),
            new FrameRunner.Step("RouteToHeadquarters",
                RouteToHeadquartersTick,
                TimeSpan.FromSeconds(120),
                () => { _routeIssued = false; _usedLifestreamForRoute = false; }),
            new FrameRunner.Step("WaitForRoute",
                WaitForRouteTick,
                TimeSpan.FromSeconds(30)),
            new FrameRunner.Step("WaitCanActAfterRoute",
                () => PlayerEx.CanAct ? StepResult.Success() : StepResult.Continue(),
                TimeSpan.FromSeconds(20)),
            FrameRunner.Delay("PostRouteBuffer", TimeSpan.FromSeconds(2)),
            new FrameRunner.Step("MoveToBarracksDoor",
                MoveToBarracksDoorTick,
                TimeSpan.FromSeconds(60),
                ResetMoveThrottle),
            new FrameRunner.Step("InteractWithBarracksDoor",
                InteractWithBarracksDoorTick,
                TimeSpan.FromSeconds(15),
                () => _nextInteract = DateTime.MinValue),
            new FrameRunner.Step("ConfirmBarracksEntry",
                ConfirmBarracksEntryTick,
                TimeSpan.FromSeconds(15)),
            new FrameRunner.Step("WaitForBarracksZone",
                WaitForBarracksZoneTick,
                TimeSpan.FromSeconds(20)),
            FrameRunner.Delay("FinalDelay", TimeSpan.FromSeconds(1)),
        };
    }

    private StepResult RouteToHeadquartersTick()
    {
        var gc = (uint)Player.GrandCompany;
        Status.Set(PluginState.ReturningToBarracks, "to Grand Company HQ");

        if (IsAlreadyInHeadquartersZone(gc))
            return StepResult.Success();

        if (_routeIssued)
            return StepResult.Success();

        if (gc == 1)
        {
            // Maelstrom: Upper Decks has no direct aetheryte — must use Lifestream aethernet task.
            if (!IPCSubscriber_Common.IsReady("Lifestream"))
                return StepResult.Fail("Lifestream is required to reach Maelstrom HQ (Limsa Upper Decks has no direct aetheryte).");
            _lifestreamIpc.ExecuteCommand(MaelstromRouteCmd);
            _usedLifestreamForRoute = true;
        }
        else
        {
            // Twin Adder / Immortal Flames: the HQ aetheryte is directly in the target territory.
            // Use native Telepo — locale-independent, no Lifestream name-lookup needed.
            var hqTerritory = PlayerEx.GetGrandCompanyTerritoryType(gc);
            if (!TeleportHelper.TryFindAetheryteForTerritory(hqTerritory, GetBarracksDoorPosition(gc), out var aetheryte, out _))
                return StepResult.Fail("Could not find an unlocked aetheryte for the Grand Company headquarters.");
            TeleportHelper.Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
            _usedLifestreamForRoute = false;
        }

        _routeIssued = true;
        return StepResult.Success();
    }

    private bool IsAlreadyInHeadquartersZone(uint gc)
    {
        var territory = _clientState.TerritoryType;
        var hq = PlayerEx.GetGrandCompanyTerritoryType(gc);
        // Accept Old Gridania (133) for Twin Adder — vnavmesh can path across to New Gridania (132).
        var inZone = gc == 2
            ? (territory == hq || territory == OldGridaniaTerritoryId)
            : territory == hq;
        return inZone && Player.DistanceTo(GetBarracksDoorPosition(gc)) < 40f;
    }

    private StepResult WaitForRouteTick()
    {
        // Only poll Lifestream IsBusy() when we actually used Lifestream for routing.
        if (_usedLifestreamForRoute && _lifestreamIpc.IsBusy())
            return StepResult.Continue();

        var gc = (uint)Player.GrandCompany;
        var hq = PlayerEx.GetGrandCompanyTerritoryType(gc);
        // Twin Adder: Gridania aetheryte deposits into Old Gridania (133); accept both.
        var arrived = gc == 2
            ? (_clientState.TerritoryType == hq || _clientState.TerritoryType == OldGridaniaTerritoryId)
            : _clientState.TerritoryType == hq;
        return arrived ? StepResult.Success() : StepResult.Continue();
    }

    private StepResult MoveToBarracksDoorTick()
    {
        Status.Set(PluginState.ReturningToBarracks, "to barracks door");
        // Use 1.5f so we get close enough to the door object to interact with it.
        return MoveTowardsTick(GetBarracksDoorPosition((uint)Player.GrandCompany), 1.5f);
    }

    private StepResult InteractWithBarracksDoorTick()
    {
        if (Player.Territory.RowId == GetBarracksTerritoryType((uint)Player.GrandCompany))
            return StepResult.Success();

        // Any dialog open means the door was triggered successfully.
        if (Addons.Ready("SelectYesno") || Addons.Ready("SelectString"))
            return StepResult.Success();

        if (DateTime.UtcNow < _nextInteract)
            return StepResult.Continue();

        VNavmesh_IPCSubscriber.Path_Stop();
        TryInteractWithNearestEObj(4f);
        _nextInteract = DateTime.UtcNow + TimeSpan.FromMilliseconds(700);
        return StepResult.Continue();
    }

    // Handles whatever dialog the door produces:
    //   SelectYesno  → click Yes (direct entry confirmation)
    //   SelectString → find the "Adventurer Squadrons" entry and select it
    private StepResult ConfirmBarracksEntryTick()
    {
        if (Player.Territory.RowId == GetBarracksTerritoryType((uint)Player.GrandCompany))
            return StepResult.Success();

        if (Addons.TryGetReady("SelectYesno", out var yesNoAddon))
        {
            new AddonMaster.SelectYesno(yesNoAddon).Yes();
            return StepResult.Success();
        }

        if (!Addons.TryGetReady("SelectString", out var menuAddon))
            return StepResult.Continue();

        var menu = new AddonMaster.SelectString(menuAddon);
        foreach (var e in menu.Entries)
        {
            if (BarracksEntryKeywords.Any(k => e.Text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                e.Select();
                return StepResult.Success();
            }
        }

        var options = string.Join(" | ", menu.Entries.Select(e => e.Text));
        return StepResult.Fail($"Could not find the Adventurer Squadrons entry. Options: {options}");
    }

    private StepResult WaitForBarracksZoneTick()
    {
        // Keep confirming any lingering dialog while waiting for the zone transition.
        if (Addons.TryGetReady("SelectYesno", out var yesNoAddon))
            new AddonMaster.SelectYesno(yesNoAddon).Yes();

        return Player.Territory.RowId == GetBarracksTerritoryType((uint)Player.GrandCompany) && PlayerEx.CanAct
            ? StepResult.Success()
            : StepResult.Continue();
    }

    private static uint GetBarracksTerritoryType(uint grandCompany) => grandCompany switch
    {
        1 => 536u,
        2 => 534u,
        _ => 535u,
    };


    private static Vector3 GetBarracksDoorPosition(uint grandCompany) => grandCompany switch
    {
        1 => new Vector3(96.70719f, 40.24894f, 63.49613f),
        2 => new Vector3(-79.164246f, -0.50020254f, -7.0414824f),
        _ => new Vector3(-152.0f, 4.0f, -97.0f),
    };
}
