using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace TheCollector.Utility;

public static class UiTheme
{
    public static readonly Vector4 Accent          = new(0.95f, 0.72f, 0.30f, 1.00f);
    public static readonly Vector4 AccentHover     = new(1.00f, 0.80f, 0.42f, 1.00f);
    public static readonly Vector4 AccentActive    = new(0.82f, 0.60f, 0.20f, 1.00f);
    public static readonly Vector4 AccentMuted     = new(0.95f, 0.72f, 0.30f, 0.35f);

    public static readonly Vector4 Danger          = new(0.85f, 0.25f, 0.25f, 1.00f);
    public static readonly Vector4 DangerHover     = new(0.95f, 0.32f, 0.32f, 1.00f);
    public static readonly Vector4 DangerActive    = new(0.70f, 0.18f, 0.18f, 1.00f);

    public static readonly Vector4 Success         = new(0.35f, 0.90f, 0.50f, 1.00f);
    public static readonly Vector4 Warning         = new(0.95f, 0.75f, 0.10f, 1.00f);
    public static readonly Vector4 Info            = new(0.40f, 0.65f, 1.00f, 1.00f);
    public static readonly Vector4 Idle            = new(0.55f, 0.55f, 0.55f, 1.00f);

    public static readonly Vector4 WindowBg        = new(0.08f, 0.08f, 0.10f, 0.97f);
    public static readonly Vector4 PanelBg         = new(0.12f, 0.12f, 0.14f, 1.00f);
    public static readonly Vector4 PanelBorder     = new(0.22f, 0.22f, 0.26f, 1.00f);
    public static readonly Vector4 PanelShadow     = new(0.00f, 0.00f, 0.00f, 0.45f);

    public static readonly Vector4 Text            = new(0.92f, 0.92f, 0.94f, 1.00f);
    public static readonly Vector4 TextDim         = new(0.62f, 0.62f, 0.68f, 1.00f);
    public static readonly Vector4 TextFaint       = new(0.45f, 0.45f, 0.50f, 1.00f);

    private static int _pushedColors;
    private static int _pushedVars;

