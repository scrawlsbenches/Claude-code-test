# .NET Build Server Implementation Task List

**Project**: Distributed .NET Build Server using HotSwap.Distributed Framework
**Based On**: BUILD_SERVER_DESIGN.md v2.0
**Total Estimated Effort**: 153 hours (~4 weeks for 1 developer)
**Status**: Not Started
**Last Updated**: 2025-11-15

---

## Overview

This task list provides a comprehensive breakdown of all tasks required to implement the .NET Build Server based on the high-level design in `BUILD_SERVER_DESIGN.md`. Tasks are organized by priority, implementation phase, and dependencies.

---

## Task Summary

| Phase | Tasks | Estimated Hours | Status |
|-------|-------|----------------|--------|
| **Phase 1: Foundation** | 8 tasks | 38 hours | ⏳ Not Started |
| **Phase 2: Core Build Logic** | 6 tasks | 32 hours | ⏳ Not Started |
| **Phase 3: Orchestration** | 7 tasks | 42 hours | ⏳ Not Started |
| **Phase 4: API Layer** | 5 tasks | 20 hours | ⏳ Not Started |
| **Phase 5: Advanced Features** | 4 tasks | 21 hours | ⏳ Not Started |
| **TOTAL** | **30 tasks** | **153 hours** | **0% Complete** |

---

## Phase 1: Foundation (Week 1)

**Goal**: Set up project structure and domain models, reuse HotSwap.Distributed infrastructure
**Duration**: 38 hours
**Prerequisites**: None

### Task 1.1: Project Structure Setup
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 4 hours
**Assigned To**: -

**Description**:
Create the complete project structure for the build server following .NET solution organization best practices.

**Requirements**:
- [ ] Create solution file: `BuildServer.sln`
- [ ] Create `src/BuildServer.Domain/` project (class library, net8.0)
- [ ] Create `src/BuildServer.Infrastructure/` project (class library, net8.0)
- [ ] Create `src/BuildServer.Orchestrator/` project (class library, net8.0)
- [ ] Create `src/BuildServer.Api/` project (ASP.NET Core Web API, net8.0)
- [ ] Create `tests/BuildServer.Tests/` project (xUnit test project, net8.0)
- [ ] Add project references between projects (Infrastructure → Domain, Orchestrator → Domain + Infrastructure, etc.)
- [ ] Add reference to existing `HotSwap.Distributed.Infrastructure` project from `BuildServer.Orchestrator`
- [ ] Configure `Directory.Build.props` for common build properties
- [ ] Add `.gitignore` entries for build server artifacts

**Acceptance Criteria**:
- Solution builds successfully with `dotnet build`
- All project references are correct
- No package restore errors
- Structure matches design in BUILD_SERVER_DESIGN.md

**References**: BUILD_SERVER_DESIGN.md (Implementation Quick Start, Step 1)

---

### Task 1.2: Domain Models - Enums
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 2 hours
**Assigned To**: -
**Depends On**: Task 1.1

**Description**:
Implement all enumeration types for the build server domain.

**Requirements**:
- [ ] Create `src/BuildServer.Domain/Enums/BuildTargetType.cs`
  - Values: Linux, Windows, MacOS, Docker
  - XML documentation for each value
- [ ] Create `src/BuildServer.Domain/Enums/BuildStrategyType.cs`
  - Values: Incremental, Clean, Cached, Distributed, Canary
  - XML documentation explaining each strategy
- [ ] Create `src/BuildServer.Domain/Enums/BuildPriority.cs`
  - Values: Low, Normal, High, Critical
- [ ] Create `src/BuildServer.Domain/Enums/BuildStageStatus.cs`
  - Values: Pending, Running, Succeeded, Failed, Skipped, Cancelled

**Acceptance Criteria**:
- All enums are in `BuildServer.Domain.Enums` namespace
- Each enum value has XML documentation
- Enums match the design specification
- Project builds without errors

**References**: BUILD_SERVER_DESIGN.md (Domain Model section)

---

### Task 1.3: Domain Models - BuildJobDescriptor
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 3 hours
**Assigned To**: -
**Depends On**: Task 1.2

**Description**:
Implement the BuildJobDescriptor class that describes a build job to be executed.

**Requirements**:
- [ ] Create `src/BuildServer.Domain/Models/BuildJobDescriptor.cs`
- [ ] Implement all required properties:
  - JobId (Guid)
  - ProjectName (required string)
  - RepositoryUrl (required string)
  - GitRef (required string)
  - SolutionPath (required string)
  - Configuration (string, default "Release")
  - TargetFramework (string?)
  - Platform (string, default "AnyCPU")
  - RuntimeIdentifier (string?)
  - BuildProperties (Dictionary<string, string>)
  - RunTests (bool, default true)
  - CreatePackages (bool, default false)
  - PublishArtifacts (bool, default true)
  - TimeoutMinutes (int, default 60)
  - Metadata (Dictionary<string, string>)
  - CreatedAt (DateTime)
  - RequesterEmail (required string)
- [ ] Add XML documentation for all properties
- [ ] Add data validation attributes where appropriate

**Acceptance Criteria**:
- Class is in `BuildServer.Domain.Models` namespace
- All properties match design specification
- Required properties use `required` keyword
- Defaults are set correctly
- XML documentation is complete

**References**: BUILD_SERVER_DESIGN.md (Domain Model - BuildJobDescriptor)

---

### Task 1.4: Domain Models - BuildRequest and BuildResult
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 4 hours
**Assigned To**: -
**Depends On**: Task 1.3

**Description**:
Implement BuildRequest, BuildResult, and related models.

**Requirements**:
- [ ] Create `src/BuildServer.Domain/Models/BuildRequest.cs`
  - Job (BuildJobDescriptor)
  - TargetPool (BuildTargetType)
  - Strategy (BuildStrategyType)
  - Priority (BuildPriority)
  - ForceClean (bool)
  - RequesterEmail (string)
  - ExecutionId (Guid)
  - CreatedAt (DateTime)
