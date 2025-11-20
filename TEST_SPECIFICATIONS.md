# Test Specifications for 15 CRITICAL Issues

**Owner**: Lisa Park (QA Lead)
**Due Date**: Thursday, November 21, 2025
**Purpose**: Comprehensive test specifications for all 15 CRITICAL issues
**Test Framework**: xUnit + Moq + FluentAssertions
**Total New Tests**: 60 tests (4 per issue average)

---

## Table of Contents

### Security Issues (8 issues, 32 tests)
1. [Issue #1: Missing Authorization on Schema Endpoints](#issue-1-missing-authorization-on-schema-endpoints)
2. [Issue #2: Tenant Isolation Middleware Not Registered](#issue-2-tenant-isolation-middleware-not-registered)
3. [Issue #4: Hardcoded Demo Credentials](#issue-4-hardcoded-demo-credentials)
4. [Issue #9: IDOR Vulnerabilities](#issue-9-idor-vulnerabilities)
5. [Issue #11: Missing CSRF Protection](#issue-11-missing-csrf-protection)
6. [Issue #12: Weak JWT Secret Key](#issue-12-weak-jwt-secret-key)
7. [Issue #13: SignalR Hub Missing Authentication](#issue-13-signalr-hub-missing-authentication)
8. [Issue #14: Production Environment Detection Weakness](#issue-14-production-environment-detection-weakness)
9. [Issue #15: Permissive CORS Configuration](#issue-15-permissive-cors-configuration)

### Stability/Concurrency Issues (7 issues, 28 tests)
10. [Issue #3: Async/Await Blocking Call](#issue-3-asyncawait-blocking-call)
11. [Issue #5: Static Dictionary Memory Leak](#issue-5-static-dictionary-memory-leak)
12. [Issue #6: Race Condition in LoadBalanced Routing](#issue-6-race-condition-in-loadbalanced-routing)
13. [Issue #7: Division by Zero in Canary Metrics](#issue-7-division-by-zero-in-canary-metrics)
14. [Issue #8: Pipeline State Management Race Condition](#issue-8-pipeline-state-management-race-condition)
15. [Issue #10: Unchecked Rollback Failures](#issue-10-unchecked-rollback-failures)

---

## Testing Standards

### Test File Naming Convention
```
tests/HotSwap.Distributed.Tests/[Category]/[ClassName]Tests.cs
```

### Test Method Naming Convention
```csharp
[MethodName]_[StateUnderTest]_[ExpectedBehavior]
```

### AAA Pattern (Arrange-Act-Assert)
All tests MUST follow this structure:
```csharp
[Fact]
public async Task MethodName_StateUnderTest_ExpectedBehavior()
{
    // Arrange - Set up test data, mocks, and dependencies
    var mockDependency = new Mock<IDependency>();
    mockDependency.Setup(x => x.MethodAsync(It.IsAny<string>()))
        .ReturnsAsync(expectedValue);
    var sut = new SystemUnderTest(mockDependency.Object);

    // Act - Execute the method being tested
    var result = await sut.MethodAsync(input);

    // Assert - Verify expected behavior using FluentAssertions
    result.Should().NotBeNull();
    result.Property.Should().Be(expectedValue);
    mockDependency.Verify(x => x.MethodAsync(It.IsAny<string>()), Times.Once);
}
```

### FluentAssertions Usage
- Use `.Should()` instead of `Assert.*`
- Use `.Should().Be()` instead of `Assert.Equal()`
- Use `.Should().BeEquivalentTo()` for complex object comparison
- Use `.Should().Throw<TException>()` for exception testing

### Test Coverage Requirements
Each issue requires:
1. **Happy path test** - Normal successful execution
2. **Edge case test** - Boundary conditions, empty inputs, null values
3. **Error case test** - Invalid input, exceptions, failure scenarios
4. **Concurrency test** (if applicable) - Concurrent access, race conditions

---

## Issue #1: Missing Authorization on Schema Endpoints

**Code Location**: `src/HotSwap.Distributed.Api/Controllers/SchemasController.cs:191-271`

**Problem**: Schema management endpoints lack `[Authorize]` attribute, allowing unauthenticated access.

**Fix**: Add `[Authorize(Roles = "Admin,TenantAdmin")]` to all schema endpoints.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Api/SchemasControllerAuthorizationTests.cs
```

### Test 1: Verify Authorize Attribute Exists on GetSchemaAsync

```csharp
[Fact]
public void GetSchemaAsync_HasAuthorizeAttribute()
{
    // Arrange
    var method = typeof(SchemasController).GetMethod(nameof(SchemasController.GetSchemaAsync));

    // Act
    var authorizeAttr = method?.GetCustomAttributes(typeof(AuthorizeAttribute), false)
        .Cast<AuthorizeAttribute>()
        .FirstOrDefault();

    // Assert
    authorizeAttr.Should().NotBeNull("GetSchemaAsync must require authorization");
    authorizeAttr.Roles.Should().Contain("Admin")
        .And.Contain("TenantAdmin");
}
```

### Test 2: Verify Authorize Attribute Exists on CreateSchemaAsync

```csharp
[Fact]
public void CreateSchemaAsync_HasAuthorizeAttribute()
{
    // Arrange
    var method = typeof(SchemasController).GetMethod(nameof(SchemasController.CreateSchemaAsync));

    // Act
    var authorizeAttr = method?.GetCustomAttributes(typeof(AuthorizeAttribute), false)
        .Cast<AuthorizeAttribute>()
        .FirstOrDefault();

    // Assert
    authorizeAttr.Should().NotBeNull("CreateSchemaAsync must require authorization");
    authorizeAttr.Roles.Should().Contain("Admin")
        .And.Contain("TenantAdmin");
}
```

### Test 3: Verify Authorize Attribute Exists on UpdateSchemaAsync

```csharp
[Fact]
public void UpdateSchemaAsync_HasAuthorizeAttribute()
{
    // Arrange
    var method = typeof(SchemasController).GetMethod(nameof(SchemasController.UpdateSchemaAsync));

    // Act
    var authorizeAttr = method?.GetCustomAttributes(typeof(AuthorizeAttribute), false)
        .Cast<AuthorizeAttribute>()
        .FirstOrDefault();

    // Assert
    authorizeAttr.Should().NotBeNull("UpdateSchemaAsync must require authorization");
    authorizeAttr.Roles.Should().Contain("Admin")
        .And.Contain("TenantAdmin");
}
```

### Test 4: Verify Authorize Attribute Exists on DeleteSchemaAsync

```csharp
[Fact]
public void DeleteSchemaAsync_HasAuthorizeAttribute()
{
    // Arrange
    var method = typeof(SchemasController).GetMethod(nameof(SchemasController.DeleteSchemaAsync));

    // Act
    var authorizeAttr = method?.GetCustomAttributes(typeof(AuthorizeAttribute), false)
        .Cast<AuthorizeAttribute>()
        .FirstOrDefault();

    // Assert
    authorizeAttr.Should().NotBeNull("DeleteSchemaAsync must require authorization");
    authorizeAttr.Roles.Should().Contain("Admin")
        .And.Contain("TenantAdmin");
}
```

### Integration Test: Unauthenticated Request Returns 401

```csharp
[Fact]
public async Task GetSchemaAsync_WithoutAuthentication_Returns401()
{
    // Arrange
    var client = _factory.CreateClient(); // No auth token

    // Act
    var response = await client.GetAsync("/api/schemas/test-schema");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

**Total Tests for Issue #1**: 5 tests (4 unit + 1 integration)

---

## Issue #2: Tenant Isolation Middleware Not Registered

**Code Location**: `src/HotSwap.Distributed.Api/Program.cs:45-89`

**Problem**: `TenantContextMiddleware` is defined but not registered in the middleware pipeline.

**Fix**: Add `app.UseMiddleware<TenantContextMiddleware>();` after authentication middleware.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Middleware/TenantContextMiddlewareTests.cs
```

### Test 1: Middleware Extracts Tenant from Subdomain

```csharp
[Fact]
public async Task InvokeAsync_WithSubdomain_SetsTenantContext()
{
    // Arrange
    var mockTenantRepo = new Mock<ITenantRepository>();
    var mockTenantContext = new Mock<ITenantContext>();
    var tenant = new Tenant { Id = "tenant-123", Subdomain = "acme" };

    mockTenantRepo.Setup(x => x.GetBySubdomainAsync("acme", It.IsAny<CancellationToken>()))
        .ReturnsAsync(tenant);

    var middleware = new TenantContextMiddleware(
        next: (ctx) => Task.CompletedTask,
        mockTenantRepo.Object,
        mockTenantContext.Object
    );

    var context = new DefaultHttpContext();
    context.Request.Host = new HostString("acme.hotswap.io");

    // Act
    await middleware.InvokeAsync(context);

    // Assert
    mockTenantContext.Verify(x => x.SetTenant(tenant), Times.Once);
}
```

### Test 2: Middleware Extracts Tenant from Header

```csharp
[Fact]
public async Task InvokeAsync_WithTenantHeader_SetsTenantContext()
{
    // Arrange
    var mockTenantRepo = new Mock<ITenantRepository>();
    var mockTenantContext = new Mock<ITenantContext>();
    var tenant = new Tenant { Id = "tenant-456", Name = "Contoso" };

    mockTenantRepo.Setup(x => x.GetByIdAsync("tenant-456", It.IsAny<CancellationToken>()))
        .ReturnsAsync(tenant);

    var middleware = new TenantContextMiddleware(
        next: (ctx) => Task.CompletedTask,
        mockTenantRepo.Object,
        mockTenantContext.Object
    );

    var context = new DefaultHttpContext();
    context.Request.Headers["X-Tenant-ID"] = "tenant-456";

    // Act
    await middleware.InvokeAsync(context);

    // Assert
    mockTenantContext.Verify(x => x.SetTenant(tenant), Times.Once);
}
```

### Test 3: Middleware Rejects Request When No Tenant Identified

```csharp
[Fact]
public async Task InvokeAsync_WithoutTenantIdentifier_Returns400()
{
    // Arrange
    var mockTenantRepo = new Mock<ITenantRepository>();
    var mockTenantContext = new Mock<ITenantContext>();

    var middleware = new TenantContextMiddleware(
        next: (ctx) => Task.CompletedTask,
        mockTenantRepo.Object,
        mockTenantContext.Object
    );

    var context = new DefaultHttpContext();
    context.Request.Host = new HostString("hotswap.io"); // No subdomain
    context.Response.Body = new MemoryStream();

    // Act
    await middleware.InvokeAsync(context);

    // Assert
    context.Response.StatusCode.Should().Be(400);
}
```

### Test 4: Middleware Rejects Request When Tenant Not Found

```csharp
[Fact]
public async Task InvokeAsync_WithInvalidTenant_Returns404()
{
    // Arrange
    var mockTenantRepo = new Mock<ITenantRepository>();
    var mockTenantContext = new Mock<ITenantContext>();

    mockTenantRepo.Setup(x => x.GetBySubdomainAsync("unknown", It.IsAny<CancellationToken>()))
        .ReturnsAsync((Tenant?)null);

    var middleware = new TenantContextMiddleware(
        next: (ctx) => Task.CompletedTask,
        mockTenantRepo.Object,
        mockTenantContext.Object
    );

    var context = new DefaultHttpContext();
    context.Request.Host = new HostString("unknown.hotswap.io");
    context.Response.Body = new MemoryStream();

    // Act
    await middleware.InvokeAsync(context);

    // Assert
    context.Response.StatusCode.Should().Be(404);
}
```

### Integration Test: Middleware Registered in Pipeline

```csharp
[Fact]
public async Task Application_HasTenantContextMiddlewareRegistered()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act - Make request without tenant identifier
    var response = await client.GetAsync("/api/deployments");

    // Assert - Should be rejected by middleware before reaching controller
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().Contain("tenant");
}
```

**Total Tests for Issue #2**: 5 tests (4 unit + 1 integration)

---

## Issue #3: Async/Await Blocking Call

**Code Location**: `src/HotSwap.Distributed.Infrastructure/Multitenancy/TenantContextService.cs:110`

**Problem**: Using `.Result` on async method causes thread pool starvation and deadlocks.

**Fix**: Make the calling method async and use `await` instead of `.Result`.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Infrastructure/TenantContextServiceTests.cs
```

### Test 1: ExtractTenantIdAsync Returns Tenant from Subdomain

```csharp
[Fact]
public async Task ExtractTenantIdAsync_WithSubdomain_ReturnsTenantId()
{
    // Arrange
    var mockTenantRepo = new Mock<ITenantRepository>();
    var tenant = new Tenant { Id = "tenant-789", Subdomain = "fabrikam" };

    mockTenantRepo.Setup(x => x.GetBySubdomainAsync("fabrikam", It.IsAny<CancellationToken>()))
        .ReturnsAsync(tenant);

    var service = new TenantContextService(mockTenantRepo.Object);

    var context = new DefaultHttpContext();
    context.Request.Host = new HostString("fabrikam.hotswap.io");

    // Act
    var tenantId = await service.ExtractTenantIdAsync(context);

    // Assert
    tenantId.Should().Be("tenant-789");
}
```

### Test 2: ExtractTenantIdAsync Does Not Block Thread Pool

```csharp
[Fact]
public async Task ExtractTenantIdAsync_DoesNotBlockThreadPool()
{
    // Arrange
    var mockTenantRepo = new Mock<ITenantRepository>();
    var tenant = new Tenant { Id = "tenant-abc", Subdomain = "test" };

    // Simulate slow database call (500ms)
    mockTenantRepo.Setup(x => x.GetBySubdomainAsync("test", It.IsAny<CancellationToken>()))
        .Returns(async () =>
        {
            await Task.Delay(500);
            return tenant;
        });

    var service = new TenantContextService(mockTenantRepo.Object);

    var context = new DefaultHttpContext();
    context.Request.Host = new HostString("test.hotswap.io");

    // Act - Run 10 concurrent requests
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => service.ExtractTenantIdAsync(context))
        .ToArray();

    var sw = Stopwatch.StartNew();
    var results = await Task.WhenAll(tasks);
    sw.Stop();

    // Assert
    results.Should().AllBe("tenant-abc");

    // Should complete in ~500ms (parallel), not ~5000ms (sequential blocking)
    sw.ElapsedMilliseconds.Should().BeLessThan(1000,
        "async calls should run concurrently, not block thread pool");
}
```

### Test 3: Method Signature Is Async

```csharp
[Fact]
public void ExtractTenantIdAsync_IsAsyncMethod()
{
    // Arrange
    var method = typeof(TenantContextService).GetMethod(
        "ExtractTenantIdAsync",
        BindingFlags.NonPublic | BindingFlags.Instance
    );

    // Act
    var returnType = method?.ReturnType;

    // Assert
    returnType.Should().NotBeNull();
    returnType.Should().BeAssignableTo<Task<string?>>(
        "method should return Task<string?>, not string");
}
```

### Test 4: No .Result or .Wait() Calls in Method

```csharp
[Fact]
public void ExtractTenantIdAsync_DoesNotUseBlockingCalls()
{
    // Arrange
    var sourceCode = File.ReadAllText(
        "src/HotSwap.Distributed.Infrastructure/Multitenancy/TenantContextService.cs"
    );

    // Act & Assert
    sourceCode.Should().NotContain(".Result",
        "method should use await instead of .Result");
    sourceCode.Should().NotContain(".Wait()",
        "method should use await instead of .Wait()");
    sourceCode.Should().NotContain(".GetAwaiter().GetResult()",
        "method should use await instead of .GetAwaiter().GetResult()");
}
```

**Total Tests for Issue #3**: 4 tests

---

## Issue #4: Hardcoded Demo Credentials

**Code Location**: `src/HotSwap.Distributed.Infrastructure/Repositories/InMemoryUserRepository.cs:15-25`

**Problem**: Hardcoded demo credentials in source code (committed to public GitHub).

**Fix**: Remove hardcoded credentials, use configuration-based seeding only in development.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Infrastructure/InMemoryUserRepositoryTests.cs
```

### Test 1: Repository Does Not Contain Hardcoded Credentials

```csharp
[Fact]
public void InMemoryUserRepository_DoesNotContainHardcodedCredentials()
{
    // Arrange
    var sourceCode = File.ReadAllText(
        "src/HotSwap.Distributed.Infrastructure/Repositories/InMemoryUserRepository.cs"
    );

    // Act & Assert
    sourceCode.Should().NotContain("admin@demo.com",
        "hardcoded demo credentials should be removed");
    sourceCode.Should().NotContain("Demo123!",
        "hardcoded demo passwords should be removed");
}
```

### Test 2: Seeded Users Come from Configuration

```csharp
[Fact]
public async Task GetUserAsync_ReturnsConfigurationBasedUsers()
{
    // Arrange
    var mockConfig = new Mock<IConfiguration>();
    var seedUsersSection = new Mock<IConfigurationSection>();

    seedUsersSection.Setup(x => x.GetChildren())
        .Returns(new List<IConfigurationSection>
        {
            CreateUserSection("config-admin", "config@example.com", "hashed-password")
        });

    mockConfig.Setup(x => x.GetSection("SeedUsers")).Returns(seedUsersSection.Object);

    var repository = new InMemoryUserRepository(mockConfig.Object);

    // Act
    var user = await repository.GetUserAsync("config-admin", CancellationToken.None);

    // Assert
    user.Should().NotBeNull();
    user!.Username.Should().Be("config-admin");
    user.Email.Should().Be("config@example.com");
}
```

### Test 3: User Seeding Only Occurs in Development Environment

```csharp
[Fact]
public async Task Constructor_InProductionEnvironment_DoesNotSeedUsers()
{
    // Arrange
    var mockConfig = new Mock<IConfiguration>();
    var mockEnv = new Mock<IHostEnvironment>();

    mockEnv.Setup(x => x.EnvironmentName).Returns("Production");

    var repository = new InMemoryUserRepository(mockConfig.Object, mockEnv.Object);

    // Act
    var user = await repository.GetUserAsync("admin", CancellationToken.None);

    // Assert
    user.Should().BeNull("production should not have seeded users");
}
```

### Test 4: Passwords Are Hashed, Not Plain Text

```csharp
[Fact]
public async Task GetUserAsync_ReturnsUserWithHashedPassword()
{
    // Arrange
    var mockConfig = new Mock<IConfiguration>();
    var repository = new InMemoryUserRepository(mockConfig.Object);

    // Seed a user with hashed password
    var hashedPassword = BCrypt.Net.BCrypt.HashPassword("SecurePassword123!");
    var user = new User
    {
        Id = "user-1",
        Username = "testuser",
        Email = "test@example.com",
        PasswordHash = hashedPassword
    };

    await repository.CreateUserAsync(user, CancellationToken.None);

    // Act
    var retrievedUser = await repository.GetUserAsync("testuser", CancellationToken.None);

    // Assert
    retrievedUser.Should().NotBeNull();
    retrievedUser!.PasswordHash.Should().NotBe("SecurePassword123!",
        "password should be hashed, not plain text");
    retrievedUser.PasswordHash.Should().StartWith("$2",
        "should use BCrypt hashing ($2a or $2b prefix)");
}
```

**Total Tests for Issue #4**: 4 tests

---

## Issue #5: Static Dictionary Memory Leak

**Code Location**: `src/HotSwap.Distributed.Orchestrator/Services/ApprovalService.cs:24,27`

**Problem**: Static dictionaries `_approvals` and `_rejections` grow unbounded.

**Fix**: Implement cleanup mechanism or use `MemoryCache` with expiration.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Orchestrator/ApprovalServiceTests.cs
```

### Test 1: Old Approvals Are Automatically Cleaned Up

```csharp
[Fact]
public async Task RecordApproval_OldApprovalsAreCleanedUp()
{
    // Arrange
    var service = new ApprovalService();
    var deploymentId = Guid.NewGuid().ToString();

    // Record approval 31 days ago
    await service.RecordApprovalAsync(deploymentId, "approver1", DateTimeOffset.UtcNow.AddDays(-31));

    // Act - Record new approval (should trigger cleanup)
    await service.RecordApprovalAsync(deploymentId, "approver2", DateTimeOffset.UtcNow);

    // Assert - Old approval should be removed
    var approvals = await service.GetApprovalsAsync(deploymentId);
    approvals.Should().HaveCount(1);
    approvals.Should().NotContain(a => a.ApproverName == "approver1");
}
```

### Test 2: Memory Does Not Grow Unbounded

```csharp
[Fact]
public async Task RecordApproval_MemoryDoesNotGrowUnbounded()
{
    // Arrange
    var service = new ApprovalService();

    // Act - Record 10,000 approvals for different deployments
    for (int i = 0; i < 10000; i++)
    {
        var deploymentId = Guid.NewGuid().ToString();
        await service.RecordApprovalAsync(deploymentId, $"approver-{i}", DateTimeOffset.UtcNow.AddDays(-i % 35));
    }

    // Force garbage collection
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    // Assert - Should only retain approvals from last 30 days (~860 out of 10,000)
    var beforeMemory = GC.GetTotalMemory(false);

    // Record one more to trigger cleanup
    await service.RecordApprovalAsync("trigger-cleanup", "approver", DateTimeOffset.UtcNow);

    var afterMemory = GC.GetTotalMemory(true);

    // Memory should not grow significantly
    (afterMemory - beforeMemory).Should().BeLessThan(1_000_000,
        "memory should be cleaned up, not grow unbounded");
}
```

### Test 3: Cleanup Does Not Remove Recent Approvals

```csharp
[Fact]
public async Task Cleanup_DoesNotRemoveRecentApprovals()
{
    // Arrange
    var service = new ApprovalService();
    var deploymentId = Guid.NewGuid().ToString();

    // Record approvals from 1, 5, 10, 20, 29 days ago
    await service.RecordApprovalAsync(deploymentId, "approver1", DateTimeOffset.UtcNow.AddDays(-1));
    await service.RecordApprovalAsync(deploymentId, "approver2", DateTimeOffset.UtcNow.AddDays(-5));
    await service.RecordApprovalAsync(deploymentId, "approver3", DateTimeOffset.UtcNow.AddDays(-10));
    await service.RecordApprovalAsync(deploymentId, "approver4", DateTimeOffset.UtcNow.AddDays(-20));
    await service.RecordApprovalAsync(deploymentId, "approver5", DateTimeOffset.UtcNow.AddDays(-29));

    // Act - Trigger cleanup
    await service.RecordApprovalAsync(deploymentId, "approver6", DateTimeOffset.UtcNow);

    // Assert - All recent approvals (within 30 days) should still exist
    var approvals = await service.GetApprovalsAsync(deploymentId);
    approvals.Should().HaveCount(6);
    approvals.Should().Contain(a => a.ApproverName == "approver1");
    approvals.Should().Contain(a => a.ApproverName == "approver5");
}
```

### Test 4: Service Uses MemoryCache Instead of Static Dictionary

```csharp
[Fact]
public void ApprovalService_UsesMemoryCacheNotStaticDictionary()
{
    // Arrange
    var sourceCode = File.ReadAllText(
        "src/HotSwap.Distributed.Orchestrator/Services/ApprovalService.cs"
    );

    // Act & Assert
    sourceCode.Should().NotContain("static ConcurrentDictionary",
        "should use MemoryCache instead of static dictionaries");
    sourceCode.Should().Contain("IMemoryCache",
        "should use IMemoryCache for automatic expiration");
}
```

**Total Tests for Issue #5**: 4 tests

---

## Issue #6: Race Condition in LoadBalanced Routing

**Code Location**: `src/HotSwap.Distributed.Orchestrator/Routing/LoadBalancedRoutingStrategy.cs:89-96`

**Problem**: `_currentIndex` can overflow after 2 billion requests, causing negative modulo.

**Fix**: Add overflow protection and thread-safe increment.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Orchestrator/LoadBalancedRoutingStrategyTests.cs
```

### Test 1: Index Does Not Overflow After Billions of Requests

```csharp
[Fact]
public async Task RouteAsync_AfterBillionRequests_DoesNotOverflow()
{
    // Arrange
    var strategy = new LoadBalancedRoutingStrategy();
    var subscriptions = new List<TopicSubscription>
    {
        new() { Id = "sub-1", Status = SubscriptionStatus.Active },
        new() { Id = "sub-2", Status = SubscriptionStatus.Active },
        new() { Id = "sub-3", Status = SubscriptionStatus.Active }
    };

    var message = new Message { Topic = "test-topic", Payload = "data" };

    // Act - Simulate approaching int.MaxValue
    var strategy reflection = strategy;
    var currentIndexField = typeof(LoadBalancedRoutingStrategy)
        .GetField("_currentIndex", BindingFlags.NonPublic | BindingFlags.Instance);

    currentIndexField?.SetValue(strategy, int.MaxValue - 10);

    // Route 20 messages (should cross int.MaxValue and reset)
    var results = new List<TopicSubscription>();
    for (int i = 0; i < 20; i++)
    {
        var result = await strategy.RouteAsync(message, subscriptions);
        results.Add(result);
    }

    // Assert - Should not throw, should reset to 0 when approaching overflow
    results.Should().HaveCount(20);
    results.Should().AllSatisfy(r => r.Should().NotBeNull());

    var currentIndex = (long)currentIndexField!.GetValue(strategy)!;
    currentIndex.Should().BeLessThan(100, "index should reset when approaching overflow");
}
```

### Test 2: Round-Robin Distribution Is Thread-Safe

```csharp
[Fact]
public async Task RouteAsync_ConcurrentRequests_ThreadSafeDistribution()
{
    // Arrange
    var strategy = new LoadBalancedRoutingStrategy();
    var subscriptions = new List<TopicSubscription>
    {
        new() { Id = "sub-1", Status = SubscriptionStatus.Active },
        new() { Id = "sub-2", Status = SubscriptionStatus.Active },
        new() { Id = "sub-3", Status = SubscriptionStatus.Active }
    };

    var message = new Message { Topic = "test-topic", Payload = "data" };

    // Act - 1000 concurrent requests
    var tasks = Enumerable.Range(0, 1000)
        .Select(_ => strategy.RouteAsync(message, subscriptions))
        .ToArray();

    var results = await Task.WhenAll(tasks);

    // Assert - Each subscription should get ~333 messages (within 10% tolerance)
    var distribution = results.GroupBy(r => r.Id)
        .ToDictionary(g => g.Key, g => g.Count());

    distribution["sub-1"].Should().BeInRange(300, 366);
    distribution["sub-2"].Should().BeInRange(300, 366);
    distribution["sub-3"].Should().BeInRange(300, 366);
}
```

### Test 3: Index Increment Uses Lock or Interlocked

```csharp
[Fact]
public void RouteAsync_UsesThreadSafeIncrement()
{
    // Arrange
    var sourceCode = File.ReadAllText(
        "src/HotSwap.Distributed.Orchestrator/Routing/LoadBalancedRoutingStrategy.cs"
    );

    // Act & Assert
    var usesLock = sourceCode.Contains("lock (") || sourceCode.Contains("lock(");
    var usesInterlocked = sourceCode.Contains("Interlocked.Increment");

    (usesLock || usesInterlocked).Should().BeTrue(
        "index increment should use lock or Interlocked for thread safety");
}
```

### Test 4: Index Reset Logged for Observability

```csharp
[Fact]
public async Task RouteAsync_WhenIndexResets_LogsInformation()
{
    // Arrange
    var mockLogger = new Mock<ILogger<LoadBalancedRoutingStrategy>>();
    var strategy = new LoadBalancedRoutingStrategy(mockLogger.Object);
    var subscriptions = new List<TopicSubscription>
    {
        new() { Id = "sub-1", Status = SubscriptionStatus.Active }
    };

    // Set index to trigger reset
    var currentIndexField = typeof(LoadBalancedRoutingStrategy)
        .GetField("_currentIndex", BindingFlags.NonPublic | BindingFlags.Instance);
    currentIndexField?.SetValue(strategy, long.MaxValue - 500);

    var message = new Message { Topic = "test-topic", Payload = "data" };

    // Act
    await strategy.RouteAsync(message, subscriptions);

    // Assert
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Round-robin index reset")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ),
        Times.Once,
        "index reset should be logged for observability"
    );
}
```

**Total Tests for Issue #6**: 4 tests

---

## Issue #7: Division by Zero in Canary Metrics

**Code Location**: `src/HotSwap.Distributed.Orchestrator/Strategies/CanaryDeploymentStrategy.cs:204-207`

**Problem**: Division by zero when `totalRequests == 0`.

**Fix**: Add null check before division.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Orchestrator/CanaryDeploymentStrategyTests.cs
```

### Test 1: CalculateMetrics Returns Zero When No Requests

```csharp
[Fact]
public void CalculateMetrics_WithZeroRequests_ReturnsZeroErrorRate()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy();
    var metrics = new DeploymentMetrics
    {
        TotalRequests = 0,
        ErrorCount = 0
    };

    // Act
    var errorRate = strategy.CalculateErrorRate(metrics);

    // Assert
    errorRate.Should().Be(0.0, "error rate should be 0% when no requests processed");
}
```

### Test 2: CalculateMetrics Handles Edge Case of Zero Total Requests

```csharp
[Fact]
public void CalculateMetrics_WithErrorsButZeroRequests_ReturnsZero()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy();
    var metrics = new DeploymentMetrics
    {
        TotalRequests = 0,
        ErrorCount = 5 // Invalid state, but should not crash
    };

    // Act
    var errorRate = strategy.CalculateErrorRate(metrics);

    // Assert
    errorRate.Should().Be(0.0, "should handle invalid state gracefully");
}
```

### Test 3: CalculateMetrics Correct with Normal Values

```csharp
[Fact]
public void CalculateMetrics_WithNormalValues_ReturnsCorrectErrorRate()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy();
    var metrics = new DeploymentMetrics
    {
        TotalRequests = 1000,
        ErrorCount = 50
    };

    // Act
    var errorRate = strategy.CalculateErrorRate(metrics);

    // Assert
    errorRate.Should().Be(0.05, "50 errors / 1000 requests = 5% error rate");
}
```

### Test 4: No Division by Zero Possible

```csharp
[Fact]
public void CalculateMetrics_NeverThrowsDivideByZeroException()
{
    // Arrange
    var strategy = new CanaryDeploymentStrategy();
    var testCases = new[]
    {
        new DeploymentMetrics { TotalRequests = 0, ErrorCount = 0 },
        new DeploymentMetrics { TotalRequests = 0, ErrorCount = 10 },
        new DeploymentMetrics { TotalRequests = 1, ErrorCount = 0 },
        new DeploymentMetrics { TotalRequests = 1000, ErrorCount = 100 }
    };

    // Act & Assert
    foreach (var metrics in testCases)
    {
        var act = () => strategy.CalculateErrorRate(metrics);
        act.Should().NotThrow<DivideByZeroException>();
    }
}
```

**Total Tests for Issue #7**: 4 tests

---

## Issue #8: Pipeline State Management Race Condition

**Code Location**: `src/HotSwap.Distributed.Orchestrator/Pipeline/DeploymentPipeline.cs:782-836`

**Problem**: Multiple concurrent deployments can corrupt pipeline state.

**Fix**: Add distributed locking or single-writer pattern.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Orchestrator/DeploymentPipelineTests.cs
```

### Test 1: Concurrent Deployments Do Not Corrupt State

```csharp
[Fact]
public async Task ExecuteAsync_ConcurrentDeployments_DoNotCorruptState()
{
    // Arrange
    var mockLockService = new Mock<IDistributedLockService>();
    var pipeline = new DeploymentPipeline(mockLockService.Object);

    var deployment1 = new DeploymentRequest { Id = "deploy-1", ModuleId = "module-1" };
    var deployment2 = new DeploymentRequest { Id = "deploy-2", ModuleId = "module-2" };
    var deployment3 = new DeploymentRequest { Id = "deploy-3", ModuleId = "module-3" };

    // Act - Execute 3 deployments concurrently
    var tasks = new[]
    {
        pipeline.ExecuteAsync(deployment1),
        pipeline.ExecuteAsync(deployment2),
        pipeline.ExecuteAsync(deployment3)
    };

    var results = await Task.WhenAll(tasks);

    // Assert - All deployments should complete successfully
    results.Should().HaveCount(3);
    results.Should().AllSatisfy(r => r.Status.Should().BeOneOf(
        DeploymentStatus.Completed,
        DeploymentStatus.Running
    ));

    // Verify distributed lock was acquired for each deployment
    mockLockService.Verify(
        x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
        Times.Exactly(3)
    );
}
```

### Test 2: Deployment Acquires Lock Before State Modification

```csharp
[Fact]
public async Task ExecuteAsync_AcquiresLockBeforeStateModification()
{
    // Arrange
    var mockLockService = new Mock<IDistributedLockService>();
    var lockAcquired = false;
    var stateModified = false;

    mockLockService.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(() =>
        {
            lockAcquired = true;
            return new DistributedLock("lock-id");
        });

    var pipeline = new DeploymentPipeline(mockLockService.Object);
    var deployment = new DeploymentRequest { Id = "deploy-1", ModuleId = "module-1" };

    // Act
    await pipeline.ExecuteAsync(deployment);

    // Assert
    lockAcquired.Should().BeTrue("lock should be acquired before state modification");
}
```

### Test 3: Lock Is Released After Deployment Completes

```csharp
[Fact]
public async Task ExecuteAsync_ReleasesLockAfterCompletion()
{
    // Arrange
    var mockLockService = new Mock<IDistributedLockService>();
    var mockLock = new Mock<IDistributedLock>();

    mockLockService.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(mockLock.Object);

    var pipeline = new DeploymentPipeline(mockLockService.Object);
    var deployment = new DeploymentRequest { Id = "deploy-1", ModuleId = "module-1" };

    // Act
    await pipeline.ExecuteAsync(deployment);

    // Assert
    mockLock.Verify(x => x.Dispose(), Times.Once, "lock should be released after deployment");
}
```

### Test 4: Lock Is Released Even on Exception

```csharp
[Fact]
public async Task ExecuteAsync_ReleasesLockOnException()
{
    // Arrange
    var mockLockService = new Mock<IDistributedLockService>();
    var mockLock = new Mock<IDistributedLock>();
    var mockValidator = new Mock<IDeploymentValidator>();

    mockLockService.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(mockLock.Object);

    mockValidator.Setup(x => x.ValidateAsync(It.IsAny<DeploymentRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new ValidationException("Invalid deployment"));

    var pipeline = new DeploymentPipeline(mockLockService.Object, mockValidator.Object);
    var deployment = new DeploymentRequest { Id = "deploy-1", ModuleId = "module-1" };

    // Act
    var act = async () => await pipeline.ExecuteAsync(deployment);

    // Assert
    await act.Should().ThrowAsync<ValidationException>();
    mockLock.Verify(x => x.Dispose(), Times.Once,
        "lock should be released even when exception occurs");
}
```

**Total Tests for Issue #8**: 4 tests

---

## Issue #9: IDOR Vulnerabilities

**Code Location**: Multiple endpoints across `src/HotSwap.Distributed.Api/Controllers/`

**Problem**: No resource ownership validation allows users to access others' resources.

**Fix**: Add resource ownership checks in all endpoints.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Api/IDORPreventionTests.cs
```

### Test 1: User Cannot Access Another User's Deployment

```csharp
[Fact]
public async Task GetDeployment_UserCannotAccessOtherUsersDeployment()
{
    // Arrange
    var mockRepo = new Mock<IDeploymentRepository>();
    var mockTenantContext = new Mock<ITenantContext>();
    var mockUserContext = new Mock<IUserContext>();

    var deployment = new Deployment
    {
        Id = "deploy-123",
        TenantId = "tenant-1",
        CreatedBy = "user-alice"
    };

    mockRepo.Setup(x => x.GetByIdAsync("deploy-123", It.IsAny<CancellationToken>()))
        .ReturnsAsync(deployment);

    mockTenantContext.Setup(x => x.CurrentTenantId).Returns("tenant-1");
    mockUserContext.Setup(x => x.CurrentUserId).Returns("user-bob"); // Different user!

    var controller = new DeploymentsController(mockRepo.Object, mockTenantContext.Object, mockUserContext.Object);

    // Act
    var result = await controller.GetDeploymentAsync("deploy-123");

    // Assert
    result.Should().BeOfType<ForbidResult>("user-bob should not access user-alice's deployment");
}
```

### Test 2: User Can Access Their Own Deployment

```csharp
[Fact]
public async Task GetDeployment_UserCanAccessOwnDeployment()
{
    // Arrange
    var mockRepo = new Mock<IDeploymentRepository>();
    var mockTenantContext = new Mock<ITenantContext>();
    var mockUserContext = new Mock<IUserContext>();

    var deployment = new Deployment
    {
        Id = "deploy-456",
        TenantId = "tenant-1",
        CreatedBy = "user-alice"
    };

    mockRepo.Setup(x => x.GetByIdAsync("deploy-456", It.IsAny<CancellationToken>()))
        .ReturnsAsync(deployment);

    mockTenantContext.Setup(x => x.CurrentTenantId).Returns("tenant-1");
    mockUserContext.Setup(x => x.CurrentUserId).Returns("user-alice"); // Same user

    var controller = new DeploymentsController(mockRepo.Object, mockTenantContext.Object, mockUserContext.Object);

    // Act
    var result = await controller.GetDeploymentAsync("deploy-456");

    // Assert
    result.Should().BeOfType<OkObjectResult>();
    var okResult = result as OkObjectResult;
    okResult!.Value.Should().BeEquivalentTo(deployment);
}
```

### Test 3: Admin Can Access Any User's Deployment

```csharp
[Fact]
public async Task GetDeployment_AdminCanAccessAnyDeployment()
{
    // Arrange
    var mockRepo = new Mock<IDeploymentRepository>();
    var mockTenantContext = new Mock<ITenantContext>();
    var mockUserContext = new Mock<IUserContext>();

    var deployment = new Deployment
    {
        Id = "deploy-789",
        TenantId = "tenant-1",
        CreatedBy = "user-alice"
    };

    mockRepo.Setup(x => x.GetByIdAsync("deploy-789", It.IsAny<CancellationToken>()))
        .ReturnsAsync(deployment);

    mockTenantContext.Setup(x => x.CurrentTenantId).Returns("tenant-1");
    mockUserContext.Setup(x => x.CurrentUserId).Returns("admin-user");
    mockUserContext.Setup(x => x.IsInRole("Admin")).Returns(true);

    var controller = new DeploymentsController(mockRepo.Object, mockTenantContext.Object, mockUserContext.Object);

    // Act
    var result = await controller.GetDeploymentAsync("deploy-789");

    // Assert
    result.Should().BeOfType<OkObjectResult>("admin should access any deployment");
}
```

### Test 4: Cross-Tenant Access Is Blocked

```csharp
[Fact]
public async Task GetDeployment_CrossTenantAccessBlocked()
{
    // Arrange
    var mockRepo = new Mock<IDeploymentRepository>();
    var mockTenantContext = new Mock<ITenantContext>();
    var mockUserContext = new Mock<IUserContext>();

    var deployment = new Deployment
    {
        Id = "deploy-999",
        TenantId = "tenant-2", // Different tenant!
        CreatedBy = "user-alice"
    };

    mockRepo.Setup(x => x.GetByIdAsync("deploy-999", It.IsAny<CancellationToken>()))
        .ReturnsAsync(deployment);

    mockTenantContext.Setup(x => x.CurrentTenantId).Returns("tenant-1");
    mockUserContext.Setup(x => x.CurrentUserId).Returns("user-alice");

    var controller = new DeploymentsController(mockRepo.Object, mockTenantContext.Object, mockUserContext.Object);

    // Act
    var result = await controller.GetDeploymentAsync("deploy-999");

    // Assert
    result.Should().BeOfType<ForbidResult>("cross-tenant access should be blocked");
}
```

**Total Tests for Issue #9**: 4 tests (repeat similar tests for UpdateDeployment, DeleteDeployment, etc.)

---

## Issue #10: Unchecked Rollback Failures

**Code Location**: `src/HotSwap.Distributed.Orchestrator/Pipeline/DeploymentPipeline.cs:543-602`

**Problem**: Rollback failures are swallowed silently, leading to inconsistent state.

**Fix**: Throw exceptions on rollback failure, implement compensating transactions.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Orchestrator/DeploymentPipelineRollbackTests.cs
```

### Test 1: Rollback Failure Throws Exception

```csharp
[Fact]
public async Task RollbackAsync_WhenFails_ThrowsException()
{
    // Arrange
    var mockExecutor = new Mock<IDeploymentExecutor>();
    mockExecutor.Setup(x => x.RollbackAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new RollbackException("Rollback failed"));

    var pipeline = new DeploymentPipeline(mockExecutor.Object);
    var context = new DeploymentContext { DeploymentId = "deploy-1" };

    // Act
    var act = async () => await pipeline.RollbackAsync(context);

    // Assert
    await act.Should().ThrowAsync<RollbackException>()
        .WithMessage("Rollback failed");
}
```

### Test 2: Rollback Failure Is Logged

```csharp
[Fact]
public async Task RollbackAsync_WhenFails_LogsError()
{
    // Arrange
    var mockExecutor = new Mock<IDeploymentExecutor>();
    var mockLogger = new Mock<ILogger<DeploymentPipeline>>();

    mockExecutor.Setup(x => x.RollbackAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new RollbackException("Database rollback failed"));

    var pipeline = new DeploymentPipeline(mockExecutor.Object, mockLogger.Object);
    var context = new DeploymentContext { DeploymentId = "deploy-2" };

    // Act
    try
    {
        await pipeline.RollbackAsync(context);
    }
    catch { }

    // Assert
    mockLogger.Verify(
        x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rollback failed")),
            It.IsAny<RollbackException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()
        ),
        Times.Once
    );
}
```

### Test 3: Rollback Failure Updates Deployment Status

```csharp
[Fact]
public async Task RollbackAsync_WhenFails_UpdatesDeploymentStatusToFailed()
{
    // Arrange
    var mockExecutor = new Mock<IDeploymentExecutor>();
    var mockRepo = new Mock<IDeploymentRepository>();

    mockExecutor.Setup(x => x.RollbackAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new RollbackException("Rollback failed"));

    var pipeline = new DeploymentPipeline(mockExecutor.Object, mockRepo.Object);
    var context = new DeploymentContext { DeploymentId = "deploy-3" };

    // Act
    try
    {
        await pipeline.RollbackAsync(context);
    }
    catch { }

    // Assert
    mockRepo.Verify(
        x => x.UpdateStatusAsync(
            "deploy-3",
            DeploymentStatus.RollbackFailed,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()
        ),
        Times.Once,
        "deployment status should be updated to RollbackFailed"
    );
}
```

### Test 4: Compensating Transaction Runs on Rollback Failure

```csharp
[Fact]
public async Task RollbackAsync_WhenFails_RunsCompensatingTransaction()
{
    // Arrange
    var mockExecutor = new Mock<IDeploymentExecutor>();
    var mockCompensator = new Mock<ICompensatingTransactionService>();

    mockExecutor.Setup(x => x.RollbackAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new RollbackException("Rollback failed"));

    var pipeline = new DeploymentPipeline(mockExecutor.Object, mockCompensator.Object);
    var context = new DeploymentContext { DeploymentId = "deploy-4" };

    // Act
    try
    {
        await pipeline.RollbackAsync(context);
    }
    catch { }

    // Assert
    mockCompensator.Verify(
        x => x.ExecuteCompensationAsync(context, It.IsAny<CancellationToken>()),
        Times.Once,
        "compensating transaction should run when rollback fails"
    );
}
```

**Total Tests for Issue #10**: 4 tests

---

## Issue #11: Missing CSRF Protection

**Code Location**: `src/HotSwap.Distributed.Api/Program.cs:55-120`

**Problem**: No CSRF protection for state-changing operations.

**Fix**: Implement anti-forgery tokens or require custom header.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Api/CSRFProtectionTests.cs
```

### Test 1: POST Request Without X-Requested-With Header Returns 403

```csharp
[Fact]
public async Task PostDeployment_WithoutXRequestedWithHeader_Returns403()
{
    // Arrange
    var client = _factory.CreateClient();
    var deployment = new DeploymentRequest { ModuleId = "module-1" };

    // Act - POST without X-Requested-With header
    var response = await client.PostAsJsonAsync("/api/deployments", deployment);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().Contain("CSRF");
}
```

### Test 2: POST Request With X-Requested-With Header Succeeds

```csharp
[Fact]
public async Task PostDeployment_WithXRequestedWithHeader_Succeeds()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
    var deployment = new DeploymentRequest { ModuleId = "module-1" };

    // Act
    var response = await client.PostAsJsonAsync("/api/deployments", deployment);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

### Test 3: GET Request Does Not Require CSRF Token

```csharp
[Fact]
public async Task GetDeployments_WithoutCSRFToken_Succeeds()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act - GET without X-Requested-With header (should be allowed)
    var response = await client.GetAsync("/api/deployments");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Test 4: All State-Changing Endpoints Protected

```csharp
[Theory]
[InlineData("POST", "/api/deployments")]
[InlineData("PUT", "/api/deployments/deploy-1")]
[InlineData("DELETE", "/api/deployments/deploy-1")]
[InlineData("POST", "/api/schemas")]
[InlineData("PUT", "/api/schemas/schema-1")]
[InlineData("DELETE", "/api/schemas/schema-1")]
public async Task StateChangingEndpoint_WithoutCSRFProtection_Returns403(string method, string url)
{
    // Arrange
    var client = _factory.CreateClient();
    var request = new HttpRequestMessage(new HttpMethod(method), url);

    // Act
    var response = await client.SendAsync(request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
        $"{method} {url} should require CSRF protection");
}
```

**Total Tests for Issue #11**: 4 tests

---

## Issue #12: Weak JWT Secret Key

**Code Location**: `src/HotSwap.Distributed.Api/appsettings.json:45-52`

**Problem**: Weak JWT secret key "SuperSecretKey123!" in configuration.

**Fix**: Generate cryptographically secure 256-bit key, store in environment variable.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Security/JwtConfigurationTests.cs
```

### Test 1: JWT Secret Key Is At Least 256 Bits

```csharp
[Fact]
public void JwtConfiguration_SecretKeyIsAtLeast256Bits()
{
    // Arrange
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddEnvironmentVariables()
        .Build();

    // Act
    var secretKey = configuration["Jwt:SecretKey"];

    // Assert
    secretKey.Should().NotBeNullOrWhiteSpace();

    var keyBytes = Encoding.UTF8.GetBytes(secretKey!);
    keyBytes.Length.Should().BeGreaterOrEqualTo(32,
        "JWT secret key must be at least 256 bits (32 bytes)");
}
```

### Test 2: JWT Secret Key Is Not Hardcoded Weak Value

```csharp
[Fact]
public void JwtConfiguration_SecretKeyIsNotWeakDefault()
{
    // Arrange
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddEnvironmentVariables()
        .Build();

    // Act
    var secretKey = configuration["Jwt:SecretKey"];

    // Assert
    secretKey.Should().NotBe("SuperSecretKey123!");
    secretKey.Should().NotBe("YourSecretKey");
    secretKey.Should().NotBe("ChangeMe");
    secretKey.Should().NotBe("secret");
}
```

### Test 3: JWT Secret Key Comes from Environment Variable in Production

```csharp
[Fact]
public void JwtConfiguration_ProductionUsesEnvironmentVariable()
{
    // Arrange
    Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "production-secret-key-256-bits-long-base64-encoded-string-here");

    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.Production.json")
        .AddEnvironmentVariables()
        .Build();

    // Act
    var secretKey = configuration["Jwt:SecretKey"];

    // Assert
    secretKey.Should().Be("production-secret-key-256-bits-long-base64-encoded-string-here");

    // Cleanup
    Environment.SetEnvironmentVariable("JWT_SECRET_KEY", null);
}
```

### Test 4: appsettings.json Does Not Contain Production Secrets

```csharp
[Fact]
public void AppsettingsJson_DoesNotContainProductionSecrets()
{
    // Arrange
    var appsettingsContent = File.ReadAllText("src/HotSwap.Distributed.Api/appsettings.json");

    // Act & Assert
    appsettingsContent.Should().NotContain("production-secret",
        "appsettings.json should not contain production secrets");

    // Development placeholder is acceptable
    if (appsettingsContent.Contains("SecretKey"))
    {
        appsettingsContent.Should().Contain("CHANGE_ME_IN_PRODUCTION")
            .Or.Contain("REPLACE_WITH_ENV_VAR");
    }
}
```

**Total Tests for Issue #12**: 4 tests

---

## Issue #13: SignalR Hub Missing Authentication

**Code Location**: `src/HotSwap.Distributed.Api/Hubs/DeploymentHub.cs:18-95`

**Problem**: SignalR hub allows unauthenticated real-time connections.

**Fix**: Add `[Authorize]` attribute to hub class.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Api/DeploymentHubAuthenticationTests.cs
```

### Test 1: DeploymentHub Has Authorize Attribute

```csharp
[Fact]
public void DeploymentHub_HasAuthorizeAttribute()
{
    // Arrange
    var hubType = typeof(DeploymentHub);

    // Act
    var authorizeAttr = hubType.GetCustomAttribute<AuthorizeAttribute>();

    // Assert
    authorizeAttr.Should().NotBeNull("DeploymentHub must require authentication");
}
```

### Test 2: Unauthenticated Connection Is Rejected

```csharp
[Fact]
public async Task Connect_WithoutAuthentication_IsRejected()
{
    // Arrange
    var hubConnection = new HubConnectionBuilder()
        .WithUrl("http://localhost:5000/hubs/deployments")
        .Build();

    // Act
    var act = async () => await hubConnection.StartAsync();

    // Assert
    await act.Should().ThrowAsync<HttpRequestException>()
        .Where(ex => ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"));
}
```

### Test 3: Authenticated Connection Succeeds

```csharp
[Fact]
public async Task Connect_WithValidToken_Succeeds()
{
    // Arrange
    var token = GenerateValidJwtToken();

    var hubConnection = new HubConnectionBuilder()
        .WithUrl("http://localhost:5000/hubs/deployments", options =>
        {
            options.AccessTokenProvider = () => Task.FromResult(token);
        })
        .Build();

    // Act
    await hubConnection.StartAsync();

    // Assert
    hubConnection.State.Should().Be(HubConnectionState.Connected);

    // Cleanup
    await hubConnection.StopAsync();
}
```

### Test 4: Hub Methods Check Authorization

```csharp
[Fact]
public async Task SubscribeToDeploymentUpdates_ChecksAuthorization()
{
    // Arrange
    var mockClients = new Mock<IHubCallerClients>();
    var mockContext = new Mock<HubCallerContext>();

    mockContext.Setup(x => x.User).Returns((ClaimsPrincipal?)null); // No user

    var hub = new DeploymentHub
    {
        Clients = mockClients.Object,
        Context = mockContext.Object
    };

    // Act
    var act = async () => await hub.SubscribeToDeploymentUpdates("deploy-1");

    // Assert
    await act.Should().ThrowAsync<UnauthorizedAccessException>();
}
```

**Total Tests for Issue #13**: 4 tests

---

## Issue #14: Production Environment Detection Weakness

**Code Location**: `src/HotSwap.Distributed.Infrastructure/Configuration/EnvironmentDetector.cs:25-42`

**Problem**: Weak environment detection allows dev settings to leak to production.

**Fix**: Use multiple detection mechanisms and fail-secure.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Infrastructure/EnvironmentDetectorTests.cs
```

### Test 1: Production Is Detected from ASPNETCORE_ENVIRONMENT

```csharp
[Fact]
public void IsProduction_WithProductionEnvironmentVariable_ReturnsTrue()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
    var detector = new EnvironmentDetector();

    // Act
    var isProduction = detector.IsProduction();

    // Assert
    isProduction.Should().BeTrue();

    // Cleanup
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
}
```

### Test 2: Development Is Detected from ASPNETCORE_ENVIRONMENT

```csharp
[Fact]
public void IsProduction_WithDevelopmentEnvironmentVariable_ReturnsFalse()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    var detector = new EnvironmentDetector();

    // Act
    var isProduction = detector.IsProduction();

    // Assert
    isProduction.Should().BeFalse();

    // Cleanup
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
}
```

### Test 3: Unknown Environment Defaults to Production (Fail-Secure)

```csharp
[Fact]
public void IsProduction_WithUnknownEnvironment_DefaultsToProduction()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Unknown");
    var detector = new EnvironmentDetector();

    // Act
    var isProduction = detector.IsProduction();

    // Assert
    isProduction.Should().BeTrue("unknown environments should fail-secure to production mode");

    // Cleanup
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
}
```

### Test 4: No Environment Variable Defaults to Production

```csharp
[Fact]
public void IsProduction_WithoutEnvironmentVariable_DefaultsToProduction()
{
    // Arrange
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
    var detector = new EnvironmentDetector();

    // Act
    var isProduction = detector.IsProduction();

    // Assert
    isProduction.Should().BeTrue("missing environment variable should fail-secure to production mode");
}
```

**Total Tests for Issue #14**: 4 tests

---

## Issue #15: Permissive CORS Configuration

**Code Location**: `src/HotSwap.Distributed.Api/Program.cs:65-72`

**Problem**: CORS allows all origins (`AllowAnyOrigin()`).

**Fix**: Configure specific allowed origins from configuration.

### Test File Location
```
tests/HotSwap.Distributed.Tests/Api/CorsConfigurationTests.cs
```

### Test 1: CORS Does Not Allow All Origins

```csharp
[Fact]
public void CorsConfiguration_DoesNotAllowAnyOrigin()
{
    // Arrange
    var sourceCode = File.ReadAllText("src/HotSwap.Distributed.Api/Program.cs");

    // Act & Assert
    sourceCode.Should().NotContain("AllowAnyOrigin()",
        "CORS should not allow any origin in production");
}
```

### Test 2: CORS Allows Only Configured Origins

```csharp
[Fact]
public async Task CorsRequest_FromConfiguredOrigin_IsAllowed()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("Origin", "https://app.hotswap.io");

    // Act
    var response = await client.GetAsync("/api/deployments");

    // Assert
    response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    response.Headers.GetValues("Access-Control-Allow-Origin")
        .Should().Contain("https://app.hotswap.io");
}
```

### Test 3: CORS Blocks Unknown Origins

```csharp
[Fact]
public async Task CorsRequest_FromUnknownOrigin_IsBlocked()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("Origin", "https://malicious-site.com");

    // Act
    var response = await client.GetAsync("/api/deployments");

    // Assert
    response.Headers.Should().NotContainKey("Access-Control-Allow-Origin",
        "unknown origins should not receive CORS headers");
}
```

### Test 4: CORS Configuration Comes from appsettings

```csharp
[Fact]
public void CorsConfiguration_LoadsFromConfiguration()
{
    // Arrange
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();

    // Act
    var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

    // Assert
    allowedOrigins.Should().NotBeNull();
    allowedOrigins.Should().NotContain("*", "wildcard origins should not be configured");
    allowedOrigins.Should().AllSatisfy(origin =>
        origin.Should().StartWith("https://"), "all origins should use HTTPS");
}
```

**Total Tests for Issue #15**: 4 tests

---

## Test Execution Strategy

### Phase 1 (Weeks 1-3): Write Tests for CRITICAL Issues

**Week 1** (Issues 1-4):
- Monday-Tuesday: Issues 1-2 (authorization, tenant isolation) - 10 tests
- Wednesday-Thursday: Issues 3-4 (async blocking, hardcoded credentials) - 8 tests

**Week 2** (Issues 5-8):
- Monday-Tuesday: Issues 5-6 (memory leak, race condition) - 8 tests
- Wednesday-Thursday: Issues 7-8 (division by zero, pipeline state) - 8 tests

**Week 3** (Issues 9-15):
- Monday-Tuesday: Issue 9 (IDOR) - 4 tests
- Wednesday: Issue 10-11 (rollback, CSRF) - 8 tests
- Thursday: Issues 12-15 (JWT, SignalR, environment, CORS) - 16 tests

**Total**: 60 new tests by end of Week 3

### Test Coverage Goals

| Category | Before | New Tests | After | Coverage % |
|----------|--------|-----------|-------|------------|
| **Security** | 45 | 32 | 77 | 90%+ |
| **Concurrency** | 38 | 28 | 66 | 85%+ |
| **Overall** | 582 | 60 | 642 | 87%+ |

### Continuous Integration

All tests must:
- ✅ Pass in CI/CD pipeline
- ✅ Run in <30 seconds (total suite)
- ✅ No flaky tests (100% reliable)
- ✅ Follow AAA pattern
- ✅ Use FluentAssertions

### Acceptance Criteria

**Each issue's tests are complete when**:
- All 4 test cases pass
- Tests cover happy path, edge case, error case, and concurrency/security
- Tests use proper mocking (no real database/network calls)
- Tests follow naming convention
- Code coverage increases for affected code

---

## Appendix: Test Template

Use this template for all new tests:

```csharp
namespace HotSwap.Distributed.Tests.[Category];

public class [ClassName]Tests
{
    [Fact]
    public async Task [MethodName]_[StateUnderTest]_[ExpectedBehavior]()
    {
        // Arrange - Set up test data and mocks
        var mockDependency = new Mock<IDependency>();
        mockDependency.Setup(x => x.MethodAsync(It.IsAny<TParam>()))
            .ReturnsAsync(expectedResult);

        var sut = new SystemUnderTest(mockDependency.Object);

        // Act - Execute the method being tested
        var result = await sut.MethodAsync(input);

        // Assert - Verify expected behavior
        result.Should().NotBeNull();
        result.Property.Should().Be(expectedValue);

        // Verify mock interactions
        mockDependency.Verify(
            x => x.MethodAsync(It.IsAny<TParam>()),
            Times.Once
        );
    }
}
```

---

**Document Owner**: Lisa Park (QA Lead)
**Last Updated**: November 20, 2025
**Next Review**: November 27, 2025 (Phase Gate 1)

---

**Questions or clarifications?**
- Slack: #code-remediation
- QA Lead: Lisa Park (@lisa.park)
- Engineering Lead: Marcus Rodriguez (@marcus.r)
