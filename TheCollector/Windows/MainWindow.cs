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
using TheCollector.Data.ScripSystem;
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
    private readonly FirmamentCatalog _firmamentCatalog;
    private readonly AutomationHandler _automationHandler;
    private readonly DiscordWebhookService _discord;
    private readonly StatusService _status;
    private ScripShopItem? SelectedScripItem = null;

    public MainWindow(Plugin plugin, IDalamudPluginInterface pluginInterface, PlogonLog log,
        ScripPlannerService plannerService,
        FirmamentCatalog firmamentCatalog,
        AutomationHandler automationHandler, DiscordWebhookService discord,
        CharacterBalanceTracker balanceTracker, StatusService status)
        : base("The Collector##CollectorMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 0),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _log = log;
        _plannerService = plannerService;
        _firmamentCatalog = firmamentCatalog;
        _automationHandler = automationHandler;
        _discord = discord;
        _balanceTracker = balanceTracker;
        _status = status;
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
        DrawHero();
        DrawHardFailBanner();
        DrawStatusRow();
        DrawSystemToggle();

        ImGui.Dummy(new Vector2(0, 4f));

        if (ImGui.BeginTabBar("##MainTabs", ImGuiTabBarFlags.NoTooltip))
        {
            if (ImGui.BeginTabItem("Main"))
            {
                ImGui.Spacing();
                DrawMainTab();
                ImGui.EndTabItem();
            }

            // The planner is Normal-system only; it has no meaningful equivalent in Firmament/Inspection mode.
            if (!configuration.ActiveSystem.IsFirmamentLike() && ImGui.BeginTabItem("Planner"))
            {
                ImGui.Spacing();
                if (ImGui.BeginChild("##PlannerScroll", new Vector2(0, -1), false))
                {
                    DrawPlannerTab();
                }
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Characters"))
            {
                ImGui.Spacing();
                DrawCharactersTab();
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
            ImGui.SameLine();
            DrawCopyTroubleshootingButton();
        });
    }

    private void DrawStatusRow()
    {
        ImGuiHelper.Panel("StatusBar", () =>
        {
            var (color, label, isActive) = PluginStatePresentation.Describe(_status.Current, _status.Detail);

            ImGuiHelper.StatusDot(color, pulse: isActive);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(label);
            ImGui.PopStyleColor();

            var statusGoal = configuration.ActiveSystem.IsFirmamentLike()
                ? configuration.FirmamentGoal
                : configuration.Goal;

            if (statusGoal.ItemsToPurchase.Count > 0)
            {
                int completed = 0;
                foreach (var i in statusGoal.ItemsToPurchase)
                    if (i.Quantity > 0 && i.AmountPurchased >= i.Quantity)
                        completed++;
                int total = statusGoal.ItemsToPurchase.Count;
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

    private void DrawSystemToggle()
    {
        ImGuiHelper.Panel("SystemToggle", () =>
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("System");
            ImGui.SameLine();
            DrawSystemOption("Normal Scrips", ScripSystemId.Normal);
            ImGui.SameLine();
            DrawSystemOption("Firmament", ScripSystemId.Firmament, requireShift: true);
        });
    }

    private void DrawSystemOption(string label, ScripSystemId id, bool requireShift = false)
    {
        bool isCurrent = configuration.ActiveSystem == id;
        // Resource Inspection is a sub-activity of Firmament, so keep the Firmament button lit
        // while it's the active (internal) system.
        bool highlight = isCurrent ||
                         (id == ScripSystemId.Firmament && configuration.ActiveSystem == ScripSystemId.Inspection);
        if (highlight) ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.Accent);
        ImGui.BeginDisabled(!isCurrent && _automationHandler.IsRunning);
        bool clicked = ImGui.Button(label);
        ImGui.EndDisabled();
        if (highlight) ImGui.PopStyleColor();

        if (requireShift && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(highlight
                ? "Experimental feature."
                : "Experimental — hold Shift and click to enable.");

        if (clicked && !isCurrent && (!requireShift || ImGui.GetIO().KeyShift))
        {
            configuration.ActiveSystem = id;
            configuration.Save();
            _plannerService.InvalidateCache();
            _planCache = null;
        }
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