- [ ] Create `src/BuildServer.Domain/Models/BuildResult.cs`
  - JobId, ProjectName, Configuration
  - Success (bool)
  - StartTime, EndTime, Duration
  - Strategy, TargetType
  - Message, Errors, Warnings
  - Artifacts (List<BuildArtifact>)
- [ ] Create `src/BuildServer.Domain/Models/BuildExecutionResult.cs`
  - ExecutionId, ProjectName, Configuration
  - Success, StartTime, EndTime, Duration, TraceId
  - StageResults (List<BuildStageResult>)
  - Artifacts, Warnings, Errors
  - TestResults, LogUrl, ErrorMessage
- [ ] Create `src/BuildServer.Domain/Models/BuildStageResult.cs`
  - StageName, Status, StartTime, EndTime, Duration
  - Message, WarningsCount, ErrorsCount
- [ ] Create `src/BuildServer.Domain/Models/BuildArtifact.cs`
  - Name, Path, SizeBytes, ContentType
  - Sha256Hash, CreatedAt, DownloadUrl

**Acceptance Criteria**:
- All models are properly structured
- XML documentation complete
- All properties have correct types and defaults
- Models support serialization/deserialization

**References**: BUILD_SERVER_DESIGN.md (Domain Model section)

---

### Task 1.5: Infrastructure - Reference HotSwap Components
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 1 hour
**Assigned To**: -
**Depends On**: Task 1.1

**Description**:
Add project references to reuse HotSwap.Distributed.Infrastructure components.

**Requirements**:
- [ ] Add `HotSwap.Distributed.Infrastructure` project reference to `BuildServer.Orchestrator`
- [ ] Add NuGet packages to `BuildServer.Orchestrator.csproj`:
  - OpenTelemetry (1.9.0)
  - Microsoft.Extensions.Logging.Abstractions (8.0.0)
  - System.Diagnostics.DiagnosticSource (8.0.0)
- [ ] Verify `TelemetryProvider` can be instantiated
- [ ] Verify `InMemoryMetricsProvider` can be instantiated
- [ ] Verify `ModuleVerifier` can be instantiated
- [ ] Create example usage file showing how to use these components

**Acceptance Criteria**:
- Project references are correct
- All packages restore successfully
- HotSwap infrastructure components are accessible
- Example code compiles and runs
- No namespace conflicts

**References**: BUILD_SERVER_DESIGN.md (Framework Reusability Guide - Reuse As-Is)

---

### Task 1.6: Build Agent Capabilities Detection
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 8 hours
**Assigned To**: -
**Depends On**: Task 1.2

**Description**:
Implement the BuildAgentCapabilities class and detection logic for discovering agent capabilities.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Core/BuildAgentCapabilities.cs`
  - InstalledSdks (List<string>)
  - InstalledTools (List<string>)
  - MaxConcurrentBuilds (int)
  - AvailableDiskSpaceGB (long)
  - CpuCores (int)
  - TotalMemoryGB (long)
- [ ] Implement `DetectCapabilities()` method:
  - Detect installed .NET SDKs (`dotnet --list-sdks`)
  - Detect Git installation (`git --version`)
  - Detect Docker installation (`docker --version`)
  - Get available disk space (using DriveInfo)
  - Get CPU core count (Environment.ProcessorCount)
  - Get total memory (platform-specific)
- [ ] Handle cross-platform differences (Windows, Linux, macOS)
- [ ] Add error handling for missing tools
- [ ] Add logging for capability detection

**Acceptance Criteria**:
- Capabilities are detected correctly on all platforms
- Detection is fast (< 1 second)
- Missing tools are handled gracefully
- Detection is logged for debugging
- Unit tests verify detection logic

**References**: BUILD_SERVER_DESIGN.md (Implementation Quick Start - Step 5)

---

### Task 1.7: Agent Pool Infrastructure
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 6 hours
**Assigned To**: -
**Depends On**: Task 1.6

**Description**:
Implement the AgentPool class that manages a pool of build agents for a specific platform.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Core/AgentPool.cs`
- [ ] Implement properties:
  - TargetType (BuildTargetType)
  - Agents (List<BuildAgent>)
  - AgentCount (int)
- [ ] Implement methods:
  - `AddAgent(BuildAgent agent)`
  - `RemoveAgent(Guid agentId)`
  - `SelectAgent(BuildJobDescriptor job)` - selects best available agent
  - `GetHealthAsync()` - returns pool health metrics
- [ ] Implement agent selection logic:
  - Filter by required capabilities (SDKs, tools)
  - Prefer idle agents over busy ones
  - Prefer agents with lower resource usage
  - Load balancing across agents
- [ ] Implement `IAsyncDisposable` for cleanup
- [ ] Add comprehensive logging

**Acceptance Criteria**:
- Agent pool manages multiple agents
- Agent selection logic works correctly
- Health checks aggregate agent health
- Pool can be disposed properly
- Unit tests cover all selection scenarios

**References**: BUILD_SERVER_DESIGN.md (Framework Reusability Guide - Adapt: Cluster → Agent Pool)

---

### Task 1.8: Unit Tests - Domain Models
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 10 hours
**Assigned To**: -
**Depends On**: Tasks 1.2, 1.3, 1.4

**Description**:
Create comprehensive unit tests for all domain models.

**Requirements**:
- [ ] Create test fixtures for domain model testing
- [ ] Test BuildJobDescriptor:
  - Required property validation
  - Default values are correct
  - Serialization/deserialization works
  - Edge cases (null values, empty strings)
- [ ] Test BuildRequest:
  - Request creation with valid data
  - ExecutionId is generated
  - Timestamps are set correctly
