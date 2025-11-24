# Domain Models Reference

**Version:** 1.0.0
**Namespace:** `HotSwap.LabManager.Domain.Models`

---

## Table of Contents

1. [Course](#course)
2. [Lab](#lab)
3. [StudentEnvironment](#studentenvironment)
4. [Submission](#submission)
5. [GradingResult](#gradingresult)
6. [ResourceTemplate](#resourcetemplate)
7. [Cohort](#cohort)
8. [ProgressMetrics](#progressmetrics)
9. [Enumerations](#enumerations)
10. [Value Objects](#value-objects)

---

## Course

Represents an academic course.

**File:** `src/HotSwap.LabManager.Domain/Models/Course.cs`

```csharp
namespace HotSwap.LabManager.Domain.Models;

/// <summary>
/// Represents an academic course.
/// </summary>
public class Course
{
    /// <summary>
    /// Unique course identifier (e.g., "CS101", "MATH200").
    /// </summary>
    public required string CourseName { get; set; }

    /// <summary>
    /// Course title (e.g., "Introduction to Programming").
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Academic term (e.g., "Fall 2025", "Spring 2026").
    /// </summary>
    public required string Term { get; set; }

    /// <summary>
    /// Instructor username or email.
    /// </summary>
    public required string Instructor { get; set; }

    /// <summary>
    /// Course description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Course status (Active, Archived).
    /// </summary>
    public CourseStatus Status { get; set; } = CourseStatus.Active;

    /// <summary>
    /// Total enrolled students.
    /// </summary>
    public int EnrollmentCount { get; set; } = 0;

    /// <summary>
    /// Course creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Course archival timestamp (UTC).
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// LMS course ID (for integration with Canvas, Moodle, etc.).
    /// </summary>
    public string? LmsCourseId { get; set; }

    /// <summary>
    /// Course configuration settings.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>
    /// Validates the course configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CourseName))
            errors.Add("CourseName is required");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(CourseName, @"^[a-zA-Z0-9_-]+$"))
            errors.Add("CourseName must contain only alphanumeric characters, underscores, and dashes");

        if (string.IsNullOrWhiteSpace(Title))
            errors.Add("Title is required");

        if (string.IsNullOrWhiteSpace(Term))
            errors.Add("Term is required");

        if (string.IsNullOrWhiteSpace(Instructor))
            errors.Add("Instructor is required");

        return errors.Count == 0;
    }

    /// <summary>
    /// Archives the course (makes it read-only).
    /// </summary>
    public void Archive()
    {
        Status = CourseStatus.Archived;
        ArchivedAt = DateTime.UtcNow;
    }
}
```

---

## Lab

Represents a lab exercise.

**File:** `src/HotSwap.LabManager.Domain/Models/Lab.cs`

```csharp
namespace HotSwap.LabManager.Domain.Models;

/// <summary>
/// Represents a lab exercise.
/// </summary>
public class Lab
{
    /// <summary>
    /// Unique lab identifier (e.g., "lab-cs101-1").
    /// </summary>
    public required string LabId { get; set; }

    /// <summary>
    /// Course this lab belongs to.
    /// </summary>
    public required string CourseName { get; set; }

    /// <summary>
    /// Lab number (1, 2, 3, ...).
    /// </summary>
    public int LabNumber { get; set; }

    /// <summary>
    /// Lab title (e.g., "Hello World").
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Lab description (Markdown format).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Lab instructions (Markdown format, can include images, code snippets).
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Lab type (Coding, Notebook, Infrastructure, Research).
    /// </summary>
    public LabType Type { get; set; } = LabType.Coding;

    /// <summary>
    /// Resource template identifier (e.g., "dotnet-basic", "jupyter-datascience").
    /// </summary>
    public required string ResourceTemplate { get; set; }

    /// <summary>
    /// Starter code repository URL (Git) or archive file path.
    /// </summary>
    public string? StarterCodeUrl { get; set; }

    /// <summary>
    /// Due date (UTC). Null means no deadline.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Late submission policy (Allow, Penalty, Deny).
    /// </summary>
    public LatePolicy LatePolicy { get; set; } = LatePolicy.Penalty;

    /// <summary>
    /// Late penalty percentage per day (0-100).
    /// </summary>
    public int LatePenaltyPercent { get; set; } = 10;

    /// <summary>
    /// Maximum submission attempts (0 = unlimited).
    /// </summary>
    public int MaxSubmissionAttempts { get; set; } = 3;

    /// <summary>
    /// Autograding enabled.
    /// </summary>
    public bool AutogradingEnabled { get; set; } = true;

    /// <summary>
    /// Autograder configuration (Docker image, test script, timeout).
    /// </summary>
    public AutograderConfig? AutograderConfig { get; set; }

    /// <summary>
    /// Lab status (Draft, Published, Archived).
    /// </summary>
    public LabStatus Status { get; set; } = LabStatus.Draft;

    /// <summary>
    /// Lab version (e.g., "1.0", "1.1").
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Lab creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Published timestamp (UTC).
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Total points for this lab (for grading).
    /// </summary>
    public int TotalPoints { get; set; } = 100;

    /// <summary>
    /// Validates the lab configuration.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(LabId))
            errors.Add("LabId is required");

        if (string.IsNullOrWhiteSpace(CourseName))
            errors.Add("CourseName is required");

        if (LabNumber < 1)
            errors.Add("LabNumber must be at least 1");

        if (string.IsNullOrWhiteSpace(Title))
            errors.Add("Title is required");

        if (string.IsNullOrWhiteSpace(ResourceTemplate))
            errors.Add("ResourceTemplate is required");

        if (DueDate.HasValue && DueDate.Value < DateTime.UtcNow)
            errors.Add("DueDate must be in the future");

        if (LatePenaltyPercent < 0 || LatePenaltyPercent > 100)
            errors.Add("LatePenaltyPercent must be between 0 and 100");

        if (MaxSubmissionAttempts < 0)
            errors.Add("MaxSubmissionAttempts must be non-negative");

        if (TotalPoints <= 0)
            errors.Add("TotalPoints must be positive");

        return errors.Count == 0;
    }

    /// <summary>
    /// Publishes the lab (makes it available to students).
    /// </summary>
    public void Publish()
    {
        Status = LabStatus.Published;
        PublishedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the lab is past due.
    /// </summary>
    public bool IsPastDue() => DueDate.HasValue && DateTime.UtcNow > DueDate.Value;
}

/// <summary>
/// Autograder configuration.
/// </summary>
public class AutograderConfig
{
    /// <summary>
    /// Autograder type (Docker, Gradescope, Custom).
    /// </summary>
    public string Type { get; set; } = "Docker";

    /// <summary>
    /// Docker image for grading (e.g., "gradescope/autograder-python").
    /// </summary>
    public string? DockerImage { get; set; }

    /// <summary>
    /// Test script path (inside Docker container).
    /// </summary>
    public string? TestScript { get; set; }

    /// <summary>
    /// Grading timeout (seconds).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gradescope assignment ID (if using Gradescope).
    /// </summary>
    public string? GradescopeAssignmentId { get; set; }

    /// <summary>
    /// Custom grader configuration (JSON).
    /// </summary>
    public string? CustomConfig { get; set; }
}
```

---

## StudentEnvironment

Represents a student's lab environment.

**File:** `src/HotSwap.LabManager.Domain/Models/StudentEnvironment.cs`

```csharp
namespace HotSwap.LabManager.Domain.Models;

/// <summary>
/// Represents a student's lab environment.
/// </summary>
public class StudentEnvironment
{
    /// <summary>
    /// Unique environment identifier (GUID format).
    /// </summary>
    public required string EnvironmentId { get; set; }

    /// <summary>
    /// Student identifier (username or email).
    /// </summary>
    public required string StudentId { get; set; }

    /// <summary>
    /// Lab identifier.
    /// </summary>
    public required string LabId { get; set; }

    /// <summary>
    /// Course name.
    /// </summary>
    public required string CourseName { get; set; }

    /// <summary>
    /// Container/VM identifier (Docker container ID, K8s pod name).
    /// </summary>
    public string? ContainerId { get; set; }

    /// <summary>
    /// Environment status (Provisioning, Active, Suspended, Submitted, Graded, Deleted).
    /// </summary>
    public EnvironmentStatus Status { get; set; } = EnvironmentStatus.Provisioning;

    /// <summary>
    /// Web access URL (HTTPS with auth token).
    /// </summary>
    public string? AccessUrl { get; set; }

    /// <summary>
    /// SSH access enabled.
    /// </summary>
    public bool SshEnabled { get; set; } = false;

    /// <summary>
    /// SSH connection string (if enabled).
    /// </summary>
    public string? SshConnection { get; set; }

    /// <summary>
    /// Resource quotas applied to this environment.
    /// </summary>
    public ResourceQuota Quota { get; set; } = new();

    /// <summary>
    /// Current resource usage.
    /// </summary>
    public ResourceUsage Usage { get; set; } = new();

    /// <summary>
    /// Environment provisioning timestamp (UTC).
    /// </summary>
    public DateTime ProvisionedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last access timestamp (UTC).
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Suspension timestamp (UTC).
    /// </summary>
    public DateTime? SuspendedAt { get; set; }

    /// <summary>
    /// Deletion timestamp (UTC).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Auto-suspend timeout (default: 30 minutes).
    /// </summary>
    public TimeSpan AutoSuspendTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Total active time (time spent working, not idle).
    /// </summary>
    public TimeSpan ActiveTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Access count (how many times student opened environment).
    /// </summary>
    public int AccessCount { get; set; } = 0;

    /// <summary>
    /// Checks if the environment should be auto-suspended.
    /// </summary>
    public bool ShouldAutoSuspend()
    {
        if (!LastAccessedAt.HasValue)
            return false;

        return DateTime.UtcNow - LastAccessedAt.Value > AutoSuspendTimeout;
    }

    /// <summary>
    /// Records an access event.
    /// </summary>
    public void RecordAccess()
    {
        LastAccessedAt = DateTime.UtcNow;
        AccessCount++;
    }

    /// <summary>
    /// Suspends the environment.
    /// </summary>
    public void Suspend()
    {
        Status = EnvironmentStatus.Suspended;
        SuspendedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resumes the environment.
    /// </summary>
    public void Resume()
    {
        Status = EnvironmentStatus.Active;
        SuspendedAt = null;
    }
}

/// <summary>
/// Resource quota for an environment.
/// </summary>
public class ResourceQuota
{
    /// <summary>
    /// CPU quota (cores).
    /// </summary>
    public double CpuCores { get; set; } = 2.0;

    /// <summary>
    /// Memory quota (GB).
    /// </summary>
    public double MemoryGb { get; set; } = 4.0;

    /// <summary>
    /// Storage quota (GB).
    /// </summary>
    public double StorageGb { get; set; } = 10.0;

    /// <summary>
    /// Network bandwidth quota (Mbps). Null = unlimited.
    /// </summary>
    public double? NetworkMbps { get; set; }
}

/// <summary>
/// Current resource usage.
/// </summary>
public class ResourceUsage
{
    /// <summary>
    /// CPU usage (cores).
    /// </summary>
    public double CpuCores { get; set; } = 0;

    /// <summary>
    /// Memory usage (GB).
    /// </summary>
    public double MemoryGb { get; set; } = 0;

    /// <summary>
    /// Storage usage (GB).
    /// </summary>
    public double StorageGb { get; set; } = 0;

    /// <summary>
    /// Network bandwidth usage (Mbps).
    /// </summary>
    public double NetworkMbps { get; set; } = 0;

    /// <summary>
    /// Last updated timestamp (UTC).
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
```

---

## Submission

Represents a student lab submission.

**File:** `src/HotSwap.LabManager.Domain/Models/Submission.cs`

```csharp
namespace HotSwap.LabManager.Domain.Models;

/// <summary>
/// Represents a student lab submission.
/// </summary>
public class Submission
{
    /// <summary>
    /// Unique submission identifier (GUID format).
    /// </summary>
    public required string SubmissionId { get; set; }

    /// <summary>
    /// Student identifier.
    /// </summary>
    public required string StudentId { get; set; }

    /// <summary>
    /// Lab identifier.
    /// </summary>
    public required string LabId { get; set; }

    /// <summary>
    /// Course name.
    /// </summary>
    public required string CourseName { get; set; }

    /// <summary>
    /// Submission attempt number (1, 2, 3, ...).
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// Submission timestamp (UTC).
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Submission files (stored in object storage like MinIO/S3).
    /// </summary>
    public List<SubmissionFile> Files { get; set; } = new();

    /// <summary>
    /// Submission status (Pending, Grading, Graded, Failed).
    /// </summary>
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

    /// <summary>
    /// Whether this submission was late.
    /// </summary>
    public bool IsLate { get; set; } = false;

    /// <summary>
    /// Days late (0 if not late).
    /// </summary>
    public int DaysLate { get; set; } = 0;

    /// <summary>
    /// Late penalty applied (percentage, 0-100).
    /// </summary>
    public int LatePenaltyPercent { get; set; } = 0;

    /// <summary>
    /// Grading result (null if not graded yet).
    /// </summary>
    public GradingResult? GradingResult { get; set; }

    /// <summary>
    /// Submission notes from student (optional).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Submission receipt ID (for student reference).
    /// </summary>
    public string? ReceiptId { get; set; }

    /// <summary>
    /// Calculates if submission is late based on lab due date.
    /// </summary>
    public void CalculateLateness(Lab lab)
    {
        if (!lab.DueDate.HasValue)
        {
            IsLate = false;
            DaysLate = 0;
            return;
        }

        if (SubmittedAt > lab.DueDate.Value)
        {
            IsLate = true;
            DaysLate = (int)(SubmittedAt - lab.DueDate.Value).TotalDays + 1;
            LatePenaltyPercent = Math.Min(DaysLate * lab.LatePenaltyPercent, 100);
        }
    }

    /// <summary>
    /// Generates a submission receipt ID.
    /// </summary>
    public string GenerateReceipt()
    {
        ReceiptId = $"RECEIPT-{SubmissionId}-{SubmittedAt:yyyyMMddHHmmss}";
        return ReceiptId;
    }
}

/// <summary>
/// Submission file metadata.
/// </summary>
public class SubmissionFile
{
    /// <summary>
    /// File path (relative to submission root).
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// File size (bytes).
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// File checksum (SHA256).
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Storage URL (MinIO/S3 object URL).
    /// </summary>
    public required string StorageUrl { get; set; }

    /// <summary>
    /// File upload timestamp (UTC).
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
```

---

## GradingResult

Represents grading results for a submission.

**File:** `src/HotSwap.LabManager.Domain/Models/GradingResult.cs`

```csharp
namespace HotSwap.LabManager.Domain.Models;

/// <summary>
/// Represents grading results for a submission.
/// </summary>
public class GradingResult
{
    /// <summary>
    /// Submission identifier.
    /// </summary>
    public required string SubmissionId { get; set; }

    /// <summary>
    /// Score earned (0 to TotalPoints).
    /// </summary>
    public double Score { get; set; } = 0;

    /// <summary>
    /// Total possible points.
    /// </summary>
    public int TotalPoints { get; set; } = 100;

    /// <summary>
    /// Percentage score (0-100).
    /// </summary>
    public double Percentage => TotalPoints > 0 ? (Score / TotalPoints) * 100 : 0;

    /// <summary>
    /// Grading status (Pending, InProgress, Completed, Failed).
    /// </summary>
    public GradingStatus Status { get; set; } = GradingStatus.Pending;

    /// <summary>
    /// Grading feedback (Markdown format).
    /// </summary>
    public string? Feedback { get; set; }

    /// <summary>
    /// Test results (individual test pass/fail).
    /// </summary>
    public List<TestResult> TestResults { get; set; } = new();

    /// <summary>
    /// Grading started timestamp (UTC).
    /// </summary>
    public DateTime? GradingStartedAt { get; set; }

    /// <summary>
    /// Grading completed timestamp (UTC).
    /// </summary>
    public DateTime? GradingCompletedAt { get; set; }

    /// <summary>
    /// Grading duration (time taken to grade).
    /// </summary>
    public TimeSpan? GradingDuration => GradingCompletedAt.HasValue && GradingStartedAt.HasValue
        ? GradingCompletedAt.Value - GradingStartedAt.Value
        : null;

    /// <summary>
    /// Grader type (Autograder, Manual).
    /// </summary>
    public string GraderType { get; set; } = "Autograder";

    /// <summary>
    /// Grader identifier (autograder job ID or instructor username).
    /// </summary>
    public string? GraderId { get; set; }

    /// <summary>
    /// Manual override applied by instructor.
    /// </summary>
    public bool ManualOverride { get; set; } = false;

    /// <summary>
    /// Override reason (required if ManualOverride is true).
    /// </summary>
    public string? OverrideReason { get; set; }

    /// <summary>
    /// Override by (instructor username).
    /// </summary>
    public string? OverrideBy { get; set; }

    /// <summary>
    /// Plagiarism detection result (if enabled).
    /// </summary>
    public PlagiarismResult? PlagiarismResult { get; set; }

    /// <summary>
    /// Calculates final score with late penalty applied.
    /// </summary>
    public double GetFinalScore(Submission submission)
    {
        if (!submission.IsLate)
            return Score;

        double penalty = Score * (submission.LatePenaltyPercent / 100.0);
        return Math.Max(0, Score - penalty);
    }
}

/// <summary>
/// Individual test result.
/// </summary>
public class TestResult
{
    /// <summary>
    /// Test name.
    /// </summary>
    public required string TestName { get; set; }

    /// <summary>
    /// Test passed.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Points earned for this test.
    /// </summary>
    public double Points { get; set; }

    /// <summary>
    /// Total points for this test.
    /// </summary>
    public double TotalPoints { get; set; }

    /// <summary>
    /// Error message (if test failed).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Test output (stdout/stderr).
    /// </summary>
    public string? Output { get; set; }
}

/// <summary>
/// Plagiarism detection result.
/// </summary>
public class PlagiarismResult
{
    /// <summary>
    /// Plagiarism detected.
    /// </summary>
    public bool Detected { get; set; } = false;

    /// <summary>
    /// Similarity percentage (0-100).
    /// </summary>
    public double SimilarityPercent { get; set; } = 0;

    /// <summary>
    /// Suspected source(s).
    /// </summary>
    public List<string> Sources { get; set; } = new();

    /// <summary>
    /// Plagiarism check timestamp (UTC).
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
```

---

## ResourceTemplate

Represents a resource template for lab environments.

**File:** `src/HotSwap.LabManager.Domain/Models/ResourceTemplate.cs`

```csharp
namespace HotSwap.LabManager.Domain.Models;

/// <summary>
/// Represents a resource template for lab environments.
/// </summary>
public class ResourceTemplate
{
    /// <summary>
    /// Unique template identifier (e.g., "dotnet-basic", "jupyter-datascience").
    /// </summary>
    public required string TemplateId { get; set; }

    /// <summary>
    /// Template name (human-readable).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Template description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Lab type this template supports.
    /// </summary>
    public LabType LabType { get; set; }

    /// <summary>
    /// Docker base image.
    /// </summary>
    public required string DockerImage { get; set; }

    /// <summary>
    /// Default resource quotas.
    /// </summary>
    public ResourceQuota DefaultQuota { get; set; } = new();

    /// <summary>
    /// Pre-installed packages/tools.
    /// </summary>
    public List<string> PreInstalledPackages { get; set; } = new();

    /// <summary>
    /// Exposed ports.
    /// </summary>
    public List<int> ExposedPorts { get; set; } = new();

    /// <summary>
    /// Environment variables.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Web IDE type (VSCode, JupyterLab, RStudio, None).
    /// </summary>
    public string WebIde { get; set; } = "VSCode";

    /// <summary>
    /// Template creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Template version.
    /// </summary>
    public string Version { get; set; } = "1.0";
}
```

---

## Cohort

Represents a student cohort (section, group).

**File:** `src/HotSwap.LabManager.Domain/Models/Cohort.cs`

```csharp
namespace HotSwap.LabManager.Domain.Models;

/// <summary>
/// Represents a student cohort (section, group).
/// </summary>
public class Cohort
{
    /// <summary>
    /// Unique cohort identifier (e.g., "section-a", "ta-group-1").
    /// </summary>
    public required string CohortName { get; set; }

    /// <summary>
    /// Course this cohort belongs to.
    /// </summary>
    public required string CourseName { get; set; }

    /// <summary>
    /// Cohort description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Teaching assistant (TA) assigned to this cohort.
    /// </summary>
    public string? AssignedTa { get; set; }

    /// <summary>
    /// Student IDs in this cohort.
    /// </summary>
    public List<string> StudentIds { get; set; } = new();

    /// <summary>
    /// Cohort creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the number of students in this cohort.
    /// </summary>
    public int GetStudentCount() => StudentIds.Count;
}
```

---

## ProgressMetrics

Represents progress metrics for a student.

**File:** `src/HotSwap.LabManager.Domain/Models/ProgressMetrics.cs`

```csharp
namespace HotSwap.LabManager.Domain.Models;

/// <summary>
/// Represents progress metrics for a student.
/// </summary>
public class ProgressMetrics
{
    /// <summary>
    /// Student identifier.
    /// </summary>
    public required string StudentId { get; set; }

    /// <summary>
    /// Course name.
    /// </summary>
    public required string CourseName { get; set; }

    /// <summary>
    /// Lab identifier (null for course-level metrics).
    /// </summary>
    public string? LabId { get; set; }

    /// <summary>
    /// Lab started (environment accessed at least once).
    /// </summary>
    public bool LabStarted { get; set; } = false;

    /// <summary>
    /// Lab submitted.
    /// </summary>
    public bool LabSubmitted { get; set; } = false;

    /// <summary>
    /// Lab graded.
    /// </summary>
    public bool LabGraded { get; set; } = false;

    /// <summary>
    /// Total active time (time spent working).
    /// </summary>
    public TimeSpan ActiveTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Environment access count.
    /// </summary>
    public int AccessCount { get; set; } = 0;

    /// <summary>
    /// Submission attempts.
    /// </summary>
    public int SubmissionAttempts { get; set; } = 0;

    /// <summary>
    /// Current score (null if not graded).
    /// </summary>
    public double? Score { get; set; }

    /// <summary>
    /// Completion percentage (0-100).
    /// </summary>
    public double CompletionPercent { get; set; } = 0;

    /// <summary>
    /// Last activity timestamp (UTC).
    /// </summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>
    /// Metrics last updated timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Checks if student is struggling (low progress, high error rate).
    /// </summary>
    public bool IsStruggling()
    {
        // Student is struggling if:
        // - Lab started but completion < 25% after 7 days
        // - High access count (>10) but no submission
        var daysSinceStart = LastActivityAt.HasValue ? (DateTime.UtcNow - LastActivityAt.Value).TotalDays : 0;

        if (LabStarted && CompletionPercent < 25 && daysSinceStart > 7)
            return true;

        if (AccessCount > 10 && !LabSubmitted)
            return true;

        return false;
    }
}
```

---

## Enumerations

### CourseStatus

**File:** `src/HotSwap.LabManager.Domain/Enums/CourseStatus.cs`

```csharp
namespace HotSwap.LabManager.Domain.Enums;

/// <summary>
/// Represents the status of a course.
/// </summary>
public enum CourseStatus
{
    /// <summary>
    /// Course is active (students can enroll, labs can be created).
    /// </summary>
    Active,

    /// <summary>
    /// Course is archived (read-only, no new labs or students).
    /// </summary>
    Archived
}
```

### LabType

```csharp
namespace HotSwap.LabManager.Domain.Enums;

/// <summary>
/// Represents the type of lab exercise.
/// </summary>
public enum LabType
{
    /// <summary>
    /// Coding lab (programming exercises).
    /// </summary>
    Coding,

    /// <summary>
    /// Interactive notebook (Jupyter, RStudio).
    /// </summary>
    Notebook,

    /// <summary>
    /// Infrastructure lab (Docker, Kubernetes, cloud).
    /// </summary>
    Infrastructure,

    /// <summary>
    /// Research lab (HPC clusters, data analysis).
    /// </summary>
    Research
}
```

### LabStatus

```csharp
namespace HotSwap.LabManager.Domain.Enums;

/// <summary>
/// Represents the status of a lab.
/// </summary>
public enum LabStatus
{
    /// <summary>
    /// Lab is in draft (not visible to students).
    /// </summary>
    Draft,

    /// <summary>
    /// Lab is published (available to students).
    /// </summary>
    Published,

    /// <summary>
    /// Lab is archived (read-only).
    /// </summary>
    Archived
}
```

### LatePolicy

```csharp
namespace HotSwap.LabManager.Domain.Enums;

/// <summary>
/// Represents the late submission policy for a lab.
/// </summary>
public enum LatePolicy
{
    /// <summary>
    /// Allow late submissions with no penalty.
    /// </summary>
    Allow,

    /// <summary>
    /// Allow late submissions with penalty (percentage per day).
    /// </summary>
    Penalty,

    /// <summary>
    /// Deny late submissions.
    /// </summary>
    Deny
}
```

### EnvironmentStatus

```csharp
namespace HotSwap.LabManager.Domain.Enums;

/// <summary>
/// Represents the status of a student environment.
/// </summary>
public enum EnvironmentStatus
{
    /// <summary>
    /// Environment is being provisioned.
    /// </summary>
    Provisioning,

    /// <summary>
    /// Environment is active (student can access).
    /// </summary>
    Active,

    /// <summary>
    /// Environment is suspended (paused to save resources).
    /// </summary>
    Suspended,

    /// <summary>
    /// Environment is read-only (student submitted work).
    /// </summary>
    Submitted,

    /// <summary>
    /// Environment is read-only (grading complete).
    /// </summary>
    Graded,

    /// <summary>
    /// Environment is deleted.
    /// </summary>
    Deleted
}
```

### SubmissionStatus

```csharp
namespace HotSwap.LabManager.Domain.Enums;

/// <summary>
/// Represents the status of a submission.
/// </summary>
public enum SubmissionStatus
{
    /// <summary>
    /// Submission is pending grading.
    /// </summary>
    Pending,

    /// <summary>
    /// Submission is currently being graded.
    /// </summary>
    Grading,

    /// <summary>
    /// Submission has been graded.
    /// </summary>
    Graded,

    /// <summary>
    /// Grading failed (technical error).
    /// </summary>
    Failed
}
```

### GradingStatus

```csharp
namespace HotSwap.LabManager.Domain.Enums;

/// <summary>
/// Represents the status of grading.
/// </summary>
public enum GradingStatus
{
    /// <summary>
    /// Grading is pending.
    /// </summary>
    Pending,

    /// <summary>
    /// Grading is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Grading is completed.
    /// </summary>
    Completed,

    /// <summary>
    /// Grading failed (technical error).
    /// </summary>
    Failed
}
```

---

## Value Objects

### DeploymentResult

**File:** `src/HotSwap.LabManager.Domain/ValueObjects/DeploymentResult.cs`

```csharp
namespace HotSwap.LabManager.Domain.ValueObjects;

/// <summary>
/// Result of a lab deployment operation.
/// </summary>
public class DeploymentResult
{
    /// <summary>
    /// Whether deployment was successful.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Number of environments provisioned.
    /// </summary>
    public int EnvironmentsProvisioned { get; private set; }

    /// <summary>
    /// Number of failures.
    /// </summary>
    public int Failures { get; private set; }

    /// <summary>
    /// Error messages (if deployment failed).
    /// </summary>
    public List<string> Errors { get; private set; } = new();

    /// <summary>
    /// Deployment timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Deployment duration.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    public static DeploymentResult SuccessResult(int provisioned, TimeSpan duration)
    {
        return new DeploymentResult
        {
            Success = true,
            EnvironmentsProvisioned = provisioned,
            Duration = duration
        };
    }

    public static DeploymentResult Failure(int provisioned, int failures, List<string> errors, TimeSpan duration)
    {
        return new DeploymentResult
        {
            Success = false,
            EnvironmentsProvisioned = provisioned,
            Failures = failures,
            Errors = errors,
            Duration = duration
        };
    }
}
```

---

## Validation Examples

### Course Validation

```csharp
var course = new Course
{
    CourseName = "CS101",
    Title = "Introduction to Programming",
    Term = "Fall 2025",
    Instructor = "instructor@example.com"
};

if (!course.IsValid(out var errors))
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Validation Error: {error}");
    }
}
```

### Lab Validation

```csharp
var lab = new Lab
{
    LabId = "lab-cs101-1",
    CourseName = "CS101",
    LabNumber = 1,
    Title = "Hello World",
    ResourceTemplate = "dotnet-basic",
    DueDate = DateTime.UtcNow.AddDays(7)
};

if (!lab.IsValid(out var errors))
{
    Console.WriteLine("Lab validation failed:");
    errors.ForEach(Console.WriteLine);
}
```

---

**Last Updated:** 2025-11-23
**Namespace:** `HotSwap.LabManager.Domain`
