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
    private readonly DiscordWebhookService _discord;
    private ScripShopItem? SelectedScripItem = null;

    public MainWindow(Plugin plugin, IDalamudPluginInterface pluginInterface, PlogonLog log,
        ScripPlannerService plannerService, AutomationHandler automationHandler, DiscordWebhookService discord)
        : base("The Collector##CollectorMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 0),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _log = log;
        _plannerService = plannerService;
        _automationHandler = automationHandler;
        _discord = discord;
        configuration = plugin.Configuration;
        this.pluginInterface = pluginInterface;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        UiTheme.Push();
    }

    public override void PostDraw()
    {
        UiTheme.Pop();
    }

    public override void Draw()
    {
        if (ScripShopItemManager.IsLoading)
        {
            ImGui.TextDisabled("Loading items...");
            return;
        }

        DrawHero();
        DrawHardFailBanner();
        DrawStatusRow();

        ImGui.Dummy(new Vector2(0, 4f));

        if (ImGui.BeginTabBar("##MainTabs", ImGuiTabBarFlags.NoTooltip))
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

    private void DrawHero()
    {
        ImGuiHelper.HeroBanner(
            "The Collector",
            "Scrip turn-ins, purchases, and gathering loops",
            DrawSupportButton);
    }

    private void DrawHardFailBanner()
    {
        if (configuration.HardFailReason == null) return;

        ImGuiHelper.Panel("HardFail", () =>
        {
            ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Danger);
            ImGui.TextUnformatted("Automation halted");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextDisabled("•");
            ImGui.SameLine();
            ImGui.TextWrapped(configuration.HardFailReason);
            ImGui.Spacing();
            if (ImGuiHelper.DangerButton("Acknowledge", new Vector2(120, 26)))
                _automationHandler.AcknowledgeHardFail();
        });
    }

    private void DrawStatusRow()
    {
        ImGuiHelper.Panel("StatusBar", () =>
        {
            var (color, label, isActive) = Plugin.State switch
            {
                PluginState.Teleporting               => (UiTheme.Warning, "Teleporting",            true),
                PluginState.MovingToCollectableVendor => (UiTheme.Warning, "Moving to vendor",       true),
                PluginState.ExchangingItems           => (UiTheme.Success, "Exchanging items",       true),
                PluginState.SpendingScrip             => (UiTheme.Success, "Spending scrip",         true),
                PluginState.AutoRetainer              => (UiTheme.Info,    "AutoRetainer running",   true),
                PluginState.Deliveroo                 => (UiTheme.Info,    "Deliveroo running",      true),
                _                                     => (UiTheme.Idle,   "Idle",                   false)
            };

            ImGuiHelper.StatusDot(color, pulse: isActive);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(label);
            ImGui.PopStyleColor();

            if (configuration.ItemsToPurchase.Count > 0)
            {
                int completed = configuration.ItemsToPurchase.Count(i => i.Quantity > 0 && i.AmountPurchased >= i.Quantity);
                int total     = configuration.ItemsToPurchase.Count;
                var chipColor = completed == total ? UiTheme.Success : UiTheme.Accent;

                var summary = $"{completed}/{total} done";
                var chipW   = ImGui.CalcTextSize(summary).X + 16f;
                // Panel wraps content in BeginGroup, so SameLine offsets are biased by WindowPadding.X
                // (the group offset). Subtract it so the chip lands flush with the panel's right edge.
                var rightOffset = ImGui.GetContentRegionMax().X - chipW - ImGui.GetStyle().WindowPadding.X;
                ImGui.SameLine(rightOffset);
                ImGuiHelper.Chip(summary, chipColor);
            }
        });
    }

    private static void DrawSupportButton()
    {
        if (ImGuiHelper.AccentButton("Support Me", new Vector2(110, 28)))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/Ashylila",
                UseShellExecute = true
            });
        }
    }
}