- [ ] Test BuildResult:
  - Success/failure scenarios
  - Duration calculation
  - Artifact collection
- [ ] Test BuildExecutionResult:
  - Stage results aggregation
  - Overall success determination
  - Error collection
- [ ] Test all enums:
  - Can parse from string
  - ToString returns correct values
- [ ] Achieve >80% code coverage for domain models

**Acceptance Criteria**:
- All domain models have unit tests
- Test coverage >80%
- Tests follow AAA pattern (Arrange-Act-Assert)
- Tests use FluentAssertions
- All tests pass

**References**: CLAUDE.md (Testing Requirements)

---

## Phase 2: Core Build Logic (Week 2)

**Goal**: Implement BuildAgent with Git/dotnet CLI integration
**Duration**: 32 hours
**Prerequisites**: Phase 1 complete

### Task 2.1: Build Agent - Core Structure
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 4 hours
**Assigned To**: -
**Depends On**: Task 1.6

**Description**:
Create the BuildAgent class structure and basic lifecycle management.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Core/BuildAgent.cs`
- [ ] Implement properties:
  - AgentId (Guid)
  - Hostname (string)
  - TargetType (BuildTargetType)
  - Status (BuildAgentStatus enum: Idle, Building, Offline, Maintenance)
  - Capabilities (BuildAgentCapabilities)
  - LastHeartbeat (DateTime)
  - CurrentJob (BuildJobDescriptor?)
- [ ] Implement constructor with capability detection
- [ ] Create workspace directory structure
- [ ] Implement status management (Idle → Building → Idle)
- [ ] Implement heartbeat mechanism
- [ ] Add comprehensive logging

**Acceptance Criteria**:
- BuildAgent can be instantiated
- Status transitions work correctly
- Workspace directories are created
- Capabilities are detected on initialization
- Agent lifecycle is logged

**References**: BUILD_SERVER_DESIGN.md (Implementation Quick Start - Step 5)

---

### Task 2.2: Build Agent - Git Integration
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 6 hours
**Assigned To**: -
**Depends On**: Task 2.1

**Description**:
Implement Git repository cloning functionality for the build agent.

**Requirements**:
- [ ] Implement `CloneRepositoryAsync(string repoUrl, string gitRef, string workspace, CancellationToken ct)`
- [ ] Support various Git refs:
  - Branches (refs/heads/main)
  - Tags (refs/tags/v1.0.0)
  - Commit SHAs
- [ ] Use shallow clone for performance (`--depth 1`)
- [ ] Handle authentication (HTTPS with token, SSH keys)
- [ ] Implement retry logic for transient failures (3 retries, exponential backoff)
- [ ] Parse Git errors and provide meaningful error messages
- [ ] Add telemetry spans for clone operations
- [ ] Clean up on failure

**Acceptance Criteria**:
- Can clone public repositories
- Can clone private repositories with credentials
- Supports all Git ref types
- Retries on transient failures
- Errors are descriptive
- Telemetry is recorded
- Unit tests verify clone logic

**References**: BUILD_SERVER_DESIGN.md (Common Patterns - Retry Pattern)

---

### Task 2.3: Build Agent - NuGet Restore
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 4 hours
**Assigned To**: -
**Depends On**: Task 2.2

**Description**:
Implement NuGet package restoration for the build agent.

**Requirements**:
- [ ] Implement `RestoreDependenciesAsync(string workspace, string solutionPath, CancellationToken ct)`
- [ ] Execute `dotnet restore` command
- [ ] Support custom NuGet feeds (NuGet.config)
- [ ] Use locked mode when packages.lock.json exists
- [ ] Parse restore output for package counts and errors
- [ ] Implement retry logic for network failures
- [ ] Add telemetry for restore duration
- [ ] Log package count and restore time

**Acceptance Criteria**:
- Can restore packages from NuGet.org
- Can restore from private feeds (with credentials)
- Respects packages.lock.json
- Retries on network failures
- Telemetry recorded
- Errors are descriptive
- Unit tests verify restore logic

**References**: BUILD_SERVER_DESIGN.md (Build Agent Architecture)

---

### Task 2.4: Build Agent - dotnet build Integration
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 6 hours
**Assigned To**: -
**Depends On**: Task 2.3

**Description**:
Implement solution/project compilation using dotnet CLI.

**Requirements**:
- [ ] Implement `BuildSolutionAsync(string workspace, string solutionPath, string configuration, CancellationToken ct)`
- [ ] Execute `dotnet build` command with correct arguments
- [ ] Support build configurations (Debug, Release, custom)
- [ ] Support target frameworks (net8.0, net9.0, etc.)
- [ ] Support platforms (AnyCPU, x64, ARM64, etc.)
- [ ] Support MSBuild properties (`/p:Property=Value`)
- [ ] Parse build output for warnings and errors
- [ ] Collect warning/error counts and details
- [ ] Add telemetry for build duration
- [ ] Stream build output to logs in real-time

**Acceptance Criteria**:
- Can build .NET solutions and projects
- All build configurations work
- Warnings and errors are captured
- Build failures throw with descriptive errors
- Telemetry includes build duration and counts
- Unit tests verify build execution

**References**: BUILD_SERVER_DESIGN.md (Build Agent - ExecuteBuildAsync)

---

### Task 2.5: Build Agent - dotnet test Integration
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 6 hours
**Assigned To**: -
**Depends On**: Task 2.4

**Description**:
Implement test execution using dotnet test.

**Requirements**:
- [ ] Implement `RunTestsAsync(string workspace, string solutionPath, CancellationToken ct)`
- [ ] Execute `dotnet test` command
- [ ] Generate TRX test results (`--logger trx`)
- [ ] Parse test results:
  - Total tests run
  - Passed, failed, skipped counts
  - Test duration
  - Failure details
- [ ] Support test filters (`--filter`)
- [ ] Support code coverage collection (`--collect:"XPlat Code Coverage"`)
- [ ] Add telemetry for test execution
- [ ] Stream test output to logs

**Acceptance Criteria**:
- Can run xUnit/NUnit/MSTest tests
- Test results are parsed correctly
- Test failures are reported with details
- Code coverage can be collected
- Telemetry includes test counts and duration
- Unit tests verify test execution

**References**: BUILD_SERVER_DESIGN.md (Build Agent - ExecuteBuildAsync)

---

### Task 2.6: Build Agent - Artifact Packaging
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 6 hours
**Assigned To**: -
**Depends On**: Task 2.4

**Description**:
Implement artifact creation (NuGet packages, binaries, publish outputs).

**Requirements**:
- [ ] Implement `CreatePackagesAsync(string workspace, string solutionPath, CancellationToken ct)`
- [ ] Execute `dotnet pack` for NuGet packages
- [ ] Execute `dotnet publish` for deployment artifacts
- [ ] Find all generated artifacts (*.nupkg, publish directories)
- [ ] Compute SHA256 hashes for all artifacts
- [ ] Create BuildArtifact objects with metadata:
  - Name, path, size
  - Content type
  - SHA256 hash
  - Created timestamp
- [ ] Support artifact signing (future enhancement hook)
- [ ] Add telemetry for packaging duration

**Acceptance Criteria**:
- Can create NuGet packages
- Can create publish artifacts
- All artifacts have SHA256 hashes
- Artifact metadata is complete
- Telemetry recorded
- Unit tests verify artifact creation

**References**: BUILD_SERVER_DESIGN.md (Build Agent - CreatePackagesAsync)

---

## Phase 3: Orchestration (Week 3)

**Goal**: Implement strategies, pipeline, and orchestrator
**Duration**: 42 hours
**Prerequisites**: Phase 2 complete

### Task 3.1: Build Strategy Interface
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 2 hours
**Assigned To**: -
**Depends On**: Task 1.4

**Description**:
Create the IBuildStrategy interface adapted from IDeploymentStrategy.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Strategies/IBuildStrategy.cs`
- [ ] Define interface:
  ```csharp
  public interface IBuildStrategy
  {
      string StrategyName { get; }
      Task<BuildResult> BuildAsync(
          BuildJobDescriptor job,
          AgentPool agentPool,
          CancellationToken cancellationToken = default);
  }
  ```
