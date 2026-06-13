using System;
using System.Collections.Generic;

namespace TheCollector.Data;


public sealed class StatusService
{
    private PluginState _current = PluginState.Idle;
    private string? _detail;

    public PluginState Current => _current;

    public string? Detail => _detail;

    public event Action<PluginState>? Changed;

    public readonly record struct ErrorRecord(DateTime FirstAtUtc, DateTime LastAtUtc, string Source, string Message, int Count);

    private const int MaxErrorRecords = 25;
    private readonly List<ErrorRecord> _errors = [];

    public IReadOnlyList<ErrorRecord> Errors => _errors;

    public void ReportError(string source, string message)
    {
        var now = DateTime.UtcNow;
        if (_errors.Count > 0 && _errors[^1] is var last && last.Source == source && last.Message == message)
        {
            _errors[^1] = last with { LastAtUtc = now, Count = last.Count + 1 };
            return;
        }
        if (_errors.Count >= MaxErrorRecords)
            _errors.RemoveAt(0);
        _errors.Add(new ErrorRecord(now, now, source, message, 1));
    }


    public void Set(PluginState state, string? detail = null)
    {
        if (_current == state && _detail == detail) return;
        _current = state;
        _detail = detail;
        Changed?.Invoke(state);
    }

    public void SetIdle() => Set(PluginState.Idle);
}
