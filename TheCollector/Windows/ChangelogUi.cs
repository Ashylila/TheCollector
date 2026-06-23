using TheCollector.Data.Models;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class ChangelogUi
{
    public const int LastChangelogVersion = 0;

    public ChangelogBook Book { get; }
    public ChangelogWindow Window { get; }

    private readonly Configuration _config;

    public ChangelogUi(Configuration config, PlogonLog log)
    {
        _config = config;
        Book = new ChangelogBook();

        Add0_28(Book);
        Add0_29(Book);
        Add0_30(Book);
        Add0_31(Book);
        Add0_32(Book);
        Add0_33(Book);
        Add0_34(Book);
        Add0_35(Book);
        Add0_36(Book);
        Add0_37(Book);
        Add0_38(Book);
        Add0_39(Book);
        Add0_40(Book);
        Add_0_4_4(Book);
        Add_0_4_5(Book);
        Add_0_4_6(Book);
        Add_0_4_8(Book);
        Add_0_5_0(Book);
        Add_0_5_1(Book);
        Add_0_5_3(Book);
        Add_0_5_5(Book);
        Add_0_5_8(Book);
        Add_0_6_0(Book);
        Add_0_6_1(Book);
        Add_0_6_2(Book);
        Add_0_7_0(Book);
        Add_0_8_0(Book);
        Add_0_8_1(Book);

        Window = new ChangelogWindow("TheCollector_Changelog", Book, GetConfig, SetConfig);
    }

    public void Open() => Window.Open();

    private (int, ChangeLogDisplayType) GetConfig()
        => (_config.LastSeenVersion, _config.ChangeLogDisplayType);

    private void SetConfig(int version, ChangeLogDisplayType type)
    {
        var dirty = false;
        if (_config.LastSeenVersion != version)
        {
            _config.LastSeenVersion = version;
            dirty = true;
        }
        if (_config.ChangeLogDisplayType != type)
        {
            _config.ChangeLogDisplayType = type;
            dirty = true;
        }
        if (dirty) _config.Save();
    }
    private static void Add_0_8_1(ChangelogBook book) =>
        book.NextVersion("Version 0.8.1")
            .RegisterEntry("Resource-inspection options now only appear on Firmament (where they apply), and the Normal system regains its 'Craft selected Artisan list on autogather finish' option")
            .RegisterEntry("Fixed the scrip shop run wandering off when you weren't in your preferred shop's zone — it now stops cleanly instead of walking toward out-of-zone coordinates");

    private static void Add_0_8_0(ChangelogBook book) =>
        book.NextVersion("Version 0.8.0")
            .RegisterImportant("The Kupo of Fortune and resource-inspection features live under the still-experimental Firmament mode (System toggle → Shift-click 'Firmament'). Please report anything that breaks")
            .RegisterHighlight("Kupo of Fortune automation — after a Firmament turn-in, automatically walk to Lizbeth and play your held cards so Kupo Vouchers earned past the 10-card cap aren't wasted. Configurable play threshold and chest choice (Settings → General)", "mpolonioli")
            .RegisterHighlight("Skybuilders' resource inspection and a full hands-free loop — chain GatherBuddyReborn autogather → resource inspection → Artisan craft → collect, then restart (Settings → Integrations)", "mpolonioli")
            .RegisterEntry("Artisan crafting now triggers correctly inside the Firmament — it was silently inert before", "mpolonioli")
            .RegisterEntry("Added an in-progress status while an Artisan list is crafting", "mpolonioli")
            .RegisterEntry("Fixed a Kupo of Fortune game crash, and Kupo interactions now respect your configured UI delay", "mpolonioli")
            .RegisterEntry("Added a 'stop after N full loops' stop condition, with full-loop counting in the session stats (Planner and Settings → Goal)", "mpolonioli")
            .RegisterEntry("Fixed Firmament appraiser row resolution so the correct collectable is turned in")
            .RegisterEntry("Per-addon UI delays — tune the wait for each game window individually (Settings → Timing)")
            .RegisterEntry("Auto turn-in when you open a Collectables shop or Firmament appraiser window yourself — skips travel and doesn't start buying (Settings → General)")
            .RegisterEntry("Kupo now waits for a real appraiser run before playing, resets the voucher count afterwards, resumes an interrupted turn-in, and skips the voucher pause on manual window turn-ins")
            .RegisterEntry("The changelog now credits outside contributors inline, and you can reopen it any time with /collector changelog or Settings → General → View changelog");

    private static void Add_0_7_0(ChangelogBook book) =>
        book.NextVersion("Version 0.7.0")
            .RegisterHighlight("Experimental Firmament (Skybuilders' Scrip) support — turn in Skybuilders' collectables and buy from the Firmament scrip shop.")
            .RegisterImportant("Firmament mode is experimental — in the System toggle, hold Shift and click 'Firmament' to enable it. Expect rough edges, and please report anything that breaks");

    private static void Add_0_6_2(ChangelogBook book) =>
        book.NextVersion("Version 0.6.2")
            .RegisterHighlight("Added a 'Copy troubleshooting info' button (Settings → General and on the hard-fail banner) — paste it along with your issue report")
            .RegisterHighlight("Purchases and turn-ins are now verified against your actual scrip count, so a missed dialog can no longer record progress that didn't happen")
            .RegisterEntry("Fixed the Deliveroo run crashing right after teleporting (now waits for the navmesh to be ready) and disabling Deliveroo mid-list")
            .RegisterEntry("Fixed the stop button fighting the Artisan inventory-full watcher — stopping now sticks")
            .RegisterEntry("Fixed the Artisan inventory-full pause giving up silently and timing out while the last craft finished")
            .RegisterEntry("Inventory space is now only checked when actually buying, not when starting automation")
            .RegisterEntry("Summoning bells in inns and player housing are recognized again for AutoRetainer")
            .RegisterEntry("Vendor data now loads in the background instead of stalling the game on login")
            .RegisterEntry("Planner tab is much lighter on the framerate");

    private static void Add_0_6_1(ChangelogBook book) =>
        book.NextVersion("Version 0.6.1")
            .RegisterHighlight("Multi-character scrip balance dashboard (Characters tab) — refreshes on login and after each turn-in / buy cycle, so AutoRetainer cycles piggyback samples for every character")
            .RegisterHighlight("Discord webhook notifications on hard-fail, goal complete, stop-condition, and (optional) scrip-cap reached")
            .RegisterHighlight("User-configurable stop conditions: scrips earned, buy cycles, session minutes")
            .RegisterEntry("Planner now credits collectables already in your bags against the estimated turn-ins (overview and per-row table)")
            .RegisterEntry("Planner Session panel shows per-currency scrips earned alongside scrips spent")
            .RegisterEntry("Settings → Goal grows a Stop Conditions section with enable+value rows")
            .RegisterEntry("Refreshed the changelog UI to match the rest of the theme; dropped the OtterGui dependency");

    private static void Add_0_6_0(ChangelogBook book) =>
        book.NextVersion("Version 0.6.0")
            .RegisterHighlight("Refreshed the UI styling across the main, planner, and settings tabs")
            .RegisterHighlight("Split the purchase list into separate Gathering and Crafting tabs")
            .RegisterEntry("Added a hard-fail banner that halts automation when something goes wrong")
            .RegisterEntry("Added a reserve scrip floor so purchases leave a configurable amount unspent");

    private static void Add_0_5_8(ChangelogBook book) =>
        book.NextVersion("Version 0.5.8")
            .RegisterEntry("Updated for API15");

    private static void Add_0_5_5(ChangelogBook book) =>
        book.NextVersion("Version 0.5.5 testing")
            .RegisterHighlight("Added Deliveroo integration — can now check for Deliveroo between runs")
            .RegisterHighlight("Added a Planner tab for setting scrip goals and stopping automation when complete")
            .RegisterEntry("Merged settings into the main window — config window now just opens main window")
            .RegisterEntry("Reorganized the UI into tabs (Main, Planner, Settings)")
            .RegisterEntry("Added collapsible panels and session stats tracking")
            .RegisterEntry("Added handling for equippable items")
            .RegisterEntry("General code cleanup and bug fixes");

    private static void Add_0_5_3(ChangelogBook book) =>
        book.NextVersion("Version 0.5.3")
            .RegisterEntry("Fixed some more bugs");

    private static void Add_0_5_1(ChangelogBook book) =>
        book.NextVersion("Version 0.5.1")
            .RegisterEntry("Fixed some bugs");

    private static void Add_0_5_0(ChangelogBook book) =>
        book.NextVersion("Version 0.5.0")
            .RegisterHighlight("Added AutoRetainer integration");

    private static void Add_0_4_8(ChangelogBook book) =>
        book.NextVersion("Version 0.4.8 testing")
            .RegisterHighlight("Added the option to check for completed ventures between runs via AutoRetainer");

    private static void Add_0_4_6(ChangelogBook book) =>
        book.NextVersion("Version 0.4.6")
            .RegisterEntry("Updated logic for reading scrip amount, fixing various issues");

    private static void Add_0_4_5(ChangelogBook book) =>
        book.NextVersion("Version 0.4.5")
            .RegisterEntry("Bandaid fix for cases where the user has missing scripshop subpages and it cant find the item - now forces through every sub page trying to find the item");

    private static void Add_0_4_4(ChangelogBook book) =>
        book.NextVersion("0.4.4")
            .RegisterEntry("Now matches scripshopitems with ItemIds instead of strings, supporting more languages than english (You might have to re-add your items to purchase for them to show up properly)");

    private static void Add0_40(ChangelogBook book) =>
        book.NextVersion("Version 0.40")
            .RegisterEntry("The plugin will now turn in any remaining collectables after finishing scrip shop purchases")
            .RegisterEntry("Collectable turn-in will now abort early if you're hitting scrip cap again as well");

    private static void Add0_39(ChangelogBook book) =>
        book.NextVersion("Version 0.39")
            .RegisterHighlight("Re-enabled functionality")
            .RegisterHighlight("Merged ArtisanBuddy functionality into TheCollector, adding the ability to craft a selected artisan list after autogather disables")
            .RegisterEntry("Removed the feature to enable auto collectable turn-in after autogather disables");

    private static void Add0_38(ChangelogBook book) =>
        book.NextVersion("Version 0.38")
            .RegisterImportant(
                "With the most recent testing build of GatherBuddyReborn, it has implemented the feature to automatically turn-in collectables and also handle scripshop purchases.\nFor the time being I'm going disable the functionality of this Plugin till the next version, where I will cut out all the gatherable collectable stuff so it'll be crafting only.\nThis should be out in the next couple of days, a big thank you to anyone using the plugin and those who decided to support!♡");

    private static void Add0_37(ChangelogBook book) =>
        book.NextVersion("Version 0.37")
            .RegisterImportant("If you had your shop preferred shop set to Gridania, please select something else and then re-select Gridania for everything to work correctly, thank you!")
            .RegisterEntry("Now properly checks if you can actually teleport when artisan is done crafting a list")
            .RegisterEntry("Fixed a bug where it wouldn't teleport when you're in a housing ward")
            .RegisterEntry("Now moves to the shop instead of teleporting if you're in the same territory and are somewhat nearby");

    private static void Add0_36(ChangelogBook book) =>
        book.NextVersion("Version 0.36")
            .RegisterHighlight("Added failsafe for buying scrip items so it wont buy the wrong shop item anymore if it cant find the selected one in the shop tab")
            .RegisterEntry("Made it fetch the data for the scrip shop items from the git repo instead of locally, allowing for edits without having to actually update the plugin");

    private static void Add0_35(ChangelogBook book) =>
        book.NextVersion("Version 0.35")
            .RegisterHighlight("Added Mason's Abrasive and fixed a few items indices");

    private static void Add0_34(ChangelogBook book) =>
        book.NextVersion("Version 0.34")
            .RegisterImportant(
                "If your crafter or gatherer is high enough level but you haven't unlocked the corresponding Scrip Exchange tab (e.g. \"Purple Scrip Exchange – Lv. 80 Materials/Bait/Tokens\"), the plugin may purchase the wrong item.\nUnlock the relevant Splendors vendor tabs before setting higher-level items.")
            .RegisterEntry("Fixed collectable sorting in your inventory completely now");

    private static void Add0_33(ChangelogBook book) =>
        book.NextVersion("Version 0.33")
            .RegisterEntry(
                "Filtered 'Gazelle Leather' out of the list of collectables in your inventory since Luminas IsCollectable flag returns true for it for some reason???");

    private static void Add0_32(ChangelogBook book) =>
        book.NextVersion("Version 0.32")
            .RegisterEntry("Increased timeout on turning in collectables, which should enable full inventory turn-ins now")
            .RegisterEntry("Fixed bought items not adding up anymore");

    private static void Add0_31(ChangelogBook book) =>
        book.NextVersion("Version 0.31")
            .RegisterHighlight("Added Lifestream integration and with that new CollectableShop locations Solution Nine and Gridania")
            .RegisterEntry("Further improved automation");

    private static void Add0_30(ChangelogBook book) =>
        book.NextVersion("Version 0.30")
            .RegisterHighlight("Added new scripshopitem Levinchrome Aethersand!")
            .RegisterEntry("Fixed scripshopautomation breaking. Sorry!");

    private static void Add0_29(ChangelogBook book) =>
        book.NextVersion("Version 0.29")
            .RegisterHighlight("Refactored the whole automation handling")
            .RegisterHighlight("Added /collector stop command to stop automation as well as a window with a stop button that appears when automation is running")
            .RegisterHighlight("Added new config option to start collecting once you finish fishing")
            .RegisterEntry("Exposed a few functions via EzIPC");

    private static void Add0_28(ChangelogBook book) =>
        book.NextVersion("Version 0.28")
            .RegisterHighlight("Added changelog window!")
            .RegisterEntry("Marked Solution nine teleport as not functional & made it not interactable and also set the Eulmore one as default")
            .RegisterEntry("Fixed a bug where it would fail to buy items if the quantity was set too high")
            .RegisterEntry("Made certain config settings not interactable if the required plugins are not installed");
}