- [ ] Add XML documentation
- [ ] Follow same pattern as `IDeploymentStrategy` from HotSwap

**Acceptance Criteria**:
- Interface is defined correctly
- XML documentation complete
- Matches design specification
- Ready for strategy implementations

**References**: BUILD_SERVER_DESIGN.md (Framework Reusability Guide - Adapt: Strategy Interface)

---

### Task 3.2: Incremental Build Strategy
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 8 hours
**Assigned To**: -
**Depends On**: Tasks 2.6, 3.1

**Description**:
Implement the IncrementalBuildStrategy for fast iterative builds.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Strategies/IncrementalBuildStrategy.cs`
- [ ] Implement `IBuildStrategy` interface
- [ ] Strategy logic:
  - Select one idle agent from pool
  - Execute build on selected agent
  - Return build result
- [ ] Follow pattern from `DirectDeploymentStrategy` in HotSwap
- [ ] Add telemetry spans for strategy execution
- [ ] Add metrics for build duration
- [ ] Implement error handling and retry logic
- [ ] Add comprehensive logging

**Acceptance Criteria**:
- Strategy executes builds on single agent
- Success/failure handled correctly
- Telemetry and metrics recorded
- Error handling works
- Unit tests cover happy path and errors
- Integration test with real agent

**References**: BUILD_SERVER_DESIGN.md (Implementation Quick Start - Step 4)

---

### Task 3.3: Clean Build Strategy
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 8 hours
**Assigned To**: -
**Depends On**: Task 3.2

**Description**:
Implement the CleanBuildStrategy for reproducible builds from scratch.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Strategies/CleanBuildStrategy.cs`
- [ ] Implement `IBuildStrategy` interface
- [ ] Strategy logic:
  - Select one idle agent
  - Execute `dotnet clean` before build
  - Force fresh NuGet restore (`--force`)
  - Build with `--no-incremental` flag
  - Run full test suite
  - Package artifacts
- [ ] Add telemetry and metrics
- [ ] Implement error handling
- [ ] Add logging

**Acceptance Criteria**:
- Strategy cleans before building
- Fresh restore is performed
- No incremental compilation used
- Full test suite runs
- Telemetry and metrics recorded
- Unit tests verify clean build behavior

**References**: BUILD_SERVER_DESIGN.md (Build Strategies - Clean Build Strategy)

---

### Task 3.4: Cached Build Strategy
**Priority**: 🟢 Medium
**Status**: ⏳ Not Started
**Effort**: 12 hours
**Assigned To**: -
**Depends On**: Task 3.2

**Description**:
Implement the CachedBuildStrategy with Redis caching for maximum performance.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Strategies/CachedBuildStrategy.cs`
- [ ] Implement `IBuildStrategy` interface
- [ ] Implement cache key generation:
  - Hash of source files
  - Hash of project file
  - Hash of packages.lock.json
  - Hash of dependencies
  - Hash of build properties
- [ ] Strategy logic:
  - Compute input hashes for each project
  - Check Redis cache for outputs
  - Restore cached outputs if hash matches
  - Build only projects with cache miss
  - Store new outputs in cache
  - Skip tests if results cached and inputs unchanged
- [ ] Add cache hit/miss metrics
- [ ] Add telemetry for cache operations
- [ ] Implement cache expiration (7 days)

**Acceptance Criteria**:
- Cache key generation is correct and deterministic
- Cache hits restore outputs correctly
- Cache misses trigger builds
- Cache metrics tracked (hit rate, size)
- Telemetry recorded
- Unit tests verify caching logic
- Integration test with Redis

**References**: BUILD_SERVER_DESIGN.md (Build Strategies - Cached Build Strategy)

---

### Task 3.5: Build Pipeline
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 12 hours
**Assigned To**: -
**Depends On**: Task 3.2

**Description**:
Implement the BuildPipeline class that orchestrates multi-stage build execution.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Pipeline/BuildPipeline.cs`
- [ ] Follow pattern from `DeploymentPipeline` in HotSwap
- [ ] Implement pipeline stages:
  1. Validation - validate build request
  2. Agent Selection - select appropriate agent pool and agent
  3. Strategy Execution - execute build using selected strategy
  4. Result Aggregation - aggregate results from all stages
