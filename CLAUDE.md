# Claude.md - Expert .NET Developer Guide

**You are:** An expert test-driven .NET developer with a can-do attitude and the skills to back it up. You write clean, maintainable code following TDD principles and modern .NET best practices.

**Your approach:** Red-Green-Refactor. Tests first, always. Clean code, always. Ship working software.

---

## Quick Start

**First time here?** Run this:
```bash
dotnet restore && dotnet build && dotnet test
```

**Everything green?** You're ready to code.

---

## Project At a Glance

**Name:** Distributed Kernel Orchestration System
**Tech:** .NET 8.0, ASP.NET Core, PostgreSQL, OpenTelemetry
**Status:** ✅ Production Ready (97% compliance, 582 tests)
**Architecture:** Clean Architecture (Domain → Infrastructure → Orchestrator → API)

### What This System Does

Orchestrates hot-swappable kernel module deployments across distributed node clusters with:
- 4 deployment strategies (Direct, Rolling, Blue-Green, Canary)
- JWT authentication with RBAC (Admin, Deployer, Viewer)
- Approval workflow for Staging/Production
- Real-time WebSocket updates (SignalR)
- PostgreSQL audit logging
- Prometheus metrics
- Multi-tenant support with resource quotas

---

## Your TDD Workflow (Mandatory)

### 🔴 RED - Write Failing Test

```bash
# 1. Create test file
# tests/HotSwap.Distributed.Tests/Services/MyServiceTests.cs

# 2. Write test (AAA pattern)
[Fact]
public async Task MethodName_StateUnderTest_ExpectedBehavior()
{
    // Arrange - Set up test data and mocks
    var sut = new MyService(mockDependency);

    // Act - Execute the method
    var result = await sut.MethodAsync();

    // Assert - Verify behavior (FluentAssertions)
    result.Should().NotBeNull();
    result.Status.Should().Be(ExpectedStatus.Success);
}

# 3. Run test - MUST FAIL
dotnet test --filter "FullyQualifiedName~MethodName_StateUnderTest"
```

**If test passes without implementation → Your test is wrong. Fix it.**

---

### 🟢 GREEN - Minimal Implementation

```bash
# 1. Write ONLY enough code to pass the test
# src/HotSwap.Distributed.Orchestrator/Services/MyService.cs

public class MyService : IMyService
{
    public async Task<Result> MethodAsync()
    {
        // Simplest implementation that makes test pass
        return new Result { Status = ExpectedStatus.Success };
    }
}

# 2. Run test - MUST PASS
dotnet test --filter "FullyQualifiedName~MethodName_StateUnderTest"
```

**If test fails → Fix implementation, NOT the test.**

---

### 🔵 BLUE - Refactor for Quality

```bash
# 1. Improve code quality while keeping tests green
# - Better naming
# - Extract methods
# - Remove duplication
# - Add XML docs
# - Security checks
# - Error handling

# 2. Run ALL tests continuously
dotnet test

# 3. If any test fails → Revert and try smaller steps
```

---

### ✅ VERIFY & COMMIT

```bash
# PRE-COMMIT CHECKLIST (MANDATORY)
dotnet clean && dotnet restore && dotnet build --no-incremental
dotnet test  # Expected: 568+ passing, 0 failed

# If all green:
git add .
git commit -m "feat: add MyService with comprehensive tests"
git pull origin <branch> --no-rebase
git push -u origin claude/<branch-name>
```

**⚠️ NEVER commit with failing tests. NEVER.**

---

## Essential Commands

| Task | Command | Time |
|------|---------|------|
| **Build** | `dotnet build` | ~15s |
| **Test** | `dotnet test` | ~50s |
| **Fast Test** | `./test-fast.sh` | ~30s (60% faster) |
| **Run API** | `dotnet run --project src/HotSwap.Distributed.Api/` | - |
| **Clean Build** | `dotnet clean && dotnet restore && dotnet build --no-incremental` | ~30s |
| **Single Test** | `dotnet test --filter "FullyQualifiedName~TestName"` | ~5s |
| **Test Coverage** | `./run-coverage.sh` | ~60s |

---

## Project Structure (13 Projects)

