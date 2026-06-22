namespace TheCollector.Data.Models;

public class StopConditions
{
    public bool StopOnScripsEarnedEnabled { get; set; } = false;
    public int MaxScripsEarned { get; set; } = 10000;

    public bool StopOnBuyCyclesEnabled { get; set; } = false;
    public int MaxBuyCycles { get; set; } = 5;

    public bool StopOnSessionTimeEnabled { get; set; } = false;
    public int MaxSessionMinutes { get; set; } = 120;

    // One full loop = a complete gather -> inspect -> craft -> ... cycle, counted each time
    // autogather is re-enabled to start the next one. Stops after finishing the current cycle.
    public bool StopOnFullLoopsEnabled { get; set; } = false;
    public int MaxFullLoops { get; set; } = 5;
}
