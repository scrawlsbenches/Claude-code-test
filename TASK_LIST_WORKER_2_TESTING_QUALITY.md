# Task List - Worker Thread 2: Testing & Quality Focus

**Assigned To:** Worker Thread 2
**Focus Area:** Integration Testing, Performance Testing, Quality Assurance, Client SDKs
**Estimated Total Effort:** 8.5-10 days
**Priority:** High (Test Coverage & Quality critical for production)

---

## Delegation Prompt

You are Worker Thread 2, responsible for **testing, quality assurance, and developer experience** for the Distributed Kernel Orchestration System. Your focus is on fixing integration tests, implementing performance testing, and creating client SDKs for 3rd party developers.

### Context

This is a production-ready .NET 8.0 distributed orchestration system currently at 97% specification compliance with 582 unit tests (568 passing, 14 skipped). The integration test suite has 69 tests with only 24 passing and 45 skipped due to performance issues and test hangs. Your mission is to fix these issues and establish comprehensive quality assurance.

### Your Responsibilities

1. **Integration Test Fixes** - Investigate and fix hanging approval workflow tests
2. **Performance Optimization** - Optimize slow deployment integration tests (<15s target)
3. **Load Testing** - Create comprehensive load testing suite with k6
4. **Client SDKs** - Build TypeScript/JavaScript SDK for 3rd party integration

### Development Environment

- **Platform:** .NET 8.0 with C# 12
- **Architecture:** Clean 4-layer architecture (API → Orchestrator → Infrastructure → Domain)
- **Testing:** xUnit, Moq, FluentAssertions (TDD mandatory)
- **Build:** 582 unit tests (568 passing, 14 skipped), 0 warnings, ~18s build time
- **Integration Tests:** 24/69 passing, 45 skipped (your focus area)
- **Documentation:** CLAUDE.md (development guide), SKILLS.md (7 automated workflows)

### Critical Guidelines

**MANDATORY - Before ANY commit:**
```bash
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test
```
If any step fails → DO NOT commit. See CLAUDE.md Pre-Commit Checklist.

**Test-Driven Development (TDD) - MANDATORY:**
- 🔴 RED: Write failing test FIRST
- 🟢 GREEN: Write minimal code to pass test
- 🔵 REFACTOR: Improve code quality
- Always run `dotnet test` before committing

**Git Workflow:**
- Branch: `claude/[task-name]-[session-id]`
- Push: `git push -u origin claude/[branch-name]`
- Retry on network errors: 4 times with exponential backoff (2s, 4s, 8s, 16s)

### Use Claude Skills