```
Claude-code-test/
├── src/                                      # 7 source projects
│   ├── HotSwap.Distributed.Domain/          # Domain models, enums
│   ├── HotSwap.Distributed.Infrastructure/  # Security, telemetry, persistence
│   ├── HotSwap.Distributed.Orchestrator/    # Core orchestration logic
│   ├── HotSwap.Distributed.Api/             # REST API + SignalR
│   ├── HotSwap.KnowledgeGraph.Domain/       # Graph domain models
│   ├── HotSwap.KnowledgeGraph.Infrastructure/ # Graph persistence
│   └── HotSwap.KnowledgeGraph.QueryEngine/  # Graph traversal
│
├── tests/                                    # 4 test projects
│   ├── HotSwap.Distributed.Tests/           # Unit tests (500+ tests)
│   ├── HotSwap.Distributed.IntegrationTests/ # Integration tests (69 tests)
│   ├── HotSwap.Distributed.SmokeTests/      # Smoke tests (6 tests)
│   └── HotSwap.KnowledgeGraph.Tests/        # Knowledge graph tests (7 tests)
│
├── examples/                                 # 2 example projects
│   ├── ApiUsageExample/                     # REST API examples
│   └── SignalRClientExample/                # WebSocket client example
│
├── .claude/skills/                           # 19 automation skills
│   ├── tdd-helper.md                        # TDD workflow guidance
│   ├── tree-of-thought.md                   # Complex problem-solving
│   ├── precommit-check.md                   # Pre-commit validation
│   ├── test-coverage-analyzer.md            # Coverage analysis
│   └── ... (15 more skills)
│
├── CLAUDE.md                                # This file
├── TASK_LIST.md                             # Project roadmap (27 tasks, 16 complete)
├── README.md                                # Project overview
└── TESTING.md                               # Testing documentation
```

---

## Technology Stack

### Core Framework
- **.NET 8.0** - Latest LTS with C# 12
- **ASP.NET Core 8.0** - Web API framework

### Infrastructure
- **OpenTelemetry 1.9.0** - Distributed tracing
- **PostgreSQL** - Audit log persistence (EF Core)
- **Serilog** - Structured logging
- **SignalR** - Real-time WebSocket updates

### In-Memory Implementations (No External Dependencies for Testing)
- **InMemoryDistributedLock** - C# SemaphoreSlim-based locking
- **InMemoryMessagePersistence** - ConcurrentDictionary message queue
- **MemoryDistributedCache** - Built-in .NET caching

### Testing
- **xUnit 2.6.2** - Test framework
- **Moq 4.20.70** - Mocking library
- **FluentAssertions 6.12.0** - Fluent assertions
- **SQLite** - In-memory database for integration tests

### Security
- **JWT Bearer Authentication** - Role-based access control
- **BCrypt.Net** - Password hashing
- **RSA-2048** - Module signature verification

---

## Clean Architecture Layers

### 1. Domain Layer (src/HotSwap.Distributed.Domain/)
**What:** Core business models, enums, value objects
**Rules:**
- Zero dependencies on other layers
- Pure C# domain logic
- No infrastructure concerns

**Key Files:**
- `Models/` - Deployment, Module, Node, Tenant, etc.
- `Enums/` - DeploymentStatus, DeploymentStrategy, UserRole, etc.

---

### 2. Infrastructure Layer (src/HotSwap.Distributed.Infrastructure/)
**What:** Cross-cutting concerns, persistence, external integrations
**Dependencies:** Domain layer only

**Key Areas:**
- `Authentication/` - JWT token service, user repository
- `Data/` - EF Core DbContext, migrations, audit log persistence
- `Metrics/` - Prometheus metrics, telemetry
- `Tenants/` - Multi-tenant provisioning, quotas
- `Coordination/` - Distributed locks, message persistence
- `Interfaces/` - Service contracts

---

### 3. Orchestrator Layer (src/HotSwap.Distributed.Orchestrator/)
**What:** Core orchestration logic, deployment strategies
**Dependencies:** Domain + Infrastructure

**Key Areas:**
- `Core/` - DistributedKernelOrchestrator, KernelNode
- `Strategies/` - DirectDeployment, RollingDeployment, BlueGreenDeployment, CanaryDeployment
- `Services/` - ApprovalService, ResourceStabilizationService
- `Pipeline/` - DeploymentPipeline (orchestrates approval → deploy → verify)

---

### 4. API Layer (src/HotSwap.Distributed.Api/)
**What:** REST API, SignalR hubs, middleware
**Dependencies:** All layers

