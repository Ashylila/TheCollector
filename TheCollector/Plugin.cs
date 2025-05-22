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
using TheCollector.CollectableManager;
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

    private const string CommandName = "/pmycommand";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("TheCollector");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);
        
        ServiceWrapper.Init(this);

        ConfigWindow = ServiceWrapper.Get<ConfigWindow>();
        MainWindow = ServiceWrapper.Get<MainWindow>();

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
        _collectableWindowHandler = new();
        Start();
    }

    public void Start()
    {
        Svc.Log.Debug("Plugin Start called.");
    
        var handler = ServiceWrapper.Get<CollectableAutomationHandler>();
        if (handler == null)
        {
            Svc.Log.Error("CollectableAutomationHandler is null!");
            return;
        }

        Svc.Log.Debug("Handler resolved successfully. Starting automation...");
    
        try
        {
            handler.Start();
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"Handler execution threw: {ex}");
        }
    }
    public void Dispose()
    {
        if(ServiceWrapper.ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
        
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleConfigUI();
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
