# Testing Strategy

**Version:** 1.0.0
**Target Coverage:** 85%+
**Test Count:** 350+ tests
**Framework:** xUnit, Moq, FluentAssertions

---

## Overview

The API Gateway Orchestrator follows **Test-Driven Development (TDD)** with comprehensive test coverage across all layers.

### Test Pyramid

```
                 ▲
                / \
               /E2E\           5% - End-to-End Tests (20 tests)
              /_____\
             /       \
            /Integration\      15% - Integration Tests (50 tests)
           /___________\
          /             \
         /  Unit Tests   \     80% - Unit Tests (280 tests)
        /_________________\
```

**Total Tests:** 350+ tests across all layers

---

## Unit Testing

**Target:** 280+ unit tests, 85%+ code coverage

### Domain Models Tests

```csharp
public class GatewayRouteTests
{
    [Fact]
    public void Route_WithValidData_PassesValidation()
    {
        var route = new GatewayRoute
        {
            RouteId = "test-route",
            Name = "Test Route",
            PathPattern = "/api/test/*",
            Backends = new List<Backend>
            {
                new Backend
                {
                    BackendId = "backend-1",
                    Name = "Test Backend",
                    Url = "http://localhost:8080"
                }
            }
        };

        var isValid = route.IsValid(out var errors);

        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/*", "/api/users", true)]
    [InlineData("/api/**", "/api/v1/users/123", true)]
    [InlineData("/api/users/{id}", "/api/users/123", true)]
    [InlineData("/api/users/*", "/api/products/123", false)]
    public void MatchesPath_WithPattern_ReturnsExpected(
        string pattern, string requestPath, bool shouldMatch)
    {
        var route = CreateRoute(pattern);

        var matches = route.MatchesPath(requestPath);

        matches.Should().Be(shouldMatch);
    }
}
```

**Estimated Tests:** 30+ tests for domain models

---

### Routing Strategy Tests

```csharp
public class RoundRobinStrategyTests
{
    [Fact]
    public async Task SelectBackend_WithThreeBackends_RotatesEvenly()
    {
        var strategy = new RoundRobinStrategy();
        var backends = CreateBackends(3);
        var selections = new List<Backend>();

        for (int i = 0; i < 9; i++)
        {
            var selected = await strategy.SelectBackendAsync(
                CreateRoute(), backends, CreateRequest());
            selections.Add(selected);
        }

        selections.Count(b => b.Name == "backend-1").Should().Be(3);
        selections.Count(b => b.Name == "backend-2").Should().Be(3);
        selections.Count(b => b.Name == "backend-3").Should().Be(3);
    }

    [Fact]
    public async Task SelectBackend_WithUnhealthyBackend_SkipsUnhealthy()
    {
        var strategy = new RoundRobinStrategy();
        var backends = CreateBackends(3);
        backends[1].HealthStatus = HealthStatus.Unhealthy;

        var selections = new List<Backend>();
        for (int i = 0; i < 6; i++)
        {
            var selected = await strategy.SelectBackendAsync(
                CreateRoute(), backends, CreateRequest());
            selections.Add(selected);
        }

        selections.Should().NotContain(b => b.Name == "backend-2");
        selections.Count(b => b.Name == "backend-1").Should().Be(3);
        selections.Count(b => b.Name == "backend-3").Should().Be(3);
    }
}
```

**Estimated Tests:** 15+ tests per strategy (×5 strategies = 75 tests)

---

### HTTP Proxy Tests

```csharp
public class HTTPProxyTests
{
    [Fact]
    public async Task ProxyRequest_CopiesHeaders_ExcludesHopByHop()
    {
        var proxy = new HTTPProxy(_mockHttpClient.Object);
        var request = CreateRequest();
        request.Headers.Add("X-Custom-Header", "value");
        request.Headers.Add("Connection", "keep-alive"); // Hop-by-hop

        var response = await proxy.ProxyRequestAsync(
            "http://backend:8080", request);

        _mockHttpClient.Verify(c => c.SendAsync(
            It.Is<HttpRequestMessage>(req =>
                req.Headers.Contains("X-Custom-Header") &&
                !req.Headers.Contains("Connection")),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ProxyRequest_WithTimeout_ThrowsTimeoutException()
    {
        var proxy = new HTTPProxy(_mockHttpClient.Object);
        _mockHttpClient.Setup(c => c.SendAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        await Assert.ThrowsAsync<GatewayTimeoutException>(() =>
            proxy.ProxyRequestAsync("http://backend:8080", CreateRequest()));
    }
}
```

