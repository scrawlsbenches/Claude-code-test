using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Defines the "IntegrationTests" collection and its shared fixture.
/// All test classes decorated with [Collection("IntegrationTests")] will share this fixture.
/// This ensures containers and factory are created once per test run, not per test class.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<SharedIntegrationTestFixture>
{
    // This class is never instantiated.
    // Its purpose is to define the collection and its shared fixture.
    // xUnit will inject the SharedIntegrationTestFixture into test class constructors.
}