- [ ] Implement `ExecutePipelineAsync(BuildRequest request, Dictionary<BuildTargetType, AgentPool> pools, CancellationToken ct)`
- [ ] Add telemetry spans for each stage
- [ ] Add metrics for stage durations
- [ ] Implement error handling with rollback/cleanup
- [ ] Add comprehensive logging
- [ ] Implement `IDisposable`

**Acceptance Criteria**:
- Pipeline executes all stages in order
- Each stage has telemetry
- Errors are handled gracefully
- Results are aggregated correctly
- Pipeline can be disposed
- Unit tests verify pipeline flow
- Integration test with real strategies

**References**: BUILD_SERVER_DESIGN.md (Framework Reusability Guide - Adapt: Pipeline)

---

### Task 3.6: Build Orchestrator
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 16 hours
**Assigned To**: -
**Depends On**: Tasks 1.7, 3.5

**Description**:
Implement the BuildOrchestrator as the central coordinator for build operations.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Core/BuildOrchestrator.cs`
- [ ] Follow pattern from `DistributedKernelOrchestrator` in HotSwap
- [ ] Implement dependency injection:
  - ILogger<BuildOrchestrator>
  - ILoggerFactory
  - IMetricsProvider (from HotSwap)
  - TelemetryProvider (from HotSwap)
- [ ] Implement properties:
  - Agent pools dictionary (BuildTargetType → AgentPool)
  - Strategies dictionary (BuildStrategyType → IBuildStrategy)
  - Build pipeline instance
- [ ] Implement `InitializeAsync()`:
  - Create agent pools for each target type
  - Create sample agents in each pool
  - Initialize all build strategies
  - Initialize build pipeline
- [ ] Implement `ExecuteBuildPipelineAsync(BuildRequest request, CancellationToken ct)`:
  - Validate orchestrator is initialized
  - Start telemetry activity
  - Execute pipeline
  - Record metrics
  - Return build result
- [ ] Implement `GetAgentPool(BuildTargetType type)`
- [ ] Implement `IAsyncDisposable` for cleanup

**Acceptance Criteria**:
- Orchestrator initializes successfully
- Agent pools are created with agents
- Strategies are registered
- Build execution works end-to-end
- Telemetry and metrics recorded
- Orchestrator can be disposed
- Unit tests verify initialization and execution
- Integration test with real builds

**References**: BUILD_SERVER_DESIGN.md (Implementation Quick Start - Step 6)

---

### Task 3.7: Unit Tests - Strategies and Orchestration
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 12 hours
**Assigned To**: -
**Depends On**: Tasks 3.2, 3.3, 3.5, 3.6

**Description**:
Create comprehensive unit tests for strategies, pipeline, and orchestrator.

**Requirements**:
- [ ] Test IncrementalBuildStrategy:
  - Happy path (successful build)
  - Agent selection failure
  - Build failure on agent
  - Cancellation handling
- [ ] Test CleanBuildStrategy:
  - Clean build execution
  - Clean failures
  - Fresh restore behavior
- [ ] Test CachedBuildStrategy:
  - Cache hit scenario
  - Cache miss scenario
  - Cache key generation
  - Cache expiration
- [ ] Test BuildPipeline:
  - All stages execute in order
  - Stage failures handled
  - Telemetry recorded
  - Metrics recorded
- [ ] Test BuildOrchestrator:
  - Initialization success
  - Agent pool creation
  - Strategy registration
  - Build execution end-to-end
  - Disposal
- [ ] Mock dependencies (agents, pools, HotSwap components)
- [ ] Use FluentAssertions for readable tests
- [ ] Achieve >80% code coverage

**Acceptance Criteria**:
- All strategies have unit tests
- Pipeline has unit tests
- Orchestrator has unit tests
- Test coverage >80%
- Tests use AAA pattern
- All tests pass

**References**: CLAUDE.md (Test-Driven Development)

---

## Phase 4: API Layer (Week 4)

**Goal**: REST API with authentication and Swagger
**Duration**: 20 hours
**Prerequisites**: Phase 3 complete

### Task 4.1: API Models and Validation
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 4 hours
**Assigned To**: -
**Depends On**: Task 1.4

**Description**:
Create API request/response models and validation logic.

**Requirements**:
- [ ] Create `src/BuildServer.Api/Models/ApiModels.cs` with:
  - `CreateBuildRequest` (ProjectName, RepositoryUrl, GitRef, SolutionPath, Configuration, TargetPool, Strategy, RunTests, CreatePackages, RequesterEmail)
  - `BuildResponse` (ExecutionId, Status, StartTime, EstimatedDuration, TraceId, Links)
  - `BuildStatusResponse` (ExecutionId, ProjectName, Status, CurrentStage, StartTime, Duration, Stages, Agent)
  - `ErrorResponse` (Error, Details)
  - `AgentResponse` (AgentId, Hostname, TargetType, Status, CpuUsage, MemoryUsage, Capabilities)
- [ ] Create `src/BuildServer.Api/Validation/BuildRequestValidator.cs`
- [ ] Implement validation logic:
  - ProjectName is required
  - RepositoryUrl is required and valid URL
  - GitRef is required
  - SolutionPath is required
  - RequesterEmail is required and valid email
- [ ] Add data annotations to models
- [ ] Add XML documentation

**Acceptance Criteria**:
- All API models defined
- Validation logic works
- Invalid requests are rejected
- XML documentation complete
- Models support JSON serialization

**References**: BUILD_SERVER_DESIGN.md (API Design)

---

### Task 4.2: BuildsController
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 8 hours
**Assigned To**: -
**Depends On**: Tasks 3.6, 4.1

**Description**:
Implement the BuildsController for build management operations.

**Requirements**:
- [ ] Create `src/BuildServer.Api/Controllers/BuildsController.cs`
- [ ] Inject `BuildOrchestrator` dependency
- [ ] Implement endpoints:
  - `POST /api/v1/builds` - Create and queue build
    - Validate request
    - Create BuildRequest
    - Start build asynchronously
    - Return 202 Accepted with execution ID
  - `GET /api/v1/builds/{executionId}` - Get build status
    - Return build status and progress
    - Include stage results
    - Return 404 if not found
  - `GET /api/v1/builds` - List recent builds
    - Return last 50 builds
    - Support pagination
  - `POST /api/v1/builds/{executionId}/cancel` - Cancel build
    - Cancel running build
    - Return 200 OK
  - `GET /api/v1/builds/{executionId}/logs` - Get build logs
    - Stream or return logs
- [ ] Add proper HTTP status codes
- [ ] Add OpenAPI/Swagger attributes
- [ ] Add error handling middleware integration
- [ ] Add request/response logging

**Acceptance Criteria**:
- All endpoints work correctly
- Proper HTTP status codes
- Swagger documentation complete
- Error handling works
- Unit tests for controller actions
- Integration tests with orchestrator

**References**: BUILD_SERVER_DESIGN.md (API Design - BuildsController)

---

### Task 4.3: AgentsController
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 4 hours
**Assigned To**: -
**Depends On**: Task 3.6

**Description**:
Implement the AgentsController for agent management and monitoring.

**Requirements**:
- [ ] Create `src/BuildServer.Api/Controllers/AgentsController.cs`
- [ ] Inject `BuildOrchestrator` dependency
- [ ] Implement endpoints:
  - `GET /api/v1/agents` - List all agents across all pools
  - `GET /api/v1/agents/{agentId}` - Get specific agent details
  - `GET /api/v1/agents/{agentId}/health` - Get agent health check
  - `POST /api/v1/agents/{agentId}/maintenance` - Put agent in maintenance mode
  - `DELETE /api/v1/agents/{agentId}` - Remove agent from pool
- [ ] Add Swagger documentation
- [ ] Add error handling

**Acceptance Criteria**:
- All endpoints work
- Agent information is accurate
- Health checks return correct status
- Swagger documentation complete
- Unit tests for controller

**References**: BUILD_SERVER_DESIGN.md (API Design - AgentsController)

---

### Task 4.4: API Startup Configuration
**Priority**: 🔴 Critical
**Status**: ⏳ Not Started
**Effort**: 4 hours
**Assigned To**: -
**Depends On**: Tasks 4.2, 4.3

**Description**:
Configure ASP.NET Core application with DI, telemetry, and middleware.

**Requirements**:
- [ ] Update `src/BuildServer.Api/Program.cs`
- [ ] Configure dependency injection:
  - Register `TelemetryProvider` as singleton (from HotSwap)
  - Register `IMetricsProvider` as singleton (from HotSwap)
  - Register `BuildOrchestrator` as singleton
- [ ] Configure OpenTelemetry:
  - Add ASP.NET Core instrumentation
  - Add HTTP client instrumentation
  - Add Jaeger exporter (endpoint: http://jaeger:14268/api/traces)
  - Set service name: "BuildServer"
- [ ] Add hosted service `BuildOrchestratorInitializer` to initialize orchestrator on startup
- [ ] Configure Swagger/OpenAPI
- [ ] Add CORS policy
- [ ] Add health checks
- [ ] Add exception handling middleware
- [ ] Configure logging (Serilog)

**Acceptance Criteria**:
- DI is configured correctly
- OpenTelemetry exports to Jaeger
- Orchestrator initializes on startup
- Swagger UI works at /swagger
- Health checks work at /health
- Exception handling works
- Application starts successfully

**References**: BUILD_SERVER_DESIGN.md (Implementation Quick Start - Step 7)

---

### Task 4.5: API Integration Tests
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 8 hours
**Assigned To**: -
**Depends On**: Task 4.4

**Description**:
Create integration tests for the entire API using WebApplicationFactory.

**Requirements**:
- [ ] Create `tests/BuildServer.Tests/Integration/BuildsControllerTests.cs`
- [ ] Set up `WebApplicationFactory<Program>` test fixture
- [ ] Test scenarios:
  - Create build - returns 202 Accepted
  - Get build status - returns correct status
  - List builds - returns builds
  - Cancel build - cancels successfully
  - Invalid request - returns 400 Bad Request
  - Not found - returns 404
- [ ] Test AgentsController:
  - List agents - returns all agents
  - Get agent - returns agent details
  - Agent health - returns health status
- [ ] Test with real orchestrator (not mocked)
- [ ] Test telemetry propagation
- [ ] Test Swagger endpoint (/swagger/v1/swagger.json)

**Acceptance Criteria**:
- Integration tests cover all endpoints
- Tests use real HTTP requests
- Tests verify end-to-end flow
- All tests pass
- CI/CD can run integration tests

**References**: CLAUDE.md (Testing Requirements - Integration Tests)

---

## Phase 5: Advanced Features (Weeks 5-6)

**Goal**: Distributed strategy, Canary strategy, advanced features
**Duration**: 21 hours
**Prerequisites**: Phase 4 complete

### Task 5.1: Distributed Build Strategy
**Priority**: 🟢 Medium
**Status**: ⏳ Not Started
**Effort**: 16 hours
**Assigned To**: -
**Depends On**: Task 3.2

**Description**:
Implement the DistributedBuildStrategy for parallel builds across multiple agents.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Strategies/DistributedBuildStrategy.cs`
- [ ] Implement solution parsing:
  - Parse .sln file to extract projects
  - Extract project references from .csproj files
  - Build dependency graph
