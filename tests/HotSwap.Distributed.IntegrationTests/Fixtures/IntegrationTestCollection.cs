using Xunit;

namespace HotSwap.Distributed.IntegrationTests.Fixtures;

/// <summary>
/// Defines the "IntegrationTests" collection and its shared fixtures.
/// All test classes decorated with [Collection("IntegrationTests")] will share these fixtures.
/// This ensures containers and factory are created once per test run, not per test class.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection :
    ICollectionFixture<PostgreSqlContainerFixture>,
    ICollectionFixture<RedisContainerFixture>,
    ICollectionFixture<IntegrationTestFactory>
{
    // This class is never instantiated.
    // Its purpose is to define the collection and its fixtures.
    // xUnit will inject these fixtures into test class constructors.
}
