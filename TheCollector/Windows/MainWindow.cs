using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TheCollector.CollectableManager;
using TheCollector.Data;
using TheCollector.Data.Models;
using TheCollector.Ipc;
using TheCollector.Utility;

namespace TheCollector.Windows;

public partial class MainWindow : Window, IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private string comboFilter = "";
    private Configuration configuration;

    private readonly PlogonLog _log;
    private readonly ScripPlannerService _plannerService;
    private readonly AutomationHandler _automationHandler;
    private ScripShopItem? SelectedScripItem = null;

    public MainWindow(Plugin plugin, IDalamudPluginInterface pluginInterface, PlogonLog log,
        ScripPlannerService plannerService, AutomationHandler automationHandler)
        : base("The Collector##CollectorMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 0),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _log = log;
        _plannerService = plannerService;
        _automationHandler = automationHandler;
        configuration = plugin.Configuration;
        this.pluginInterface = pluginInterface;
    }

    public void Dispose() { }

    public override void PreDraw() { }

    public override void Draw()
    {
        if (ScripShopItemManager.IsLoading)
        {
            ImGui.TextDisabled("Loading items...");
            return;
        }

        DrawSupportButton();
        DrawStatusBar();

        if (ImGui.BeginTabBar("##MainTabs"))
        {
            if (ImGui.BeginTabItem("Main"))
            {
                ImGui.Spacing();
                DrawMainTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Planner"))
            {
                ImGui.Spacing();
                if (ImGui.BeginChild("##PlannerScroll", new Vector2(0, -1), false))
                {
                    DrawPlannerTab();
                }
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Settings"))
            {
                ImGui.Spacing();
                DrawSettingsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawStatusBar()
    {
        ImGuiHelper.Panel("StatusBar", () =>
        {
            var (color, label) = Plugin.State switch
            {
                PluginState.Teleporting              => (new Vector4(0.95f, 0.75f, 0.10f, 1f), "Teleporting..."),
                PluginState.MovingToCollectableVendor => (new Vector4(0.95f, 0.75f, 0.10f, 1f), "Moving to vendor..."),
                PluginState.ExchangingItems           => (new Vector4(0.30f, 0.85f, 0.30f, 1f), "Exchanging items..."),
                PluginState.SpendingScrip             => (new Vector4(0.30f, 0.85f, 0.30f, 1f), "Spending scrip..."),
                PluginState.AutoRetainer              => (new Vector4(0.40f, 0.65f, 1.00f, 1f), "AutoRetainer running..."),
                PluginState.Deliveroo                => (new Vector4(0.40f, 0.65f, 1.00f, 1f), "Deliveroo running..."),
                _                                     => (new Vector4(0.55f, 0.55f, 0.55f, 1f), "Idle")
            };

            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted($"● {label}");
            ImGui.PopStyleColor();

            if (configuration.ItemsToPurchase.Count > 0)
            {
                int completed = configuration.ItemsToPurchase.Count(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity);
                ImGui.SameLine();
                ImGui.TextDisabled($"   {completed}/{configuration.ItemsToPurchase.Count} items done");
            }

        });
    }

    private static void DrawSupportButton()
    {
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.20f, 0.60f, 0.86f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.30f, 0.70f, 0.96f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.10f, 0.50f, 0.76f, 1.00f));

        float buttonWidth = ImGui.CalcTextSize("Support Me").X + ImGui.GetStyle().FramePadding.X * 2;
        float windowWidth = ImGui.GetWindowContentRegionMax().X;
        ImGui.SetCursorPosX(windowWidth - buttonWidth);
        if (ImGui.Button("Support Me"))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/Ashylila",
                UseShellExecute = true
            });
        }

        ImGui.PopStyleColor(3);
    }
}