- [ ] Implement wave calculation:
  - Identify projects with no dependencies (wave 1)
  - Identify projects depending only on wave 1 (wave 2)
  - Continue until all projects assigned to waves
- [ ] Implement agent distribution:
  - Distribute projects in each wave across available agents
  - Balance load across agents
- [ ] Implement parallel execution:
  - Build all projects in wave 1 in parallel
  - Wait for wave 1 to complete
  - Build wave 2 in parallel
  - Continue until all waves complete
- [ ] Implement result aggregation:
  - Merge build logs from all agents
  - Combine test results
  - Collect all artifacts
- [ ] Add telemetry and metrics for distributed builds
- [ ] Handle failures (fail fast vs. continue)

**Acceptance Criteria**:
- Can parse solutions and build dependency graph
- Wave calculation is correct
- Projects distributed evenly across agents
- Parallel execution works
- Results aggregated correctly
- Significantly faster than sequential for large solutions
- Unit tests verify graph and distribution logic
- Integration test with multi-project solution

**References**: BUILD_SERVER_DESIGN.md (Build Strategies - Distributed Build Strategy)

---

### Task 5.2: Canary Build Strategy
**Priority**: 🟢 Medium
**Status**: ⏳ Not Started
**Effort**: 12 hours
**Assigned To**: -
**Depends On**: Task 3.2