Leverage automated workflows in `.claude/skills/`:
- `/tdd-helper` - Guide Red-Green-Refactor TDD workflow (use for ALL code changes)
- `/precommit-check` - Validate before commits (use before EVERY commit)
- `/test-coverage-analyzer` - Maintain 85%+ coverage (use after features)
- `/race-condition-debugger` - Debug async/await issues (CRITICAL for Task #23)

See [SKILLS.md](SKILLS.md) for complete documentation.

---

## Task #23: Investigate ApprovalWorkflow Test Hang 🟡 HIGH PRIORITY

**Status:** ⏳ Not Implemented
**Effort:** 1-2 days
**Priority:** 🟡 Medium-High (Critical for approval workflow validation)
**References:** INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md:Phase7

### Requirements

- [ ] Investigate why ApprovalWorkflowIntegrationTests hang indefinitely
- [ ] Profile test execution to identify blocking code
- [ ] Fix root cause (likely timeout, deadlock, or background service issue)
- [ ] Optimize tests to complete in <30 seconds total
- [ ] Un-skip ApprovalWorkflowIntegrationTests (7 tests)
- [ ] Verify all tests pass
- [ ] Document findings and fix

### Implementation Guidance

**Current State:**
- ApprovalWorkflowIntegrationTests exist (7 tests)
- Tests hang indefinitely (>30 seconds, killed per troubleshooting rule)
- Tests are skipped with: `[Fact(Skip = "ApprovalWorkflow tests hang - need investigation")]`

**Investigation Steps:**

1. **Run Single Test with Debugger:**
```bash
# Attach debugger to single test
dotnet test --filter "ApprovalWorkflowIntegrationTests" --logger "console;verbosity=detailed"

# Look for:
# - Where execution stops
# - Any Task.Wait() or .Result calls (deadlock suspects)
# - Background service timing issues
```

2. **Check Background Services:**
```csharp
// Potential issue: ApprovalTimeoutBackgroundService
// Problem: Background service may be waiting for approval timeout (24 hours!)
// Fix: Configure faster timeout for tests (e.g., 5 seconds)
```

3. **Profile Async/Await Patterns:**
```bash
# Use /race-condition-debugger skill to identify async issues
# Look for:
# - Missing await keywords
# - Synchronous blocking on async code (Task.Wait, .Result)
# - ConfigureAwait(false) usage preventing context continuation
```

4. **Check Approval Service Configuration:**
```csharp
// File: tests/HotSwap.Distributed.IntegrationTests/Fixtures/IntegrationTestFactory.cs
// Ensure approval timeout is configured for tests:
builder.Services.Configure<ApprovalOptions>(options =>
{
    options.ApprovalTimeout = TimeSpan.FromSeconds(5); // Test timeout (not 24 hours!)
    options.BackgroundServiceInterval = TimeSpan.FromSeconds(1); // Fast polling
});
```

**Possible Root Causes:**

1. **Approval Timeout Too Long:**
   - Production: 24 hours
   - Tests: Should be 5-10 seconds
   - Fix: Configure test-specific timeout in IntegrationTestFactory

2. **Background Service Blocking:**
   - ApprovalTimeoutBackgroundService may block test completion
   - Fix: Use shorter polling interval or mock background service in tests

3. **Deadlock in Approval Service:**
   - Synchronous wait on async code
   - Fix: Ensure all async methods use await (not .Result or .Wait)

4. **Missing Cancellation Token:**
   - Approval service may not respect test cancellation
   - Fix: Pass CancellationToken through all async methods

**Files to Investigate:**
- `tests/HotSwap.Distributed.IntegrationTests/Tests/ApprovalWorkflowIntegrationTests.cs`
- `src/HotSwap.Distributed.Infrastructure/Services/ApprovalService.cs`
- `src/HotSwap.Distributed.Api/BackgroundServices/ApprovalTimeoutBackgroundService.cs`
- `tests/HotSwap.Distributed.IntegrationTests/Fixtures/IntegrationTestFactory.cs`

### Debugging Strategy

**Use `/race-condition-debugger` skill:**
1. Identify all async methods in approval workflow
2. Check for Task.Wait(), .Result, or GetAwaiter().GetResult()
3. Verify ConfigureAwait usage
4. Check for missing cancellation token propagation

**Add Diagnostic Logging:**
```csharp
[Fact]
public async Task ApprovalWorkflow_FullCycle_CompletesSuccessfully()
{
    _output.WriteLine("Starting approval workflow test...");

    // Create deployment
    _output.WriteLine("Creating deployment...");
    var deployment = await CreateDeploymentAsync();

    // Wait for approval request
    _output.WriteLine("Waiting for approval request...");
    await WaitForApprovalRequestAsync(deployment.ExecutionId);

    // Approve deployment
    _output.WriteLine("Approving deployment...");
    await ApproveDeploymentAsync(deployment.ExecutionId);

    // Verify completion
    _output.WriteLine("Verifying deployment completion...");
    await VerifyDeploymentCompletedAsync(deployment.ExecutionId);

    _output.WriteLine("Test completed successfully");
}
```

### Acceptance Criteria

- ✅ All 7 ApprovalWorkflowIntegrationTests pass
- ✅ Tests complete in <30 seconds total (<5s per test average)
- ✅ Root cause identified and documented
- ✅ No Skip attributes remain
- ✅ Integration test count: 24 → 31 passing (+7 tests)
- ✅ No test hangs or timeouts

### Documentation Required

- Document findings in `docs/APPROVAL_WORKFLOW_TEST_FIX.md` (~200-300 lines)
- Update `INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md` with fix
- Update `TASK_LIST.md` (mark as complete)
- Update test counts in `README.md`

---

## Task #24: Optimize Slow Deployment Integration Tests 🟢 MEDIUM PRIORITY

**Status:** ⏳ Not Implemented
**Effort:** 2-3 days
**Priority:** 🟢 Medium
**References:** INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md:Phase7

### Requirements

- [ ] Optimize DeploymentStrategyIntegrationTests (9 tests)
- [ ] Optimize ConcurrentDeploymentIntegrationTests (7 tests)
- [ ] Reduce test execution time from >30s to <15s per test
- [ ] Un-skip all 16 tests
- [ ] Verify all tests pass in <5 minutes total

### Implementation Guidance

**Current State:**
- DeploymentStrategyIntegrationTests: 9 tests, all >30s each
- ConcurrentDeploymentIntegrationTests: 7 tests, all >30s each
- Tests are skipped with: `[Fact(Skip = "Deployment tests too slow - need optimization")]`

**Optimization Strategies:**

1. **Reduce Deployment Scale:**
```csharp
// BEFORE: 20 nodes per deployment (slow)
var cluster = new EnvironmentCluster("Production", 20);

// AFTER: 3-5 nodes per deployment (fast, still validates logic)
var cluster = new EnvironmentCluster("Production", 3);
```

2. **Parallelize Test Operations:**
```csharp
// BEFORE: Sequential deployments
for (int i = 0; i < 10; i++)
{
    await CreateDeploymentAsync(); // Slow
}

// AFTER: Parallel deployments
var tasks = Enumerable.Range(0, 10)
    .Select(_ => CreateDeploymentAsync());
await Task.WhenAll(tasks); // Fast
```

3. **Mock Time-Based Delays:**
```csharp
// BEFORE: Real time delays
await Task.Delay(TimeSpan.FromMinutes(5)); // 5 minutes!

// AFTER: Use testable time provider (ITimeProvider)
public interface ITimeProvider
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct = default);
}

// In tests: Mock returns immediately
mockTimeProvider.Setup(x => x.DelayAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);
```

4. **Faster Canary Wave Timeouts:**
```csharp
// Already configured in IntegrationTestFactory, but verify:
CanaryWaitDuration = TimeSpan.FromSeconds(5),  // vs 15 minutes production
StagingSmokeTestTimeout = TimeSpan.FromSeconds(10), // vs 5 minutes production
CanaryIncrementPercentage = 50, // vs 20% production (fewer waves)
```

5. **Skip Optional Pipeline Stages:**
```csharp
// For tests, disable non-essential stages:
builder.Services.Configure<PipelineOptions>(options =>
{
    options.EnableSecurityScan = false; // Skip in tests
    options.EnableValidation = false;   // Skip in tests
    options.SimulateStages = true;      // Use fast simulation
});
```

**Files to Optimize:**
- `tests/HotSwap.Distributed.IntegrationTests/Tests/DeploymentStrategyIntegrationTests.cs`
- `tests/HotSwap.Distributed.IntegrationTests/Tests/ConcurrentDeploymentIntegrationTests.cs`
- `tests/HotSwap.Distributed.IntegrationTests/Fixtures/IntegrationTestFactory.cs`
- `src/HotSwap.Distributed.Orchestrator/Strategies/*.cs` (add ITimeProvider)

**Example Optimization:**

**BEFORE (45+ seconds):**
```csharp
[Fact]
public async Task CanaryDeployment_With20Nodes_CompletesSuccessfully()
{
    var cluster = TestDataBuilder.CreateCluster(environment: "Production", nodeCount: 20);
    var deployment = TestDataBuilder.CreateDeployment(targetEnvironment: "Production");

    var result = await _orchestrator.DeployAsync(deployment); // 45+ seconds

    result.Status.Should().Be(DeploymentStatus.Completed);
}
```

**AFTER (<10 seconds):**
```csharp
[Fact]
public async Task CanaryDeployment_ValidatesIncrementalRollout()
{
    var cluster = TestDataBuilder.CreateCluster(environment: "Production", nodeCount: 3); // Reduced nodes
    var deployment = TestDataBuilder.CreateDeployment(targetEnvironment: "Production");

    var result = await _orchestrator.DeployAsync(deployment); // <10 seconds

    result.Status.Should().Be(DeploymentStatus.Completed);
    // Still validates canary logic, just with fewer nodes
}
```

### Test Execution Time Targets

| Test Suite | Current | Target | Optimization |
|------------|---------|--------|--------------|
| DeploymentStrategyIntegrationTests (9 tests) | >4 minutes | <2 minutes | 50% reduction |
| ConcurrentDeploymentIntegrationTests (7 tests) | >3 minutes | <1.5 minutes | 50% reduction |
| **Total (16 tests)** | **>7 minutes** | **<3.5 minutes** | **50% reduction** |

### Acceptance Criteria

- ✅ All 16 deployment tests pass
- ✅ Each test completes in <15 seconds
- ✅ Total execution time <5 minutes (preferably <3.5 minutes)
- ✅ Tests still validate core deployment logic
- ✅ No Skip attributes remain
- ✅ Integration test count: 31 → 47 passing (+16 tests from #23+#24)

### Documentation Required

- Document optimization strategies in `docs/INTEGRATION_TEST_OPTIMIZATION.md` (~300-400 lines)
- Update `INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md` with performance tips
- Update `TASK_LIST.md` (mark as complete)
- Update test counts and timing in `README.md`

---

## Task #10: Load Testing Suite 🟢 MEDIUM PRIORITY

**Status:** ⏳ Not Implemented
**Effort:** 2 days
**Priority:** 🟢 Low-Medium (Important for production capacity planning)
**References:** TESTING.md:236

### Requirements

- [ ] Create k6 load test scripts (JavaScript)
- [ ] Test deployment endpoint under load
- [ ] Test metrics endpoint under load
- [ ] Test concurrent deployments
- [ ] Measure API latency percentiles (p50, p95, p99)
- [ ] Identify performance bottlenecks
- [ ] Document performance characteristics
- [ ] Add load test to CI/CD (optional, may be too slow)

### Implementation Guidance

**k6 Installation:**
```bash
# macOS
brew install k6

# Linux
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

**Test Structure:**
```
tests/LoadTests/
├── scenarios/
│   ├── sustained-load.js        # 100 req/s for 10 minutes
│   ├── spike-test.js            # 0 → 500 req/s sudden spike
│   ├── soak-test.js             # 50 req/s for 1 hour
│   ├── stress-test.js           # Increase until breaking point
│   └── concurrent-deployments.js # Test deployment concurrency limits
├── helpers/
│   ├── auth.js                  # JWT token generation
│   ├── deployment-builder.js   # Create deployment requests
│   └── thresholds.js            # Performance SLA thresholds
├── run-load-tests.sh            # Convenience script
└── README.md                    # Load testing documentation
```

**Example Test Script:**

**tests/LoadTests/scenarios/sustained-load.js:**
```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';
import { getAuthToken } from '../helpers/auth.js';
import { buildDeploymentRequest } from '../helpers/deployment-builder.js';

// Custom metrics
const errorRate = new Rate('errors');

// Test configuration
export const options = {
    stages: [
        { duration: '2m', target: 100 },  // Ramp up to 100 users
        { duration: '10m', target: 100 }, // Sustained load
        { duration: '2m', target: 0 },    // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<500', 'p(99)<1000'], // 95% < 500ms, 99% < 1s
        http_req_failed: ['rate<0.01'],  // Error rate < 1%
        errors: ['rate<0.01'],
    },
};

const BASE_URL = __ENV.API_URL || 'http://localhost:5000';
let authToken;

export function setup() {
    // Get auth token once for all VUs
    return { token: getAuthToken(BASE_URL) };
}

export default function(data) {
    const params = {
        headers: {
            'Authorization': `Bearer ${data.token}`,
            'Content-Type': 'application/json',
        },
    };

    // Test 1: Create deployment
    const deploymentRequest = buildDeploymentRequest();
    const createResponse = http.post(
        `${BASE_URL}/api/v1/deployments`,
        JSON.stringify(deploymentRequest),
        params
    );

    check(createResponse, {
        'deployment created': (r) => r.status === 202,
        'response time < 500ms': (r) => r.timings.duration < 500,
    }) || errorRate.add(1);

    // Test 2: Get deployment status
    if (createResponse.status === 202) {
        const executionId = createResponse.json('executionId');
        const statusResponse = http.get(
            `${BASE_URL}/api/v1/deployments/${executionId}`,
            params
        );

        check(statusResponse, {
            'status retrieved': (r) => r.status === 200,
            'response time < 200ms': (r) => r.timings.duration < 200,
        }) || errorRate.add(1);
    }

    // Test 3: Get cluster metrics
    const metricsResponse = http.get(
        `${BASE_URL}/api/v1/clusters/Development/metrics`,
        params
    );

    check(metricsResponse, {
        'metrics retrieved': (r) => r.status === 200,
        'response time < 200ms': (r) => r.timings.duration < 200,
    }) || errorRate.add(1);

    sleep(1); // 1 request per second per VU
}
```

**Test Scenarios:**

1. **Sustained Load (sustained-load.js):**
   - 100 req/s for 10 minutes
   - Target: p95 < 500ms, p99 < 1s, error rate < 1%

2. **Spike Test (spike-test.js):**
   - 0 → 500 req/s sudden spike
   - Measure: Recovery time, error rate during spike

3. **Soak Test (soak-test.js):**
   - 50 req/s for 1 hour
   - Detect: Memory leaks, resource exhaustion

4. **Stress Test (stress-test.js):**
   - Gradually increase load until breaking point
   - Identify: Maximum capacity, failure modes

5. **Concurrent Deployments (concurrent-deployments.js):**
   - 20+ simultaneous deployments
   - Measure: Distributed lock performance, Redis contention

**Run Load Tests:**
```bash
# Sustained load
k6 run tests/LoadTests/scenarios/sustained-load.js

# Spike test
k6 run tests/LoadTests/scenarios/spike-test.js

# Soak test (long-running)
k6 run --out json=soak-results.json tests/LoadTests/scenarios/soak-test.js

# Stress test
k6 run tests/LoadTests/scenarios/stress-test.js

# Or use convenience script
./tests/LoadTests/run-load-tests.sh --scenario sustained-load
```

### Performance Targets (SLAs)

| Metric | Target | Critical Threshold |
|--------|--------|-------------------|
| p50 latency | < 200ms | < 500ms |
| p95 latency | < 500ms | < 1s |
| p99 latency | < 1s | < 2s |
| Error rate | < 0.5% | < 1% |
| Throughput | 100 req/s | 50 req/s minimum |
| Concurrent deployments | 20+ | 10+ minimum |

### Acceptance Criteria

- ✅ Load tests run successfully against API
- ✅ Performance metrics documented (p50, p95, p99 latencies)
- ✅ No memory leaks under sustained load (soak test)
- ✅ API meets SLA targets (p95 < 500ms, error rate < 1%)
- ✅ Maximum capacity identified (stress test)
- ✅ Bottlenecks documented with remediation recommendations

### Documentation Required

- Create `tests/LoadTests/README.md` (~400-600 lines)
  - Installation and setup
  - Test scenario descriptions
  - How to run load tests
  - Interpreting results
  - Performance tuning recommendations
- Create `docs/PERFORMANCE_CHARACTERISTICS.md` (~300-500 lines)
  - Documented test results
  - Latency percentiles
  - Throughput limits
  - Resource utilization
  - Scaling recommendations
- Update `TESTING.md` with load testing section
- Update `TASK_LIST.md` (mark as complete)

---

## Task #18: API Client SDKs (TypeScript/JavaScript) 🟢 MEDIUM PRIORITY

**Status:** ⏳ Not Implemented (C# example exists)
**Effort:** 3-4 days (TypeScript/JavaScript SDK)
**Priority:** 🟢 Medium (Improves developer experience for 3rd party integration)
**References:** TASK_LIST.md

### Requirements

- [ ] Create TypeScript/JavaScript SDK for Node.js and browsers
- [ ] Implement all API endpoints (Deployments, Clusters, Auth, Approvals)
- [ ] Add TypeScript type definitions
- [ ] Implement automatic retry logic with exponential backoff
- [ ] Add JWT token management (automatic refresh)
- [ ] Create comprehensive examples
- [ ] Add unit tests for SDK (Jest)
- [ ] Publish to npm registry (optional)
- [ ] Add SDK documentation

### Implementation Guidance

**SDK Structure:**
```
clients/typescript-sdk/
├── src/
│   ├── index.ts                   # Main exports
│   ├── client.ts                  # OrchestrationClient class
│   ├── auth/
│   │   ├── auth-provider.ts       # JWT token management
│   │   └── credentials.ts         # User credentials model
│   ├── deployments/
│   │   ├── deployments-client.ts  # Deployment operations
│   │   └── models.ts              # Deployment models
│   ├── clusters/
│   │   ├── clusters-client.ts     # Cluster operations
│   │   └── models.ts              # Cluster models
│   ├── approvals/
│   │   ├── approvals-client.ts    # Approval operations
│   │   └── models.ts              # Approval models
│   ├── http/
│   │   ├── http-client.ts         # Axios/fetch wrapper
│   │   ├── retry-policy.ts        # Exponential backoff
│   │   └── error-handler.ts       # API error parsing
│   └── types/
│       └── index.ts               # Shared type definitions
├── tests/
│   ├── client.test.ts             # Client tests
│   ├── deployments.test.ts        # Deployment client tests
│   └── __mocks__/                 # Mock API responses
├── examples/
│   ├── basic-usage.ts             # Getting started example
│   ├── deployment-workflow.ts     # Complete deployment lifecycle
│   ├── approval-workflow.ts       # Approval management
│   └── monitoring.ts              # Cluster monitoring
├── package.json
├── tsconfig.json
├── jest.config.js
└── README.md
```

**Example Implementation:**

**src/client.ts:**
```typescript
import { AuthProvider } from './auth/auth-provider';
import { DeploymentsClient } from './deployments/deployments-client';
import { ClustersClient } from './clusters/clusters-client';
import { ApprovalsClient } from './approvals/approvals-client';
import { HttpClient } from './http/http-client';

export interface OrchestrationClientOptions {
    baseUrl: string;
    credentials?: {
        username: string;
        password: string;
    };
    token?: string;
    retryAttempts?: number;
    timeout?: number;
}

export class OrchestrationClient {
    private httpClient: HttpClient;
    private authProvider: AuthProvider;

    public readonly deployments: DeploymentsClient;
    public readonly clusters: ClustersClient;
    public readonly approvals: ApprovalsClient;

    constructor(options: OrchestrationClientOptions) {
        this.authProvider = new AuthProvider(options.baseUrl, options.credentials);
        this.httpClient = new HttpClient({
            baseUrl: options.baseUrl,
            authProvider: this.authProvider,
            retryAttempts: options.retryAttempts ?? 3,
            timeout: options.timeout ?? 30000,
        });

        this.deployments = new DeploymentsClient(this.httpClient);
        this.clusters = new ClustersClient(this.httpClient);
        this.approvals = new ApprovalsClient(this.httpClient);
    }

    async authenticate(): Promise<void> {
        await this.authProvider.login();
    }

    setToken(token: string): void {
        this.authProvider.setToken(token);
    }
}
```

**src/deployments/deployments-client.ts:**
```typescript
import { HttpClient } from '../http/http-client';
import {
    CreateDeploymentRequest,
    DeploymentResponse,
    DeploymentStatus,
    RollbackRequest,
} from './models';

export class DeploymentsClient {
    constructor(private httpClient: HttpClient) {}

    async create(request: CreateDeploymentRequest): Promise<DeploymentResponse> {
        const response = await this.httpClient.post<DeploymentResponse>(
            '/api/v1/deployments',
            request
        );
        return response.data;
    }

    async getStatus(executionId: string): Promise<DeploymentStatus> {
        const response = await this.httpClient.get<DeploymentStatus>(
            `/api/v1/deployments/${executionId}`
        );
        return response.data;
    }

    async list(): Promise<DeploymentResponse[]> {
        const response = await this.httpClient.get<DeploymentResponse[]>(
            '/api/v1/deployments'
        );
        return response.data;
    }

    async rollback(executionId: string, request?: RollbackRequest): Promise<void> {
        await this.httpClient.post(
            `/api/v1/deployments/${executionId}/rollback`,
            request ?? {}
        );
    }

    async waitForCompletion(
        executionId: string,
        pollInterval: number = 5000,
        timeout: number = 600000
    ): Promise<DeploymentStatus> {
        const startTime = Date.now();

        while (Date.now() - startTime < timeout) {
            const status = await this.getStatus(executionId);

            if (status.status === 'Completed' || status.status === 'Failed' || status.status === 'RolledBack') {
                return status;
            }

            await this.delay(pollInterval);
        }

        throw new Error(`Deployment ${executionId} did not complete within ${timeout}ms`);
    }

    private delay(ms: number): Promise<void> {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}
```

**examples/basic-usage.ts:**
```typescript
import { OrchestrationClient } from '@hotswap/distributed-kernel-sdk';

async function main() {
    // Initialize client
    const client = new OrchestrationClient({
        baseUrl: 'https://api.example.com',
        credentials: {
            username: 'admin',
            password: 'Admin123!',
        },
    });

    // Authenticate
    await client.authenticate();

    // Create deployment
    const deployment = await client.deployments.create({
        moduleName: 'payment-processor',
        version: '2.1.0',
        targetEnvironment: 'Production',
        requesterEmail: 'user@example.com',
    });

    console.log(`Deployment created: ${deployment.executionId}`);

    // Wait for completion
    const status = await client.deployments.waitForCompletion(deployment.executionId);

    if (status.status === 'Completed') {
        console.log('Deployment completed successfully!');
    } else {
        console.error(`Deployment failed: ${status.error}`);
    }

    // Get cluster metrics
    const metrics = await client.clusters.getMetrics('Production');
    console.log(`Cluster health: ${metrics.health}`);
}

main().catch(console.error);
```

**Features to Implement:**

1. **Automatic JWT Token Management:**
   - Auto-refresh tokens before expiration
   - Handle 401 Unauthorized by re-authenticating
   - Store tokens securely (memory only, not localStorage in browser)

2. **Retry Logic with Exponential Backoff:**
   - Retry 3 times on network errors
   - Exponential backoff: 2s, 4s, 8s
   - Don't retry on 4xx errors (except 401)
   - Retry on 5xx errors

3. **Type Safety:**
   - Full TypeScript type definitions
   - Strongly-typed request/response models
   - Enum support for DeploymentStatus, Environment, etc.

4. **Error Handling:**
   - Parse API error responses
   - Custom error classes (ApiError, AuthenticationError, etc.)
   - Include request ID in errors for tracing

5. **Testing:**
   - Unit tests with Jest
   - Mock API responses
   - Test retry logic
   - Test token refresh

### Package Configuration

**package.json:**
```json
{
    "name": "@hotswap/distributed-kernel-sdk",
    "version": "1.0.0",
    "description": "TypeScript/JavaScript SDK for HotSwap Distributed Kernel Orchestration",
    "main": "dist/index.js",
    "types": "dist/index.d.ts",
    "scripts": {
        "build": "tsc",
        "test": "jest",
        "lint": "eslint src --ext .ts",
        "prepublishOnly": "npm run build && npm test"
    },
    "keywords": ["hotswap", "kernel", "orchestration", "deployment", "sdk"],
    "author": "HotSwap Team",
    "license": "MIT",
    "dependencies": {
        "axios": "^1.6.0"
    },
    "devDependencies": {
        "@types/jest": "^29.5.0",
        "@types/node": "^20.0.0",
        "@typescript-eslint/eslint-plugin": "^6.0.0",
        "@typescript-eslint/parser": "^6.0.0",
        "eslint": "^8.50.0",
        "jest": "^29.7.0",
        "ts-jest": "^29.1.0",
        "typescript": "^5.2.0"
    }
}
```

### Acceptance Criteria

- ✅ TypeScript SDK implements all API endpoints
- ✅ Full TypeScript type definitions included
- ✅ Automatic JWT token management working
- ✅ Retry logic with exponential backoff implemented
- ✅ Unit tests: 20+ tests with >80% coverage
- ✅ Comprehensive examples (4+ scenarios)
- ✅ README with installation and usage guide
- ✅ Published to npm (optional, or private registry)

### Documentation Required

- Create `clients/typescript-sdk/README.md` (~500-800 lines)
  - Installation instructions
  - Quick start guide
  - API reference (all methods)
  - Examples for each use case
  - Error handling guide
  - TypeScript usage tips
- Update `README.md` to mention TypeScript SDK availability
- Update `TASK_LIST.md` (mark TypeScript SDK as complete)

---

## Sprint Planning

### Recommended Execution Order

1. **Task #23** (1-2 days) - **START HERE** - Critical for approval workflow validation
2. **Task #24** (2-3 days) - Unblocks 16 integration tests, improves CI/CD speed
3. **Task #10** (2 days) - Performance baseline for production capacity planning
4. **Task #18** (3-4 days) - Improves developer experience for 3rd party integration

**Total:** 8.5-10 days across 4 tasks

### Dependencies

- Task #23: Requires approval workflow knowledge (Task #2 completed)
- Task #24: Depends on understanding deployment strategies (all implemented)
- Task #10: Can start immediately (no dependencies)
- Task #18: Requires stable API (all endpoints implemented)

### Success Metrics

- ✅ Integration tests: 24 → 47 passing (+23 tests from #23 and #24)
- ✅ Integration test execution time: <5 minutes (down from >7 minutes)
- ✅ Load test suite operational with documented performance SLAs
- ✅ TypeScript SDK published with >80% test coverage
- ✅ Test coverage maintained at 85%+
- ✅ Zero build warnings or errors

---

## Reference Documentation

**Essential Reading (MANDATORY):**
- [CLAUDE.md](CLAUDE.md) - Development guidelines, TDD workflow, pre-commit checklist
- [SKILLS.md](SKILLS.md) - Use `/race-condition-debugger` for Task #23
- [TESTING.md](TESTING.md) - Testing patterns and examples
- [INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md](docs/INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md) - Task #23 & #24 context

**Helpful Resources:**
- [TASK_LIST.md](TASK_LIST.md) - Master task list (update after completing tasks)
- [PROJECT_STATUS_REPORT.md](PROJECT_STATUS_REPORT.md) - Current project state
- `tests/HotSwap.Distributed.IntegrationTests/` - Integration test examples
- `examples/ApiUsageExample/` - C# API client example (reference for TypeScript SDK)

---

## Final Checklist

Before considering your work complete:

- [ ] All tasks marked complete in this file
- [ ] All tests passing: `dotnet test` (0 failures)
- [ ] Build successful: `dotnet build --no-incremental` (0 warnings)
- [ ] Pre-commit checklist completed for EVERY commit
- [ ] TDD followed for all code changes (Red-Green-Refactor)
- [ ] Test coverage maintained at 85%+ (use `/test-coverage-analyzer`)
- [ ] Documentation updated (README.md, TASK_LIST.md, new guides)
- [ ] All commits pushed to remote branch
- [ ] Integration test count updated: 24 → 47 passing
- [ ] Load test performance results documented
- [ ] TypeScript SDK published and documented
- [ ] TASK_LIST.md updated with completion status

---

**Worker Thread 2 Focus:** Testing, Quality, Performance, Developer Experience
**Start Date:** [Your session start date]
**Target Completion:** 8.5-10 days
**Questions?** See CLAUDE.md, SKILLS.md, or INTEGRATION_TEST_TROUBLESHOOTING_GUIDE.md

Good luck! Remember to use `/race-condition-debugger` for Task #23 and `/test-coverage-analyzer` throughout.
