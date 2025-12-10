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
        Add0_34(Changelog);
        Add0_35(Changelog);
        Add0_36(Changelog);
        Add0_37(Changelog);
        Add0_38(Changelog);
        
    }

    public static void Add0_38(Changelog log) =>
        log.NextVersion("Version 0.38")
           .RegisterImportant(
               "With the most recent testing build of GatherBuddyReborn, it has implemented the feature to automatically turn-in collectables and also handle scripshop purchases.\n For the time being I'm going disable the functionality of this Plugin till the next version, where I will cut out all the gatherable collectable stuff so it'll be crafting only.\n This should be out in the next couple of days, a big thank you to anyone using the plugin and those who decided to support!♡");

    public static void Add0_37(Changelog log) =>
        log.NextVersion("Version 0.37")
           .RegisterImportant("If you had your shop preferred shop set to Gridania, please select something else and then re-select Gridania for everything to work correctly, thank you!")
           .RegisterEntry("Now properly checks if you can actually teleport when artisan is done crafting a list")
           .RegisterEntry("Fixed a bug where it wouldn't teleport when you're in a housing ward")
           .RegisterEntry(
               "Now moves to the shop instead of teleporting if you're in the same territory and are somewhat nearby");
    public static void Add0_36(Changelog log) =>
        log.NextVersion("Version 0.36")
           .RegisterHighlight("Added failsafe for buying scrip items so it wont buy the wrong shop item anymore if it cant find the selected one in the shop tab")
           .RegisterEntry("Made it fetch the data for the scrip shop items from the git repo instead of locally, allowing for edits without having to actually update the plugin");
    public static void Add0_35(Changelog log) =>
        log.NextVersion("Version 0.35")
           .RegisterHighlight("Added Mason's Abrasive and fixed a few items indices");
    public static void Add0_34(Changelog log) =>
        log.NextVersion("Version 0.34")
           .RegisterHighlight(
               "!!!IMPORTANT!!! If your crafter or gatherer is high enough level but you haven’t unlocked the corresponding Scrip Exchange tab (e.g. “Purple Scrip Exchange – Lv. 80 Materials/Bait/Tokens”), the plugin may purchase the wrong item.\nUnlock the relevant Splendors vendor tabs before setting higher-level items.")
           .RegisterEntry("Fixed collectable sorting in your inventory completely now");
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
