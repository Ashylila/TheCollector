using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using OtterGui.Services;
using TheCollector.CollectableManager;
using TheCollector.Data;
using TheCollector.Ipc;
using TheCollector.Utility;
using TheCollector.Windows;

namespace TheCollector;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;
    
    
    private readonly CollectableWindowHandler _collectableWindowHandler;

    private const string CommandName = "/collector";
    public const string InternalName = "The Collector";
    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("TheCollector");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private ChangelogUi ChangelogUi { get; init; }
    private readonly AutomationHandler _automationHandler;
    private readonly PlogonLog _log;

    public static PluginState State { get; set; } = PluginState.Idle;
    public static event Action<bool> OnCollectorStatusChanged;
    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);
        ServiceWrapper.Init(this);
        
        ServiceWrapper.Get<IpcProvider>().Init();
        
        ConfigWindow = ServiceWrapper.Get<ConfigWindow>();
        MainWindow = ServiceWrapper.Get<MainWindow>();
        ChangelogUi = ServiceWrapper.Get<ChangelogUi>();

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ChangelogUi.Changelog);


        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the main UI. \n/collector config - Opens up the config UI\n/collector collect - Starts turning in collectables."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
        _collectableWindowHandler = new();
        _automationHandler = ServiceWrapper.Get<AutomationHandler>();
        _log = ServiceWrapper.Get<PlogonLog>();
        Start();
    }

    public void Start()
    {
        _log.Debug("Start called");
        _automationHandler.Init();
    }
    public void Dispose()
    {
        ECommonsMain.Dispose();
        if(ServiceWrapper.ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
        
    }

    private void OnCommand(string command, string args)
    {
        HandleCommand(args);
    }

    private void HandleCommand(string args)
    {
        switch (args.ToLower())
        {
            case "collect":
                ServiceWrapper.Get<AutomationHandler>().Invoke();
                break;
            case "config":
                ToggleConfigUI();
                break;
            default:
                ToggleMainUI();
                break;
        }
    }
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
