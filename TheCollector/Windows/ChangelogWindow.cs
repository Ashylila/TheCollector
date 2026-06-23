using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using TheCollector.Data.Models;
using TheCollector.Utility;

namespace TheCollector.Windows;

public class ChangelogWindow : Window
{
    private readonly ChangelogBook _book;
    private readonly Func<(int LastSeen, ChangeLogDisplayType Display)> _getConfig;
    private readonly Action<int, ChangeLogDisplayType> _setConfig;

    private int _viewIndex = -1;
    private bool _autoOpenedThisSession;

    public ChangelogWindow(string label, ChangelogBook book,
        Func<(int, ChangeLogDisplayType)> getConfig,
        Action<int, ChangeLogDisplayType> setConfig)
        : base("###" + label,
            ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoSavedSettings)
    {
        _book = book;
        _getConfig = getConfig;
        _setConfig = setConfig;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 380),
            MaximumSize = new Vector2(720, 720),
        };
    }

    public override void PreOpenCheck()
    {
        if (_autoOpenedThisSession) return;
        _autoOpenedThisSession = true;

        var (lastSeen, display) = _getConfig();
        if (display == ChangeLogDisplayType.Never) return;
        if (_book.Versions.Count <= lastSeen) return;

        if (display == ChangeLogDisplayType.HighlightOnly)
        {
            bool anyHighlight = false;
            for (var i = lastSeen; i < _book.Versions.Count; i++)
            {
                if (_book.Versions[i].HasHighlight || _book.Versions[i].HasImportant)
                {
                    anyHighlight = true;
                    break;
                }
            }
            if (!anyHighlight) return;
        }

        _viewIndex = _book.Versions.Count - 1;
        IsOpen = true;
        WindowName = $"What's new — {_book.Versions[_viewIndex].Title}###changelog";
    }


    public void Open()
    {
        if (_book.Versions.Count == 0) return;
        _viewIndex = _book.Versions.Count - 1;
        WindowName = $"Changelog — {_book.Versions[_viewIndex].Title}###changelog";
        IsOpen = true;
    }

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
        if (_book.Versions.Count == 0)
        {
            ImGui.TextDisabled("No changelog entries yet.");
            return;
        }

        if (_viewIndex < 0 || _viewIndex >= _book.Versions.Count)
            _viewIndex = _book.Versions.Count - 1;

        var version = _book.Versions[_viewIndex];
        var (lastSeen, display) = _getConfig();
        bool isUnread = version.Index >= lastSeen;

        DrawHero(version, isUnread);
        ImGui.Dummy(new Vector2(0, 4));

        if (ImGui.BeginChild("##changelogBody", new Vector2(0, -56), false))
        {
            DrawEntries(version);
        }
        ImGui.EndChild();

        ImGui.Separator();
        DrawFooter(display);
    }

    private void DrawHero(ChangelogVersion version, bool isUnread)
    {
        ImGuiHelper.HeroBanner(
            title: $"The Collector — {version.Title}",
            subtitle: isUnread ? "New since you last opened the changelog" : "From the changelog history",
            rightAction: () =>
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled($"{_viewIndex + 1} / {_book.Versions.Count}");
            },
            rightActionWidth: 90f);
    }

    private void DrawEntries(ChangelogVersion version)
    {
        int important = 0, highlight = 0, plain = 0;
        foreach (var e in version.Entries)
        {
            switch (e.Kind)
            {
                case ChangelogEntryKind.Important: important++; break;
                case ChangelogEntryKind.Highlight: highlight++; break;
                default:                            plain++;    break;
            }
        }

        if (important > 0)
        {
            ImGuiHelper.Panel("changelogImportant", () =>
            {
                ImGuiHelper.SectionHeader("Heads up", UiTheme.Danger);
                foreach (var e in version.Entries)
                    if (e.Kind == ChangelogEntryKind.Important)
                        DrawImportantEntry(e.Text, e.Credit);
            });
        }

        if (highlight > 0)
        {
            ImGuiHelper.Panel("changelogHighlights", () =>
            {
                ImGuiHelper.SectionHeader("Highlights", UiTheme.Accent);
                foreach (var e in version.Entries)
                    if (e.Kind == ChangelogEntryKind.Highlight)
                        DrawBulletEntry(e.Text, UiTheme.Accent, bold: true, e.Credit);
            });
        }

        if (plain > 0)
        {
            ImGuiHelper.Panel("changelogEntries", () =>
            {
                ImGuiHelper.SectionHeader("Changes", UiTheme.TextDim);
                foreach (var e in version.Entries)
                    if (e.Kind == ChangelogEntryKind.Entry)
                        DrawBulletEntry(e.Text, UiTheme.TextDim, bold: false, e.Credit);
            });
        }

        if (version.Entries.Count == 0)
        {
            ImGui.TextDisabled("(no entries)");
        }
    }

    private static void DrawBulletEntry(string text, Vector4 bulletColor, bool bold, string credit = "")
    {
        var availW = ImGui.GetContentRegionAvail().X;
        var startCursor = ImGui.GetCursorPos();

        ImGui.PushStyleColor(ImGuiCol.Text, bulletColor);
        ImGui.TextUnformatted(bold ? "▸" : "•");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        var textCursor = ImGui.GetCursorPos();
        ImGui.SetCursorPos(textCursor);
        ImGui.PushTextWrapPos(textCursor.X + availW - (textCursor.X - startCursor.X));
        if (bold) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Text);
        ImGui.TextUnformatted(text);
        if (bold) ImGui.PopStyleColor();
        ImGui.PopTextWrapPos();

        DrawCredit(credit, textCursor.X);

        ImGui.Dummy(new Vector2(0, 2));
    }

    private static void DrawCredit(string credit, float textIndentX)
    {
        if (string.IsNullOrEmpty(credit)) return;

        ImGui.SetCursorPosX(textIndentX);
        ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.TextFaint);
        ImGui.TextUnformatted($"— @{credit}");
        ImGui.PopStyleColor();
    }

    private static void DrawImportantEntry(string text, string credit = "")
    {
        var dl     = ImGui.GetWindowDrawList();
        var pos    = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;
        var pad    = new Vector2(10f, 8f);

        ImGui.Indent(2f);
        var startCursor = ImGui.GetCursorScreenPos();
        ImGui.PushTextWrapPos(startCursor.X + availW - pad.X * 2 - 2f);

        ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Danger);
        ImGui.TextUnformatted("⚠ ");
        ImGui.SameLine();
        var textX = ImGui.GetCursorScreenPos().X;
        ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Text);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor(2);

        if (!string.IsNullOrEmpty(credit))
        {
            ImGui.SetCursorScreenPos(new Vector2(textX, ImGui.GetCursorScreenPos().Y));
            ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.TextFaint);
            ImGui.TextUnformatted($"— @{credit}");
            ImGui.PopStyleColor();
        }

        ImGui.PopTextWrapPos();
        ImGui.Unindent(2f);

        var endY = ImGui.GetItemRectMax().Y + pad.Y * 0.5f;
        var min  = new Vector2(pos.X, pos.Y - pad.Y * 0.5f);
        var max  = new Vector2(pos.X + availW, endY);

        dl.AddRectFilled(min, max, ImGui.GetColorU32(UiTheme.WithAlpha(UiTheme.Danger, 0.10f)), 4f);
        dl.AddRect(min, max, ImGui.GetColorU32(UiTheme.WithAlpha(UiTheme.Danger, 0.45f)), 4f);

        ImGui.Dummy(new Vector2(0, 4));
    }

    private void DrawFooter(ChangeLogDisplayType display)
    {
        ImGui.BeginDisabled(_viewIndex <= 0);
        if (ImGui.ArrowButton("##chPrev", ImGuiDir.Left))
            _viewIndex = Math.Max(0, _viewIndex - 1);
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("##chVersion", _book.Versions[_viewIndex].Title))
        {
            for (var i = _book.Versions.Count - 1; i >= 0; i--)
            {
                bool selected = i == _viewIndex;
                if (ImGui.Selectable(_book.Versions[i].Title, selected))
                    _viewIndex = i;
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_viewIndex >= _book.Versions.Count - 1);
        if (ImGui.ArrowButton("##chNext", ImGuiDir.Right))
            _viewIndex = Math.Min(_book.Versions.Count - 1, _viewIndex + 1);
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        var displayInt = (int)display;
        var labels = new[]
        {
            "Show new updates",
            "Only show important updates",
            "Never show on launch",
        };
        if (ImGui.Combo("##chDisplay", ref displayInt, labels, labels.Length))
            _setConfig(_book.Versions.Count, (ChangeLogDisplayType)displayInt);

        ImGui.SameLine();
        var closeW = 80f;
        var spacing = ImGui.GetContentRegionAvail().X - closeW;
        if (spacing > 0) ImGui.Dummy(new Vector2(spacing, 0));
        ImGui.SameLine();
        if (ImGuiHelper.AccentButton("Got it", new Vector2(closeW, 26)))
        {
            _setConfig(_book.Versions.Count, display);
            IsOpen = false;
        }
    }
}
