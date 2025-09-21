using System;
using System.IO;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace TheCollector.Utility;
public class PlogonLog
{
    private readonly IPluginLog _log = Svc.Log;

    private static string Prefix(string file, string member, int line)
        => $"[{Path.GetFileNameWithoutExtension(file)}.{member}:{line}]";

    public void Information(string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => _log.Information($"{Prefix(file, member, line)} {message}");

    public void Debug(string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => _log.Debug($"{Prefix(file, member, line)} {message}");

    public void Verbose(string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => _log.Verbose($"{Prefix(file, member, line)} {message}");

    public void Error(string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => _log.Error($"{Prefix(file, member, line)} {message}");

    public void Error(Exception ex, string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => _log.Error(ex, $"{Prefix(file, member, line)} {message}");

    public void Fatal(string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => _log.Fatal($"{Prefix(file, member, line)} {message}");

    public void Fatal(Exception ex, string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => _log.Fatal(ex, $"{Prefix(file, member, line)} {message}");
}
