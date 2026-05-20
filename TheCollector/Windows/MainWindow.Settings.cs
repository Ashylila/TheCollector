using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using TheCollector.CollectableManager;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.Windows;

public partial class MainWindow
{
    private void DrawSettingsTab()
    {
        DrawInstalledPlugins();

        bool vnavReady = IPCSubscriber_Common.IsReady("vnavmesh");
        ImGui.BeginDisabled(!vnavReady);
        if (!vnavReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("vnavmesh plugin is not installed or not ready.");

        if (ImGui.BeginTabBar("##SettingsTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                ImGui.Spacing();
                DrawSettingsGeneral();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Integrations"))
            {
                ImGui.Spacing();
                DrawSettingsIntegrations();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Goal"))
            {
                ImGui.Spacing();
                DrawSettingsGoal();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Timing"))
            {
                ImGui.Spacing();
                DrawSettingsTiming();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.EndDisabled();
    }

    private static void DrawInstalledPlugins()
    {
        ImGuiHelper.Panel("InstalledPlgs", () =>
        {
            ImGuiHelper.SectionHeader("Required & Optional Plugins");

            if (!ImGui.BeginTable("##PluginGrid", 2, ImGuiTableFlags.SizingStretchSame))
                return;

            void Cell(string key, string label, bool required)
            {
                ImGui.TableNextColumn();
                DrawPluginStatus(key, label, required);
            }

            Cell("vnavmesh",          "vnavmesh",          required: true);
            Cell("GatherBuddyReborn", "GatherBuddyReborn", required: false);
            Cell("Artisan",           "Artisan",           required: false);
            Cell("AutoRetainer",      "AutoRetainer",      required: false);
            Cell("Deliveroo",         "Deliveroo",         required: false);
            Cell("Lifestream",        "Lifestream",        required: false);

            ImGui.EndTable();
        });
    }

    private static void DrawPluginStatus(string pluginKey, string displayName, bool required)
    {
        bool ready    = IPCSubscriber_Common.IsReady(pluginKey);
        var dotColor  = ready ? UiTheme.Success : UiTheme.Danger;

        ImGuiHelper.StatusDot(dotColor);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(displayName);
        ImGui.SameLine();
        ImGuiHelper.Chip(required ? "required" : "optional", required ? UiTheme.Accent : UiTheme.TextDim);
    }

    private void DrawSettingsGeneral()
    {
        ImGuiHelper.SectionHeader("Collectable Shop");

        string currentShopName = configuration.PreferredCollectableShop.DisplayName ?? "Select a shop";
        ImGui.PushItemWidth(-1);
        if (ImGui.BeginCombo("##shopselection", currentShopName))
        {
            for (int i = 0; i < CollectableNpcLocations.CollectableShops.Count; i++)
            {
                var shop = CollectableNpcLocations.CollectableShops[i];
                bool lifestreamMissing = shop.IsLifestreamRequired && !IPCSubscriber_Common.IsReady("Lifestream");
                ImGui.BeginDisabled(shop.Disabled || lifestreamMissing);

                string shopLabel = lifestreamMissing
                    ? $"{shop.DisplayName} (Lifestream required)"
                    : shop.DisplayName;

                if (ImGui.Selectable(shopLabel))
                {
                    configuration.PreferredCollectableShop = CollectableNpcLocations.CollectableShops[i];
                    configuration.Save();
                }
                if (lifestreamMissing && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Lifestream plugin is required for this location.");

                ImGui.EndDisabled();
            }

            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Automation");

        var buyAfterEach = configuration.BuyAfterEachCollect;
        if (ImGui.Checkbox("Buy items after each trade instead of on capping scrips", ref buyAfterEach))
        {
            configuration.BuyAfterEachCollect = buyAfterEach;
            configuration.Save();
        }

        var resetOnComplete = configuration.ResetEachQuantityAfterCompletingList;
        if (ImGui.Checkbox("Reset each quantity after completing the list", ref resetOnComplete))
        {
            configuration.ResetEachQuantityAfterCompletingList = resetOnComplete;
            configuration.Save();
        }

        bool gbrReady = IPCSubscriber_Common.IsReady("GatherBuddyReborn");
        ImGui.BeginDisabled(!gbrReady);

        var autogatherOnFinish = configuration.EnableAutogatherOnFinish;
        if (ImGui.Checkbox("Enable Autogather on finish", ref autogatherOnFinish))
        {
            configuration.EnableAutogatherOnFinish = autogatherOnFinish;
            configuration.Save();
        }
        if (!gbrReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("GatherbuddyReborn is not installed or not ready.");

        ImGui.EndDisabled();

        var collectOnFishing = configuration.CollectOnFinishedFishing;
        if (ImGui.Checkbox("Collect on finished fishing", ref collectOnFishing))
        {
            configuration.CollectOnFinishedFishing = collectOnFishing;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Reserve scrips:");
        ImGui.SameLine();
        var reserve = configuration.ReserveScripAmount;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##ReserveScrip", ref reserve, 0, Configuration.ScripCeiling))
        {
            configuration.ReserveScripAmount = Math.Clamp(reserve, 0, Configuration.ScripCeiling);
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Purchases will leave at least this many scrips of each currency unspent.");
    }

    private void DrawSettingsIntegrations()
    {
        ImGuiHelper.SectionHeader("Artisan");

        bool artisanGbrReady = IPCSubscriber_Common.IsReady("GatherBuddyReborn");
        bool artisanReady = IPCSubscriber_Common.IsReady("Artisan");
        bool artisanSectionReady = artisanGbrReady && artisanReady;
        string? artisanDisabledReason = !artisanReady && !artisanGbrReady
            ? "Artisan and GatherbuddyReborn are not installed or not ready."
            : !artisanReady
                ? "Artisan is not installed or not ready."
                : !artisanGbrReady
                    ? "GatherbuddyReborn is not installed or not ready."
                    : null;

        ImGui.BeginDisabled(!artisanSectionReady);

        var craftOnAutogather = configuration.ShouldCraftOnAutogatherChanged;
        if (ImGui.Checkbox("Craft selected Artisan list on autogather finish", ref craftOnAutogather))
        {
            configuration.ShouldCraftOnAutogatherChanged = craftOnAutogather;
            configuration.Save();
        }
        if (artisanDisabledReason != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(artisanDisabledReason);

        ImGui.BeginDisabled(!craftOnAutogather);

        ImGui.TextUnformatted("Artisan List ID:");
        ImGui.SameLine();
        var listId = configuration.ArtisanListId;
        ImGui.PushItemWidth(80);
        if (ImGui.InputInt("##ArtisanListID", ref listId, 0, 0))
        {
            configuration.ArtisanListId = listId;
            configuration.Save();
        }
        ImGui.PopItemWidth();

        var collectOnFinish = configuration.CollectOnFinishCraftingList;
        if (ImGui.Checkbox("Collect on finish crafting an Artisan list", ref collectOnFinish))
        {
            configuration.CollectOnFinishCraftingList = collectOnFinish;
            configuration.Save();
        }

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("AutoRetainer");

        bool arReady = IPCSubscriber_Common.IsReady("AutoRetainer");
        ImGui.BeginDisabled(!arReady);

        var checkVentures = configuration.CheckForVenturesBetweenRuns;
        if (ImGui.Checkbox("Check for available ventures between runs", ref checkVentures))
        {
            configuration.CheckForVenturesBetweenRuns = checkVentures;
            configuration.Save();
        }
        if (!arReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("AutoRetainer is not installed or not ready.");

        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Deliveroo");

        bool deliverooReady = IPCSubscriber_Common.IsReady("Deliveroo");
        bool isMaelstrom = PlayerHelper.GetGrandCompany() == 1;
        bool lifestreamReady = IPCSubscriber_Common.IsReady("Lifestream");
        bool deliverooDisabled = !deliverooReady || (isMaelstrom && !lifestreamReady);
        ImGui.BeginDisabled(deliverooDisabled);

        var checkDeliveroo = configuration.CheckForDeliverooBetweenRuns;
        if (ImGui.Checkbox("Run Deliveroo GC turn-ins between runs", ref checkDeliveroo))
        {
            configuration.CheckForDeliverooBetweenRuns = checkDeliveroo;
            configuration.Save();
        }
        if (deliverooDisabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            if (!deliverooReady)
                ImGui.SetTooltip("Deliveroo is not installed or not ready.");
            else
                ImGui.SetTooltip("Lifestream plugin is required for Maelstrom GC turn-ins.");
        }

        ImGui.EndDisabled();

        ImGui.Spacing();
        DrawSettingsDiscord();
    }

    private void DrawSettingsDiscord()
    {
        ImGuiHelper.SectionHeader("Discord Webhook");

        var enabled = configuration.Discord.Enabled;
        if (ImGui.Checkbox("Send Discord notifications", ref enabled))
        {
            configuration.Discord.Enabled = enabled;
            configuration.Save();
        }

        ImGui.BeginDisabled(!enabled);

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Webhook URL:");
        ImGui.SameLine();
        var url = configuration.Discord.WebhookUrl ?? "";
        ImGui.SetNextItemWidth(-90);
        if (ImGui.InputText("##discordurl", ref url, 256, ImGuiInputTextFlags.Password))
        {
            configuration.Discord.WebhookUrl = url;
            configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Test##discord", new Vector2(80, 0)))
            _ = _discord.TestAsync();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Posts a test message to verify the webhook works.");

        ImGui.Spacing();
        ImGui.TextDisabled("Notify on:");

        var notifyHardFail = configuration.Discord.NotifyOnHardFail;
        if (ImGui.Checkbox("Hard fail", ref notifyHardFail))
        {
            configuration.Discord.NotifyOnHardFail = notifyHardFail;
            configuration.Save();
        }

        var notifyGoal = configuration.Discord.NotifyOnGoalComplete;
        if (ImGui.Checkbox("Goal complete (purchase list done)", ref notifyGoal))
        {
            configuration.Discord.NotifyOnGoalComplete = notifyGoal;
            configuration.Save();
        }

        var notifyStop = configuration.Discord.NotifyOnStopCondition;
        if (ImGui.Checkbox("Stop condition met", ref notifyStop))
        {
            configuration.Discord.NotifyOnStopCondition = notifyStop;
            configuration.Save();
        }

        var notifyCap = configuration.Discord.NotifyOnScripCap;
        if (ImGui.Checkbox("Scrip cap reached", ref notifyCap))
        {
            configuration.Discord.NotifyOnScripCap = notifyCap;
            configuration.Save();
        }

        ImGui.EndDisabled();
    }

    private void DrawSettingsTiming()
    {
        ImGuiHelper.SectionHeader("UI Delay");

        ImGui.TextWrapped("Delay between UI interactions during automation. Lower values run faster but may misbehave on slower machines or with high latency.");
        ImGui.Spacing();

        var delay = configuration.UiDelayMs;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##UiDelay", ref delay, 50, 1500, "%d ms"))
        {
            configuration.UiDelayMs = delay;
            configuration.Save();
        }

        if (ImGui.Button($"Reset to default ({Configuration.DefaultUiDelayMs} ms)"))
        {
            configuration.UiDelayMs = Configuration.DefaultUiDelayMs;
            configuration.Save();
        }
    }

    private void DrawSettingsGoal()
    {
        ImGuiHelper.SectionHeader("Goal Automation");

        var stopOnComplete = configuration.Goal.StopGatheringWhenComplete;
        if (ImGui.Checkbox("Stop gathering when purchase list is complete", ref stopOnComplete))
        {
            configuration.Goal.StopGatheringWhenComplete = stopOnComplete;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When enabled, automation will stop instead of\nre-enabling autogather once all items are purchased.");

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Stop Conditions");
        ImGui.TextWrapped("Evaluated between turn-in and buy cycles. Counters reset when the plugin reloads.");
        ImGui.Spacing();

        {
            var en = configuration.Stop.StopOnScripsEarnedEnabled;
            var v  = configuration.Stop.MaxScripsEarned;
            if (DrawStopConditionRow("Stop after scrips earned", ref en, ref v, 100, 1_000_000, 500,
                "Total scrips estimated earned this session (using HighReward).\nApplies across currencies."))
            {
                configuration.Stop.StopOnScripsEarnedEnabled = en;
                configuration.Stop.MaxScripsEarned = v;
                configuration.Save();
            }
        }
        {
            var en = configuration.Stop.StopOnBuyCyclesEnabled;
            var v  = configuration.Stop.MaxBuyCycles;
            if (DrawStopConditionRow("Stop after buy cycles", ref en, ref v, 1, 1000, 1,
                "Number of times the shop has been visited and purchases completed."))
            {
                configuration.Stop.StopOnBuyCyclesEnabled = en;
                configuration.Stop.MaxBuyCycles = v;
                configuration.Save();
            }
        }
        {
            var en = configuration.Stop.StopOnSessionTimeEnabled;
            var v  = configuration.Stop.MaxSessionMinutes;
            if (DrawStopConditionRow("Stop after session minutes", ref en, ref v, 5, 1440, 5,
                "Real elapsed minutes since the current session started."))
            {
                configuration.Stop.StopOnSessionTimeEnabled = en;
                configuration.Stop.MaxSessionMinutes = v;
                configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGuiHelper.SectionHeader("Planner");

        var hideFish = configuration.Goal.HideFishingCollectables;
        if (ImGui.Checkbox("Hide fishing collectables from planner", ref hideFish))
        {
            configuration.Goal.HideFishingCollectables = hideFish;
            configuration.Save();
        }

        var hideUnobtainable = configuration.Goal.HideUnobtainableCollectables;
        if (ImGui.Checkbox("Hide collectables above your job level", ref hideUnobtainable))
        {
            configuration.Goal.HideUnobtainableCollectables = hideUnobtainable;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filters the planner to only show collectables you can\nactually gather or craft. Also gates the 'best' pick.");
    }

    private static bool DrawStopConditionRow(string label, ref bool enabled, ref int value,
        int min, int max, int step, string tooltip)
    {
        bool changed = false;

        if (ImGui.Checkbox($"##en_{label}", ref enabled))
            changed = true;
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        ImGui.BeginDisabled(!enabled);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt($"##v_{label}", ref value, step, step * 10))
        {
            value = Math.Clamp(value, min, max);
            changed = true;
        }
        ImGui.EndDisabled();

        return changed;
    }
}