    public static void Push()
    {
        PushColor(ImGuiCol.WindowBg,             WindowBg);
        PushColor(ImGuiCol.ChildBg,              PanelBg);
        PushColor(ImGuiCol.PopupBg,              new Vector4(0.10f, 0.10f, 0.12f, 0.98f));
        PushColor(ImGuiCol.Border,               PanelBorder);
        PushColor(ImGuiCol.BorderShadow,         new Vector4(0f, 0f, 0f, 0.30f));

        PushColor(ImGuiCol.Text,                 Text);
        PushColor(ImGuiCol.TextDisabled,         TextDim);

        PushColor(ImGuiCol.FrameBg,              new Vector4(0.16f, 0.16f, 0.19f, 1.00f));
        PushColor(ImGuiCol.FrameBgHovered,       new Vector4(0.20f, 0.20f, 0.24f, 1.00f));
        PushColor(ImGuiCol.FrameBgActive,        new Vector4(0.24f, 0.24f, 0.28f, 1.00f));

        PushColor(ImGuiCol.TitleBg,              new Vector4(0.06f, 0.06f, 0.08f, 1.00f));
        PushColor(ImGuiCol.TitleBgActive,        new Vector4(0.10f, 0.10f, 0.12f, 1.00f));
        PushColor(ImGuiCol.TitleBgCollapsed,     new Vector4(0.06f, 0.06f, 0.08f, 0.85f));

        PushColor(ImGuiCol.Button,               new Vector4(0.22f, 0.22f, 0.26f, 1.00f));
        PushColor(ImGuiCol.ButtonHovered,        new Vector4(0.30f, 0.30f, 0.35f, 1.00f));
        PushColor(ImGuiCol.ButtonActive,         new Vector4(0.18f, 0.18f, 0.22f, 1.00f));

        PushColor(ImGuiCol.Header,               new Vector4(0.95f, 0.72f, 0.30f, 0.18f));
        PushColor(ImGuiCol.HeaderHovered,        new Vector4(0.95f, 0.72f, 0.30f, 0.32f));
        PushColor(ImGuiCol.HeaderActive,         new Vector4(0.95f, 0.72f, 0.30f, 0.42f));

        PushColor(ImGuiCol.Tab,                  new Vector4(0.10f, 0.10f, 0.12f, 1.00f));
        PushColor(ImGuiCol.TabHovered,           new Vector4(0.95f, 0.72f, 0.30f, 0.55f));
        PushColor(ImGuiCol.TabActive,            new Vector4(0.95f, 0.72f, 0.30f, 0.80f));
        PushColor(ImGuiCol.TabUnfocused,         new Vector4(0.08f, 0.08f, 0.10f, 1.00f));
        PushColor(ImGuiCol.TabUnfocusedActive,   new Vector4(0.30f, 0.22f, 0.10f, 1.00f));

        PushColor(ImGuiCol.CheckMark,            Accent);
        PushColor(ImGuiCol.SliderGrab,           Accent);
        PushColor(ImGuiCol.SliderGrabActive,     AccentHover);

        PushColor(ImGuiCol.Separator,            new Vector4(0.30f, 0.30f, 0.34f, 1.00f));
        PushColor(ImGuiCol.SeparatorHovered,     AccentMuted);
        PushColor(ImGuiCol.SeparatorActive,      Accent);

        PushColor(ImGuiCol.ResizeGrip,           new Vector4(0f, 0f, 0f, 0f));
        PushColor(ImGuiCol.ResizeGripHovered,    AccentMuted);
        PushColor(ImGuiCol.ResizeGripActive,     Accent);

        PushColor(ImGuiCol.TableHeaderBg,        new Vector4(0.14f, 0.14f, 0.17f, 1.00f));
        PushColor(ImGuiCol.TableBorderStrong,    new Vector4(0.28f, 0.28f, 0.32f, 1.00f));
        PushColor(ImGuiCol.TableBorderLight,     new Vector4(0.20f, 0.20f, 0.23f, 1.00f));
        PushColor(ImGuiCol.TableRowBg,           new Vector4(0f, 0f, 0f, 0f));
        PushColor(ImGuiCol.TableRowBgAlt,        new Vector4(1f, 1f, 1f, 0.025f));

        PushColor(ImGuiCol.PlotHistogram,        Accent);
        PushColor(ImGuiCol.PlotHistogramHovered, AccentHover);

        PushColor(ImGuiCol.ScrollbarBg,          new Vector4(0f, 0f, 0f, 0f));
        PushColor(ImGuiCol.ScrollbarGrab,        new Vector4(0.30f, 0.30f, 0.34f, 1.00f));
        PushColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.40f, 0.40f, 0.45f, 1.00f));
        PushColor(ImGuiCol.ScrollbarGrabActive,  AccentMuted);

        PushColor(ImGuiCol.NavHighlight,         AccentMuted);

        PushVar(ImGuiStyleVar.WindowPadding,     new Vector2(10f, 10f));
        PushVar(ImGuiStyleVar.FramePadding,      new Vector2(6f, 4f));
        PushVar(ImGuiStyleVar.ItemSpacing,       new Vector2(6f, 6f));
        PushVar(ImGuiStyleVar.ItemInnerSpacing,  new Vector2(5f, 4f));
        PushVar(ImGuiStyleVar.CellPadding,       new Vector2(6f, 4f));
        PushVar(ImGuiStyleVar.WindowRounding,    6f);
        PushVar(ImGuiStyleVar.ChildRounding,     5f);
        PushVar(ImGuiStyleVar.FrameRounding,     4f);
        PushVar(ImGuiStyleVar.PopupRounding,     5f);
        PushVar(ImGuiStyleVar.ScrollbarRounding, 4f);
        PushVar(ImGuiStyleVar.GrabRounding,      3f);
        PushVar(ImGuiStyleVar.TabRounding,       4f);
        PushVar(ImGuiStyleVar.WindowBorderSize,  1f);
        PushVar(ImGuiStyleVar.ChildBorderSize,   1f);
        PushVar(ImGuiStyleVar.FrameBorderSize,   0f);
        PushVar(ImGuiStyleVar.ScrollbarSize,     11f);
    }

    public static void Pop()
    {
        if (_pushedVars > 0)
        {
            ImGui.PopStyleVar(_pushedVars);
            _pushedVars = 0;
        }
        if (_pushedColors > 0)
        {
            ImGui.PopStyleColor(_pushedColors);
            _pushedColors = 0;
        }
    }

    private static void PushColor(ImGuiCol col, Vector4 value)
    {
        ImGui.PushStyleColor(col, value);
        _pushedColors++;
    }

    private static void PushVar(ImGuiStyleVar var, float value)
    {
        ImGui.PushStyleVar(var, value);
        _pushedVars++;
    }

    private static void PushVar(ImGuiStyleVar var, Vector2 value)
    {
        ImGui.PushStyleVar(var, value);
        _pushedVars++;
    }

    public static float Pulse(float min = 0.55f, float max = 1.00f, float speed = 2.4f)
    {
        var t = (MathF.Sin((float)ImGui.GetTime() * speed) + 1f) * 0.5f;
        return min + (max - min) * t;
    }

    public static Vector4 WithAlpha(Vector4 color, float alpha)
        => new(color.X, color.Y, color.Z, alpha);
}