**Estimated Tests:** 25+ tests for HTTP proxy

---

### Circuit Breaker Tests

```csharp
public class CircuitBreakerTests
{
    [Fact]
    public async Task CircuitBreaker_AfterThresholdFailures_OpensCircuit()
    {
        var breaker = new CircuitBreaker(failureThreshold: 5);

        // Trigger 5 failures
        for (int i = 0; i < 5; i++)
        {
            await breaker.RecordFailureAsync();
        }

        breaker.State.Should().Be(CircuitBreakerState.Open);
    }

    [Fact]
    public async Task CircuitBreaker_WhenOpen_ExecutesFastFail()
    {
        var breaker = new CircuitBreaker(failureThreshold: 1);
        await breaker.RecordFailureAsync(); // Opens circuit

        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            breaker.ExecuteAsync(() => Task.FromResult(true)));
    }

    [Fact]
    public async Task CircuitBreaker_AfterTimeout_EntersHalfOpen()
    {
        var breaker = new CircuitBreaker(
            failureThreshold: 1,
            timeout: TimeSpan.FromMilliseconds(100));

        await breaker.RecordFailureAsync(); // Opens
        breaker.State.Should().Be(CircuitBreakerState.Open);

        await Task.Delay(150); // Wait for timeout

        var state = breaker.State;
        state.Should().Be(CircuitBreakerState.HalfOpen);
    }
}
```

**Estimated Tests:** 20+ tests for circuit breaker

---

## Integration Testing

**Target:** 50+ integration tests

### End-to-End Request Flow Test

```csharp
[Collection("Integration")]
public class RequestFlowTests : IClassFixture<TestServerFixture>
{
    [Fact]
    public async Task EndToEndFlow_ProxyRequest_WorksCorrectly()
    {
        // Arrange - Create route
        var createRouteRequest = new
        {
            name = "integration-test",
            pathPattern = "/test/*",
            backends = new[]
            {
                new
                {
                    name = "test-backend",
                    url = TestBackend.Url
                }
            },
            strategy = "RoundRobin"
        };
        await _client.PostAsJsonAsync("/api/v1/gateway/routes", createRouteRequest);

        // Act - Send request through gateway
        var response = await _client.GetAsync("/test/users/123");

        // Assert - Request proxied successfully
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("user-123");

        // Verify trace spans created
        await AssertTraceExists("/test/users/123");
    }

    [Fact]
    public async Task CanaryDeployment_TrafficSplit_WorksCorrectly()
    {
        // Arrange - Create route with weighted backends
        var createRouteRequest = new
        {
            name = "canary-test",
            pathPattern = "/canary/*",
            backends = new[]
            {
                new { name = "stable", url = StableBackend.Url, weight = 90 },
                new { name = "canary", url = CanaryBackend.Url, weight = 10 }
            },
            strategy = "WeightedRoundRobin"
        };
        await _client.PostAsJsonAsync("/api/v1/gateway/routes", createRouteRequest);

        // Act - Send 100 requests
        var results = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            var response = await _client.GetAsync("/canary/test");
            var content = await response.Content.ReadAsStringAsync();
            results.Add(content);
        }

        // Assert - ~90% stable, ~10% canary (allow ±5% variance)
        var stableCount = results.Count(r => r.Contains("stable"));
        var canaryCount = results.Count(r => r.Contains("canary"));

        stableCount.Should().BeInRange(85, 95);
        canaryCount.Should().BeInRange(5, 15);
    }
}
```

**Estimated Tests:** 40+ integration tests

---

## End-to-End Testing

**Target:** 20+ E2E tests

### Complete Deployment Lifecycle Test

```csharp
[Collection("E2E")]
public class DeploymentLifecycleTests : IClassFixture<E2ETestFixture>
{
    [Fact]
    public async Task CanaryDeployment_FullLifecycle_WorksCorrectly()
    {
        // Arrange - Create initial route
        var initialRoute = await CreateRoute("api-test", StableBackend);

        // Act - Deploy canary configuration
        var deployment = await DeployCanary(
            routeId: "api-test",
            canaryBackend: CanaryBackend,
            canaryPercentage: 10);

        // Assert - Phase 1: 10% canary
        await VerifyTrafficSplit(stablePercent: 90, canaryPercent: 10);

        // Act - Promote to 50%
        await PromoteDeployment(deployment.DeploymentId, percentage: 50);

        // Assert - Phase 2: 50% canary
        await VerifyTrafficSplit(stablePercent: 50, canaryPercent: 50);

        // Act - Promote to 100%
        await PromoteDeployment(deployment.DeploymentId, percentage: 100);

        // Assert - Phase 3: 100% canary (stable decommissioned)
        await VerifyTrafficSplit(stablePercent: 0, canaryPercent: 100);

        // Verify metrics tracked
        var metrics = await GetDeploymentMetrics(deployment.DeploymentId);
        metrics.TotalRequests.Should().BeGreaterThan(0);
        metrics.ErrorRate.Should().BeLessThan(0.01); // < 1% errors
    }
}
```

