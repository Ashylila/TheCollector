using System;
using System.Collections.Generic;
using TheCollector.Utility;

namespace TheCollector.Data.ScripSystem;

public interface ITurnInPipeline : IPipeline
{
    event Action OnFinished;
    event Action<uint, int> OnScripsEarned;
    event Action<Exception> OnError;

    uint? LastEarnedCurrency { get; }
    bool CapReached { get; }
    bool HasCollectible { get; }
}

public interface IBuyPipeline : IPipeline
{
    event Action<Dictionary<uint, int>> OnFinishedTrading;
    event Action<Exception> OnError;
}

public interface IScripSystem
{
    ScripSystemId Id { get; }
    string DisplayName { get; }
    ITurnInPipeline TurnIn { get; }
    IBuyPipeline Buy { get; }
}