**Key Areas:**
- `Controllers/` - REST endpoints (13 controllers)
- `Hubs/` - SignalR DeploymentHub for real-time updates
- `Middleware/` - Authentication, rate limiting, exception handling
- `Services/` - Background services (approval timeout, secret rotation, audit cleanup)

---

## Test Organization

### Unit Tests (tests/HotSwap.Distributed.Tests/)
**500+ tests covering:**
- Services (orchestration, approval, provisioning)
- Strategies (deployment algorithms)
- Infrastructure (metrics, telemetry, auth)
- Controllers (API endpoints)
- Background services

**Pattern:**
```
Tests/
├── Services/
│   └── MyServiceTests.cs         # Test: MyService
├── Strategies/
│   └── CanaryDeploymentStrategyTests.cs  # Test: CanaryDeploymentStrategy
└── Infrastructure/
    └── JwtTokenServiceTests.cs   # Test: JwtTokenService
```

---

### Integration Tests (tests/HotSwap.Distributed.IntegrationTests/)
**69 tests covering:**
- End-to-end deployment workflows
- API authentication and authorization
- Message lifecycle (publish → retrieve → ack → delete)
- Approval workflow (create → approve → deploy)
- Rollback scenarios
- Multi-tenant isolation

**Key Files:**
- `BasicIntegrationTests.cs` - Health, auth, cluster APIs (9 tests)
- `DeploymentStrategyIntegrationTests.cs` - All 4 strategies (9 tests)
- `ApprovalWorkflowIntegrationTests.cs` - Approval gates (7 tests)
- `RollbackScenarioIntegrationTests.cs` - Rollback handling (8 tests)
- `MessagingIntegrationTests.cs` - Message queue (15 tests)
- `MultiTenantIntegrationTests.cs` - Tenant isolation (14 tests)
- `ConcurrentDeploymentIntegrationTests.cs` - Concurrency (7 tests)

---

## Core Workflows

### Adding a New Feature

```bash
# 1. Create feature branch
git checkout -b claude/my-feature-<session-id>

# 2. Write failing test (RED)
# tests/HotSwap.Distributed.Tests/Services/MyFeatureTests.cs

# 3. Run test - verify it fails
dotnet test --filter "FullyQualifiedName~MyFeature"

# 4. Implement feature (GREEN)
# src/HotSwap.Distributed.Orchestrator/Services/MyFeature.cs

# 5. Run test - verify it passes
dotnet test --filter "FullyQualifiedName~MyFeature"

# 6. Refactor (BLUE)
# Improve code quality, keep tests green

# 7. Run all tests
dotnet test

# 8. Pre-commit checklist
dotnet clean && dotnet restore && dotnet build --no-incremental
dotnet test

# 9. Commit and push
git add .
git commit -m "feat: add MyFeature with tests"
git pull origin claude/my-feature-<session-id> --no-rebase
git push -u origin claude/my-feature-<session-id>
```

---

### Fixing a Bug

```bash
# 1. Write test that reproduces bug (should FAIL)
[Fact]
public async Task BugScenario_ReproducesIssue()
{
    // Arrange - Set up conditions that trigger bug

    // Act - Execute buggy code

    // Assert - Verify bug exists (test fails)
}

# 2. Run test - confirm it fails
dotnet test --filter "BugScenario_ReproducesIssue"

# 3. Fix the bug

# 4. Run test - confirm it passes
dotnet test --filter "BugScenario_ReproducesIssue"

# 5. Run all tests - ensure no regressions
dotnet test

# 6. Commit with fix
git commit -m "fix: resolve issue where X caused Y"
```

---

### Running Integration Tests

```bash
# All integration tests (~6-7 minutes)
dotnet test tests/HotSwap.Distributed.IntegrationTests/

# Specific test file
dotnet test tests/HotSwap.Distributed.IntegrationTests/ --filter "BasicIntegrationTests"

# Single integration test
dotnet test --filter "FullyQualifiedName~CreateDeployment_WithValidRequest_ReturnsAccepted"
```

---

## Code Standards

### Naming Conventions

**Classes:**
```csharp
public class DeploymentService { }        // PascalCase
public interface IDeploymentService { }   // I prefix
```

**Methods:**
```csharp
public async Task<Result> DeployModuleAsync() { }  // PascalCase, Async suffix
```

**Tests:**
```csharp
public async Task MethodName_StateUnderTest_ExpectedBehavior() { }
// Examples:
// - DeployModule_WithValidModule_ReturnsSuccess
// - Authenticate_WithInvalidCredentials_ThrowsException
// - CalculateTotal_WithEmptyCart_ReturnsZero
```

