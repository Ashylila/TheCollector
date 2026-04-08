using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using TheCollector.Utility;

namespace TheCollector.Windows;

public partial class MainWindow
{
    private string _collectableFilter = "";
    private bool _costBreakdownOpen = true;
    private bool _collectablesOpen = true;
    private bool _sessionOpen = true;
    private const int CollectablePageSize = 10;
    private readonly Dictionary<uint, int> _collectableVisibleCount = new();

    private void DrawPlannerTab()
    {
        if (configuration.ItemsToPurchase.Count == 0)
        {
            ImGui.TextDisabled("Add items to your purchase list on the Main tab to see the plan.");
            return;
        }

        var plan = _plannerService.Calculate();

        DrawPlannerOverview(plan);
        DrawPlannerCostBreakdown(plan);
        DrawPlannerCollectables(plan);
        DrawPlannerSession();
    }

    private void DrawPlannerOverview(PlanSummary plan)
    {
        ImGuiHelper.Panel("PlanOverview", () =>
        {
            if (plan.IsListComplete)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.90f, 0.35f, 1f));
                ImGui.TextUnformatted("All items purchased!");
                ImGui.PopStyleColor();
                return;
            }

            foreach (var cs in plan.CurrencySummaries)
            {
                var currName = GetCurrencyName(cs.CurrencyId);
                ImGui.TextUnformatted($"{cs.TotalScripsNeeded:N0} {currName} needed");
                if (cs.BestCollectable != null)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"(~{cs.EstimatedTurnIns} turn-ins via {cs.BestCollectable.Name})");
                }
            }
        });
    }

    private static string GetCurrencyName(uint specialId)
        => CurrencyHelper.GetCurrencyName(specialId);

    private void DrawPlannerCostBreakdown(PlanSummary plan)
    {
        ImGuiHelper.CollapsiblePanel("CostBreakdown", "Scrip Cost Breakdown", ref _costBreakdownOpen, () =>
        {
            var grouped = plan.ItemBreakdowns
                .GroupBy(i => i.CurrencyId)
                .ToList();

            foreach (var group in grouped)
            {
                var currencyName = GetCurrencyName(group.Key);
                ImGui.TextColored(new Vector4(0.80f, 0.70f, 0.30f, 1f), currencyName);
                ImGui.Spacing();

                var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.RowBg;
                if (!ImGui.BeginTable($"##CostTable{group.Key}", 4, tableFlags))
                    continue;

                ImGui.TableSetupColumn("Item",      ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Unit Cost", ImGuiTableColumnFlags.WidthFixed,   70f);
                ImGui.TableSetupColumn("Remaining", ImGuiTableColumnFlags.WidthFixed,   70f);
                ImGui.TableSetupColumn("Total",     ImGuiTableColumnFlags.WidthFixed,   70f);
                ImGui.TableHeadersRow();

                int groupTotal = 0;
                foreach (var item in group)
                {
                    bool done = item.QuantityRemaining <= 0;
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    if (done) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.90f, 0.35f, 1f));
                    ImGui.TextUnformatted(item.Name);
                    if (done) ImGui.PopStyleColor();

                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted($"{item.UnitCost:N0}");

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextUnformatted(done ? "Done" : $"{item.QuantityRemaining}");

                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextUnformatted(done ? "-" : $"{item.TotalCost:N0}");

                    groupTotal += item.TotalCost;
                }

                ImGui.TableNextRow();
                for (int col = 0; col < 4; col++)
                {
                    ImGui.TableSetColumnIndex(col);
                    ImGui.Separator();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted("Subtotal");
                ImGui.TableSetColumnIndex(3);
                ImGui.TextUnformatted($"{groupTotal:N0}");

                ImGui.EndTable();
                ImGui.Spacing();
            }
        });
    }

    private void DrawPlannerCollectables(PlanSummary plan)
    {
        if (plan.IsListComplete) return;

        ImGuiHelper.CollapsiblePanel("CollectableOptions", "Collectable Turn-in Options", ref _collectablesOpen, () =>
        {
            ImGui.InputTextWithHint("##CollFilter", "Filter collectables...", ref _collectableFilter, 100);
            ImGui.Spacing();

            foreach (var cs in plan.CurrencySummaries)
            {
                if (cs.Collectables.Count == 0) continue;

                var currencyName = GetCurrencyName(cs.CurrencyId);
                ImGui.TextColored(new Vector4(0.80f, 0.70f, 0.30f, 1f), currencyName);
                ImGui.Spacing();

                var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX
                    | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable;
                bool hideFish = configuration.Goal.HideFishingCollectables;

                if (!ImGui.BeginTable($"##CollTable{cs.CurrencyId}", 5, tableFlags))
                    continue;

                ImGui.TableSetupColumn("Lv",                ImGuiTableColumnFlags.WidthFixed,   25f);
                ImGui.TableSetupColumn("Collectable",       ImGuiTableColumnFlags.WidthFixed, 200f);
                ImGui.TableSetupColumn("Scrips / Turn-in",  ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 1f);
                ImGui.TableSetupColumn("Turn-ins Needed",   ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("##Recipe",          ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 50f);
                ImGui.TableHeadersRow();

                var filtered = cs.Collectables
                    .Where(c => (!hideFish || !c.IsFish) &&
                                (string.IsNullOrEmpty(_collectableFilter) ||
                                 c.Name.Contains(_collectableFilter, StringComparison.OrdinalIgnoreCase)));

                var sortSpecs = ImGui.TableGetSortSpecs();
                int sortCol = sortSpecs.Specs.ColumnIndex;
                bool ascending = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending;

                var sorted = (sortCol switch
                {
                    0 => ascending ? filtered.OrderBy(c => c.Level) : filtered.OrderByDescending(c => c.Level),
                    1 => ascending ? filtered.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase) : filtered.OrderByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase),
                    3 => ascending
                        ? filtered.OrderBy(c => c.HighReward > 0 ? Math.Ceiling((double)cs.TotalScripsNeeded / c.HighReward) : double.MaxValue)
                        : filtered.OrderByDescending(c => c.HighReward > 0 ? Math.Ceiling((double)cs.TotalScripsNeeded / c.HighReward) : 0),
                    _ => ascending ? filtered.OrderBy(c => c.HighReward) : filtered.OrderByDescending(c => c.HighReward),
                }).ToList();

                if (!_collectableVisibleCount.ContainsKey(cs.CurrencyId))
                    _collectableVisibleCount[cs.CurrencyId] = CollectablePageSize;

                int maxVisible = _collectableVisibleCount[cs.CurrencyId];
                int visibleCount = Math.Min(sorted.Count, maxVisible);

                for (int i = 0; i < visibleCount; i++)
                {
                    var col = sorted[i];
                    var turnIns = col.HighReward > 0
                        ? (int)Math.Ceiling((double)cs.TotalScripsNeeded / col.HighReward)
                        : 0;

                    bool isBest = col == cs.BestCollectable;

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextDisabled($"{col.Level}");

                    ImGui.TableSetColumnIndex(1);
                    if (isBest) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.30f, 0.85f, 0.30f, 1f));
                    ImGui.TextUnformatted(col.Name);
                    if (isBest) ImGui.PopStyleColor();
                    if (isBest && ImGui.IsItemHovered())
                        ImGui.SetTooltip("Most efficient collectable for this currency.");

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextUnformatted($"{col.HighReward}");

                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextUnformatted($"{turnIns}");

                    ImGui.TableSetColumnIndex(4);
                    if (!col.IsFish)
                    {
                        if (ImGui.SmallButton($"Recipe##{col.ItemId}"))
                        {
                            var recipeSheet = Svc.Data.GetExcelSheet<Recipe>();
                            var recipe = recipeSheet?.FirstOrDefault(r => r.ItemResult.RowId == col.ItemId);
                            if (recipe.HasValue)
                            {
                                unsafe
                                {
                                    var agent = AgentRecipeNote.Instance();
                                    agent->OpenRecipeByRecipeId(recipe.Value.RowId);
                                }
                            }
                        }
                    }
                }

                ImGui.EndTable();

                int remaining = sorted.Count - visibleCount;
                if (remaining > 0)
                {
                    if (ImGui.SmallButton($"Show more ({remaining} remaining)##{cs.CurrencyId}"))
                        _collectableVisibleCount[cs.CurrencyId] = maxVisible + CollectablePageSize;
                }
                else if (visibleCount > CollectablePageSize)
                {
                    if (ImGui.SmallButton($"Show less##{cs.CurrencyId}"))
                        _collectableVisibleCount[cs.CurrencyId] = CollectablePageSize;
                }

                ImGui.Spacing();
            }
        });
    }

    private void DrawPlannerSession()
    {
        if (_automationHandler.SessionStarted.HasValue)
        {
            var elapsed = DateTime.UtcNow - _automationHandler.SessionStarted.Value;

            ImGuiHelper.CollapsiblePanel("SessionStats", "Session", ref _sessionOpen, () =>
            {
                ImGui.TextUnformatted($"Turn-ins:    {_automationHandler.SessionCollectablesTurnedIn}");
                ImGui.TextUnformatted($"Buy cycles:  {_automationHandler.SessionItemsPurchased}");

                if (_automationHandler.SessionScripsSpent.Count > 0)
                {
                    foreach (var (currencyId, amount) in _automationHandler.SessionScripsSpent)
                        ImGui.TextUnformatted($"  {GetCurrencyName(currencyId)} spent: {amount:N0}");
                }

                ImGui.TextUnformatted($"Elapsed:     {elapsed.Hours}h {elapsed.Minutes}m");
            });
        }

        if (configuration.TotalScripsSpent.Count > 0)
        {
            ImGuiHelper.Panel("AllTimeScrips", () =>
            {
                ImGui.TextDisabled("All-Time Scrips Spent");
                ImGui.Separator();
                foreach (var (currencyId, amount) in configuration.TotalScripsSpent)
                    ImGui.TextUnformatted($"{GetCurrencyName(currencyId)}: {amount:N0}");
            });
        }
    }
}