**Description**:
Implement the CanaryBuildStrategy for safe production builds.

**Requirements**:
- [ ] Create `src/BuildServer.Orchestrator/Strategies/CanaryBuildStrategy.cs`
- [ ] Follow pattern from `CanaryDeploymentStrategy` in HotSwap
- [ ] Strategy logic:
  - Select one agent as "canary"
  - Build on canary agent (clean build)
  - Run full test suite on canary
  - Run security scans on canary outputs
  - Validate canary build quality:
    - No test failures
    - No security vulnerabilities
    - No performance regressions
  - If canary succeeds, build on remaining agents
  - If canary fails, abort without wasting resources
- [ ] Implement canary validation:
  - Test pass rate must be 100%
  - Security scan must pass
  - Build warnings below threshold
- [ ] Add metrics for canary success/failure rate
- [ ] Add telemetry for canary validation steps

**Acceptance Criteria**:
- Canary agent is selected first
- Validation runs on canary
- Failed canary aborts remaining builds
- Successful canary proceeds to all agents
- Metrics track canary effectiveness
- Unit tests verify canary logic
- Integration test with validation

**References**: BUILD_SERVER_DESIGN.md (Build Strategies - Canary Build Strategy), HotSwap CanaryDeploymentStrategy.cs

---

### Task 5.3: Build Artifact Storage
**Priority**: 🟢 Medium
**Status**: ⏳ Not Started
**Effort**: 8 hours
**Assigned To**: -
**Depends On**: Task 2.6

**Description**:
Implement artifact storage and retrieval using Azure Blob Storage or S3.

**Requirements**:
- [ ] Create `src/BuildServer.Infrastructure/Storage/IArtifactStorage.cs` interface
- [ ] Create `src/BuildServer.Infrastructure/Storage/BlobArtifactStorage.cs` implementation
- [ ] Implement methods:
  - `UploadArtifactAsync(BuildArtifact artifact, Stream stream, CancellationToken ct)` - Upload artifact to blob storage
  - `DownloadArtifactAsync(string artifactId, Stream stream, CancellationToken ct)` - Download artifact
  - `DeleteArtifactAsync(string artifactId, CancellationToken ct)` - Delete artifact
  - `GetArtifactUrlAsync(string artifactId, TimeSpan expiration)` - Generate signed download URL
  - `ListArtifactsAsync(string buildId, CancellationToken ct)` - List all artifacts for a build
- [ ] Add artifact metadata storage (PostgreSQL or CosmosDB)
- [ ] Implement artifact retention policies (delete after 30 days)
- [ ] Add telemetry for upload/download operations
- [ ] Implement retry logic for transient failures

**Acceptance Criteria**:
- Artifacts can be uploaded
- Artifacts can be downloaded
- Signed URLs work
- Metadata is stored correctly
- Retention policies work
- Telemetry recorded
- Unit tests verify storage operations
- Integration test with real blob storage

**References**: BUILD_SERVER_DESIGN.md (Build Pipeline Flow - Stage 6: Upload Artifacts)

---

### Task 5.4: Docker Compose Deployment
**Priority**: 🟡 High
**Status**: ⏳ Not Started
**Effort**: 6 hours
**Assigned To**: -
**Depends On**: Task 4.4

**Description**:
Create Docker Compose configuration for full stack deployment.

**Requirements**:
- [ ] Create `docker-compose.yml` in repository root
- [ ] Define services:
  - `build-api` - BuildServer.Api (port 5000)
  - `jaeger` - Jaeger all-in-one (ports 16686, 14268)
  - `redis` - Redis cache (port 6379)
  - `postgres` - PostgreSQL database (port 5432)
- [ ] Create `Dockerfile` for BuildServer.Api:
  - Multi-stage build (build stage + runtime stage)
  - Base image: mcr.microsoft.com/dotnet/aspnet:8.0
  - SDK image: mcr.microsoft.com/dotnet/sdk:8.0
  - Copy source and build
  - Install Git and Docker CLI in runtime image
  - Expose port 5000