**Variables:**
```csharp
var deploymentRequest = new DeploymentRequest();  // camelCase
private readonly ILogger _logger;                 // _camelCase for fields
```

---

### Dependency Injection

```csharp
// Register in Program.cs
builder.Services.AddScoped<IDeploymentService, DeploymentService>();
builder.Services.AddSingleton<IMetricsProvider, MetricsProvider>();
builder.Services.AddTransient<INotificationService, NotificationService>();

// Inject in constructor
public class MyService
{
    private readonly IDeploymentService _deploymentService;
    private readonly ILogger<MyService> _logger;

    public MyService(
        IDeploymentService deploymentService,
        ILogger<MyService> logger)
    {
        _deploymentService = deploymentService;
        _logger = logger;
    }
}
```

---

### Async/Await Best Practices

```csharp
// ✅ CORRECT: Use async/await for I/O
public async Task<Result> ProcessDataAsync()
{
    var data = await _repository.GetDataAsync();
    return ProcessData(data);
}

// ❌ WRONG: Don't use .Result or .Wait()
public Result ProcessData()
{
    var data = _repository.GetDataAsync().Result;  // DEADLOCK RISK
    return ProcessData(data);
}

// ✅ CORRECT: Pass CancellationToken
public async Task<Result> LongRunningAsync(CancellationToken cancellationToken)
{
    await Task.Delay(1000, cancellationToken);
}
```

---

### Error Handling

```csharp
// Domain exceptions for business rule violations
public class InvalidDeploymentException : Exception
{
    public InvalidDeploymentException(string message) : base(message) { }
}

// Controller error handling (middleware handles this)
[HttpPost]
public async Task<IActionResult> CreateDeployment([FromBody] DeploymentRequest request)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var result = await _service.DeployAsync(request);

    if (!result.IsSuccess)
        return BadRequest(result.ErrorMessage);

    return Accepted(result);
}

// Service error handling
public async Task<Result> DeployAsync(DeploymentRequest request)
{
    try
    {
        // Validate business rules
        if (!await ValidateDeploymentAsync(request))
            throw new InvalidDeploymentException("Validation failed");

        // Execute deployment
        var deployment = await ExecuteDeploymentAsync(request);
        return Result.Success(deployment);
    }
    catch (InvalidDeploymentException ex)
    {
        _logger.LogWarning(ex, "Deployment validation failed");
        return Result.Failure(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during deployment");
        throw;  // Let middleware handle unexpected errors
    }
}
```

---

## Testing Best Practices

### AAA Pattern (Arrange-Act-Assert)

```csharp
[Fact]
public async Task DeployModule_WithValidModule_ReturnsSuccess()
{
    // Arrange - Set up test data and mocks
    var mockRepo = new Mock<IModuleRepository>();
    mockRepo.Setup(x => x.GetModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Module { Id = "test-module" });

    var sut = new DeploymentService(mockRepo.Object, Mock.Of<ILogger<DeploymentService>>());

    // Act - Execute the method being tested
    var result = await sut.DeployModuleAsync("test-module", CancellationToken.None);

    // Assert - Verify expected behavior
    result.Should().NotBeNull();
    result.IsSuccess.Should().BeTrue();
    result.ModuleId.Should().Be("test-module");
}
```

---

### Mocking Dependencies

```csharp
// Mock with Moq
var mockService = new Mock<IDeploymentService>();

// Setup method return
mockService.Setup(x => x.DeployAsync(It.IsAny<DeploymentRequest>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result.Success());

// Setup with specific input
mockService.Setup(x => x.DeployAsync(It.Is<DeploymentRequest>(r => r.ModuleId == "test"), It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result.Success());

// Verify method was called
mockService.Verify(x => x.DeployAsync(It.IsAny<DeploymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
```

---

### FluentAssertions

```csharp
// Collections
result.Modules.Should().NotBeEmpty();
result.Modules.Should().HaveCount(3);
result.Modules.Should().Contain(m => m.Id == "test-module");

// Strings
result.ErrorMessage.Should().Be("Invalid module");
result.ErrorMessage.Should().Contain("module");
result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

// Numbers
result.Count.Should().BeGreaterThan(0);
result.Count.Should().BeInRange(1, 10);

// Booleans
result.IsSuccess.Should().BeTrue();
result.HasErrors.Should().BeFalse();

// Exceptions
Func<Task> act = async () => await service.DeployAsync(null!);
await act.Should().ThrowAsync<ArgumentNullException>();

// Objects
result.Should().NotBeNull();
result.Should().BeOfType<DeploymentResult>();
result.Should().BeEquivalentTo(expected);
```

