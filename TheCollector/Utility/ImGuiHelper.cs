using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace TheCollector.Utility;

public static class ImGuiHelper
{
    public static void Panel(string id, Action body)
        => DrawPanel(id, null, null, body);

    public static void CollapsiblePanel(string id, string label, ref bool isOpen, Action body)
    {
        bool open = isOpen;
        DrawPanel(id, label, () => open = !open, body, collapsible: true, isOpen: open);
        isOpen = open;
    }

    public static void SectionHeader(string label, Vector4? accent = null)
    {
        var color    = accent ?? UiTheme.Accent;
        var dl       = ImGui.GetWindowDrawList();
        var pos      = ImGui.GetCursorScreenPos();
        var availW   = ImGui.GetContentRegionAvail().X;
        var height   = ImGui.GetTextLineHeight();
        var textSize = ImGui.CalcTextSize(label);

        dl.AddRectFilled(
            new Vector2(pos.X, pos.Y + 2f),
            new Vector2(pos.X + 3f, pos.Y + height - 2f),
            ImGui.GetColorU32(color),
            1.5f);

        dl.AddText(
            new Vector2(pos.X + 10f, pos.Y),
            ImGui.GetColorU32(UiTheme.Text),
            label);

        var midY   = pos.Y + height * 0.5f;
        var startX = pos.X + 10f + textSize.X + 8f;
        var endX   = pos.X + availW;
        if (endX > startX)
        {
            dl.AddLine(
                new Vector2(startX, midY),
                new Vector2(endX,   midY),
                ImGui.GetColorU32(UiTheme.WithAlpha(color, 0.20f)),
                1f);
        }

        ImGui.Dummy(new Vector2(availW, height + 4f));
    }

