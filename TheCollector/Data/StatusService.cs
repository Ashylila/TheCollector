using System;

namespace TheCollector.Data;


public sealed class StatusService
{
    private PluginState _current = PluginState.Idle;
    private string? _detail;

    public PluginState Current => _current;

    public string? Detail => _detail;

    public event Action<PluginState>? Changed;


    public void Set(PluginState state, string? detail = null)
    {
        if (_current == state && _detail == detail) return;
        _current = state;
        _detail = detail;
        Changed?.Invoke(state);
    }

    public void SetIdle() => Set(PluginState.Idle);
}
