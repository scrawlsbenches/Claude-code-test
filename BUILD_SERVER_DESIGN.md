# .NET Build Server - High-Level Design

## Executive Summary

This document provides a comprehensive high-level design for a **distributed .NET build server** built on top of the HotSwap.Distributed framework. The design leverages the existing orchestration, deployment strategies, telemetry, and distributed coordination capabilities to create a scalable, resilient build infrastructure.

**Key Features:**
- Distributed build execution across multiple build agents
- Multiple build strategies (Incremental, Clean, Distributed, Cached)
- Real-time build monitoring and telemetry
- Build artifact management and caching
- NuGet package restoration and caching
- Integration with CI/CD pipelines
- Role-based access control (Developer, BuildMaster, Admin)
- OpenTelemetry distributed tracing for build operations

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Component Mapping](#component-mapping)
3. [Domain Model](#domain-model)
4. [Build Strategies](#build-strategies)
5. [Build Agent Architecture](#build-agent-architecture)
6. [API Design](#api-design)
7. [Build Pipeline Flow](#build-pipeline-flow)
8. [Caching and Optimization](#caching-and-optimization)
9. [Monitoring and Telemetry](#monitoring-and-telemetry)
10. [Security Considerations](#security-considerations)
11. [Scalability and Performance](#scalability-and-performance)
12. [Implementation Roadmap](#implementation-roadmap)

---

## Architecture Overview

### System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Build API Layer                          │
│  (ASP.NET Core REST API - BuildsController, ArtifactsController) │
└────────────────────────┬────────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────────┐
│                   Build Orchestrator                             │
│  - Job Queue Management                                          │
│  - Build Strategy Selection                                      │
│  - Agent Pool Coordination                                       │
│  - Build Result Aggregation                                      │
└────────────────────────┬────────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         │                               │
┌────────▼────────┐           ┌─────────▼──────────┐
│  Build Pipeline │           │  Agent Coordinator │
│                 │           │                    │
│ - Source Fetch  │           │ - Agent Discovery  │
│ - Dependency    │           │ - Health Checks    │
│   Restore       │           │ - Load Balancing   │
│ - Compilation   │           │ - Agent Selection  │
│ - Testing       │           └────────────────────┘
│ - Packaging     │
│ - Publishing    │
└────────┬────────┘
         │
┌────────▼──────────────────────────────────────────────────────┐
│              Build Agent Pool (Clusters)                       │
│                                                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐        │
│  │ Linux Agents │  │ Windows      │  │ macOS Agents │        │
│  │              │  │ Agents       │  │              │        │
│  │ - .NET SDK   │  │ - .NET SDK   │  │ - .NET SDK   │        │
│  │ - Docker     │  │ - MSBuild    │  │ - Xcode      │        │
│  │ - Build Tools│  │ - VS Build   │  │ - Build Tools│        │
│  └──────────────┘  └──────────────┘  └──────────────┘        │
└───────────────────────────────────────────────────────────────┘
         │
┌────────▼────────────────────────────────────────────────────┐
│                 Infrastructure Services                      │
│                                                              │
│  - Redis (Caching, Distributed Locks, Job Queue)            │
│  - Blob Storage (Artifacts, Binaries, Packages)             │
│  - Jaeger (Distributed Tracing)                             │
│  - Prometheus (Metrics Collection)                          │
│  - PostgreSQL (Build History, Metadata)                     │
└─────────────────────────────────────────────────────────────┘
```

### Key Architectural Principles

1. **Separation of Concerns**: Build orchestration separate from execution
2. **Horizontal Scalability**: Add more build agents to handle increased load
3. **Fault Tolerance**: Build failures don't crash the system; automatic retries
4. **Observable**: Full telemetry and tracing for every build operation
5. **Cacheable**: Aggressive caching at multiple levels (NuGet, compilation, artifacts)
6. **Secure**: Role-based access, artifact signing, secure credential management

---

## Component Mapping

The build server leverages existing HotSwap.Distributed components with semantic mappings:

| HotSwap Concept | Build Server Concept | Description |
|----------------|---------------------|-------------|
| **ModuleDescriptor** | **BuildJobDescriptor** | Describes a build job (project, configuration, platform) |
| **DeploymentRequest** | **BuildRequest** | Request to build a project or solution |
| **EnvironmentType** | **BuildTargetType** | Build target (Debug, Release, Platform-specific) |
| **KernelNode** | **BuildAgent** | Worker that executes builds |
| **EnvironmentCluster** | **AgentPool** | Group of build agents (Linux, Windows, macOS) |
| **IDeploymentStrategy** | **IBuildStrategy** | Strategy for executing builds |
| **DeploymentPipeline** | **BuildPipeline** | Orchestrates multi-stage build process |
| **DistributedKernelOrchestrator** | **BuildOrchestrator** | Central coordinator for build operations |
| **PipelineExecutionResult** | **BuildExecutionResult** | Result of a build execution |
| **ModuleVerifier** | **ArtifactVerifier** | Validates build artifacts and checksums |

---

## Domain Model

### Core Build Models

#### BuildJobDescriptor

```csharp
namespace BuildServer.Domain.Models;

/// <summary>
/// Describes a build job to be executed.
/// </summary>
public class BuildJobDescriptor
{
    /// <summary>
    /// Unique identifier for the build job.
    /// </summary>
    public Guid JobId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Project or solution name.
    /// </summary>
    public required string ProjectName { get; set; }

    /// <summary>
    /// Git repository URL.
    /// </summary>
    public required string RepositoryUrl { get; set; }

    /// <summary>
    /// Git branch, tag, or commit SHA.
    /// </summary>
    public required string GitRef { get; set; }

    /// <summary>
    /// Solution or project file path relative to repo root.
    /// </summary>
    public required string SolutionPath { get; set; }

    /// <summary>
    /// Build configuration (Debug, Release, etc.).
    /// </summary>
    public string Configuration { get; set; } = "Release";

    /// <summary>
    /// Target framework (net8.0, net9.0, etc.).
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Target platform (AnyCPU, x64, ARM64, etc.).
    /// </summary>
    public string Platform { get; set; } = "AnyCPU";

    /// <summary>
    /// Target runtime identifier (linux-x64, win-x64, osx-arm64, etc.).
    /// </summary>
    public string? RuntimeIdentifier { get; set; }

    /// <summary>
    /// MSBuild properties to pass to the build.
    /// </summary>
    public Dictionary<string, string> BuildProperties { get; set; } = new();

    /// <summary>
    /// Whether to run tests after build.
    /// </summary>
    public bool RunTests { get; set; } = true;

    /// <summary>
    /// Whether to create NuGet packages.
    /// </summary>
    public bool CreatePackages { get; set; } = false;

    /// <summary>
    /// Whether to publish build artifacts.
    /// </summary>
    public bool PublishArtifacts { get; set; } = true;

    /// <summary>
    /// Build timeout in minutes.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Metadata (build trigger, user, CI system, etc.).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Timestamp when job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Email of person who requested the build.
    /// </summary>
    public required string RequesterEmail { get; set; }
}
```

#### BuildRequest

```csharp
namespace BuildServer.Domain.Models;

/// <summary>
/// Request to execute a build.
/// </summary>
public class BuildRequest
{
    /// <summary>
    /// Build job descriptor.
    /// </summary>
    public required BuildJobDescriptor Job { get; set; }

    /// <summary>
    /// Target build pool (Linux, Windows, macOS, Docker).
    /// </summary>
    public BuildTargetType TargetPool { get; set; }

    /// <summary>
    /// Build strategy to use (Incremental, Clean, Distributed, Cached).
    /// </summary>
    public BuildStrategyType Strategy { get; set; } = BuildStrategyType.Incremental;

    /// <summary>
    /// Priority level (Low, Normal, High, Critical).
    /// </summary>
    public BuildPriority Priority { get; set; } = BuildPriority.Normal;

    /// <summary>
    /// Whether to skip cache and force clean build.
    /// </summary>
    public bool ForceClean { get; set; } = false;

    /// <summary>
    /// Email of requester.
    /// </summary>
    public required string RequesterEmail { get; set; }

    /// <summary>
    /// Unique execution ID.
    /// </summary>
    public Guid ExecutionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

#### BuildTargetType (Enum)

```csharp
namespace BuildServer.Domain.Enums;

/// <summary>
/// Build target pool type (maps to EnvironmentType).
/// </summary>
public enum BuildTargetType
{
    /// <summary>
    /// Linux build agents (.NET SDK, Docker, apt packages).
    /// </summary>
    Linux,

    /// <summary>
    /// Windows build agents (MSBuild, Visual Studio, .NET SDK).
    /// </summary>
    Windows,

    /// <summary>
    /// macOS build agents (.NET SDK, Xcode, iOS/macOS tooling).
    /// </summary>
    MacOS,

    /// <summary>
    /// Docker-based build agents (containerized builds).
    /// </summary>
    Docker
}
```

#### BuildStrategyType (Enum)

```csharp
namespace BuildServer.Domain.Enums;

/// <summary>
/// Build execution strategy.
/// </summary>
public enum BuildStrategyType
{
    /// <summary>
    /// Incremental build - only rebuild changed projects.
    /// Fastest for iterative development.
    /// </summary>
    Incremental,

    /// <summary>
    /// Clean build - clean all artifacts before building.
    /// Ensures reproducibility.
    /// </summary>
    Clean,

    /// <summary>
    /// Distributed build - split solution across multiple agents.
    /// Fastest for large solutions (requires dependency graph analysis).
    /// </summary>
    Distributed,

    /// <summary>
    /// Cached build - maximize use of build cache.
    /// Fastest for repeated builds with minimal changes.
    /// </summary>
    Cached,

    /// <summary>
    /// Canary build - build on one agent, verify, then build on all.
    /// Safest for production releases.
    /// </summary>
    Canary
}
```

#### BuildExecutionResult

```csharp
namespace BuildServer.Domain.Models;

/// <summary>
/// Result of a build execution (maps to PipelineExecutionResult).
/// </summary>
public class BuildExecutionResult
{
    public Guid ExecutionId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Configuration { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Build stages (Restore, Build, Test, Package, Publish).
    /// </summary>
    public List<BuildStageResult> StageResults { get; set; } = new();

    /// <summary>
    /// Build artifacts produced.
    /// </summary>
    public List<BuildArtifact> Artifacts { get; set; } = new();

    /// <summary>
    /// Warnings generated during build.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Errors generated during build.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Test results (if tests were run).
    /// </summary>
    public TestResults? TestResults { get; set; }

    /// <summary>
    /// Build output log URL.
    /// </summary>
    public string? LogUrl { get; set; }

    /// <summary>
    /// Error message if build failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
```

#### BuildStageResult

```csharp
namespace BuildServer.Domain.Models;

/// <summary>
/// Result of a single build stage.
/// </summary>
public class BuildStageResult
{
    public string StageName { get; set; } = string.Empty;
    public BuildStageStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Message { get; set; }
    public int? WarningsCount { get; set; }
    public int? ErrorsCount { get; set; }
}

public enum BuildStageStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped,
    Cancelled
}
```

#### BuildArtifact

```csharp
namespace BuildServer.Domain.Models;

/// <summary>
/// Build artifact produced by the build.
/// </summary>
public class BuildArtifact
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string Sha256Hash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
```

---

## Build Strategies

The build server implements multiple build strategies, each optimized for different scenarios.

### 1. Incremental Build Strategy

**Purpose**: Fastest builds for iterative development
**Use Case**: Development, rapid iteration
**How it Works**:
- Restores NuGet packages only if packages.lock.json changed
- Rebuilds only projects with source code changes
- Skips tests for unchanged projects
- Uses MSBuild incremental compilation

**Implementation**:
```csharp
public class IncrementalBuildStrategy : IBuildStrategy
{
    public string StrategyName => "Incremental";

    public async Task<BuildResult> BuildAsync(
        BuildJobDescriptor job,
        BuildAgent agent,
        CancellationToken cancellationToken)
    {
        // 1. Fetch source from Git (shallow clone)
        // 2. Restore packages only if lock file changed
        // 3. Run MSBuild with /incremental flag
        // 4. Run tests only for changed projects
        // 5. Package artifacts
    }
}
```

**Performance**: ~2-5 minutes for small changes

---

### 2. Clean Build Strategy

**Purpose**: Guaranteed reproducible builds
**Use Case**: Release builds, CI/CD pipelines
**How it Works**:
- Deletes all bin/, obj/, and artifact directories
- Restores all NuGet packages fresh
- Rebuilds all projects from scratch
- Runs full test suite
- Creates signed packages

**Implementation**:
```csharp
public class CleanBuildStrategy : IBuildStrategy
{
    public string StrategyName => "Clean";

    public async Task<BuildResult> BuildAsync(
        BuildJobDescriptor job,
        BuildAgent agent,
        CancellationToken cancellationToken)
    {
        // 1. Fetch source from Git (full clone)
        // 2. dotnet clean --configuration Release
        // 3. Delete bin/, obj/ directories
        // 4. dotnet restore --force
        // 5. dotnet build --no-incremental
        // 6. dotnet test --no-build
        // 7. dotnet pack/publish
    }
}
```

**Performance**: ~10-20 minutes depending on solution size

---

### 3. Distributed Build Strategy

**Purpose**: Fastest builds for large solutions
**Use Case**: Large monorepos, microservices
**How it Works**:
- Analyzes project dependency graph
- Identifies projects that can be built in parallel
- Distributes independent projects across multiple agents
- Builds dependencies first, then dependent projects
- Aggregates results from all agents

**Implementation**:
```csharp
public class DistributedBuildStrategy : IBuildStrategy
{
    public string StrategyName => "Distributed";

    public async Task<BuildResult> BuildAsync(
        BuildJobDescriptor job,
        AgentPool agentPool,
        CancellationToken cancellationToken)
    {
        // 1. Parse solution file and extract project references
        // 2. Build dependency graph
        // 3. Identify parallelizable projects
        // 4. Distribute projects across available agents
        // 5. Build in waves (dependencies first)
        // 6. Aggregate build outputs
        // 7. Run tests in parallel on multiple agents
        // 8. Merge coverage reports
    }
}
```

**Example**:
```
Solution with 50 projects, 5 build agents:
- Wave 1: Build 10 core libraries (2 per agent)  → 3 min
- Wave 2: Build 25 services (5 per agent)        → 5 min
- Wave 3: Build 10 API projects (2 per agent)    → 4 min
- Wave 4: Build 5 test projects (1 per agent)    → 2 min
Total: ~14 minutes (vs. 45 minutes sequential)
```

**Performance**: 3-5x faster than sequential for large solutions

---

### 4. Cached Build Strategy

**Purpose**: Maximum cache utilization
**Use Case**: Repeated builds with minimal changes
**How it Works**:
- Uses Redis for build cache (compilation outputs, test results)
- Checks cache before building each project
- Restores compiled assemblies from cache if inputs unchanged
- Only rebuilds projects with changed inputs
- Updates cache with new outputs

**Implementation**:
```csharp
public class CachedBuildStrategy : IBuildStrategy
{
    private readonly IDistributedCache _cache;

    public string StrategyName => "Cached";

    public async Task<BuildResult> BuildAsync(
        BuildJobDescriptor job,
        BuildAgent agent,
        CancellationToken cancellationToken)
    {
        // 1. Compute input hash for each project (source files, dependencies)
        // 2. Check Redis cache for existing outputs
        // 3. Restore cached outputs if hash matches
        // 4. Build only projects with cache miss
        // 5. Store new outputs in cache
        // 6. Skip tests if test results cached and inputs unchanged
    }
}
```

**Cache Key Format**:
```
build:cache:{projectName}:{configHash}:{inputHash}
```

**Performance**: ~1-3 minutes for builds with 80%+ cache hit rate

---

### 5. Canary Build Strategy

**Purpose**: Safest builds for production releases
**Use Case**: Production deployments, critical releases
**How it Works**:
- Builds on one agent first (canary)
- Validates canary build outputs (tests, code quality, security)
- If canary succeeds, builds on remaining agents
- If canary fails, aborts without wasting resources

**Implementation**:
```csharp
public class CanaryBuildStrategy : IBuildStrategy
{
    public string StrategyName => "Canary";

    public async Task<BuildResult> BuildAsync(
        BuildJobDescriptor job,
        AgentPool agentPool,
        CancellationToken cancellationToken)
    {
        // 1. Select one agent as canary
        // 2. Build on canary agent (clean build)
        // 3. Run full test suite on canary
        // 4. Run security scans on canary outputs
        // 5. If canary healthy, build on all other agents
        // 6. If canary fails, rollback and report
    }
}
```

**Performance**: +5-10 minutes overhead for validation, but prevents wasted builds

---

## Build Agent Architecture

### BuildAgent (extends KernelNode concept)

```csharp
namespace BuildServer.Orchestrator.Core;

/// <summary>
/// A build agent that executes build jobs.
/// </summary>
public class BuildAgent
{
    public Guid AgentId { get; set; }
    public string Hostname { get; set; }
    public BuildTargetType TargetType { get; set; }
    public BuildAgentStatus Status { get; set; }
    public BuildAgentCapabilities Capabilities { get; set; }
    public BuildJobDescriptor? CurrentJob { get; set; }
    public DateTime LastHeartbeat { get; set; }

    /// <summary>
    /// Executes a build job on this agent.
    /// </summary>
    public async Task<BuildResult> ExecuteBuildAsync(
        BuildJobDescriptor job,
        CancellationToken cancellationToken)
    {
        // 1. Clone repository
        // 2. Restore dependencies
        // 3. Run build
        // 4. Run tests
        // 5. Package artifacts
        // 6. Upload artifacts to blob storage
        // 7. Return build result
    }

    /// <summary>
    /// Gets current resource usage of this agent.
    /// </summary>
    public async Task<AgentResourceUsage> GetResourceUsageAsync()
    {
        // Return CPU, memory, disk, network usage
    }

    /// <summary>
    /// Health check for this agent.
    /// </summary>
    public async Task<AgentHealth> GetHealthAsync()
    {
        // Check .NET SDK installed
        // Check disk space available
        // Check network connectivity
        // Check required tools installed
    }
}

public enum BuildAgentStatus
{
    Idle,
    Building,
    Offline,
    Maintenance
}

public class BuildAgentCapabilities
{
    public List<string> InstalledSdks { get; set; } = new();  // "net8.0", "net9.0"
    public List<string> InstalledTools { get; set; } = new(); // "docker", "git", "msbuild"
    public int MaxConcurrentBuilds { get; set; } = 1;
    public long AvailableDiskSpaceGB { get; set; }
    public int CpuCores { get; set; }
    public long TotalMemoryGB { get; set; }
}
```

### AgentPool (extends EnvironmentCluster concept)

```csharp
namespace BuildServer.Orchestrator.Core;

/// <summary>
/// Pool of build agents for a specific platform.
/// </summary>
public class AgentPool
{
    public BuildTargetType TargetType { get; set; }
    public List<BuildAgent> Agents { get; set; } = new();

    /// <summary>
    /// Selects best available agent for a build job.
    /// </summary>
    public BuildAgent? SelectAgent(BuildJobDescriptor job)
    {
        // 1. Filter agents by required capabilities
        // 2. Prefer idle agents over busy ones
        // 3. Prefer agents with lower resource usage
        // 4. Prefer agents with cached artifacts for this project
    }

    /// <summary>
    /// Gets health status of the entire pool.
    /// </summary>
    public async Task<PoolHealth> GetHealthAsync()
    {
        var agentHealthTasks = Agents.Select(a => a.GetHealthAsync());
        var results = await Task.WhenAll(agentHealthTasks);

        return new PoolHealth
        {
            TotalAgents = Agents.Count,
            HealthyAgents = results.Count(h => h.IsHealthy),
            BusyAgents = Agents.Count(a => a.Status == BuildAgentStatus.Building),
            IdleAgents = Agents.Count(a => a.Status == BuildAgentStatus.Idle)
        };
    }
}
```

---

## API Design

### Build API Endpoints

#### POST /api/v1/builds - Create Build

**Request**:
```json
{
  "job": {
    "projectName": "MyApp",
    "repositoryUrl": "https://github.com/org/myapp",
    "gitRef": "refs/heads/main",
    "solutionPath": "MyApp.sln",
    "configuration": "Release",
    "targetFramework": "net8.0",
    "platform": "AnyCPU",
    "runTests": true,
    "createPackages": true,
    "publishArtifacts": true,
    "requesterEmail": "developer@example.com"
  },
  "targetPool": "Linux",
  "strategy": "Incremental",
  "priority": "Normal",
  "requesterEmail": "developer@example.com"
}
```

**Response** (202 Accepted):
```json
{
  "executionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Queued",
  "queuePosition": 3,
  "estimatedStartTime": "2025-11-15T10:05:00Z",
  "estimatedDuration": "PT15M",
  "links": {
    "self": "/api/v1/builds/3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "logs": "/api/v1/builds/3fa85f64-5717-4562-b3fc-2c963f66afa6/logs",
    "trace": "https://jaeger.example.com/trace/3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
}
```

#### GET /api/v1/builds/{executionId} - Get Build Status

**Response** (200 OK):
```json
{
  "executionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "projectName": "MyApp",
  "configuration": "Release",
  "status": "Running",
  "currentStage": "Testing",
  "startTime": "2025-11-15T10:05:00Z",
  "duration": "PT12M",
  "stages": [
    {
      "name": "Clone",
      "status": "Succeeded",
      "startTime": "2025-11-15T10:05:00Z",
      "duration": "PT1M"
    },
    {
      "name": "Restore",
      "status": "Succeeded",
      "startTime": "2025-11-15T10:06:00Z",
      "duration": "PT3M"
    },
    {
      "name": "Build",
      "status": "Succeeded",
      "startTime": "2025-11-15T10:09:00Z",
      "duration": "PT6M",
      "warningsCount": 3
    },
    {
      "name": "Test",
      "status": "Running",
      "startTime": "2025-11-15T10:15:00Z"
    }
  ],
  "agent": {
    "hostname": "linux-build-01",
    "targetType": "Linux"
  }
}
```

#### GET /api/v1/builds/{executionId}/logs - Get Build Logs

**Response** (200 OK):
```
[2025-11-15 10:05:00] INFO: Starting build for MyApp (Release)
[2025-11-15 10:05:01] INFO: Cloning repository https://github.com/org/myapp
[2025-11-15 10:05:45] INFO: Checked out commit abc123def
[2025-11-15 10:06:00] INFO: Restoring NuGet packages...
[2025-11-15 10:08:30] INFO: Restored 127 packages
[2025-11-15 10:09:00] INFO: Building solution MyApp.sln
[2025-11-15 10:14:30] WARN: CS8618: Non-nullable field 'Name' must contain a non-null value
[2025-11-15 10:15:00] INFO: Build succeeded. 3 Warning(s), 0 Error(s)
[2025-11-15 10:15:01] INFO: Running tests...
```

#### POST /api/v1/builds/{executionId}/cancel - Cancel Build

**Response** (200 OK):
```json
{
  "executionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Cancelling",
  "message": "Build cancellation requested"
}
```

#### GET /api/v1/builds/{executionId}/artifacts - List Artifacts

**Response** (200 OK):
```json
{
  "artifacts": [
    {
      "name": "MyApp-1.0.0.nupkg",
      "path": "artifacts/packages/MyApp.1.0.0.nupkg",
      "sizeBytes": 2457600,
      "contentType": "application/octet-stream",
      "sha256Hash": "abc123...",
      "createdAt": "2025-11-15T10:18:00Z",
      "downloadUrl": "/api/v1/artifacts/abc123/download"
    },
    {
      "name": "MyApp.dll",
      "path": "bin/Release/net8.0/MyApp.dll",
      "sizeBytes": 512000,
      "contentType": "application/octet-stream",
      "sha256Hash": "def456...",
      "createdAt": "2025-11-15T10:15:30Z",
      "downloadUrl": "/api/v1/artifacts/def456/download"
    }
  ]
}
```

#### GET /api/v1/agents - List Build Agents

**Response** (200 OK):
```json
{
  "agents": [
    {
      "agentId": "agent-linux-01",
      "hostname": "linux-build-01",
      "targetType": "Linux",
      "status": "Building",
      "currentJob": "MyApp v1.0.0",
      "cpuUsage": 85.5,
      "memoryUsage": 62.3,
      "capabilities": {
        "installedSdks": ["net8.0", "net9.0"],
        "installedTools": ["docker", "git", "dotnet"],
        "maxConcurrentBuilds": 2,
        "availableDiskSpaceGB": 250
      }
    }
  ]
}
```

#### GET /api/v1/agents/{agentId}/health - Agent Health Check

**Response** (200 OK):
```json
{
  "agentId": "agent-linux-01",
  "healthy": true,
  "status": "Idle",
  "cpuUsage": 12.5,
  "memoryUsage": 35.2,
  "diskSpaceAvailableGB": 250,
  "lastHeartbeat": "2025-11-15T10:20:00Z",
  "uptime": "PT72H",
  "checks": [
    {"name": ".NET SDK", "status": "Healthy"},
    {"name": "Git", "status": "Healthy"},
    {"name": "Docker", "status": "Healthy"},
    {"name": "Disk Space", "status": "Healthy"}
  ]
}
```

---

## Build Pipeline Flow

### Sequential Build Flow (Clean Strategy)

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Queue Build Request                                      │
│    - Validate request                                       │
│    - Assign execution ID                                    │
│    - Add to Redis job queue                                 │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 2. Select Build Agent                                       │
│    - Find idle agent in target pool                         │
│    - Check agent capabilities match job requirements        │
│    - Reserve agent for this job                             │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 3. Clone Repository (Stage 1)                               │
│    - git clone --depth 1 {repo} (shallow clone)             │
│    - git checkout {gitRef}                                  │
│    - Report progress: "Cloning repository..."               │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 4. Restore Dependencies (Stage 2)                           │
│    - dotnet restore {solution} --locked-mode                │
│    - Cache NuGet packages in Redis                          │
│    - Report progress: "Restoring 127 packages..."           │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 5. Build Projects (Stage 3)                                 │
│    - dotnet build {solution} --no-restore                   │
│    - Collect warnings and errors                            │
│    - Report progress: "Building 15 projects..."             │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 6. Run Tests (Stage 4)                                      │
│    - dotnet test --no-build --logger trx                    │
│    - Collect test results                                   │
│    - Generate code coverage (if enabled)                    │
│    - Report progress: "Running 238 tests..."                │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 7. Package Artifacts (Stage 5)                              │
│    - dotnet pack --no-build (if createPackages=true)        │
│    - dotnet publish --no-build (if publishArtifacts=true)   │
│    - Compute SHA256 hashes for all outputs                  │
│    - Report progress: "Creating 3 packages..."              │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 8. Upload Artifacts (Stage 6)                               │
│    - Upload to Azure Blob Storage / S3                      │
│    - Store artifact metadata in PostgreSQL                  │
│    - Generate signed download URLs                          │
│    - Report progress: "Uploading artifacts..."              │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 9. Complete Build                                           │
│    - Mark build as Succeeded/Failed                         │
│    - Release build agent                                    │
│    - Send notifications (email, webhook, Slack)             │
│    - Record telemetry and metrics                           │
└─────────────────────────────────────────────────────────────┘
```

### Distributed Build Flow

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Queue Build Request (Distributed Strategy)              │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 2. Analyze Solution Dependency Graph                       │
│    - Parse .sln file                                        │
│    - Extract project references                             │
│    - Build dependency tree                                  │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 3. Identify Build Waves                                     │
│    Wave 1: Projects with no dependencies (core libraries)   │
│    Wave 2: Projects depending on Wave 1                     │
│    Wave 3: Projects depending on Wave 2                     │
│    Wave N: Final projects (APIs, executables)               │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│ 4. Allocate Agents (5 agents available)                    │
│    Agent 1: Projects A, B                                   │
│    Agent 2: Projects C, D                                   │
│    Agent 3: Projects E, F                                   │
│    Agent 4: Projects G, H                                   │
│    Agent 5: Projects I, J                                   │
└──────────────────────┬──────────────────────────────────────┘
                       │
         ┌─────────────┴─────────────┐
         │                           │
    ┌────▼────┐                 ┌────▼────┐
    │ Wave 1  │                 │ Wave 2  │
    │ (5 min) │────────────────▶│ (6 min) │
    │ Build   │                 │ Build   │
    │ A-J     │                 │ K-T     │
    └─────────┘                 └────┬────┘
                                     │
                                ┌────▼────┐
                                │ Wave 3  │
                                │ (4 min) │
                                │ Build   │
                                │ U-Z     │
                                └────┬────┘
                                     │
                      ┌──────────────▼──────────────┐
                      │ 5. Aggregate Results        │
                      │    - Merge build logs       │
                      │    - Combine test results   │
                      │    - Collect all artifacts  │
                      └─────────────────────────────┘
```

**Performance Improvement**:
- Sequential: 45 minutes (30 projects × 1.5 min each)
- Distributed (5 agents): 15 minutes (3 waves × 5 min each)
- **Speedup: 3x faster**

---

## Caching and Optimization

### Multi-Level Caching Strategy

```
┌─────────────────────────────────────────────────────────────┐
│                       Cache Hierarchy                        │
└─────────────────────────────────────────────────────────────┘

Level 1: Agent-Local Cache (Fastest - ~100ms access)
├── NuGet packages: ~/.nuget/packages/
├── Build outputs: ~/.dotnet/build-cache/
└── Git repositories: ~/build-cache/repos/

Level 2: Redis Distributed Cache (Fast - ~5ms access)
├── NuGet package metadata
├── Build result hashes
├── Test result cache
└── Artifact manifests

Level 3: Blob Storage Cache (Slow - ~500ms access)
├── Compiled assemblies
├── NuGet packages
├── Docker images
└── Build artifacts

Level 4: Source Repository (Slowest - ~5s access)
└── Original source code
```

### Cache Invalidation Strategy

**When to Invalidate**:
1. **Source code changes**: Invalidate affected projects and dependents
2. **Dependency changes**: Invalidate when packages.lock.json changes
3. **Configuration changes**: Invalidate on MSBuild property changes
4. **Time-based expiration**: Cache entries expire after 7 days

**Cache Key Format**:
```
build:cache:{projectName}:{configuration}:{inputHash}

Where inputHash = SHA256(
    sourceFiles +
    projectFile +
    packageLockFile +
    dependencyOutputs +
    buildProperties
)
```

### NuGet Package Caching

```csharp
public class NuGetCacheService
{
    private readonly IDistributedCache _redis;
    private readonly IBlobStorage _blobStorage;

    public async Task<bool> RestoreFromCacheAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        // 1. Compute hash of packages.lock.json
        var lockFileHash = ComputeFileHash(projectPath + "/packages.lock.json");

        // 2. Check Redis for cached package manifest
        var cacheKey = $"nuget:packages:{lockFileHash}";
        var manifest = await _redis.GetAsync<PackageManifest>(cacheKey);

        if (manifest == null)
            return false; // Cache miss

        // 3. Restore packages from agent-local cache or blob storage
        foreach (var package in manifest.Packages)
        {
            var localPath = GetNuGetLocalPath(package);
            if (!File.Exists(localPath))
            {
                // Download from blob storage
                await _blobStorage.DownloadAsync(
                    $"nuget/{package.Id}/{package.Version}",
                    localPath,
                    cancellationToken);
            }
        }

        return true; // Cache hit
    }
}
```

---

## Monitoring and Telemetry

### OpenTelemetry Integration

Every build operation is fully traced using OpenTelemetry:

```csharp
public class BuildOrchestrator
{
    private readonly ActivitySource _activitySource;

    public async Task<BuildExecutionResult> ExecuteBuildAsync(
        BuildRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity(
            "BuildOrchestrator.ExecuteBuild",
            ActivityKind.Server);

        activity?.SetTag("build.project", request.Job.ProjectName);
        activity?.SetTag("build.configuration", request.Job.Configuration);
        activity?.SetTag("build.strategy", request.Strategy);
        activity?.SetTag("build.executionId", request.ExecutionId);

        try
        {
            // Execute build stages
            var result = await ExecuteBuildPipelineAsync(request, cancellationToken);

            activity?.SetTag("build.success", result.Success);
            activity?.SetTag("build.duration", result.Duration.TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

### Metrics Collection

**Key Metrics** (exported to Prometheus):

```csharp
public class BuildMetrics
{
    // Build throughput
    public Counter<long> BuildsStarted { get; }
    public Counter<long> BuildsCompleted { get; }
    public Counter<long> BuildsFailed { get; }

    // Build performance
    public Histogram<double> BuildDuration { get; }
    public Histogram<double> QueueWaitTime { get; }
    public Histogram<double> RestoreDuration { get; }
    public Histogram<double> CompilationDuration { get; }
    public Histogram<double> TestDuration { get; }

    // Resource utilization
    public ObservableGauge<int> ActiveAgents { get; }
    public ObservableGauge<int> QueuedBuilds { get; }
    public ObservableGauge<double> AgentCpuUsage { get; }
    public ObservableGauge<double> AgentMemoryUsage { get; }

    // Cache efficiency
    public Counter<long> CacheHits { get; }
    public Counter<long> CacheMisses { get; }
    public ObservableGauge<double> CacheHitRate { get; }
}
```

### Grafana Dashboards

**Build Server Overview Dashboard**:
- Build success rate (last 24h, 7d, 30d)
- Average build duration by strategy
- Queue length over time
- Agent utilization (busy vs. idle)
- Cache hit rate
- Top 10 slowest builds
- Build failures by project

**Agent Health Dashboard**:
- Agent status (online/offline/building)
- CPU usage per agent
- Memory usage per agent
- Disk space per agent
- Concurrent builds per agent
- Agent uptime

---

## Security Considerations

### 1. Authentication and Authorization

**Roles**:
- **Developer**: Can trigger builds, view build status, download artifacts
- **BuildMaster**: All Developer permissions + cancel builds, manage agents
- **Admin**: All permissions + configure system, manage secrets

**Implementation**:
```csharp
[ApiController]
[Route("api/v1/builds")]
[Authorize] // All endpoints require authentication
public class BuildsController : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Developer,BuildMaster,Admin")]
    public async Task<IActionResult> CreateBuild([FromBody] CreateBuildRequest request)
    {
        // Only authenticated users with Developer+ role can create builds
    }

    [HttpPost("{executionId}/cancel")]
    [Authorize(Roles = "BuildMaster,Admin")]
    public async Task<IActionResult> CancelBuild(Guid executionId)
    {
        // Only BuildMaster+ can cancel builds
    }
}
```

### 2. Artifact Signing

All build artifacts are signed using code signing certificates:

```csharp
public class ArtifactVerifier
{
    public async Task<bool> SignArtifactAsync(
        string artifactPath,
        X509Certificate2 signingCertificate)
    {
        // 1. Compute SHA256 hash of artifact
        var hash = ComputeSHA256(artifactPath);

        // 2. Sign hash with private key
        var signature = SignData(hash, signingCertificate);

        // 3. Attach signature to artifact metadata
        await StoreSignatureAsync(artifactPath, signature);

        return true;
    }

    public async Task<bool> VerifyArtifactAsync(string artifactPath)
    {
        // 1. Retrieve signature from metadata
        var signature = await GetSignatureAsync(artifactPath);

        // 2. Compute current hash
        var currentHash = ComputeSHA256(artifactPath);

        // 3. Verify signature with public key
        return VerifySignature(currentHash, signature);
    }
}
```

### 3. Secret Management

Build secrets (API keys, connection strings, certificates) stored in Azure Key Vault:

```csharp
public class SecretManager
{
    private readonly IKeyVaultClient _keyVault;

    public async Task<string> GetSecretAsync(string secretName)
    {
        // Retrieve secret from Azure Key Vault
        var secret = await _keyVault.GetSecretAsync(secretName);

        // Audit secret access
        _logger.LogInformation("Secret {SecretName} accessed by {User}",
            secretName, _currentUser.Email);

        return secret.Value;
    }
}
```

**Usage in Builds**:
- Secrets injected as environment variables during build
- Never logged or stored in build outputs
- Masked in build logs (e.g., `***REDACTED***`)

### 4. Sandboxed Builds

Each build runs in an isolated container to prevent cross-contamination:

```dockerfile
# Build agent container
FROM mcr.microsoft.com/dotnet/sdk:8.0

# Run as non-root user
RUN useradd -m builduser
USER builduser

# Isolated workspace
WORKDIR /workspace

# Limited network access (no outbound internet except NuGet)
# Resource limits (CPU, memory, disk)
```

---

## Scalability and Performance

### Horizontal Scaling

**Add More Build Agents**:
```bash
# Deploy 10 more Linux build agents
kubectl scale deployment build-agents-linux --replicas=20

# Agents auto-register with orchestrator
# Load balancing automatic
```

**Agent Auto-Scaling (Kubernetes HPA)**:
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: build-agents-linux
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: build-agents-linux
  minReplicas: 5
  maxReplicas: 50
  metrics:
  - type: Pods
    pods:
      metric:
        name: build_queue_length
      target:
        type: AverageValue
        averageValue: "2"  # Scale up if >2 builds queued per agent
```

### Performance Benchmarks

**Single Agent Performance**:
- Small project (5 projects): 2-3 minutes
- Medium solution (20 projects): 10-15 minutes
- Large monorepo (100 projects): 60-90 minutes (sequential)

**Multi-Agent Performance (10 agents, distributed strategy)**:
- Small project: 2-3 minutes (no benefit)
- Medium solution: 4-6 minutes (2.5x faster)
- Large monorepo: 15-25 minutes (4x faster)

**Cache Performance**:
- No cache: 15 minutes
- 50% cache hit: 8 minutes
- 80% cache hit: 3 minutes
- 100% cache hit: 30 seconds (validation only)

### Throughput Capacity

**Single Build Orchestrator**:
- Can handle: 1000+ concurrent builds
- Queue processing: 10,000 jobs/hour
- Bottleneck: Database writes (PostgreSQL can handle 10k writes/sec)

**Redis Performance**:
- Cache operations: 100k ops/sec
- Job queue: 50k enqueue/dequeue per sec

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-2)

**Tasks**:
1. Create domain models (BuildJobDescriptor, BuildRequest, BuildResult)
2. Implement BuildAgent (basic functionality)
3. Implement AgentPool (agent registration and selection)
4. Implement IncrementalBuildStrategy
5. Implement BuildPipeline (clone, restore, build, test stages)
6. Create BuildOrchestrator (job queue, agent coordination)
7. Add basic telemetry (OpenTelemetry traces)

**Deliverable**: Single-agent build server that can build .NET solutions

---

### Phase 2: API and Web Interface (Weeks 3-4)

**Tasks**:
1. Implement BuildsController API endpoints
2. Implement AgentsController API endpoints
3. Implement ArtifactsController API endpoints
4. Add authentication (JWT tokens)
5. Add authorization (role-based access control)
6. Create Swagger/OpenAPI documentation
7. Build simple web UI (React/Blazor) for build monitoring

**Deliverable**: REST API for build management with auth

---

### Phase 3: Advanced Strategies (Weeks 5-6)

**Tasks**:
1. Implement CleanBuildStrategy
2. Implement CachedBuildStrategy (Redis integration)
3. Implement DistributedBuildStrategy (solution graph analysis)
4. Implement CanaryBuildStrategy
5. Add build result caching
6. Add NuGet package caching
7. Add artifact caching

**Deliverable**: Multiple build strategies with caching

---

### Phase 4: Scaling and Performance (Weeks 7-8)

**Tasks**:
1. Add multi-agent support (agent pool management)
2. Add build queue prioritization
3. Add agent auto-scaling (Kubernetes HPA)
4. Optimize build parallelization
5. Add build artifact compression
6. Add incremental test execution
7. Performance testing and tuning

**Deliverable**: Horizontally scalable build system

---

### Phase 5: Monitoring and Operations (Weeks 9-10)

**Tasks**:
1. Add Prometheus metrics
2. Create Grafana dashboards
3. Add alerting (build failures, agent health)
4. Add audit logging
5. Add build retention policies
6. Add build history analytics
7. Create operational runbooks

**Deliverable**: Production-ready monitoring and ops

---

### Phase 6: Enterprise Features (Weeks 11-12)

**Tasks**:
1. Add build approval workflows
2. Add build artifact signing
3. Add secret management (Azure Key Vault)
4. Add multi-tenancy support
5. Add build cost tracking
6. Add SLA monitoring
7. Add disaster recovery procedures

**Deliverable**: Enterprise-grade build server

---

## Conclusion

This high-level design provides a comprehensive blueprint for building a **distributed .NET build server** using the HotSwap.Distributed framework. The design:

✅ **Leverages existing framework components** (orchestrator, strategies, telemetry)
✅ **Scales horizontally** (add more build agents)
✅ **Optimizes performance** (caching, distributed builds, incremental builds)
✅ **Provides observability** (OpenTelemetry, metrics, logs)
✅ **Ensures security** (auth, artifact signing, secret management)
✅ **Supports multiple platforms** (Linux, Windows, macOS, Docker)
✅ **Production-ready** (monitoring, alerts, disaster recovery)

**Next Steps**:
1. Review and approve this design
2. Begin Phase 1 implementation (Foundation)
3. Set up development environment and CI/CD pipeline
4. Create project backlog from implementation roadmap

---

**Document Version**: 1.0
**Last Updated**: 2025-11-15
**Author**: Claude (AI Assistant)
**Status**: Draft - Awaiting Review
