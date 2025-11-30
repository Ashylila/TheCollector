using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using TheCollector.Data;
using TheCollector.Data.Models;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.CollectableManager;

public partial class CollectableAutomationHandler
{
    private readonly PlogonLog _log;
    private readonly CollectableWindowHandler _collectibleWindowHandler;
    private readonly IDataManager _dataManager;
    private readonly Configuration _configuration;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly GatherbuddyReborn_IPCSubscriber _gatherbuddyService;
    private readonly Lifestream_IPCSubscriber _lifestreamIpc;

    public static readonly string[] FishingCollectables =
{
    "Yellow Peacock Bass",
    "Chain Shark",
    "Cloud Wasp",
    "Hydro Louvar",
    "Copper Shark",
    "Zorgor Condor",
    "Goldgrouper",
    "Iq Rrax Crab",
    "Wivre Cod",
    "Toari Sucker",
    "Moxutural Gar",
    "Glittergill",
    "Mirror Carp",
    "Plattershell",
    "Piraputanga",
    "Purussaurus",
    "Zorlortor",
    "Labyrinthos Tilapia",
    "Red Drum",
    "Basilosaurus",
    "Lunar Deathworm",
    "Mangar",
    "Phallaina",
    "Foun Myhk",
    "Echinos",
    "Banana Eel",
    "Forgeflame",
    "Fleeting Brand",
    "Tebqeyiq Smelt",
    "Kitefin Shark",
    "Xiphactinus",
    "Pantherscale Grouper",
    "Seema Duta",
    "Pipefish",
    "Shogun's Kabuto",
    "Othardian Wrasse",
    "Topminnow",
    "Platinum Guppy",
    "Henodus",
    "Thorned Lizard",
    "Toadhead",
    "Darkdweller",
    "Aapoak",
    "Ondo Harpoon",
    "Pancake Octopus",
    "Winged Hatchetfish",
    "Eryops",
    "Diamond Pipira",
    "Blue Mountain Bubble",
    "Viis Ear",
    "Elder Pixie",
    "Rak'tika Goby",
    "Golden Lobster",
    "Weedy Seadragon",
    "Bothriolepis",
    "Little Bismarck",
    "Albino Caiman",
    "Hak Bitterling",
    "Sculptor",
    "Silken Koi",
    "Deemster",
    "Swordfish",
    "Wraithfish",
    "Ala Mhigan Ribbon",
    "Seraphim",
    "Daio Squid",
    "Samurai Fish",
    "Tao Bitterling",
    "Thousandfang",
    "Fangshi",
    "Soul of the Stallion",
    "Mosasaur",
    "Cherubfish",
    "Eternal Eye",
    "Silken Sunfish",
    "Mitsukuri Shark",
    "Killifish",
    "Yanxian Koi",
    "Butterfly Fish",
    "Velodyna Grass Carp",
    "Amber Salamander",
    "Stupendemys",
    "Barreleye",
    "Loosetongue",
    "Capelin",
    "Tiny Axolotl",
    "Thunderbolt Eel",
    "Vampiric Tapestry",
    "Dravanian Smelt",
    "Moogle Spirit",
    "Illuminati Perch",
    "Weston Bowfin",
    "Noontide Oscar",
    "Warmwater Bichir",
    "Dravanian Squeaker",
    "Bubble Eye",
    "Sorcerer Fish",
    "Whilom Catfish",
    "Icepick",
    "Glacier Core"
};
    public event Action<string>? OnError;
    public event Action<bool>? OnScripsCapped;
    public event System.Action? OnFinishCollecting;

    public bool IsRunning = false;

    internal static CollectableAutomationHandler? Instance { get; private set; }

    public CollectableAutomationHandler(
        PlogonLog log,
        CollectableWindowHandler collectibleWindowHandler,
        IDataManager data,
        Configuration config,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IFramework frameWork,
        IClientState clientState,
        GatherbuddyReborn_IPCSubscriber gatherbuddyService,
        Lifestream_IPCSubscriber lifestreamIpc,
        IPlayerState playerState)
    {
        _log = log;
        _collectibleWindowHandler = collectibleWindowHandler;
        _dataManager = data;
        _configuration = config;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _framework = frameWork;
        _clientState = clientState;
        _gatherbuddyService = gatherbuddyService;
        _lifestreamIpc = lifestreamIpc;
        _player = playerState;
        Instance = this;
    }

    public bool HasCollectible => ItemHelper.GetCurrentInventoryItems().Any(i => i.IsCollectable);

    public void Start()
    {
        if (IsRunning) return;
        if (!HasCollectible)
        {
            _log.Debug("No collectables found in inventory, cancelling");
            IsRunning = false;
            return;
        }
        StartPipeline();
        
    }

    private unsafe void OpenShop()
    {
        var gameObj = _objectTable.FirstOrDefault(a =>
            a.Name.TextValue.Contains("collectable", StringComparison.OrdinalIgnoreCase));

        if (gameObj == null) return;

        VNavmesh_IPCSubscriber.Path_Stop();
        TargetSystem.Instance()->Target = (GameObject*)gameObj.Address;
        TargetSystem.Instance()->OpenObjectInteraction(TargetSystem.Instance()->Target);
    }

    public void ForceStop(string reason)
    {
        _collectibleWindowHandler.CloseWindow();
        StopPipeline();
        VNavmesh_IPCSubscriber.Path_Stop();
        OnError?.Invoke(reason);
        IsRunning = false;
        Plugin.State = PluginState.Idle;
        _log.Error(new Exception("TheCollector has stopped unexpectedly."), reason);
    }

    private static List<Item> GetCollectablesInInventory()
    {
        return ItemHelper.GetLuminaItemsFromInventory()
            .Where(i => i.IsCollectable)
            .OrderBy(i => i.Name.ExtractText())
            .ToList();
    }
}
