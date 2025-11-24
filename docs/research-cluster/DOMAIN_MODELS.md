# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.ResearchCluster.Domain.Models`

---

## Table of Contents

1. [ResearchProject](#researchproject)
2. [Workflow](#workflow)
3. [Job](#job)
4. [ResourceAllocation](#resourceallocation)
5. [ClusterEnvironment](#clusterenvironment)
6. [CostRecord](#costrecord)
7. [OptimizationRecommendation](#optimizationrecommendation)
8. [Enumerations](#enumerations)
9. [Value Objects](#value-objects)

---

## ResearchProject

Represents a research project with compute budget.

**File:** `src/HotSwap.ResearchCluster.Domain/Models/ResearchProject.cs`

```csharp
namespace HotSwap.ResearchCluster.Domain.Models;

/// <summary>
/// Represents a research project.
/// </summary>
public class ResearchProject
{
    /// <summary>
    /// Unique project identifier (e.g., "genomics-2025").
    /// </summary>
    public required string ProjectId { get; set; }

    /// <summary>
    /// Project name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Project description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Principal Investigator (PI) email/username.
    /// </summary>
    public required string Owner { get; set; }

    /// <summary>
    /// Team members (researchers, collaborators).
    /// </summary>
    public List<string> TeamMembers { get; set; } = new();

    /// <summary>
    /// Compute budget (total amount allocated).
    /// </summary>
    public decimal ComputeBudget { get; set; }

    /// <summary>
    /// Currency code (USD, EUR, GBP).
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Budget consumed so far.
    /// </summary>
    public decimal BudgetConsumed { get; set; } = 0;

    /// <summary>
    /// Budget remaining.
    /// </summary>
    public decimal BudgetRemaining => ComputeBudget - BudgetConsumed;

    /// <summary>
    /// Budget utilization percentage (0-100).
    /// </summary>
    public double BudgetUtilization => ComputeBudget > 0 ? (double)(BudgetConsumed / ComputeBudget) * 100 : 0;

    /// <summary>
    /// Project status.
    /// </summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    /// <summary>
    /// Project creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Project archival timestamp (UTC).
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// Validates the project configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ProjectId))
            errors.Add("ProjectId is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(ProjectId, @"^[a-zA-Z0-9_-]+$"))
            errors.Add("ProjectId must contain only alphanumeric characters, underscores, and dashes");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Owner))
            errors.Add("Owner is required");

        if (ComputeBudget < 0)
            errors.Add("ComputeBudget must be non-negative");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks if budget can accommodate a job cost.
    /// </summary>
    public bool CanAfford(decimal jobCost)
    {
        return BudgetRemaining >= jobCost;
    }

    /// <summary>
    /// Consumes budget for a job.
    /// </summary>
    public void ConsumeBudget(decimal amount)
    {
        BudgetConsumed += amount;
    }

    /// <summary>
    /// Archives the project.
    /// </summary>
    public void Archive()
    {
        Status = ProjectStatus.Archived;
        ArchivedAt = DateTime.UtcNow;
    }
}
```

---

## Workflow

Represents a computational workflow.

**File:** `src/HotSwap.ResearchCluster.Domain/Models/Workflow.cs`

```csharp
namespace HotSwap.ResearchCluster.Domain.Models;

/// <summary>
/// Represents a computational workflow.
/// </summary>
public class Workflow
{
    /// <summary>
    /// Unique workflow identifier.
    /// </summary>
    public required string WorkflowId { get; set; }

    /// <summary>
    /// Project this workflow belongs to.
    /// </summary>
    public required string ProjectId { get; set; }

    /// <summary>
    /// Workflow name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Workflow description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Workflow type (Batch, Array, Pipeline, Interactive).
    /// </summary>
    public WorkflowType Type { get; set; } = WorkflowType.Batch;

    /// <summary>
    /// Workflow version (semantic versioning).
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// HPC scheduler runtime (Slurm, PBS, SGE).
    /// </summary>
    public string Runtime { get; set; } = "slurm";

    /// <summary>
    /// Resource requirements.
    /// </summary>
    public ResourceRequirements Requirements { get; set; } = new();

    /// <summary>
    /// Workflow definition (YAML or JSON).
    /// </summary>
    public required string Definition { get; set; }

    /// <summary>
    /// Container image (if containerized).
    /// </summary>
    public string? ContainerImage { get; set; }

    /// <summary>
    /// Environment modules to load (e.g., gcc/11.2, openmpi/4.1).
    /// </summary>
    public List<string> Modules { get; set; } = new();

    /// <summary>
    /// Job dependencies (for pipeline workflows).
    /// </summary>
    public List<JobDependency> Dependencies { get; set; } = new();

    /// <summary>
    /// Workflow status.
    /// </summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;

    /// <summary>
    /// Validation errors (if validation failed).
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Workflow creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the workflow configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(WorkflowId))
            errors.Add("WorkflowId is required");

        if (string.IsNullOrWhiteSpace(ProjectId))
            errors.Add("ProjectId is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(Definition))
            errors.Add("Definition is required");

        // Validate resource requirements
        if (!Requirements.IsValid(out var reqErrors))
            errors.AddRange(reqErrors);

        // Validate dependencies (no circular dependencies)
        if (HasCircularDependencies())
            errors.Add("Workflow has circular dependencies");

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks for circular dependencies in the workflow.
    /// </summary>
    private bool HasCircularDependencies()
    {
        // Simplified check (full implementation would use topological sort)
        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        foreach (var dep in Dependencies)
        {
            if (HasCycleDFS(dep.JobId, visited, stack))
                return true;
        }

        return false;
    }

    private bool HasCycleDFS(string jobId, HashSet<string> visited, HashSet<string> stack)
    {
        if (stack.Contains(jobId))
            return true;

        if (visited.Contains(jobId))
            return false;

        visited.Add(jobId);
        stack.Add(jobId);

        var jobDeps = Dependencies.Where(d => d.JobId == jobId);
        foreach (var dep in jobDeps)
        {
            if (HasCycleDFS(dep.DependsOn, visited, stack))
                return true;
        }

        stack.Remove(jobId);
        return false;
    }
}

/// <summary>
/// Resource requirements for a workflow.
/// </summary>
public class ResourceRequirements
{
    /// <summary>
    /// Number of compute nodes.
    /// </summary>
    public int Nodes { get; set; } = 1;

    /// <summary>
    /// CPU cores per node.
    /// </summary>
    public int CpuPerNode { get; set; } = 1;

    /// <summary>
    /// Memory (GB) per node.
    /// </summary>
    public int MemoryGbPerNode { get; set; } = 4;

    /// <summary>
    /// GPUs per node (0 if no GPU required).
    /// </summary>
    public int GpuPerNode { get; set; } = 0;

    /// <summary>
    /// Wall time limit (format: HH:MM:SS).
    /// </summary>
    public string Walltime { get; set; } = "01:00:00";

    /// <summary>
    /// Validates resource requirements.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (Nodes < 1)
            errors.Add("Nodes must be at least 1");

        if (CpuPerNode < 1)
            errors.Add("CpuPerNode must be at least 1");

        if (MemoryGbPerNode < 1)
            errors.Add("MemoryGbPerNode must be at least 1");

        if (GpuPerNode < 0)
            errors.Add("GpuPerNode must be non-negative");

        // Validate walltime format (HH:MM:SS)
        if (!System.Text.RegularExpressions.Regex.IsMatch(Walltime, @"^\d{2}:\d{2}:\d{2}$"))
            errors.Add("Walltime must be in HH:MM:SS format");

        return errors.Count == 0;
    }
}

/// <summary>
/// Job dependency (for pipeline workflows).
/// </summary>
public class JobDependency
{
    /// <summary>
    /// Job ID.
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// Dependency type (AfterOK, AfterAny, AfterNotOK).
    /// </summary>
    public string DependencyType { get; set; } = "AfterOK";

    /// <summary>
    /// Job this job depends on.
    /// </summary>
    public required string DependsOn { get; set; }
}
```

---

## Job

Represents a job submitted to the HPC scheduler.

**File:** `src/HotSwap.ResearchCluster.Domain/Models/Job.cs`

```csharp
namespace HotSwap.ResearchCluster.Domain.Models;

/// <summary>
/// Represents a job submitted to the HPC scheduler.
/// </summary>
public class Job
{
    /// <summary>
    /// Unique job identifier (generated by system).
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// Scheduler job ID (e.g., Slurm job ID).
    /// </summary>
    public string? SchedulerJobId { get; set; }

    /// <summary>
    /// Workflow this job belongs to.
    /// </summary>
    public required string WorkflowId { get; set; }

    /// <summary>
    /// Project this job belongs to.
    /// </summary>
    public required string ProjectId { get; set; }

    /// <summary>
    /// Cluster environment (dev, qa, production).
    /// </summary>
    public required string EnvironmentId { get; set; }

    /// <summary>
    /// Job status.
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Queued;

    /// <summary>
    /// Job submission timestamp (UTC).
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Job start timestamp (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Job completion timestamp (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Job runtime (if completed).
    /// </summary>
    public TimeSpan? Runtime => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    /// <summary>
    /// Queue wait time.
    /// </summary>
    public TimeSpan? QueueWaitTime => StartedAt.HasValue
        ? StartedAt.Value - SubmittedAt
        : null;

    /// <summary>
    /// Exit code (0 = success, non-zero = failure).
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Error message (if job failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Allocated resources.
    /// </summary>
    public ResourceAllocation? Allocation { get; set; }

    /// <summary>
    /// Resource usage metrics.
    /// </summary>
    public ResourceUsageMetrics? UsageMetrics { get; set; }

    /// <summary>
    /// Job cost (calculated after completion).
    /// </summary>
    public decimal Cost { get; set; } = 0;

    /// <summary>
    /// Stdout log file path.
    /// </summary>
    public string? StdoutPath { get; set; }

    /// <summary>
    /// Stderr log file path.
    /// </summary>
    public string? StderrPath { get; set; }

    /// <summary>
    /// Marks job as started.
    /// </summary>
    public void MarkStarted()
    {
        Status = JobStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks job as completed.
    /// </summary>
    public void MarkCompleted(int exitCode)
    {
        Status = exitCode == 0 ? JobStatus.Completed : JobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ExitCode = exitCode;
    }

    /// <summary>
    /// Marks job as cancelled.
    /// </summary>
    public void MarkCancelled()
    {
        Status = JobStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Resource usage metrics for a job.
/// </summary>
public class ResourceUsageMetrics
{
    /// <summary>
    /// Average CPU utilization (0-100%).
    /// </summary>
    public double AvgCpuUtilization { get; set; }

    /// <summary>
    /// Peak memory usage (GB).
    /// </summary>
    public double PeakMemoryGb { get; set; }

    /// <summary>
    /// Average memory usage (GB).
    /// </summary>
    public double AvgMemoryGb { get; set; }

    /// <summary>
    /// Average GPU utilization (0-100%, null if no GPU).
    /// </summary>
    public double? AvgGpuUtilization { get; set; }

    /// <summary>
    /// Disk I/O read (GB).
    /// </summary>
    public double DiskReadGb { get; set; }

    /// <summary>
    /// Disk I/O write (GB).
    /// </summary>
    public double DiskWriteGb { get; set; }

    /// <summary>
    /// Network I/O sent (GB).
    /// </summary>
    public double NetworkSentGb { get; set; }

    /// <summary>
    /// Network I/O received (GB).
    /// </summary>
    public double NetworkReceivedGb { get; set; }

    /// <summary>
    /// Metrics collection timestamp (UTC).
    /// </summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}
```

---

## ResourceAllocation

Represents resource allocation for a job.

**File:** `src/HotSwap.ResearchCluster.Domain/Models/ResourceAllocation.cs`

```csharp
namespace HotSwap.ResearchCluster.Domain.Models;

/// <summary>
/// Represents resource allocation for a job.
/// </summary>
public class ResourceAllocation
{
    /// <summary>
    /// Allocation ID.
    /// </summary>
    public required string AllocationId { get; set; }

    /// <summary>
    /// Job ID.
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// Cluster ID.
    /// </summary>
    public required string ClusterId { get; set; }

    /// <summary>
    /// Allocated nodes (node IDs).
    /// </summary>
    public List<string> AllocatedNodes { get; set; } = new();

    /// <summary>
    /// Total CPU cores allocated.
    /// </summary>
    public int TotalCpuCores { get; set; }

    /// <summary>
    /// Total memory (GB) allocated.
    /// </summary>
    public int TotalMemoryGb { get; set; }

    /// <summary>
    /// Total GPUs allocated.
    /// </summary>
    public int TotalGpus { get; set; }

    /// <summary>
    /// Allocation timestamp (UTC).
    /// </summary>
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deallocation timestamp (UTC).
    /// </summary>
    public DateTime? DeallocatedAt { get; set; }

    /// <summary>
    /// Allocation duration.
    /// </summary>
    public TimeSpan? Duration => DeallocatedAt.HasValue
        ? DeallocatedAt.Value - AllocatedAt
        : null;

    /// <summary>
    /// Deallocates resources.
    /// </summary>
    public void Deallocate()
    {
        DeallocatedAt = DateTime.UtcNow;
    }
}
```

---

## ClusterEnvironment

Represents a cluster environment (dev, qa, prod).

**File:** `src/HotSwap.ResearchCluster.Domain/Models/ClusterEnvironment.cs`

```csharp
namespace HotSwap.ResearchCluster.Domain.Models;

/// <summary>
/// Represents a cluster environment.
/// </summary>
public class ClusterEnvironment
{
    /// <summary>
    /// Environment ID (e.g., "dev", "qa", "prod").
    /// </summary>
    public required string EnvironmentId { get; set; }

    /// <summary>
    /// Environment name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Cluster ID this environment belongs to.
    /// </summary>
    public required string ClusterId { get; set; }

    /// <summary>
    /// Environment type (Dev, QA, Production).
    /// </summary>
    public EnvironmentType Type { get; set; }

    /// <summary>
    /// Node pool assigned to this environment.
    /// </summary>
    public List<string> NodePool { get; set; } = new();

    /// <summary>
    /// Resource quotas for this environment.
    /// </summary>
    public EnvironmentQuota Quota { get; set; } = new();

    /// <summary>
    /// Requires approval for deployments.
    /// </summary>
    public bool RequiresApproval { get; set; } = false;

    /// <summary>
    /// Auto-cleanup enabled (for dev environments).
    /// </summary>
    public bool AutoCleanup { get; set; } = false;

    /// <summary>
    /// Auto-cleanup after hours (for dev environments).
    /// </summary>
    public int AutoCleanupHours { get; set; } = 24;

    /// <summary>
    /// Environment creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Environment resource quotas.
/// </summary>
public class EnvironmentQuota
{
    /// <summary>
    /// Maximum nodes per job.
    /// </summary>
    public int MaxNodesPerJob { get; set; } = 10;

    /// <summary>
    /// Maximum concurrent jobs.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 100;

    /// <summary>
    /// Maximum walltime (hours).
    /// </summary>
    public int MaxWalltimeHours { get; set; } = 48;

    /// <summary>
    /// Maximum total nodes allocated.
    /// </summary>
    public int MaxTotalNodes { get; set; } = 100;
}
```

---

## CostRecord

Represents a cost record for a job.

**File:** `src/HotSwap.ResearchCluster.Domain/Models/CostRecord.cs`

```csharp
namespace HotSwap.ResearchCluster.Domain.Models;

/// <summary>
/// Represents a cost record for a job.
/// </summary>
public class CostRecord
{
    /// <summary>
    /// Record ID.
    /// </summary>
    public required string RecordId { get; set; }

    /// <summary>
    /// Job ID.
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// Project ID.
    /// </summary>
    public required string ProjectId { get; set; }

    /// <summary>
    /// Cluster ID.
    /// </summary>
    public required string ClusterId { get; set; }

    /// <summary>
    /// CPU node-hours.
    /// </summary>
    public double CpuNodeHours { get; set; }

    /// <summary>
    /// CPU cost per node-hour.
    /// </summary>
    public decimal CpuRatePerNodeHour { get; set; }

    /// <summary>
    /// GPU hours.
    /// </summary>
    public double GpuHours { get; set; }

    /// <summary>
    /// GPU cost per hour.
    /// </summary>
    public decimal GpuRatePerHour { get; set; }

    /// <summary>
    /// Storage GB-hours.
    /// </summary>
    public double StorageGbHours { get; set; }

    /// <summary>
    /// Storage cost per GB-hour.
    /// </summary>
    public decimal StorageRatePerGbHour { get; set; }

    /// <summary>
    /// Total cost.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Currency code.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Cost calculation timestamp (UTC).
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Calculates total cost based on usage.
    /// </summary>
    public void CalculateCost()
    {
        var cpuCost = (decimal)CpuNodeHours * CpuRatePerNodeHour;
        var gpuCost = (decimal)GpuHours * GpuRatePerHour;
        var storageCost = (decimal)StorageGbHours * StorageRatePerGbHour;

        TotalCost = cpuCost + gpuCost + storageCost;
    }
}
```

---

## OptimizationRecommendation

Represents a workflow optimization recommendation.

**File:** `src/HotSwap.ResearchCluster.Domain/Models/OptimizationRecommendation.cs`

```csharp
namespace HotSwap.ResearchCluster.Domain.Models;

/// <summary>
/// Represents a workflow optimization recommendation.
/// </summary>
public class OptimizationRecommendation
{
    /// <summary>
    /// Recommendation ID.
    /// </summary>
    public required string RecommendationId { get; set; }

    /// <summary>
    /// Workflow ID.
    /// </summary>
    public required string WorkflowId { get; set; }

    /// <summary>
    /// Recommendation type.
    /// </summary>
    public RecommendationType Type { get; set; }

    /// <summary>
    /// Recommendation title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Recommendation description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Current configuration.
    /// </summary>
    public string? CurrentConfig { get; set; }

    /// <summary>
    /// Recommended configuration.
    /// </summary>
    public string? RecommendedConfig { get; set; }

    /// <summary>
    /// Estimated cost savings (per job).
    /// </summary>
    public decimal EstimatedSavings { get; set; }

    /// <summary>
    /// Estimated runtime change (positive = slower, negative = faster).
    /// </summary>
    public TimeSpan EstimatedRuntimeChange { get; set; }

    /// <summary>
    /// Confidence score (0-100).
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Number of jobs analyzed.
    /// </summary>
    public int JobsAnalyzed { get; set; }

    /// <summary>
    /// Recommendation status.
    /// </summary>
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Pending;

    /// <summary>
    /// Recommendation creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Applied timestamp (UTC).
    /// </summary>
    public DateTime? AppliedAt { get; set; }

    /// <summary>
    /// Marks recommendation as applied.
    /// </summary>
    public void MarkApplied()
    {
        Status = RecommendationStatus.Applied;
        AppliedAt = DateTime.UtcNow;
    }
}
```

---

## Enumerations

### ProjectStatus

```csharp
namespace HotSwap.ResearchCluster.Domain.Enums;

public enum ProjectStatus
{
    Active,
    Archived
}
```

### WorkflowType

```csharp
public enum WorkflowType
{
    Batch,
    Array,
    Pipeline,
    Interactive
}
```

### WorkflowStatus

```csharp
public enum WorkflowStatus
{
    Draft,
    Validated,
    Deployed,
    Archived
}
```

### JobStatus

```csharp
public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    Timeout
}
```

### EnvironmentType

```csharp
public enum EnvironmentType
{
    Dev,
    QA,
    Staging,
    Production
}
```

### RecommendationType

```csharp
public enum RecommendationType
{
    ResourceRightSizing,
    Parallelization,
    DataLocality,
    Scheduling,
    CostOptimization
}
```

### RecommendationStatus

```csharp
public enum RecommendationStatus
{
    Pending,
    Applied,
    Dismissed
}
```

---

## Value Objects

### DeploymentResult

```csharp
namespace HotSwap.ResearchCluster.Domain.ValueObjects;

public class DeploymentResult
{
    public bool Success { get; private set; }
    public int JobsSubmitted { get; private set; }
    public int Failures { get; private set; }
    public List<string> Errors { get; private set; } = new();
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; private set; }

    public static DeploymentResult SuccessResult(int submitted, TimeSpan duration)
    {
        return new DeploymentResult
        {
            Success = true,
            JobsSubmitted = submitted,
            Duration = duration
        };
    }

    public static DeploymentResult Failure(int submitted, int failures, List<string> errors, TimeSpan duration)
    {
        return new DeploymentResult
        {
            Success = false,
            JobsSubmitted = submitted,
            Failures = failures,
            Errors = errors,
            Duration = duration
        };
    }
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.ResearchCluster.Domain`
