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
            ImGui.TextDisabled("Required & Optional Plugins");
            ImGui.Separator();
            ImGui.Spacing();

            DrawPluginStatus("vnavmesh",          "vnavmesh",          required: true);
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X / 2f);
            DrawPluginStatus("GatherBuddyReborn", "GatherBuddyReborn", required: false);

            DrawPluginStatus("Artisan",           "Artisan",           required: false);
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X / 2f);
            DrawPluginStatus("AutoRetainer",      "AutoRetainer",      required: false);

            DrawPluginStatus("Deliveroo",         "Deliveroo",         required: false);
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X / 2f);
            DrawPluginStatus("Lifestream",        "Lifestream",        required: false);
        });
    }

    private static void DrawPluginStatus(string pluginKey, string displayName, bool required)
    {
        bool ready = IPCSubscriber_Common.IsReady(pluginKey);
        var dotColor = ready ? new Vector4(0.20f, 0.85f, 0.20f, 1f) : new Vector4(0.85f, 0.20f, 0.20f, 1f);
        var label = required ? $"{displayName} (required)" : $"{displayName} (optional)";

        ImGui.PushStyleColor(ImGuiCol.Text, dotColor);
        ImGui.TextUnformatted("●");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }

    private void DrawSettingsGeneral()
    {
        ImGui.TextDisabled("Collectable Shop");
        ImGui.Separator();
        ImGui.Spacing();

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
        ImGui.TextDisabled("Automation");
        ImGui.Separator();
        ImGui.Spacing();

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
    }

    private void DrawSettingsIntegrations()
    {
        ImGui.TextDisabled("Artisan");
        ImGui.Separator();
        ImGui.Spacing();

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
        ImGui.TextDisabled("AutoRetainer");
        ImGui.Separator();
        ImGui.Spacing();

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
        ImGui.TextDisabled("Deliveroo");
        ImGui.Separator();
        ImGui.Spacing();

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
    }

    private void DrawSettingsTiming()
    {
        ImGui.TextDisabled("UI Delay");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped("Delay between UI interactions during automation. Lower values run faster but may misbehave on slower machines or with high latency.");
        ImGui.Spacing();

        var delay = configuration.UiDelayMs;
        ImGui.PushItemWidth(160);
        if (ImGui.SliderInt("ms##UiDelay", ref delay, 50, 1500))
        {
            configuration.UiDelayMs = delay;
            configuration.Save();
        }
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (ImGui.Button($"Reset to default ({Configuration.DefaultUiDelayMs} ms)"))
        {
            configuration.UiDelayMs = Configuration.DefaultUiDelayMs;
            configuration.Save();
        }
    }

    private void DrawSettingsGoal()
    {
        ImGui.TextDisabled("Goal Automation");
        ImGui.Separator();
        ImGui.Spacing();

        var stopOnComplete = configuration.Goal.StopGatheringWhenComplete;
        if (ImGui.Checkbox("Stop gathering when purchase list is complete", ref stopOnComplete))
        {
            configuration.Goal.StopGatheringWhenComplete = stopOnComplete;
            configuration.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When enabled, automation will stop instead of\nre-enabling autogather once all items are purchased.");

        ImGui.Spacing();
        ImGui.TextDisabled("Planner");
        ImGui.Separator();
        ImGui.Spacing();

        var hideFish = configuration.Goal.HideFishingCollectables;
        if (ImGui.Checkbox("Hide fishing collectables from planner", ref hideFish))
        {
            configuration.Goal.HideFishingCollectables = hideFish;
            configuration.Save();
        }
    }
}