---

## Performance Testing

**Target:** Meet SLA requirements

### Throughput Test

```csharp
[Fact]
public async Task Throughput_50KRequestsPerSecond_Achieved()
{
    var requestCount = 500_000;
    var targetDuration = TimeSpan.FromSeconds(10); // 50K req/sec

    var stopwatch = Stopwatch.StartNew();
    var tasks = Enumerable.Range(0, requestCount)
        .Select(_ => _client.GetAsync("/api/test"))
        .ToList();
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    stopwatch.Elapsed.Should().BeLessThan(targetDuration);
    var throughput = requestCount / stopwatch.Elapsed.TotalSeconds;
    throughput.Should().BeGreaterThan(50_000);
}
```

### Latency Test

```csharp
[Fact]
public async Task ProxyOverhead_P99_LessThan5ms()
{
    var requestCount = 1_000;
    var latencies = new List<double>();

    for (int i = 0; i < requestCount; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        await _gateway.ProxyRequestAsync(CreateRequest());
        stopwatch.Stop();

        // Subtract backend latency (1ms) to get proxy overhead
        var proxyOverhead = stopwatch.Elapsed.TotalMilliseconds - 1;
        latencies.Add(proxyOverhead);
    }

    var p99 = latencies.OrderBy(x => x).ElementAt((int)(requestCount * 0.99));
    p99.Should().BeLessThan(5);
}
```

---

## TDD Workflow

**Red-Green-Refactor:**

### Example: Implementing Weighted Round Robin

**Step 1: 🔴 RED - Write Failing Test**

```csharp
[Fact]
public async Task SelectBackend_With90_10Weight_DistributesCorrectly()
{
    var strategy = new WeightedRoundRobinStrategy();
    var backends = new List<Backend>
    {
        new Backend { Name = "stable", Weight = 90 },
        new Backend { Name = "canary", Weight = 10 }
    };

    var selections = new List<string>();
    for (int i = 0; i < 100; i++)
    {
        var selected = await strategy.SelectBackendAsync(
            CreateRoute(), backends, CreateRequest());
        selections.Add(selected.Name);
    }

    var stableCount = selections.Count(s => s == "stable");
    stableCount.Should().BeInRange(85, 95); // 90% ±5%
}
```

Run test: **FAILS** ❌

**Step 2: 🟢 GREEN - Minimal Implementation**

```csharp
public class WeightedRoundRobinStrategy : IRoutingStrategy
{
    public async Task<Backend> SelectBackendAsync(
        GatewayRoute route,
        List<Backend> backends,
        HttpRequest request)
    {
        var totalWeight = backends.Sum(b => b.Weight);
        var random = Random.Shared.Next(0, totalWeight);

        var cumulative = 0;
        foreach (var backend in backends)
        {
            cumulative += backend.Weight;
            if (random < cumulative)
                return backend;
        }

        return backends.Last();
    }
}
```

Run test: **PASSES** ✓

**Step 3: 🔵 REFACTOR - Improve**

```csharp
public async Task<Backend> SelectBackendAsync(
    GatewayRoute route,
    List<Backend> backends,
    HttpRequest request)
{
    var healthyBackends = backends
        .Where(b => b.IsAvailable() && b.Weight > 0)
        .ToList();

    if (healthyBackends.Count == 0)
        throw new NoHealthyBackendsException();

    var totalWeight = healthyBackends.Sum(b => b.Weight);
    var random = Random.Shared.Next(0, totalWeight);

    var cumulative = 0;
    foreach (var backend in healthyBackends)
    {
        cumulative += backend.Weight;
        if (random < cumulative)
            return backend;
    }

    return healthyBackends.Last();
}
```

Run test: **PASSES** ✓

---

## Test Coverage Requirements

**Minimum Coverage:** 85%

**Coverage by Layer:**
- Domain: 95%+ (simple models, high coverage easy)
- Infrastructure: 80%+ (external dependencies)
- Orchestrator: 85%+ (core business logic)
- API: 80%+ (mostly integration tests)

---

**Last Updated:** 2025-11-23
**Test Count:** 350+ tests
**Coverage Target:** 85%+
