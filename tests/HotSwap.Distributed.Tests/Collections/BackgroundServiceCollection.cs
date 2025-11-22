using Xunit;

namespace HotSwap.Distributed.Tests.Collections;

/// <summary>
/// Collection definition to run BackgroundService tests sequentially.
/// BackgroundService tests create PeriodicTimers which, when run in parallel (38 simultaneous),
/// overwhelm the .NET runtime's single timer thread, causing deadlock.
/// Running these tests sequentially avoids timer queue saturation.
/// </summary>
[CollectionDefinition("BackgroundService Sequential", DisableParallelization = true)]
public class BackgroundServiceCollection
{
    // This class is never instantiated. It exists solely to define the collection.
}
