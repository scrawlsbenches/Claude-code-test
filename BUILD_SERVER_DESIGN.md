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
3. [Framework Reusability Guide](#framework-reusability-guide)
4. [Domain Model](#domain-model)
5. [Build Strategies](#build-strategies)
6. [Build Agent Architecture](#build-agent-architecture)
7. [API Design](#api-design)
8. [Build Pipeline Flow](#build-pipeline-flow)
9. [Caching and Optimization](#caching-and-optimization)
10. [Monitoring and Telemetry](#monitoring-and-telemetry)
11. [Security Considerations](#security-considerations)
12. [Scalability and Performance](#scalability-and-performance)
13. [Implementation Roadmap](#implementation-roadmap)
14. [Implementation Quick Start](#implementation-quick-start)
15. [Extension Patterns](#extension-patterns)
16. [Common Patterns](#common-patterns)

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

## Framework Reusability Guide

This section provides a comprehensive guide on which HotSwap.Distributed framework components can be **reused as-is**, which need **adaptation**, and which must be **created from scratch**.

### ✅ Reuse As-Is (No Changes Needed)

These components can be used directly in the build server without modification:

#### 1. **Infrastructure Services**

```csharp
// ✅ TelemetryProvider - Use for distributed tracing
using HotSwap.Distributed.Infrastructure.Telemetry;

var telemetry = new TelemetryProvider();
using var activity = telemetry.StartBuildActivity(buildJobName);
```

**Location**: `src/HotSwap.Distributed.Infrastructure/Telemetry/TelemetryProvider.cs`

**Usage in Build Server**:
- Trace build operations across agents
- Create activities for each build stage (clone, restore, build, test)
- Propagate trace context between orchestrator and agents
- Export traces to Jaeger for visualization

---

#### 2. **Metrics Collection**

```csharp
// ✅ IMetricsProvider & InMemoryMetricsProvider - Use for build metrics
using HotSwap.Distributed.Infrastructure.Metrics;
using HotSwap.Distributed.Infrastructure.Interfaces;

var metricsProvider = new InMemoryMetricsProvider(logger);
await metricsProvider.RecordBuildMetricAsync(buildId, "build_duration", duration);
```

**Location**:
- Interface: `src/HotSwap.Distributed.Infrastructure/Interfaces/IMetricsProvider.cs`
- Implementation: `src/HotSwap.Distributed.Infrastructure/Metrics/InMemoryMetricsProvider.cs`

**Usage in Build Server**:
- Record build durations, queue times, agent utilization
- Track cache hit rates, test pass rates
- Monitor agent health metrics (CPU, memory, disk)

---

#### 3. **Module Verification (Artifact Verification)**

```csharp
// ✅ ModuleVerifier - Reuse for artifact verification
using HotSwap.Distributed.Infrastructure.Security;

var verifier = new ModuleVerifier(logger);
var isValid = await verifier.VerifyModuleSignatureAsync(artifactPath, signature);
```

**Location**: `src/HotSwap.Distributed.Infrastructure/Security/ModuleVerifier.cs`

**Usage in Build Server**:
- Verify build artifact signatures (DLLs, NuGet packages, executables)
- Validate artifact checksums before upload/download
- Ensure artifact integrity during distribution

---

#### 4. **Configuration Models**

```csharp
// ✅ PipelineConfiguration - Extend for build pipeline config
using HotSwap.Distributed.Orchestrator.Pipeline;

var pipelineConfig = new PipelineConfiguration
{
    QaMaxConcurrentNodes = 5,
    CanaryWaitDuration = TimeSpan.FromMinutes(5)
};
```

**Location**: `src/HotSwap.Distributed.Orchestrator/Pipeline/PipelineConfiguration.cs`

**Usage in Build Server**:
- Configure build timeouts
- Set max concurrent builds per agent
- Define retry policies

---

### 🔧 Adapt and Extend (Requires Modification)

These components need to be adapted for build server semantics but follow the same patterns:

#### 1. **Strategy Interface → Build Strategy Interface**

**Existing**: `IDeploymentStrategy`

```csharp
// 🔧 ADAPT: Change semantics from "deploy" to "build"
namespace BuildServer.Orchestrator.Interfaces;

public interface IBuildStrategy
{
    string StrategyName { get; }

    Task<BuildResult> BuildAsync(
        BuildJobDescriptor job,      // Instead of ModuleDeploymentRequest
        AgentPool agentPool,         // Instead of EnvironmentCluster
        CancellationToken cancellationToken = default);
}
```

**Pattern to Follow**: Look at existing strategies in `src/HotSwap.Distributed.Orchestrator/Strategies/`
- `CanaryDeploymentStrategy.cs` → `CanaryBuildStrategy.cs`
- `RollingDeploymentStrategy.cs` → `IncrementalBuildStrategy.cs`
- `DirectDeploymentStrategy.cs` → `CleanBuildStrategy.cs`

**Key Adaptation**:
- Keep the strategy pattern structure
- Replace deployment logic with build logic
- Reuse telemetry, metrics, and error handling patterns

---

#### 2. **Orchestrator → Build Orchestrator**

**Existing**: `DistributedKernelOrchestrator`

```csharp
// 🔧 ADAPT: Extend for build operations
namespace BuildServer.Orchestrator.Core;

public class BuildOrchestrator : IClusterRegistry, IAsyncDisposable
{
    private readonly ILogger<BuildOrchestrator> _logger;
    private readonly TelemetryProvider _telemetry;  // ✅ Reuse as-is
    private readonly IMetricsProvider _metrics;     // ✅ Reuse as-is
    private readonly ConcurrentDictionary<BuildTargetType, AgentPool> _agentPools;

    // Similar structure to DistributedKernelOrchestrator
    public async Task<BuildExecutionResult> ExecuteBuildPipelineAsync(
        BuildRequest request,
        CancellationToken cancellationToken)
    {
        // Follow same pattern as ExecuteDeploymentPipelineAsync
    }
}
```

**Reference**: `src/HotSwap.Distributed.Orchestrator/Core/DistributedKernelOrchestrator.cs`

**Key Points**:
- Keep the same orchestrator architecture
- Reuse cluster registry pattern
- Adapt initialization to create agent pools instead of environment clusters
- Maintain same telemetry and metrics integration

---

#### 3. **Pipeline → Build Pipeline**

**Existing**: `DeploymentPipeline`

```csharp
// 🔧 ADAPT: Change stages from deployment to build
namespace BuildServer.Orchestrator.Pipeline;

public class BuildPipeline : IDisposable
{
    private readonly ILogger<BuildPipeline> _logger;
    private readonly TelemetryProvider _telemetry;    // ✅ Reuse
    private readonly IModuleVerifier _verifier;       // ✅ Reuse as ArtifactVerifier

    public async Task<BuildExecutionResult> ExecutePipelineAsync(
        BuildRequest request,
        CancellationToken cancellationToken)
    {
        // Stage 1: Clone repository
        // Stage 2: Restore dependencies
        // Stage 3: Build
        // Stage 4: Test
        // Stage 5: Package
        // Stage 6: Publish artifacts
    }
}
```

**Reference**: `src/HotSwap.Distributed.Orchestrator/Pipeline/DeploymentPipeline.cs`

**Key Points**:
- Follow multi-stage pipeline pattern
- Each stage returns a result with timing, success/failure
- Stages can be skipped based on configuration
- Use same telemetry span pattern for each stage

---

#### 4. **Cluster → Agent Pool**

**Existing**: `EnvironmentCluster`

```csharp
// 🔧 ADAPT: Rename and adjust for build agents
namespace BuildServer.Orchestrator.Core;

public class AgentPool : IAsyncDisposable
{
    public BuildTargetType TargetType { get; set; }  // Linux, Windows, macOS
    public List<BuildAgent> Agents { get; set; }     // Instead of KernelNode

    public BuildAgent? SelectAgent(BuildJobDescriptor job)
    {
        // Similar to how cluster selects nodes
        // Filter by capabilities, prefer idle agents
    }

    public async Task<PoolHealth> GetHealthAsync()
    {
        // Similar to ClusterHealth
    }
}
```

**Reference**: `src/HotSwap.Distributed.Domain/Models/EnvironmentCluster.cs`

**Key Points**:
- Maintain the same pooling/clustering concept
- Keep health check patterns
- Adapt node selection to agent selection

---

### ➕ Create New (Build-Specific Components)

These components are unique to the build server and must be created from scratch:

#### 1. **BuildAgent (New Implementation)**

```csharp
// ➕ CREATE NEW: Build-specific agent implementation
namespace BuildServer.Orchestrator.Core;

public class BuildAgent
{
    // NEW: Build-specific capabilities
    public BuildAgentCapabilities Capabilities { get; set; }

    // NEW: Execute build job
    public async Task<BuildResult> ExecuteBuildAsync(
        BuildJobDescriptor job,
        CancellationToken cancellationToken)
    {
        // 1. Clone Git repository
        // 2. Restore NuGet packages
        // 3. Run dotnet build
        // 4. Run dotnet test
        // 5. Create artifacts
    }
}

public class BuildAgentCapabilities
{
    public List<string> InstalledSdks { get; set; }      // "net8.0", "net9.0"
    public List<string> InstalledTools { get; set; }     // "docker", "git"
    public long AvailableDiskSpaceGB { get; set; }
}
```

**Why New**: Build execution is fundamentally different from module deployment. Need Git operations, NuGet restore, MSBuild/dotnet CLI integration.

---

#### 2. **BuildJobDescriptor (New Model)**

```csharp
// ➕ CREATE NEW: Build job specification
namespace BuildServer.Domain.Models;

public class BuildJobDescriptor
{
    public required string ProjectName { get; set; }
    public required string RepositoryUrl { get; set; }
    public required string GitRef { get; set; }
    public required string SolutionPath { get; set; }
    public string Configuration { get; set; } = "Release";
    public Dictionary<string, string> BuildProperties { get; set; } = new();
}
```

**Why New**: Build jobs have unique properties (Git repo, solution path, configuration) not present in ModuleDescriptor.

---

#### 3. **Build Strategies (New Implementations)**

```csharp
// ➕ CREATE NEW: Build-specific strategies
namespace BuildServer.Orchestrator.Strategies;

// Incremental build strategy
public class IncrementalBuildStrategy : IBuildStrategy
{
    public async Task<BuildResult> BuildAsync(...)
    {
        // Only rebuild changed projects
        // Use dotnet build with incremental compilation
    }
}

// Distributed build strategy
public class DistributedBuildStrategy : IBuildStrategy
{
    public async Task<BuildResult> BuildAsync(...)
    {
        // Parse solution dependency graph
        // Distribute projects across agents
        // Build in waves
    }
}

// Cached build strategy
public class CachedBuildStrategy : IBuildStrategy
{
    private readonly IDistributedCache _cache;

    public async Task<BuildResult> BuildAsync(...)
    {
        // Check cache for compiled outputs
        // Restore from cache if available
        // Build only cache misses
    }
}
```

**Why New**: Build strategies require .NET-specific logic (MSBuild, solution parsing, NuGet) not applicable to generic deployment.

---

#### 4. **Build API Controllers (New)**

```csharp
// ➕ CREATE NEW: REST API for builds
namespace BuildServer.Api.Controllers;

[ApiController]
[Route("api/v1/builds")]
public class BuildsController : ControllerBase
{
    private readonly BuildOrchestrator _orchestrator;

    [HttpPost]
    public async Task<IActionResult> CreateBuild([FromBody] CreateBuildRequest request)
    {
        // Queue build job
        // Return 202 Accepted
    }

    [HttpGet("{executionId}")]
    public IActionResult GetBuildStatus(Guid executionId)
    {
        // Return build status and results
    }
}
```

**Why New**: Build-specific API endpoints with build job semantics.

---

### 📋 Reusability Summary Table

| Component | Action | Effort | Notes |
|-----------|--------|--------|-------|
| **TelemetryProvider** | ✅ Reuse | 0 hours | Use as-is for tracing |
| **IMetricsProvider** | ✅ Reuse | 0 hours | Use for build metrics |
| **ModuleVerifier** | ✅ Reuse | 0 hours | Rename to ArtifactVerifier |
| **PipelineConfiguration** | ✅ Reuse | 1 hour | Extend with build-specific config |
| **IDeploymentStrategy** | 🔧 Adapt | 4 hours | Create IBuildStrategy interface |
| **CanaryDeploymentStrategy** | 🔧 Adapt | 8 hours | Pattern for CanaryBuildStrategy |
| **DistributedKernelOrchestrator** | 🔧 Adapt | 16 hours | Pattern for BuildOrchestrator |
| **DeploymentPipeline** | 🔧 Adapt | 12 hours | Pattern for BuildPipeline |
| **EnvironmentCluster** | 🔧 Adapt | 6 hours | Pattern for AgentPool |
| **BuildAgent** | ➕ Create | 24 hours | New implementation with Git/dotnet CLI |
| **BuildJobDescriptor** | ➕ Create | 2 hours | New domain model |
| **Build Strategies** | ➕ Create | 40 hours | 5 strategies × 8 hours each |
| **Build API Controllers** | ➕ Create | 16 hours | REST endpoints for builds |
| **Agent Capabilities** | ➕ Create | 8 hours | SDK/tool detection |
| **Build Cache Service** | ➕ Create | 16 hours | Redis-based caching |
| **TOTAL EFFORT** | | **153 hours** | ~4 weeks (1 developer) |

---

### 🎯 Recommended Implementation Order

**Week 1: Foundation (Reuse existing infrastructure)**
1. ✅ Copy and set up `TelemetryProvider` (0 hours - already works)
2. ✅ Copy and set up `IMetricsProvider` (0 hours - already works)
3. ✅ Copy and set up `ModuleVerifier` → `ArtifactVerifier` (1 hour)
4. ➕ Create `BuildJobDescriptor` model (2 hours)
5. ➕ Create `BuildRequest` model (1 hour)
6. ➕ Create `BuildResult` model (2 hours)

**Week 2: Core Build Logic**
1. ➕ Create `BuildAgent` class (24 hours)
   - Git clone implementation
   - dotnet restore implementation
   - dotnet build implementation
   - dotnet test implementation
2. ➕ Create `AgentCapabilities` detection (8 hours)

**Week 3: Orchestration (Adapt existing patterns)**
1. 🔧 Create `IBuildStrategy` interface (2 hours)
2. ➕ Implement `IncrementalBuildStrategy` (8 hours)
3. ➕ Implement `CleanBuildStrategy` (8 hours)
4. 🔧 Create `AgentPool` (adapt from `EnvironmentCluster`) (6 hours)
5. 🔧 Create `BuildPipeline` (adapt from `DeploymentPipeline`) (12 hours)

**Week 4: API and Advanced Features**
1. 🔧 Create `BuildOrchestrator` (adapt from `DistributedKernelOrchestrator`) (16 hours)
2. ➕ Create `BuildsController` API (8 hours)
3. ➕ Create `AgentsController` API (4 hours)
4. ➕ Implement `CachedBuildStrategy` (12 hours)
5. ➕ Implement `DistributedBuildStrategy` (16 hours)

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

## Implementation Quick Start

This section provides a step-by-step guide to implement the build server, with concrete code examples showing exactly how to leverage the existing HotSwap.Distributed framework.

### Step 1: Set Up Project Structure

Create the following project structure that mirrors HotSwap.Distributed:

```
BuildServer/
├── src/
│   ├── BuildServer.Domain/
│   │   ├── Models/
│   │   │   ├── BuildJobDescriptor.cs
│   │   │   ├── BuildRequest.cs
│   │   │   ├── BuildResult.cs
│   │   │   └── BuildArtifact.cs
│   │   └── Enums/
│   │       ├── BuildTargetType.cs
│   │       └── BuildStrategyType.cs
│   │
│   ├── BuildServer.Infrastructure/  (Reuse HotSwap.Distributed.Infrastructure)
│   │   └── Reference: HotSwap.Distributed.Infrastructure project
│   │
│   ├── BuildServer.Orchestrator/
│   │   ├── Core/
│   │   │   ├── BuildOrchestrator.cs
│   │   │   ├── BuildAgent.cs
│   │   │   └── AgentPool.cs
│   │   ├── Strategies/
│   │   │   ├── IBuildStrategy.cs
│   │   │   ├── IncrementalBuildStrategy.cs
│   │   │   ├── CleanBuildStrategy.cs
│   │   │   ├── CachedBuildStrategy.cs
│   │   │   └── DistributedBuildStrategy.cs
│   │   └── Pipeline/
│   │       └── BuildPipeline.cs
│   │
│   └── BuildServer.Api/
│       ├── Controllers/
│       │   ├── BuildsController.cs
│       │   ├── AgentsController.cs
│       │   └── ArtifactsController.cs
│       ├── Models/
│       │   └── ApiModels.cs
│       └── Program.cs
│
└── tests/
    └── BuildServer.Tests/
        ├── Strategies/
        └── Core/
```

### Step 2: Reference HotSwap.Distributed Projects

**In BuildServer.Orchestrator.csproj**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference existing HotSwap.Distributed infrastructure -->
    <ProjectReference Include="..\..\HotSwap.Distributed\src\HotSwap.Distributed.Infrastructure\HotSwap.Distributed.Infrastructure.csproj" />

    <!-- Your build server domain -->
    <ProjectReference Include="..\BuildServer.Domain\BuildServer.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Same packages as HotSwap.Distributed -->
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageReference Include="OpenTelemetry" Version="1.9.0" />
  </ItemGroup>
</Project>
```

### Step 3: Implement IBuildStrategy (Adapt IDeploymentStrategy)

**File**: `src/BuildServer.Orchestrator/Strategies/IBuildStrategy.cs`

```csharp
using BuildServer.Domain.Models;
using BuildServer.Orchestrator.Core;

namespace BuildServer.Orchestrator.Strategies;

/// <summary>
/// Strategy for executing builds (adapted from IDeploymentStrategy).
/// </summary>
public interface IBuildStrategy
{
    /// <summary>
    /// Name of the strategy.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Executes a build using this strategy.
    /// </summary>
    Task<BuildResult> BuildAsync(
        BuildJobDescriptor job,
        AgentPool agentPool,
        CancellationToken cancellationToken = default);
}
```

### Step 4: Implement IncrementalBuildStrategy (Following CanaryDeploymentStrategy Pattern)

**File**: `src/BuildServer.Orchestrator/Strategies/IncrementalBuildStrategy.cs`

```csharp
using BuildServer.Domain.Models;
using BuildServer.Orchestrator.Core;
using Microsoft.Extensions.Logging;

namespace BuildServer.Orchestrator.Strategies;

/// <summary>
/// Incremental build strategy - only rebuilds changed projects.
/// Pattern adapted from DirectDeploymentStrategy and CanaryDeploymentStrategy.
/// </summary>
public class IncrementalBuildStrategy : IBuildStrategy
{
    private readonly ILogger<IncrementalBuildStrategy> _logger;

    public string StrategyName => "Incremental";

    public IncrementalBuildStrategy(ILogger<IncrementalBuildStrategy> logger)
    {
        _logger = logger;
    }

    public async Task<BuildResult> BuildAsync(
        BuildJobDescriptor job,
        AgentPool agentPool,
        CancellationToken cancellationToken = default)
    {
        // ✅ PATTERN: Same structure as CanaryDeploymentStrategy
        var result = new BuildResult
        {
            Strategy = StrategyName,
            TargetType = agentPool.TargetType,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation(
                "Starting incremental build of {ProjectName} on {TargetType}",
                job.ProjectName, agentPool.TargetType);

            // ✅ PATTERN: Select agent (same as cluster.SelectNode)
            var agent = agentPool.SelectAgent(job);
            if (agent == null)
            {
                result.Success = false;
                result.Message = "No available agents in pool";
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation(
                "Selected agent {AgentId} for build",
                agent.AgentId);

            // ✅ PATTERN: Execute on agent (same as node.DeployModuleAsync)
            var buildResult = await agent.ExecuteBuildAsync(job, cancellationToken);

            result.Success = buildResult.Success;
            result.Message = buildResult.Message;
            result.Artifacts = buildResult.Artifacts;
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Incremental build {Status} for {ProjectName}",
                result.Success ? "succeeded" : "failed",
                job.ProjectName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Incremental build failed for {ProjectName}",
                job.ProjectName);

            result.Success = false;
            result.Message = $"Build failed: {ex.Message}";
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }
}
```

### Step 5: Implement BuildAgent (New Component with Framework Integration)

**File**: `src/BuildServer.Orchestrator/Core/BuildAgent.cs`

```csharp
using System.Diagnostics;
using BuildServer.Domain.Enums;
using BuildServer.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BuildServer.Orchestrator.Core;

/// <summary>
/// Build agent that executes build jobs.
/// New implementation that integrates with HotSwap infrastructure.
/// </summary>
public class BuildAgent
{
    private readonly ILogger<BuildAgent> _logger;
    private readonly string _workspaceRoot;

    public Guid AgentId { get; }
    public string Hostname { get; }
    public BuildTargetType TargetType { get; }
    public BuildAgentStatus Status { get; private set; }
    public BuildAgentCapabilities Capabilities { get; }
    public DateTime LastHeartbeat { get; private set; }

    public BuildAgent(
        string hostname,
        BuildTargetType targetType,
        ILogger<BuildAgent> logger)
    {
        AgentId = Guid.NewGuid();
        Hostname = hostname;
        TargetType = targetType;
        Status = BuildAgentStatus.Idle;
        _logger = logger;
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "build-workspace", AgentId.ToString());
        Capabilities = DetectCapabilities();
        LastHeartbeat = DateTime.UtcNow;

        Directory.CreateDirectory(_workspaceRoot);
    }

    /// <summary>
    /// Executes a build job on this agent.
    /// </summary>
    public async Task<BuildResult> ExecuteBuildAsync(
        BuildJobDescriptor job,
        CancellationToken cancellationToken)
    {
        Status = BuildAgentStatus.Building;
        LastHeartbeat = DateTime.UtcNow;

        var result = new BuildResult
        {
            JobId = job.JobId,
            ProjectName = job.ProjectName,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation(
                "Agent {AgentId} starting build for {ProjectName}",
                AgentId, job.ProjectName);

            var workspace = Path.Combine(_workspaceRoot, job.JobId.ToString());
            Directory.CreateDirectory(workspace);

            // Stage 1: Clone repository
            _logger.LogInformation("Cloning repository {Repo}", job.RepositoryUrl);
            await CloneRepositoryAsync(job.RepositoryUrl, job.GitRef, workspace, cancellationToken);

            // Stage 2: Restore dependencies
            _logger.LogInformation("Restoring dependencies");
            await RestoreDependenciesAsync(workspace, job.SolutionPath, cancellationToken);

            // Stage 3: Build
            _logger.LogInformation("Building solution");
            await BuildSolutionAsync(workspace, job.SolutionPath, job.Configuration, cancellationToken);

            // Stage 4: Run tests (if enabled)
            if (job.RunTests)
            {
                _logger.LogInformation("Running tests");
                await RunTestsAsync(workspace, job.SolutionPath, cancellationToken);
            }

            // Stage 5: Package artifacts (if enabled)
            if (job.CreatePackages)
            {
                _logger.LogInformation("Creating packages");
                var artifacts = await CreatePackagesAsync(workspace, job.SolutionPath, cancellationToken);
                result.Artifacts = artifacts;
            }

            result.Success = true;
            result.Message = "Build completed successfully";
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Agent {AgentId} completed build for {ProjectName} in {Duration}s",
                AgentId, job.ProjectName, result.Duration.TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Agent {AgentId} build failed for {ProjectName}",
                AgentId, job.ProjectName);

            result.Success = false;
            result.Message = $"Build failed: {ex.Message}";
            result.EndTime = DateTime.UtcNow;
            return result;
        }
        finally
        {
            Status = BuildAgentStatus.Idle;
            LastHeartbeat = DateTime.UtcNow;
        }
    }

    private async Task CloneRepositoryAsync(
        string repoUrl,
        string gitRef,
        string workspace,
        CancellationToken cancellationToken)
    {
        // Execute: git clone --depth 1 --branch {gitRef} {repoUrl} {workspace}
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone --depth 1 --branch {gitRef} {repoUrl} {workspace}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Git clone failed: {error}");
        }
    }

    private async Task RestoreDependenciesAsync(
        string workspace,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        // Execute: dotnet restore {solutionPath}
        var fullPath = Path.Combine(workspace, solutionPath);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"restore \"{fullPath}\"",
                WorkingDirectory = workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Restore failed: {error}");
        }
    }

    private async Task BuildSolutionAsync(
        string workspace,
        string solutionPath,
        string configuration,
        CancellationToken cancellationToken)
    {
        // Execute: dotnet build {solutionPath} -c {configuration} --no-restore
        var fullPath = Path.Combine(workspace, solutionPath);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{fullPath}\" -c {configuration} --no-restore",
                WorkingDirectory = workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Build failed: {error}");
        }
    }

    private async Task RunTestsAsync(
        string workspace,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        // Execute: dotnet test {solutionPath} --no-build
        var fullPath = Path.Combine(workspace, solutionPath);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{fullPath}\" --no-build",
                WorkingDirectory = workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Tests failed: {error}");
        }
    }

    private async Task<List<BuildArtifact>> CreatePackagesAsync(
        string workspace,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        // Execute: dotnet pack {solutionPath} --no-build
        var fullPath = Path.Combine(workspace, solutionPath);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack \"{fullPath}\" --no-build",
                WorkingDirectory = workspace,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Pack failed: {error}");
        }

        // Find all .nupkg files in workspace
        var artifacts = new List<BuildArtifact>();
        var nupkgFiles = Directory.GetFiles(workspace, "*.nupkg", SearchOption.AllDirectories);

        foreach (var file in nupkgFiles)
        {
            var fileInfo = new FileInfo(file);
            artifacts.Add(new BuildArtifact
            {
                Name = fileInfo.Name,
                Path = file,
                SizeBytes = fileInfo.Length,
                CreatedAt = DateTime.UtcNow
            });
        }

        return artifacts;
    }

    private BuildAgentCapabilities DetectCapabilities()
    {
        // Detect installed .NET SDKs, tools, etc.
        var capabilities = new BuildAgentCapabilities
        {
            InstalledSdks = new List<string>(),
            InstalledTools = new List<string>(),
            MaxConcurrentBuilds = 1,
            AvailableDiskSpaceGB = GetAvailableDiskSpace(),
            CpuCores = Environment.ProcessorCount,
            TotalMemoryGB = GetTotalMemory()
        };

        // Check for .NET SDKs
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-sdks",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse SDK versions from output
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        capabilities.InstalledSdks.Add(parts[0]);
                    }
                }
            }
        }
        catch
        {
            _logger.LogWarning("Could not detect .NET SDKs");
        }

        // Check for Git
        if (IsToolInstalled("git"))
        {
            capabilities.InstalledTools.Add("git");
        }

        // Check for Docker
        if (IsToolInstalled("docker"))
        {
            capabilities.InstalledTools.Add("docker");
        }

        return capabilities;
    }

    private bool IsToolInstalled(string toolName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = toolName,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private long GetAvailableDiskSpace()
    {
        var drive = new DriveInfo(Path.GetPathRoot(_workspaceRoot)!);
        return drive.AvailableFreeSpace / (1024 * 1024 * 1024); // GB
    }

    private long GetTotalMemory()
    {
        // Simplified - would use platform-specific APIs in production
        return 16; // Default 16GB
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
    public List<string> InstalledSdks { get; set; } = new();
    public List<string> InstalledTools { get; set; } = new();
    public int MaxConcurrentBuilds { get; set; }
    public long AvailableDiskSpaceGB { get; set; }
    public int CpuCores { get; set; }
    public long TotalMemoryGB { get; set; }
}
```

### Step 6: Implement BuildOrchestrator (Following DistributedKernelOrchestrator Pattern)

**File**: `src/BuildServer.Orchestrator/Core/BuildOrchestrator.cs`

```csharp
using System.Collections.Concurrent;
using BuildServer.Domain.Enums;
using BuildServer.Domain.Models;
using BuildServer.Orchestrator.Interfaces;
using BuildServer.Orchestrator.Pipeline;
using BuildServer.Orchestrator.Strategies;
using HotSwap.Distributed.Infrastructure.Interfaces;  // ✅ Reuse
using HotSwap.Distributed.Infrastructure.Metrics;    // ✅ Reuse
using HotSwap.Distributed.Infrastructure.Telemetry; // ✅ Reuse
using Microsoft.Extensions.Logging;

namespace BuildServer.Orchestrator.Core;

/// <summary>
/// Central orchestrator for the build server.
/// Pattern adapted from DistributedKernelOrchestrator.
/// </summary>
public class BuildOrchestrator : IAsyncDisposable
{
    private readonly ILogger<BuildOrchestrator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMetricsProvider _metricsProvider;      // ✅ Reuse from HotSwap
    private readonly TelemetryProvider _telemetry;           // ✅ Reuse from HotSwap
    private readonly ConcurrentDictionary<BuildTargetType, AgentPool> _agentPools;
    private readonly Dictionary<BuildStrategyType, IBuildStrategy> _strategies;
    private BuildPipeline? _pipeline;
    private bool _initialized;
    private bool _disposed;

    public BuildOrchestrator(
        ILogger<BuildOrchestrator> logger,
        ILoggerFactory loggerFactory,
        IMetricsProvider? metricsProvider = null,
        TelemetryProvider? telemetry = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;

        // ✅ REUSE: Same infrastructure components as HotSwap
        _metricsProvider = metricsProvider ?? new InMemoryMetricsProvider(
            _loggerFactory.CreateLogger<InMemoryMetricsProvider>());
        _telemetry = telemetry ?? new TelemetryProvider();

        _agentPools = new ConcurrentDictionary<BuildTargetType, AgentPool>();
        _strategies = new Dictionary<BuildStrategyType, IBuildStrategy>();

        _logger.LogInformation("Build Orchestrator created");
    }

    /// <summary>
    /// Initializes agent pools and strategies.
    /// Pattern from DistributedKernelOrchestrator.InitializeClustersAsync
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            _logger.LogWarning("Orchestrator already initialized");
            return;
        }

        _logger.LogInformation("Initializing build orchestrator");

        try
        {
            // Create agent pools for each target type
            foreach (var targetType in Enum.GetValues<BuildTargetType>())
            {
                var pool = new AgentPool(
                    targetType,
                    _loggerFactory.CreateLogger<AgentPool>());

                _agentPools[targetType] = pool;

                // Add sample agents to each pool
                var agentCount = targetType switch
                {
                    BuildTargetType.Linux => 5,
                    BuildTargetType.Windows => 3,
                    BuildTargetType.MacOS => 2,
                    BuildTargetType.Docker => 10,
                    _ => 1
                };

                for (int i = 0; i < agentCount; i++)
                {
                    var agent = new BuildAgent(
                        $"{targetType.ToString().ToLower()}-agent-{i + 1:D2}",
                        targetType,
                        _loggerFactory.CreateLogger<BuildAgent>());

                    pool.AddAgent(agent);
                }

                _logger.LogInformation(
                    "Initialized {TargetType} pool with {AgentCount} agents",
                    targetType, agentCount);
            }

            // Initialize build strategies
            _strategies[BuildStrategyType.Incremental] = new IncrementalBuildStrategy(
                _loggerFactory.CreateLogger<IncrementalBuildStrategy>());

            _strategies[BuildStrategyType.Clean] = new CleanBuildStrategy(
                _loggerFactory.CreateLogger<CleanBuildStrategy>());

            // Initialize build pipeline
            _pipeline = new BuildPipeline(
                _loggerFactory.CreateLogger<BuildPipeline>(),
                _telemetry,   // ✅ Reuse HotSwap telemetry
                _strategies);

            _initialized = true;

            _logger.LogInformation(
                "Orchestrator initialization completed. Total agents: {TotalAgents}",
                _agentPools.Values.Sum(p => p.AgentCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize orchestrator");
            throw;
        }
    }

    /// <summary>
    /// Executes a build pipeline.
    /// Pattern from DistributedKernelOrchestrator.ExecuteDeploymentPipelineAsync
    /// </summary>
    public async Task<BuildExecutionResult> ExecuteBuildPipelineAsync(
        BuildRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "Orchestrator not initialized. Call InitializeAsync first.");
        }

        if (_pipeline == null)
        {
            throw new InvalidOperationException("Pipeline not initialized");
        }

        _logger.LogInformation(
            "Executing build pipeline for {ProjectName} using {Strategy} strategy",
            request.Job.ProjectName, request.Strategy);

        // ✅ REUSE: Same telemetry pattern as HotSwap
        using var activity = _telemetry.StartActivity(
            "BuildOrchestrator.ExecuteBuildPipeline");

        activity?.SetTag("build.project", request.Job.ProjectName);
        activity?.SetTag("build.strategy", request.Strategy.ToString());
        activity?.SetTag("build.executionId", request.ExecutionId.ToString());

        try
        {
            var result = await _pipeline.ExecutePipelineAsync(
                request,
                _agentPools,
                cancellationToken);

            _logger.LogInformation(
                "Build pipeline completed for {ProjectName}: {Success}",
                request.Job.ProjectName,
                result.Success ? "SUCCESS" : "FAILED");

            // ✅ REUSE: Same metrics recording as HotSwap
            await _metricsProvider.RecordMetricAsync(
                "build_duration_seconds",
                result.Duration.TotalSeconds,
                new Dictionary<string, string>
                {
                    ["project"] = request.Job.ProjectName,
                    ["strategy"] = request.Strategy.ToString(),
                    ["success"] = result.Success.ToString()
                },
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Build pipeline failed for {ProjectName}",
                request.Job.ProjectName);
            throw;
        }
    }

    public AgentPool GetAgentPool(BuildTargetType targetType)
    {
        if (!_agentPools.TryGetValue(targetType, out var pool))
        {
            throw new InvalidOperationException(
                $"Agent pool for {targetType} not found");
        }
        return pool;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing Build Orchestrator");

        // Dispose pipeline
        _pipeline?.Dispose();

        // Dispose all agent pools
        var disposeTasks = _agentPools.Values.Select(p => p.DisposeAsync().AsTask());
        await Task.WhenAll(disposeTasks);

        _agentPools.Clear();

        // ✅ REUSE: Dispose telemetry
        _telemetry.Dispose();

        _disposed = true;

        _logger.LogInformation("Orchestrator disposed");
    }
}
```

### Step 7: Wire Up in ASP.NET Core API

**File**: `src/BuildServer.Api/Program.cs`

```csharp
using BuildServer.Orchestrator.Core;
using HotSwap.Distributed.Infrastructure.Telemetry;   // ✅ Reuse
using HotSwap.Distributed.Infrastructure.Metrics;     // ✅ Reuse
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ✅ REUSE: Same OpenTelemetry setup as HotSwap
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("BuildServer"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("BuildServer.*")
            .AddJaegerExporter(options =>
            {
                options.Endpoint = new Uri("http://jaeger:14268/api/traces");
            });
    });

// ✅ REUSE: Register HotSwap infrastructure components
builder.Services.AddSingleton<TelemetryProvider>();
builder.Services.AddSingleton<IMetricsProvider, InMemoryMetricsProvider>();

// Register build orchestrator
builder.Services.AddSingleton<BuildOrchestrator>();

// Initialize orchestrator on startup
builder.Services.AddHostedService<BuildOrchestratorInitializer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Hosted service to initialize orchestrator
public class BuildOrchestratorInitializer : IHostedService
{
    private readonly BuildOrchestrator _orchestrator;
    private readonly ILogger<BuildOrchestratorInitializer> _logger;

    public BuildOrchestratorInitializer(
        BuildOrchestrator orchestrator,
        ILogger<BuildOrchestratorInitializer> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing Build Orchestrator");
        await _orchestrator.InitializeAsync(cancellationToken);
        _logger.LogInformation("Build Orchestrator ready");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

### Step 8: Implement BuildsController API

**File**: `src/BuildServer.Api/Controllers/BuildsController.cs`

```csharp
using BuildServer.Api.Models;
using BuildServer.Domain.Enums;
using BuildServer.Domain.Models;
using BuildServer.Orchestrator.Core;
using Microsoft.AspNetCore.Mvc;

namespace BuildServer.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BuildsController : ControllerBase
{
    private readonly BuildOrchestrator _orchestrator;
    private readonly ILogger<BuildsController> _logger;
    private static readonly Dictionary<Guid, BuildExecutionResult> _results = new();

    public BuildsController(
        BuildOrchestrator orchestrator,
        ILogger<BuildsController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBuild(
        [FromBody] CreateBuildRequest request,
        CancellationToken cancellationToken)
    {
        var buildRequest = new BuildRequest
        {
            Job = new BuildJobDescriptor
            {
                ProjectName = request.ProjectName,
                RepositoryUrl = request.RepositoryUrl,
                GitRef = request.GitRef,
                SolutionPath = request.SolutionPath,
                Configuration = request.Configuration,
                RunTests = request.RunTests,
                CreatePackages = request.CreatePackages,
                RequesterEmail = request.RequesterEmail
            },
            TargetPool = Enum.Parse<BuildTargetType>(request.TargetPool),
            Strategy = Enum.Parse<BuildStrategyType>(request.Strategy),
            RequesterEmail = request.RequesterEmail
        };

        // Start build asynchronously
        _ = Task.Run(async () =>
        {
            var result = await _orchestrator.ExecuteBuildPipelineAsync(
                buildRequest,
                cancellationToken);

            _results[buildRequest.ExecutionId] = result;
        }, cancellationToken);

        return Accepted(new
        {
            executionId = buildRequest.ExecutionId,
            status = "Queued",
            links = new
            {
                self = $"/api/v1/builds/{buildRequest.ExecutionId}"
            }
        });
    }

    [HttpGet("{executionId}")]
    public IActionResult GetBuildStatus(Guid executionId)
    {
        if (!_results.TryGetValue(executionId, out var result))
        {
            return NotFound(new { error = "Build not found" });
        }

        return Ok(new
        {
            executionId = result.ExecutionId,
            projectName = result.ProjectName,
            status = result.Success ? "Succeeded" : "Failed",
            duration = result.Duration.ToString(),
            artifacts = result.Artifacts
        });
    }
}
```

### Summary: What You've Implemented

✅ **Reused from HotSwap.Distributed** (0 hours):
- `TelemetryProvider` for distributed tracing
- `IMetricsProvider` for metrics collection
- Infrastructure patterns and best practices

🔧 **Adapted from HotSwap.Distributed** (6 hours):
- `IBuildStrategy` interface (from `IDeploymentStrategy`)
- `IncrementalBuildStrategy` (following `CanaryDeploymentStrategy` pattern)
- `BuildOrchestrator` (following `DistributedKernelOrchestrator` pattern)

➕ **Created New** (32 hours):
- `BuildAgent` with Git/dotnet CLI integration
- `BuildJobDescriptor` domain model
- `BuildsController` API endpoints
- ASP.NET Core wiring

**Total Implementation Time**: ~38 hours (~1 week)

**Next Steps**:
1. Run `dotnet build` to verify everything compiles
2. Run `dotnet test` to verify basic functionality
3. Start the API: `dotnet run --project src/BuildServer.Api`
4. Test with Swagger UI at `http://localhost:5000/swagger`
5. Implement remaining strategies (Clean, Cached, Distributed)

---

## Extension Patterns

This section demonstrates common patterns for extending HotSwap.Distributed components for the build server.

### Pattern 1: Adding Custom Telemetry Spans

**HotSwap Pattern** (from `CanaryDeploymentStrategy.cs`):

```csharp
// Existing HotSwap pattern
using var activity = _telemetry.StartDeploymentActivity(moduleName);
activity?.SetTag("deployment.strategy", "Canary");
activity?.SetTag("deployment.environment", environment.ToString());
```

**Build Server Adaptation**:

```csharp
// Adapted for build server
using var activity = _telemetry.StartActivity("BuildAgent.ExecuteBuild");
activity?.SetTag("build.project", projectName);
activity?.SetTag("build.configuration", configuration);
activity?.SetTag("build.agent", agentId.ToString());

// Nested spans for each build stage
using var cloneActivity = _telemetry.StartActivity("BuildStage.Clone");
await CloneRepositoryAsync(...);

using var buildActivity = _telemetry.StartActivity("BuildStage.Build");
await BuildSolutionAsync(...);
```

### Pattern 2: Recording Custom Metrics

**HotSwap Pattern** (from `InMemoryMetricsProvider.cs`):

```csharp
// Existing HotSwap pattern
await _metricsProvider.RecordMetricAsync(
    "deployment_duration_seconds",
    duration.TotalSeconds,
    new Dictionary<string, string>
    {
        ["environment"] = environment.ToString(),
        ["strategy"] = strategyName
    });
```

**Build Server Adaptation**:

```csharp
// Adapted for build server
await _metricsProvider.RecordMetricAsync(
    "build_duration_seconds",
    duration.TotalSeconds,
    new Dictionary<string, string>
    {
        ["project"] = projectName,
        ["configuration"] = configuration,
        ["strategy"] = strategyName,
        ["success"] = success.ToString()
    });

// Cache hit rate metric
await _metricsProvider.RecordMetricAsync(
    "build_cache_hit_rate",
    cacheHitRate,
    new Dictionary<string, string>
    {
        ["project"] = projectName
    });
```

### Pattern 3: Strategy Selection Logic

**HotSwap Pattern** (from `DistributedKernelOrchestrator.cs`):

```csharp
// Existing HotSwap pattern
var strategy = environment switch
{
    EnvironmentType.Development => _strategies[StrategyType.Direct],
    EnvironmentType.QA => _strategies[StrategyType.Rolling],
    EnvironmentType.Production => _strategies[StrategyType.Canary],
    _ => _strategies[StrategyType.Direct]
};
```

**Build Server Adaptation**:

```csharp
// Adapted for build server
var strategy = request.Strategy switch
{
    BuildStrategyType.Incremental => _strategies[BuildStrategyType.Incremental],
    BuildStrategyType.Clean => _strategies[BuildStrategyType.Clean],
    BuildStrategyType.Cached => _strategies[BuildStrategyType.Cached],
    BuildStrategyType.Distributed => _strategies[BuildStrategyType.Distributed],
    _ => _strategies[BuildStrategyType.Incremental]
};

// Or auto-select based on build characteristics
var strategy = SelectStrategy(request.Job);

IBuildStrategy SelectStrategy(BuildJobDescriptor job)
{
    // Use Distributed strategy for large solutions
    if (job.ProjectCount > 20)
        return _strategies[BuildStrategyType.Distributed];

    // Use Cached strategy for repeated builds
    if (HasRecentBuild(job.ProjectName))
        return _strategies[BuildStrategyType.Cached];

    // Default to Incremental
    return _strategies[BuildStrategyType.Incremental];
}
```

### Pattern 4: Error Handling and Rollback

**HotSwap Pattern** (from `CanaryDeploymentStrategy.cs`):

```csharp
// Existing HotSwap rollback pattern
if (canaryFailed)
{
    _logger.LogWarning("Canary failed. Rolling back deployment.");

    result.RollbackPerformed = true;
    await RollbackAllAsync(moduleName, deployedNodes, cluster, result);

    result.Success = false;
    result.Message = "Canary metrics degraded. Rolled back deployment.";
    return result;
}
```

**Build Server Adaptation**:

```csharp
// Adapted for build server
if (buildFailed)
{
    _logger.LogWarning("Build failed on agent {AgentId}. Cleaning workspace.", agentId);

    result.CleanupPerformed = true;
    await CleanupWorkspaceAsync(workspace);

    // Retry on different agent if available
    var retryAgent = agentPool.SelectAgent(job, excludeAgents: new[] { agentId });
    if (retryAgent != null)
    {
        _logger.LogInformation("Retrying build on agent {RetryAgentId}", retryAgent.AgentId);
        result = await retryAgent.ExecuteBuildAsync(job, cancellationToken);
    }
    else
    {
        result.Success = false;
        result.Message = $"Build failed and no retry agents available.";
    }

    return result;
}
```

### Pattern 5: Health Checks

**HotSwap Pattern** (from `EnvironmentCluster.cs`):

```csharp
// Existing HotSwap pattern
public async Task<ClusterHealth> GetHealthAsync()
{
    var nodeTasks = Nodes.Select(n => n.GetHealthAsync());
    var nodeHealths = await Task.WhenAll(nodeTasks);

    return new ClusterHealth
    {
        TotalNodes = Nodes.Count,
        HealthyNodes = nodeHealths.Count(h => h.IsHealthy),
        AverageCpuUsage = nodeHealths.Average(h => h.CpuUsage),
        AverageMemoryUsage = nodeHealths.Average(h => h.MemoryUsage)
    };
}
```

**Build Server Adaptation**:

```csharp
// Adapted for build server
public async Task<AgentPoolHealth> GetHealthAsync()
{
    var agentTasks = Agents.Select(a => a.GetHealthAsync());
    var agentHealths = await Task.WhenAll(agentTasks);

    return new AgentPoolHealth
    {
        TargetType = TargetType,
        TotalAgents = Agents.Count,
        IdleAgents = Agents.Count(a => a.Status == BuildAgentStatus.Idle),
        BuildingAgents = Agents.Count(a => a.Status == BuildAgentStatus.Building),
        OfflineAgents = Agents.Count(a => a.Status == BuildAgentStatus.Offline),
        AverageCpuUsage = agentHealths.Average(h => h.CpuUsage),
        AverageMemoryUsage = agentHealths.Average(h => h.MemoryUsage),
        TotalDiskSpaceGB = agentHealths.Sum(h => h.AvailableDiskSpaceGB)
    };
}
```

---

## Common Patterns

### Telemetry Pattern (Distributed Tracing)

**When to use**: For all build operations that span multiple components

**Example** (from HotSwap.Distributed):

```csharp
// Start activity for entire build
using var buildActivity = _telemetry.StartActivity("BuildOrchestrator.ExecuteBuild");
buildActivity?.SetTag("build.project", projectName);
buildActivity?.SetTag("build.executionId", executionId.ToString());

try
{
    // Stage 1: Clone (child span)
    using var cloneActivity = _telemetry.StartActivity("BuildStage.Clone");
    await CloneRepositoryAsync(...);

    // Stage 2: Build (child span)
    using var compileActivity = _telemetry.StartActivity("BuildStage.Compile");
    await BuildSolutionAsync(...);

    buildActivity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    buildActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    buildActivity?.RecordException(ex);
    throw;
}
```

**Benefits**:
- End-to-end tracing of build operations
- Visualize build flow in Jaeger
- Identify bottlenecks and slow stages
- Debug distributed build issues

---

### Metrics Pattern (Performance Tracking)

**When to use**: For tracking build performance, resource usage, cache efficiency

**Example** (from HotSwap.Distributed):

```csharp
// Record build duration
await _metricsProvider.RecordMetricAsync(
    "build_duration_seconds",
    duration.TotalSeconds,
    new Dictionary<string, string>
    {
        ["project"] = projectName,
        ["strategy"] = strategyName,
        ["success"] = success.ToString()
    });

// Record cache hit rate
await _metricsProvider.RecordMetricAsync(
    "build_cache_hit_rate",
    (double)cacheHits / totalProjects * 100,
    new Dictionary<string, string>
    {
        ["project"] = projectName
    });

// Record agent utilization
await _metricsProvider.RecordMetricAsync(
    "agent_utilization_percent",
    (double)busyAgents / totalAgents * 100,
    new Dictionary<string, string>
    {
        ["pool"] = targetType.ToString()
    });
```

**Benefits**:
- Track build performance trends over time
- Identify performance regressions
- Monitor resource utilization
- Optimize build strategies based on data

---

### Validation Pattern (Input Validation)

**When to use**: Before processing build requests, agent registration

**Example** (from HotSwap.Distributed API):

```csharp
public class BuildRequestValidator
{
    public static void ValidateAndThrow(CreateBuildRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ProjectName))
            errors.Add("ProjectName is required");

        if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
            errors.Add("RepositoryUrl is required");

        if (!Uri.TryCreate(request.RepositoryUrl, UriKind.Absolute, out _))
            errors.Add("RepositoryUrl must be a valid URL");

        if (string.IsNullOrWhiteSpace(request.GitRef))
            errors.Add("GitRef is required");

        if (string.IsNullOrWhiteSpace(request.SolutionPath))
            errors.Add("SolutionPath is required");

        if (errors.Any())
        {
            throw new ValidationException(
                "Build request validation failed: " + string.Join(", ", errors));
        }
    }
}
```

**Benefits**:
- Fail fast with clear error messages
- Prevent invalid builds from consuming resources
- Consistent validation across API and orchestrator

---

### Retry Pattern (Transient Failure Handling)

**When to use**: For operations that might fail transiently (Git clone, NuGet restore, network operations)

**Example** (from HotSwap.Distributed patterns):

```csharp
private async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> operation,
    int maxRetries = 3,
    TimeSpan? initialDelay = null,
    CancellationToken cancellationToken = default)
{
    var delay = initialDelay ?? TimeSpan.FromSeconds(2);

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (attempt < maxRetries && IsTransientError(ex))
        {
            _logger.LogWarning(
                "Operation failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                attempt, maxRetries, delay.TotalSeconds);

            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // Exponential backoff
        }
    }

    throw new InvalidOperationException($"Operation failed after {maxRetries} retries");
}

private bool IsTransientError(Exception ex)
{
    // Network errors, timeout errors, etc.
    return ex is HttpRequestException or TimeoutException;
}

// Usage
var result = await ExecuteWithRetryAsync(async () =>
{
    await CloneRepositoryAsync(repoUrl, gitRef, workspace);
    return true;
}, maxRetries: 3, initialDelay: TimeSpan.FromSeconds(2));
```

**Benefits**:
- Handle transient network failures gracefully
- Avoid build failures due to temporary issues
- Exponential backoff prevents overwhelming failing services

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

**Document Version**: 2.0
**Last Updated**: 2025-11-15
**Author**: Claude (AI Assistant)
**Status**: Enhanced - Ready for Implementation

**Changelog**:
- v2.0 (2025-11-15): Added Framework Reusability Guide, Implementation Quick Start, Extension Patterns, and Common Patterns sections
- v1.0 (2025-11-15): Initial design document
