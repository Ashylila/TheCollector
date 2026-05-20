namespace TheCollector.Data.Models;

public class DiscordNotificationSettings
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = "";

    public bool NotifyOnHardFail { get; set; } = true;
    public bool NotifyOnGoalComplete { get; set; } = true;
    public bool NotifyOnStopCondition { get; set; } = true;
    public bool NotifyOnScripCap { get; set; } = false;
}
