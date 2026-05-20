namespace TheCollector.Data.Models;

public class StopConditions
{
    public bool StopOnScripsEarnedEnabled { get; set; } = false;
    public int MaxScripsEarned { get; set; } = 10000;

    public bool StopOnBuyCyclesEnabled { get; set; } = false;
    public int MaxBuyCycles { get; set; } = 5;

    public bool StopOnSessionTimeEnabled { get; set; } = false;
    public int MaxSessionMinutes { get; set; } = 120;
}