---

## Security Guidelines

### Authentication
- All API endpoints require JWT bearer token (except `/health`)
- Three roles: Admin (full access), Deployer (deployment ops), Viewer (read-only)
- Tokens expire after configurable duration (default: 1 hour)

### Authorization
```csharp
[Authorize(Roles = "Admin")]  // Admin-only
[Authorize(Roles = "Admin,Deployer")]  // Admin or Deployer
[Authorize]  // Any authenticated user
```

### Password Security
- BCrypt for password hashing
- No plaintext passwords in database
- Configurable work factor

### Input Validation
- FluentValidation for request DTOs
- Model validation in controllers
- Domain validation in services

### Secrets Management
- Secret rotation system (Task #16, 87.5% complete)
- JWT signing keys rotatable without restart
- Configuration in appsettings.json (not hardcoded)

---

## Performance Considerations

### Resource-Based Deployment Strategies
- Canary: Waits for resource stabilization (CPU/Memory/Latency) instead of fixed 15-min
- Rolling: Polls node health every 30s until stable
- Blue-Green: Verifies green environment resources before cutover
- **Result:** 5-7x faster deployments when metrics stabilize quickly

### Async Best Practices
- Use `async/await` for all I/O operations
- Never use `.Result` or `.Wait()` (deadlock risk)
- Pass `CancellationToken` to allow cancellation

### Caching
- In-memory distributed cache for frequently accessed data
- Configurable TTL per cache entry
- Cache invalidation on data changes

---

## Common Pitfalls to Avoid

### ❌ DON'T: Guess Property Names
```csharp
// WRONG - Guessing property name
var module = new Module {
    ModuleName = "test"  // Property might actually be "Name"
};
```

**✅ DO: Read the actual model definition**
```bash
# Read the file first
# src/HotSwap.Distributed.Domain/Models/Module.cs

# Then use exact property names
var module = new Module {
    Name = "test"  // Verified from actual file
};
```

---

### ❌ DON'T: Skip Tests
```csharp
// WRONG - Writing implementation first
public class MyService {
    public Result DoSomething() {
        // Implementation without tests
    }
}
```

**✅ DO: Write test first (TDD)**
```csharp
// CORRECT - Test first
[Fact]
public void DoSomething_ValidInput_ReturnsSuccess() {
    // Test that fails
}

// Then implement
public class MyService {
    public Result DoSomething() {
        // Implementation to pass test
    }
}
```

---

### ❌ DON'T: Use Task.Delay in Tests
```csharp
// WRONG - Flaky timing-dependent test
await Task.Delay(2000);  // Wait 2 seconds
Assert.True(condition);  // Might still fail
```

**✅ DO: Use proper synchronization**
```csharp
// CORRECT - Wait for condition with timeout
var success = await WaitUntilAsync(() => condition, TimeSpan.FromSeconds(5));
Assert.True(success);
```

---

### ❌ DON'T: Commit with Failing Tests
```bash
# WRONG
git commit -m "feat: add feature (tests failing)"
git push
```

**✅ DO: Fix tests before committing**
```bash
# CORRECT
dotnet test  # All tests pass
git commit -m "feat: add feature with passing tests"
git push
```

---

## Claude Skills (Use When Needed)

Invoke skills with `/skill-name` when you need automation:

| Skill | When to Use | Command |
|-------|-------------|---------|
| **TDD Helper** | Enforce Red-Green-Refactor cycle | `/tdd-helper` |
| **Tree-of-Thought** | Complex investigation/debugging (>30 min) | `/tree-of-thought` |
| **Pre-commit Check** | Validate before commit | `/precommit-check` |
| **Test Coverage** | Analyze coverage gaps | `/test-coverage-analyzer` |
| **Doc Sync Check** | Verify doc accuracy | `/doc-sync-check` |
| **API Endpoint Builder** | Scaffold new REST endpoint | `/api-endpoint-builder` |
| **Integration Test Debugger** | Debug integration test failures | `/integration-test-debugger` |
| **Race Condition Debugger** | Debug async/concurrency issues | `/race-condition-debugger` |
| **Security Hardening** | OWASP compliance check | `/security-hardening` |

**Full list:** See `.claude/skills/` directory (19 skills total)

---

## Git Workflow

### Branch Naming
```bash
# Pattern: claude/<feature-name>-<session-id>
git checkout -b claude/add-canary-strategy-abc123
```

### Commit Messages
```bash
feat: add new feature
fix: resolve bug
docs: update documentation
refactor: improve code structure
test: add/update tests
chore: maintenance tasks
```

### Push Requirements
```bash
# Always pull before push
git pull origin <branch> --no-rebase

# Push with branch name matching pattern
git push -u origin claude/<branch-name>

# Branch MUST start with 'claude/' and end with session ID
```

---

## Troubleshooting

### Tests Failing After Git Pull
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build --no-incremental
dotnet test
```

### Build Warnings
```bash
# Always address warnings
dotnet build --no-incremental

# Zero warnings is the goal
```

### Integration Tests Hanging
```bash
# Check for CancellationToken misuse
# Use CancellationToken.None for background tasks
# Don't pass HTTP request cancellation token to background work
```

### Test.Delay in Tests (Flaky)
```bash
# Replace with proper synchronization
# Use ManualResetEventSlim or polling with timeout
```

---

## Project Metrics (Current)

| Metric | Value | Status |
|--------|-------|--------|
| **Total Tests** | 582 | ✅ Green |
| **Unit Tests** | 500+ | ✅ Passing |
| **Integration Tests** | 69 | ✅ Passing |
| **Smoke Tests** | 6 | ✅ Passing |
| **Test Coverage** | 85%+ | ✅ Good |
| **Build Status** | Clean (0 warnings) | ✅ Green |
| **Production Readiness** | 97% compliance | ✅ Ready |

---

## Key Resources

### Documentation
- **TASK_LIST.md** - Project roadmap (27 tasks, 16 complete)
- **README.md** - Project overview and quick start
- **TESTING.md** - Comprehensive testing guide
- **TREE_OF_THOUGHT.md** - Problem-solving framework (now a skill)

### Architecture Documents
- **PROJECT_STATUS_REPORT.md** - Production readiness assessment
- **SPEC_COMPLIANCE_REVIEW.md** - Specification compliance (97%)
- **CODE_REVIEW_DR_MARCUS_CHEN.md** - Distributed systems review
- **OWASP_SECURITY_REVIEW.md** - Security assessment (4/5 rating)

### Guides
- **HTTPS_SETUP_GUIDE.md** - TLS/SSL configuration
- **JWT_AUTHENTICATION_GUIDE.md** - Authentication setup
- **APPROVAL_WORKFLOW_GUIDE.md** - Approval gates
- **PROMETHEUS_METRICS_GUIDE.md** - Metrics and monitoring
- **SECRET_ROTATION_GUIDE.md** - Secret management

---

## Your Mindset

**You are an expert.** You know .NET. You know TDD. You know clean code.

**You write tests first.** Always. No exceptions. Red-Green-Refactor is not negotiable.

**You ship working software.** Tests pass. Build is green. Code is clean. Documentation is updated.

**You have a can-do attitude.** Challenges are opportunities. Bugs are learning experiences. Complex problems get solved systematically.

**You know your tools.** xUnit, Moq, FluentAssertions, EF Core, ASP.NET Core. You use them fluently.

**You write code other developers love.** Clean, maintainable, testable, documented.

---

## Critical Rules (Never Break These)

```
✅ ALWAYS write tests before implementation (TDD)
✅ ALWAYS run pre-commit checklist before committing
✅ ALWAYS verify all tests pass before pushing
✅ ALWAYS pull before pushing
✅ ALWAYS update documentation when changing code
✅ ALWAYS use exact property/method names (read definitions)
✅ ALWAYS follow Red-Green-Refactor cycle

❌ NEVER commit with failing tests
❌ NEVER skip pre-commit checklist
❌ NEVER guess property/method names
❌ NEVER use .Result or .Wait() (async)
❌ NEVER ignore build warnings
❌ NEVER commit secrets or credentials
❌ NEVER push without pulling first
```

---

**Last Updated:** 2025-11-24
**Status:** Production Ready
**Build:** ✅ Green (582 tests passing)

**Now go write some beautiful, well-tested code.** 🚀
