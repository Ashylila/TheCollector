using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using TheCollector.CollectableManager;
using TheCollector.Ipc;
using TheCollector.Utility;
using TheCollector.Windows;
using TheCollector.ScripShopManager;

namespace TheCollector;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IPlayerState PlayerState { get; private set; } = null!;
    
    private const string CommandName = "/collector";
    public const string InternalName = "TheCollector";
    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("TheCollector");
    private MainWindow MainWindow { get; init; }
    private ChangelogUi ChangelogUi { get; init; }
    private StopUi StopUi { get; init; }
    private readonly AutomationHandler _automationHandler;
    private readonly PlogonLog _log;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);
        ServiceWrapper.Init(this);
        
        ServiceWrapper.Get<IpcProvider>();

        MainWindow = ServiceWrapper.Get<MainWindow>();
        ChangelogUi = ServiceWrapper.Get<ChangelogUi>();
        StopUi = ServiceWrapper.Get<StopUi>();

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ChangelogUi.Window);
        WindowSystem.AddWindow(StopUi);


        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the main UI. \n/collector config - Opens up the config UI\n/collector collect - Starts turning in collectables.\n/collector inspect - Runs the Skybuilders' resource inspection.\n/collector changelog - Opens the changelog."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
        _automationHandler = ServiceWrapper.Get<AutomationHandler>();
        _log = ServiceWrapper.Get<PlogonLog>();
        Configuration.Migrate();
        Start();
        
    }

    public void Start()
    {
        _automationHandler.Init();
        ServiceWrapper.Get<ArtisanWatcher>();
        ServiceWrapper.Get<CollectableTurnInWatcher>();
        _ = ServiceWrapper.Get<ScripShopItemManager>();
        var tracker = ServiceWrapper.Get<CharacterBalanceTracker>();
        Svc.Framework.RunOnFrameworkThread(() => tracker.SampleNow());
    }
    public void Dispose()
    {
        // Tear down any in-flight pipeline synchronously — Cancel() waits for a framework
        // tick that will never come once the plugin is gone, leaking the Update handler.
        ServiceWrapper.Get<PipelineRegistry>().AbortAll("Plugin unloading");

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);

        if (ServiceWrapper.ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        VNavmesh_IPCSubscriber.Dispose();
        Autoretainer_IPCSubscriber.Dispose();
        Deliveroo_IPCSubscriber.Dispose();

        // Everything disposed above still relies on ECommons internals, so this goes last.
        ECommonsMain.Dispose();
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
                _automationHandler.Invoke();
                break;
            case "inspect":
                _automationHandler.InvokeInspection();
                break;
            case "config":
                ToggleConfigUI();
                break;
            case "stop":
                _automationHandler.ForceStop("Manually stopped by user");
                break;
            case "buy":
                _automationHandler.InvokeBuy();
                break;
            case "changelog":
                ChangelogUi.Open();
                break;
            default:
                ToggleMainUI();
                break;
        }
    }
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => MainWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