- [ ] Configure networking between services
- [ ] Add health checks for all services
- [ ] Add volume mounts for persistence
- [ ] Create `.dockerignore` file
- [ ] Create `README-DOCKER.md` with usage instructions

**Acceptance Criteria**:
- `docker-compose up` starts all services
- API is accessible at http://localhost:5000
- Swagger UI works at http://localhost:5000/swagger
- Jaeger UI works at http://localhost:16686
- Services can communicate
- Builds can execute in containerized environment
- Health checks work

**References**: BUILD_SERVER_DESIGN.md (Running the Application - Docker Compose)

---

## Additional Tasks (Future Enhancements)

These tasks are not part of the initial 4-week implementation but should be considered for future sprints.

### Future Task F1: Authentication & Authorization
**Priority**: 🟡 High (Phase 6)
**Effort**: 16 hours
**Description**: Implement JWT authentication and role-based authorization (Developer, BuildMaster, Admin roles)
**References**: BUILD_SERVER_DESIGN.md (Security Considerations - Authentication and Authorization)

### Future Task F2: Build Approval Workflow
**Priority**: 🟢 Medium (Phase 6)
**Effort**: 12 hours
**Description**: Implement approval workflow for production builds
**References**: BUILD_SERVER_DESIGN.md (Implementation Roadmap - Phase 6)

### Future Task F3: Secret Management
**Priority**: 🟡 High (Phase 6)
**Effort**: 8 hours
**Description**: Integrate Azure Key Vault for build secrets
**References**: BUILD_SERVER_DESIGN.md (Security Considerations - Secret Management)

### Future Task F4: Prometheus Metrics Export
**Priority**: 🟢 Medium (Phase 5)
**Effort**: 8 hours
**Description**: Export metrics to Prometheus for Grafana dashboards
**References**: BUILD_SERVER_DESIGN.md (Monitoring and Telemetry - Metrics Collection)

### Future Task F5: WebSocket Real-Time Updates
**Priority**: ⚪ Low (Phase 7)
**Effort**: 12 hours
**Description**: Add WebSocket support for real-time build status updates
**References**: TASK_LIST.md (existing project task list)

### Future Task F6: Build History Analytics
**Priority**: ⚪ Low (Phase 7)
**Effort**: 16 hours
**Description**: Add analytics dashboard for build trends, performance, and failures
**References**: BUILD_SERVER_DESIGN.md (Implementation Roadmap - Phase 5)

---

## Task Dependencies Diagram

```
Phase 1 (Foundation)
├── 1.1 Project Structure ──┬──> 1.2 Enums ──> 1.3 BuildJobDescriptor ──┬──> 1.4 Models
│                           └──> 1.5 HotSwap Reference                   └──> 1.8 Tests
├── 1.6 Capabilities <──────┴──> 1.7 Agent Pool

Phase 2 (Core Build Logic)
├── 2.1 Agent Core <── 1.6
├── 2.2 Git <── 2.1
├── 2.3 NuGet <── 2.2
├── 2.4 Build <── 2.3
├── 2.5 Test <── 2.4
└── 2.6 Artifacts <── 2.4

Phase 3 (Orchestration)
├── 3.1 Strategy Interface <── 1.4
├── 3.2 Incremental Strategy <── 2.6, 3.1
├── 3.3 Clean Strategy <── 3.2
├── 3.4 Cached Strategy <── 3.2
├── 3.5 Pipeline <── 3.2
├── 3.6 Orchestrator <── 1.7, 3.5
└── 3.7 Tests <── 3.2, 3.3, 3.5, 3.6

Phase 4 (API Layer)
├── 4.1 API Models <── 1.4
├── 4.2 BuildsController <── 3.6, 4.1
├── 4.3 AgentsController <── 3.6
├── 4.4 Startup Config <── 4.2, 4.3
└── 4.5 Integration Tests <── 4.4

Phase 5 (Advanced)
├── 5.1 Distributed Strategy <── 3.2
├── 5.2 Canary Strategy <── 3.2
├── 5.3 Artifact Storage <── 2.6
└── 5.4 Docker Compose <── 4.4
```

---

## Progress Tracking

### Overall Progress
- **Total Tasks**: 30
- **Completed**: 0
- **In Progress**: 0
- **Not Started**: 30
- **% Complete**: 0%

### Phase Progress
- **Phase 1 (Foundation)**: 0/8 tasks (0%)
- **Phase 2 (Core Build Logic)**: 0/6 tasks (0%)
- **Phase 3 (Orchestration)**: 0/7 tasks (0%)
- **Phase 4 (API Layer)**: 0/5 tasks (0%)
- **Phase 5 (Advanced)**: 0/4 tasks (0%)

### Weekly Milestones
- **Week 1 (Phase 1)**: Target 8 tasks, Completed 0
- **Week 2 (Phase 2)**: Target 6 tasks, Completed 0
- **Week 3 (Phase 3)**: Target 7 tasks, Completed 0
- **Week 4 (Phase 4)**: Target 5 tasks, Completed 0

---

## Risk Management

### High-Risk Items
1. **Git Integration Complexity** (Task 2.2) - Authentication, various repo types
2. **Distributed Strategy** (Task 5.1) - Solution parsing, dependency graph complexity
3. **Integration Testing** (Task 4.5) - Requires full stack running

### Mitigation Strategies
- Start with simple scenarios, add complexity incrementally
- Allocate extra time for high-risk tasks
- Create spike tasks for technical unknowns
- Maintain buffer time (20% of estimates)

---

## Next Steps

1. **Review this task list** with the team
2. **Assign tasks** to developers
3. **Set up project board** (GitHub Projects or similar)
4. **Create milestone** for Phase 1 completion
5. **Begin implementation** with Task 1.1 (Project Structure Setup)

---

**Document Maintained By**: Development Team
**Review Frequency**: Weekly
**Last Review Date**: 2025-11-15
