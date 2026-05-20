using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TheCollector.Data.Models;

namespace TheCollector.Utility;

public class DiscordWebhookService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private const string AllowedHostPrefix1 = "https://discord.com/api/webhooks/";
    private const string AllowedHostPrefix2 = "https://discordapp.com/api/webhooks/";

    private readonly Configuration _config;
    private readonly PlogonLog _log;

    public DiscordWebhookService(Configuration config, PlogonLog log)
    {
        _config = config;
        _log = log;
    }

    public void Notify(DiscordEvent which, string message)
    {
        var d = _config.Discord;
        if (!d.Enabled) return;
        if (!IsAllowed(which, d)) return;
        if (!TryGetWebhookUrl(d.WebhookUrl, out var url)) return;

        _ = SendAsync(url, message);
    }

    public Task<bool> TestAsync()
    {
        if (!TryGetWebhookUrl(_config.Discord.WebhookUrl, out var url))
            return Task.FromResult(false);
        return SendAsync(url, "TheCollector: test message — webhook is wired up.");
    }

    private static bool IsAllowed(DiscordEvent which, DiscordNotificationSettings d) => which switch
    {
        DiscordEvent.HardFail      => d.NotifyOnHardFail,
        DiscordEvent.GoalComplete  => d.NotifyOnGoalComplete,
        DiscordEvent.StopCondition => d.NotifyOnStopCondition,
        DiscordEvent.ScripCap      => d.NotifyOnScripCap,
        _                          => false,
    };

    private static bool TryGetWebhookUrl(string? raw, out string url)
    {
        url = (raw ?? "").Trim();
        if (url.Length == 0) return false;
        return url.StartsWith(AllowedHostPrefix1, StringComparison.OrdinalIgnoreCase)
            || url.StartsWith(AllowedHostPrefix2, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> SendAsync(string url, string content)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { content, username = "TheCollector" });
            using var body = new StringContent(payload, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(url, body).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _log.Error($"Discord webhook returned {(int)resp.StatusCode}.");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Discord webhook send failed: {ex.Message}");
            return false;
        }
    }
}

public enum DiscordEvent
{
    HardFail,
    GoalComplete,
    StopCondition,
    ScripCap,
}
