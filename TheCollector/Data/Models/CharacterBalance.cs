using System;
using System.Collections.Generic;

namespace TheCollector.Data.Models;

public class CharacterBalance
{
    public ulong ContentId { get; set; }
    public string LastSeenName { get; set; } = "";
    public string LastSeenWorld { get; set; } = "";
    public Dictionary<uint, int> ScripBalances { get; set; } = new();
    public DateTime LastSampledAt { get; set; }
}
