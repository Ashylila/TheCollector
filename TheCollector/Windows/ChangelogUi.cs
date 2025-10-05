using OtterGui.Services;
using OtterGui.Widgets;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class ChangelogUi : IUiService
{
    public const int LastChangelogVersion = 0;
    public readonly Changelog Changelog;
    private readonly Configuration _config;
    private readonly PlogonLog _log;
    public ChangelogUi(Configuration config, PlogonLog log)
    {
        _log = log;
        _config = config;
        Changelog = new Changelog("TheCollector Changelog", ConfigData , Save );
        Add0_28(Changelog);
        Add0_29(Changelog);
        Add0_30(Changelog);
        Add0_31(Changelog);
        Add0_32(Changelog);
        Add0_33(Changelog);
    }

    public static void Add0_33(Changelog log) =>
        log.NextVersion("Version 0.33")
           .RegisterEntry(
               "Filtered 'Gazelle Leather' out of the list of collectables in your inventory since Luminas IsCollectable flag returns true for it for some reason???");
    public static void Add0_32(Changelog log) =>
        log.NextVersion("Version 0.32")
           .RegisterEntry(
               "Increased timeout on turning in collectables, which should enable full inventory turn-ins now")
           .RegisterEntry("Fixed bought items not adding up anymore");
    public static void Add0_31(Changelog log) =>
        log.NextVersion("Version 0.31")
           .RegisterHighlight("Added Lifestream integration and with that new CollectableShop locations Solution Nine and Gridania")
           .RegisterEntry("Further improved automation");
    public static void Add0_30(Changelog log) =>
        log.NextVersion("Version 0.30")
           .RegisterHighlight("Added new scripshopitem Levinchrome Aethersand!")
           .RegisterEntry("Fixed scripshopautomation breaking. Sorry!");
    
    public static void Add0_29(Changelog log) =>
        log.NextVersion("Version 0.29")
           .RegisterHighlight("Refactored the whole automation handling")
           .RegisterHighlight("Added /collector stop command to stop automation as well as a window with a stop button that appears when automation is running")
           .RegisterHighlight("Added new config option to start collecting once you finish fishing")
           .RegisterEntry("Exposed a few functions via EzIPC");
    
    private static void Add0_28(Changelog log)=>
        log.NextVersion("Version 0.28")
           .RegisterHighlight("Added changelog window!")
           .RegisterEntry("Marked Solution nine teleport as not functional & made it not interactable and also set the Eulmore one as default")
           .RegisterEntry("Fixed a bug where it would fail to buy items if the quantity was set too high")
           .RegisterEntry("Made certain config settings not interactable if the required plugins are not installed");
    
    private (int, ChangeLogDisplayType) ConfigData()
        => (_config.LastSeenVersion, _config.ChangeLogDisplayType);

    private void Save(int version, ChangeLogDisplayType type)
    {
        if (_config.LastSeenVersion != version)
        {
            _config.LastSeenVersion = version;
            _config.Save();
        }

        if (_config.ChangeLogDisplayType != type)
        {
            _config.ChangeLogDisplayType = type;
            _config.Save();
        }
    }
}