    public static bool AccentButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button,        UiTheme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UiTheme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  UiTheme.AccentActive);
        ImGui.PushStyleColor(ImGuiCol.Text,          new Vector4(0.10f, 0.08f, 0.04f, 1f));
        var clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(4);
        return clicked;
    }

    public static bool DangerButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button,        UiTheme.WithAlpha(UiTheme.Danger, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, UiTheme.DangerHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  UiTheme.DangerActive);
        var clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(3);
        return clicked;
    }

    public static void StatusDot(Vector4 color, bool pulse = false)
    {
        var dl    = ImGui.GetWindowDrawList();
        var pos   = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();
        var cx    = pos.X + lineH * 0.5f;
        var cy    = pos.Y + lineH * 0.5f;
        var r     = lineH * 0.30f;

        if (pulse)
        {
            var glow = UiTheme.Pulse(0.20f, 0.55f);
            dl.AddCircleFilled(new Vector2(cx, cy), r * 2.4f, ImGui.GetColorU32(UiTheme.WithAlpha(color, glow)));
        }
        dl.AddCircleFilled(new Vector2(cx, cy), r, ImGui.GetColorU32(color));

        ImGui.Dummy(new Vector2(lineH, lineH));
    }

    public static void Chip(string label, Vector4 color)
    {
        var dl     = ImGui.GetWindowDrawList();
        var pos    = ImGui.GetCursorScreenPos();
        var pad    = new Vector2(8f, 2f);
        var size   = ImGui.CalcTextSize(label) + pad * 2f;
        var frameH = ImGui.GetFrameHeight();
        var yOff   = MathF.Max(0f, (frameH - size.Y) * 0.5f);
        var min    = pos + new Vector2(0f, yOff);
        var max    = min + size;

        dl.AddRectFilled(min, max, ImGui.GetColorU32(UiTheme.WithAlpha(color, 0.18f)), 4f);
        dl.AddRect(min, max, ImGui.GetColorU32(UiTheme.WithAlpha(color, 0.55f)), 4f);
        dl.AddText(min + pad, ImGui.GetColorU32(color), label);

        ImGui.Dummy(new Vector2(size.X, MathF.Max(size.Y, frameH)));
    }

    public static void HeroBanner(string title, string subtitle, Action? rightAction = null, float rightActionWidth = 120f)
    {
        var dl     = ImGui.GetWindowDrawList();
        var pos    = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;
        var height = 56f;

        var topCol    = ImGui.GetColorU32(new Vector4(0.18f, 0.13f, 0.06f, 1.00f));
        var bottomCol = ImGui.GetColorU32(new Vector4(0.10f, 0.08f, 0.10f, 1.00f));
        dl.AddRectFilledMultiColor(
            pos,
            new Vector2(pos.X + availW, pos.Y + height),
            topCol, topCol, bottomCol, bottomCol);

        dl.AddRectFilled(
            pos,
            new Vector2(pos.X + 4f, pos.Y + height),
            ImGui.GetColorU32(UiTheme.Accent),
            2f);

        dl.AddLine(
            new Vector2(pos.X,           pos.Y + height - 1f),
            new Vector2(pos.X + availW,  pos.Y + height - 1f),
            ImGui.GetColorU32(UiTheme.WithAlpha(UiTheme.Accent, 0.45f)),
            1f);

        var reserve   = rightAction != null ? rightActionWidth : 0f;
        var textLeft  = pos.X + 16f;
        var textRight = pos.X + availW - reserve - 8f;
        if (textRight > textLeft)
        {
            dl.PushClipRect(new Vector2(textLeft, pos.Y), new Vector2(textRight, pos.Y + height), true);
            dl.AddText(new Vector2(textLeft, pos.Y + 10f), ImGui.GetColorU32(UiTheme.Text),    title);
            dl.AddText(new Vector2(textLeft, pos.Y + 30f), ImGui.GetColorU32(UiTheme.TextDim), subtitle);
            dl.PopClipRect();
        }

        if (rightAction != null)
        {
            ImGui.SetCursorScreenPos(new Vector2(pos.X + availW - rightActionWidth, pos.Y + 14f));
            rightAction();
        }

        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + height));
        ImGui.Dummy(new Vector2(availW, 4f));
    }

    public static void GradientProgressBar(float fraction, Vector2 size, string overlay, Vector4 from, Vector4 to)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        var dl   = ImGui.GetWindowDrawList();
        var pos  = ImGui.GetCursorScreenPos();
        var min  = pos;
        var max  = pos + size;

        dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.10f, 1f)), 3f);

        if (fraction > 0f)
        {
            var fillMax = new Vector2(min.X + size.X * fraction, max.Y);
            var fromCol = ImGui.GetColorU32(from);
            var toCol   = ImGui.GetColorU32(to);
            dl.PushClipRect(min, fillMax, true);
            dl.AddRectFilledMultiColor(min, max, fromCol, toCol, toCol, fromCol);
            dl.PopClipRect();
        }

        dl.AddRect(min, max, ImGui.GetColorU32(UiTheme.PanelBorder), 3f);

        if (!string.IsNullOrEmpty(overlay))
        {
            var textSize = ImGui.CalcTextSize(overlay);
            var textPos  = new Vector2(min.X + (size.X - textSize.X) * 0.5f, min.Y + (size.Y - textSize.Y) * 0.5f);
            dl.AddText(textPos + new Vector2(1, 1), ImGui.GetColorU32(new Vector4(0, 0, 0, 0.7f)), overlay);
            dl.AddText(textPos, ImGui.GetColorU32(UiTheme.Text), overlay);
        }

        ImGui.Dummy(size);
    }

    private static void DrawPanel(string id, string? label, Action? onHeaderClick, Action body, bool collapsible = false, bool isOpen = true)
    {
        var style = ImGui.GetStyle();
        var pad   = style.FramePadding;

        var startScreen = ImGui.GetCursorScreenPos();
        var availW      = ImGui.GetContentRegionAvail().X;

        ImGui.PushID(id);

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        ImGui.BeginGroup();

        if (collapsible && label != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Accent);
            ImGui.TextUnformatted(isOpen ? "v" : ">");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Text);
            ImGui.TextUnformatted(label);
            ImGui.PopStyleColor();

            var headerMin  = new Vector2(startScreen.X - pad.X, startScreen.Y - pad.Y);
            var headerMax  = new Vector2(startScreen.X + availW + pad.X, ImGui.GetItemRectMax().Y + pad.Y);
            var savedCursor = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(headerMin);
            if (ImGui.InvisibleButton($"##header_{id}", new Vector2(headerMax.X - headerMin.X, headerMax.Y - headerMin.Y)))
                onHeaderClick?.Invoke();
            ImGui.SetCursorScreenPos(savedCursor);

            if (isOpen)
            {
                ImGui.Spacing();
                body();
            }
        }
        else
        {
            body();
        }

        ImGui.EndGroup();

        var endY = ImGui.GetItemRectMax().Y;

        var bgMin = new Vector2(startScreen.X - pad.X, startScreen.Y - pad.Y);
        var bgMax = new Vector2(startScreen.X + availW + pad.X, endY + pad.Y);
        var round = style.FrameRounding + 1f;

        dl.ChannelsSetCurrent(0);

        dl.AddRectFilled(
            bgMin + new Vector2(0f, 2f),
            bgMax + new Vector2(0f, 2f),
            ImGui.GetColorU32(UiTheme.PanelShadow),
            round);

        dl.AddRectFilled(bgMin, bgMax, ImGui.GetColorU32(UiTheme.PanelBg), round);
        dl.AddRect(bgMin, bgMax, ImGui.GetColorU32(UiTheme.PanelBorder), round);

        dl.AddRectFilled(
            bgMin,
            new Vector2(bgMin.X + 3f, bgMax.Y),
            ImGui.GetColorU32(UiTheme.WithAlpha(UiTheme.Accent, 0.55f)),
            round - 1f);

        dl.ChannelsMerge();

        ImGui.PopID();

        ImGui.Dummy(new Vector2(0, style.ItemSpacing.Y));
    }
}
