# Testing Strategy - Educational Lab Environment Manager

**Version:** 1.0.0
**Last Updated:** 2025-11-23
**Target Coverage:** 85%+

---

## Table of Contents

1. [Overview](#overview)
2. [Testing Pyramid](#testing-pyramid)
3. [Unit Tests](#unit-tests)
4. [Integration Tests](#integration-tests)
5. [End-to-End Tests](#end-to-end-tests)
6. [Load & Performance Tests](#load--performance-tests)
7. [Security Tests](#security-tests)
8. [Test Data Management](#test-data-management)
9. [Continuous Integration](#continuous-integration)
10. [Test Coverage Goals](#test-coverage-goals)

---

## Overview

The testing strategy follows **Test-Driven Development (TDD)** principles and aims for **85%+ code coverage** across all components.

### Testing Principles

1. **Write tests BEFORE implementation** (Red-Green-Refactor)
2. **Fast tests** - Unit tests run in < 5 seconds total
3. **Isolated tests** - Each test independent, no shared state
4. **Repeatable tests** - Same results every run
5. **Comprehensive coverage** - Happy paths, edge cases, error scenarios

### Test Categories

| Type | Count | Execution Time | Purpose |
|------|-------|----------------|---------|
| Unit Tests | 280 | < 5 sec | Test individual components |
| Integration Tests | 50 | < 60 sec | Test component interactions |
| E2E Tests | 20 | < 5 min | Test complete workflows |
| Load Tests | 5 | < 30 min | Test performance under load |

**Total: 355 tests**

---

## Testing Pyramid

```
        /\
       /  \
      / E2E \         20 tests (5%)
     /--------\
    /          \
   / Integration\     50 tests (14%)
  /--------------\
 /                \
/    Unit Tests    \   280 tests (81%)
/____________________\
```

---

## Unit Tests

### Coverage: 280 tests

Unit tests validate individual components in isolation using mocks/stubs for dependencies.

### Domain Models Tests (70 tests)

#### Course Model Tests (12 tests)

**File:** `tests/HotSwap.LabManager.Tests/Domain/Models/CourseTests.cs`

```csharp
using Xunit;
using HotSwap.LabManager.Domain.Models;

namespace HotSwap.LabManager.Tests.Domain.Models;

public class CourseTests
{
    [Fact]
    public void IsValid_ValidCourse_ReturnsTrue()
    {
        // Arrange
        var course = new Course
        {
            CourseName = "CS101",
            Title = "Introduction to Programming",
            Term = "Fall 2025",
            Instructor = "instructor@example.com"
        };

        // Act
        var isValid = course.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void IsValid_MissingCourseName_ReturnsFalse()
    {
        // Arrange
        var course = new Course
        {
            CourseName = "",
            Title = "Introduction to Programming",
            Term = "Fall 2025",
            Instructor = "instructor@example.com"
        };

        // Act
        var isValid = course.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains("CourseName is required", errors);
    }

    [Fact]
    public void Archive_SetsStatusToArchived()
    {
        // Arrange
        var course = new Course
        {
            CourseName = "CS101",
            Title = "Introduction to Programming",
            Term = "Fall 2025",
            Instructor = "instructor@example.com",
            Status = CourseStatus.Active
        };

        // Act
        course.Archive();

        // Assert
        Assert.Equal(CourseStatus.Archived, course.Status);
        Assert.NotNull(course.ArchivedAt);
    }

    // ... 9 more tests
}
```

**Test Cases:**
- ✅ Valid course passes validation
- ✅ Missing CourseName fails validation
- ✅ Invalid CourseName format fails validation
- ✅ Missing Title fails validation
- ✅ Missing Term fails validation
- ✅ Missing Instructor fails validation
- ✅ Archive() sets status to Archived
- ✅ Archive() sets ArchivedAt timestamp
- ✅ JSON serialization/deserialization works
- ✅ Default values set correctly
- ✅ Config dictionary can store custom settings
- ✅ Enrollment count increments correctly

#### Lab Model Tests (15 tests)

**Test Cases:**
- ✅ Valid lab passes validation
- ✅ Missing LabId fails validation
- ✅ Missing CourseName fails validation
- ✅ LabNumber < 1 fails validation
- ✅ DueDate in past fails validation
- ✅ LatePenaltyPercent out of range fails validation
- ✅ Publish() sets status to Published
- ✅ IsPastDue() returns true when past due date
- ✅ IsPastDue() returns false when not past due
- ✅ Autograder config validated
- ✅ TotalPoints <= 0 fails validation
- ✅ MaxSubmissionAttempts < 0 fails validation
- ✅ Lab versioning works
- ✅ Starter code URL validated
- ✅ Resource template required

#### StudentEnvironment Model Tests (12 tests)

**Test Cases:**
- ✅ ShouldAutoSuspend() returns true after timeout
- ✅ ShouldAutoSuspend() returns false before timeout
- ✅ RecordAccess() updates LastAccessedAt
- ✅ RecordAccess() increments AccessCount
- ✅ Suspend() sets status to Suspended
- ✅ Resume() sets status to Active
- ✅ Resource quota enforced
- ✅ Resource usage tracked
- ✅ ActiveTime calculated correctly
- ✅ Access URL generated
- ✅ SSH connection string format valid
- ✅ Container ID stored

#### Submission Model Tests (15 tests)

**Test Cases:**
- ✅ CalculateLateness() detects late submission
- ✅ CalculateLateness() calculates days late correctly
- ✅ CalculateLateness() applies penalty percentage
- ✅ CalculateLateness() handles no due date
- ✅ GenerateReceipt() creates unique receipt ID
- ✅ Receipt ID format correct
- ✅ Submission files stored
- ✅ File checksum calculated
- ✅ AttemptNumber increments
- ✅ IsLate flag set correctly
- ✅ Status transitions valid
- ✅ Submission notes preserved
- ✅ Multiple files supported
- ✅ File size validated
- ✅ Storage URL required

#### GradingResult Model Tests (16 tests)

**Test Cases:**
- ✅ Percentage calculated correctly
- ✅ GetFinalScore() applies late penalty
- ✅ GetFinalScore() returns full score if not late
- ✅ TestResults aggregated correctly
- ✅ Grading duration calculated
- ✅ Manual override tracked
- ✅ Override reason required
- ✅ Plagiarism result stored
- ✅ Feedback rendered as Markdown
- ✅ Score bounded (0 to TotalPoints)
- ✅ Status transitions valid
- ✅ Grader type recorded
- ✅ Test pass/fail counted
- ✅ Error messages preserved
- ✅ Partial credit supported
- ✅ Grading timeout enforced

### Repository Tests (80 tests)

#### CourseRepository Tests (20 tests)

**File:** `tests/HotSwap.LabManager.Tests/Infrastructure/Repositories/CourseRepositoryTests.cs`

```csharp
using Xunit;
using Moq;
using HotSwap.LabManager.Infrastructure.Repositories;

namespace HotSwap.LabManager.Tests.Infrastructure.Repositories;

public class CourseRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public CourseRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAsync_ValidCourse_Succeeds()
    {
        // Arrange
        var repository = new PostgresCourseRepository(_fixture.Context);
        var course = new Course
        {
            CourseName = "CS101",
            Title = "Introduction to Programming",
            Term = "Fall 2025",
            Instructor = "instructor@example.com"
        };

        // Act
        await repository.CreateAsync(course);
        var retrieved = await repository.GetByNameAsync("CS101");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("CS101", retrieved.CourseName);
    }

    // ... 19 more tests
}
```

**Test Cases:**
- ✅ CreateAsync() creates course
- ✅ GetByNameAsync() retrieves course
- ✅ GetAllAsync() returns all courses
- ✅ UpdateAsync() updates course
- ✅ DeleteAsync() deletes course
- ✅ GetByInstructorAsync() filters by instructor
- ✅ GetByTermAsync() filters by term
- ✅ GetByStatusAsync() filters by status
- ✅ Duplicate CourseName throws exception
- ✅ GetByNameAsync() returns null for non-existent
- ✅ UpdateAsync() throws for non-existent
- ✅ DeleteAsync() throws for non-existent
- ✅ Concurrent updates handled correctly
- ✅ Transaction rollback on error
- ✅ Pagination works
- ✅ Sorting works
- ✅ Search by title works
- ✅ Enrollment count calculated
- ✅ Archived courses excluded by default
- ✅ LMS course ID indexed

#### LabRepository Tests (20 tests)
#### SubmissionRepository Tests (20 tests)
#### EnvironmentRepository Tests (20 tests)

### Service Tests (80 tests)

#### EnvironmentProvisioner Tests (25 tests)

**File:** `tests/HotSwap.LabManager.Tests/Services/EnvironmentProvisionerTests.cs`

```csharp
using Xunit;
using Moq;
using Docker.DotNet;
using HotSwap.LabManager.Services;

namespace HotSwap.LabManager.Tests.Services;

public class EnvironmentProvisionerTests
{
    private readonly Mock<IDockerClient> _dockerClientMock;
    private readonly DockerEnvironmentProvisioner _provisioner;

    public EnvironmentProvisionerTests()
    {
        _dockerClientMock = new Mock<IDockerClient>();
        _provisioner = new DockerEnvironmentProvisioner(_dockerClientMock.Object);
    }

    [Fact]
    public async Task ProvisionAsync_CreatesContainer()
    {
        // Arrange
        var lab = CreateTestLab();
        var student = "student@example.com";

        _dockerClientMock
            .Setup(x => x.Containers.CreateContainerAsync(
                It.IsAny<CreateContainerParameters>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateContainerResponse { ID = "container123" });

        // Act
        var environment = await _provisioner.ProvisionAsync(lab, student);

        // Assert
        Assert.NotNull(environment);
        Assert.Equal("container123", environment.ContainerId);
        _dockerClientMock.Verify(x => x.Containers.CreateContainerAsync(
            It.IsAny<CreateContainerParameters>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ... 24 more tests
}
```

**Test Cases:**
- ✅ ProvisionAsync() creates container
- ✅ Resource quotas applied (CPU, memory, storage)
- ✅ Starter code mounted
- ✅ Network isolation configured
- ✅ Web IDE container started
- ✅ Access URL generated
- ✅ Environment variables set
- ✅ Ports exposed correctly
- ✅ Health check configured
- ✅ Provisioning timeout enforced
- ✅ Error handling for Docker failures
- ✅ Container cleanup on failure
- ✅ SSH access configured (if enabled)
- ✅ Resource template loaded
- ✅ Container naming convention
- ✅ Labels applied
- ✅ Volume permissions set
- ✅ Auto-suspend timer started
- ✅ Metrics collection started
- ✅ Concurrent provisioning handled
- ✅ Provisioning queue managed
- ✅ Rate limiting enforced
- ✅ Student namespace isolation
- ✅ Storage quota enforced
- ✅ Container restart policy set

#### Autograder Tests (20 tests)
#### ProgressTracker Tests (15 tests)
#### LmsIntegration Tests (20 tests)

### API Controller Tests (50 tests)

#### CoursesController Tests (12 tests)
#### LabsController Tests (15 tests)
#### EnvironmentsController Tests (12 tests)
#### SubmissionsController Tests (11 tests)

---

## Integration Tests

### Coverage: 50 tests

Integration tests validate interactions between components using a real database (PostgreSQL in test container).

### API Integration Tests (30 tests)

**File:** `tests/HotSwap.LabManager.IntegrationTests/API/CoursesApiTests.cs`

```csharp
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

namespace HotSwap.LabManager.IntegrationTests.API;

public class CoursesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CoursesApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateCourse_ValidRequest_Returns201()
    {
        // Arrange
        var request = new
        {
            CourseName = "CS101",
            Title = "Introduction to Programming",
            Term = "Fall 2025",
            Instructor = "instructor@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/courses", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = await response.Content.ReadFromJsonAsync<Course>();
        Assert.Equal("CS101", course.CourseName);
    }

    // ... 11 more tests
}
```

**Test Cases:**
- ✅ Create course returns 201
- ✅ Get course returns 200
- ✅ List courses returns 200
- ✅ Update course returns 200
- ✅ Archive course returns 200
- ✅ Create lab returns 201
- ✅ Publish lab returns 200
- ✅ Deploy lab returns 202
- ✅ Submit lab returns 201
- ✅ Get grading results returns 200
- ✅ Override grade returns 200 (instructor only)
- ✅ Get progress returns 200

### Database Integration Tests (10 tests)

**Test Cases:**
- ✅ Database migrations applied successfully
- ✅ Seed data created
- ✅ Constraints enforced
- ✅ Indexes created
- ✅ Foreign keys cascade correctly
- ✅ Transactions rollback on error
- ✅ Concurrent updates handled
- ✅ Query performance acceptable
- ✅ Connection pooling works
- ✅ Backup/restore works

### Docker Integration Tests (10 tests)

**Test Cases:**
- ✅ Container provisioning end-to-end
- ✅ Web IDE accessible
- ✅ Resource quotas enforced
- ✅ Auto-suspend works
- ✅ Environment cleanup works
- ✅ Volume mounting works
- ✅ Network isolation works
- ✅ Health checks work
- ✅ Container logging works
- ✅ Container restart works

---

## End-to-End Tests

### Coverage: 20 tests

E2E tests validate complete user workflows using Playwright/Selenium.

### Student Workflow Tests (10 tests)

**File:** `tests/HotSwap.LabManager.E2ETests/StudentWorkflowTests.cs`

```csharp
using Xunit;
using Microsoft.Playwright;

namespace HotSwap.LabManager.E2ETests;

public class StudentWorkflowTests : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    private IPage _page;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
        _page = await _browser.NewPageAsync();
    }

    [Fact]
    public async Task StudentCanAccessLabAndSubmit()
    {
        // 1. Student logs in
        await _page.GotoAsync("https://labs.example.com/login");
        await _page.FillAsync("#username", "student@example.com");
        await _page.FillAsync("#password", "Student123!");
        await _page.ClickAsync("#login-button");

        // 2. Student navigates to course
        await _page.WaitForSelectorAsync("text=CS101");
        await _page.ClickAsync("text=CS101");

        // 3. Student accesses lab environment
        await _page.ClickAsync("text=Lab 1: Hello World");
        await _page.ClickAsync("#access-environment");

        // 4. Wait for environment to load
        await _page.WaitForSelectorAsync("iframe#web-ide", new() { Timeout = 60000 });

        // 5. Student submits lab
        await _page.ClickAsync("#submit-lab");
        await _page.FillAsync("#submission-notes", "My submission");
        await _page.ClickAsync("#confirm-submit");

        // 6. Verify submission successful
        await _page.WaitForSelectorAsync("text=Submission successful");
        var receiptId = await _page.TextContentAsync("#receipt-id");
        Assert.NotEmpty(receiptId);
    }

    // ... 9 more tests
}
```

**Test Cases:**
- ✅ Student can log in
- ✅ Student can view courses
- ✅ Student can access lab environment
- ✅ Student can submit lab
- ✅ Student can view grades
- ✅ Student can resubmit (if allowed)
- ✅ Student cannot access other students' environments
- ✅ Student receives email notifications
- ✅ Late submission penalty applied
- ✅ Submission receipt generated

### Instructor Workflow Tests (10 tests)

**Test Cases:**
- ✅ Instructor can create course
- ✅ Instructor can create lab
- ✅ Instructor can publish lab
- ✅ Instructor can deploy lab to cohort
- ✅ Instructor can view progress analytics
- ✅ Instructor can override grade
- ✅ Instructor can identify struggling students
- ✅ Instructor can export grades to CSV
- ✅ Instructor can configure autograder
- ✅ Instructor can archive course

---

## Load & Performance Tests

### Coverage: 5 tests

Load tests validate performance under realistic and peak loads using k6.

### Concurrent Student Access Test

**File:** `tests/HotSwap.LabManager.LoadTests/concurrent-access.js`

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 100 },   // Ramp up to 100 users
    { duration: '5m', target: 1000 },  // Ramp up to 1000 users
    { duration: '10m', target: 1000 }, // Stay at 1000 users
    { duration: '2m', target: 0 },     // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests < 500ms
    http_req_failed: ['rate<0.01'],   // Error rate < 1%
  },
};

export default function () {
  // Student accesses environment
  const res = http.get('https://labs.example.com/api/v1/environments/env-123/access', {
    headers: { Authorization: `Bearer ${__ENV.TOKEN}` },
  });

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });

  sleep(1);
}
```

**Test Cases:**
- ✅ 1,000 concurrent students accessing environments
- ✅ 5,000 concurrent students (peak load)
- ✅ 100 simultaneous lab deployments
- ✅ 500 simultaneous autograding jobs
- ✅ 1,000 simultaneous submissions

**Performance Targets:**
- Environment provisioning: p95 < 60s
- API response time: p95 < 500ms
- Grading throughput: 100 jobs/min
- Database query time: p95 < 100ms

---

## Security Tests

### Authentication Tests (8 tests)

**Test Cases:**
- ✅ Unauthenticated requests rejected
- ✅ Expired tokens rejected
- ✅ Invalid tokens rejected
- ✅ Token refresh works
- ✅ JWT claims validated
- ✅ Role-based access enforced
- ✅ SQL injection prevented
- ✅ XSS attacks prevented

### Authorization Tests (10 tests)

**Test Cases:**
- ✅ Student cannot access instructor endpoints
- ✅ Student cannot access other students' data
- ✅ TA can only access assigned cohorts
- ✅ Instructor can only manage own courses
- ✅ Admin has full access
- ✅ Environment isolation enforced
- ✅ File upload validated (size, type)
- ✅ Rate limiting enforced
- ✅ CSRF protection enabled
- ✅ Audit logging captures all operations

---

## Test Data Management

### Test Fixtures

```csharp
public class DatabaseFixture : IDisposable
{
    public LabManagerDbContext Context { get; private set; }

    public DatabaseFixture()
    {
        var options = new DbContextOptionsBuilder<LabManagerDbContext>()
            .UseNpgsql("Host=localhost;Database=labmanager_test;Username=test;Password=test")
            .Options;

        Context = new LabManagerDbContext(options);
        Context.Database.EnsureCreated();
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Seed courses
        Context.Courses.Add(new Course
        {
            CourseName = "CS101",
            Title = "Introduction to Programming",
            Term = "Fall 2025",
            Instructor = "instructor@example.com"
        });

        // Seed labs
        // Seed students
        // ...

        Context.SaveChanges();
    }

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
    }
}
```

### Mock Data Builders

```csharp
public class CourseBuilder
{
    private string _courseName = "CS101";
    private string _title = "Introduction to Programming";
    private string _term = "Fall 2025";
    private string _instructor = "instructor@example.com";

    public CourseBuilder WithCourseName(string courseName)
    {
        _courseName = courseName;
        return this;
    }

    public CourseBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public Course Build()
    {
        return new Course
        {
            CourseName = _courseName,
            Title = _title,
            Term = _term,
            Instructor = _instructor
        };
    }
}
```

---

## Continuous Integration

### GitHub Actions Workflow

**File:** `.github/workflows/test.yml`

```yaml
name: Test Suite

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      - name: Run unit tests
        run: dotnet test tests/HotSwap.LabManager.Tests --configuration Release --logger "trx;LogFileName=unit-test-results.trx"
      - name: Upload test results
        uses: actions/upload-artifact@v3
        with:
          name: unit-test-results
          path: '**/unit-test-results.trx'

  integration-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:15-alpine
        env:
          POSTGRES_DB: labmanager_test
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test
        ports:
          - 5432:5432
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      - name: Run integration tests
        run: dotnet test tests/HotSwap.LabManager.IntegrationTests --configuration Release

  code-coverage:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      - name: Run tests with coverage
        run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v3
        with:
          files: ./coverage/**/coverage.cobertura.xml
          fail_ci_if_error: true
```

---

## Test Coverage Goals

### Overall Coverage: 85%+

| Component | Coverage Target | Current |
|-----------|-----------------|---------|
| Domain Models | 95% | - |
| Repositories | 85% | - |
| Services | 85% | - |
| API Controllers | 80% | - |
| Infrastructure | 75% | - |

### Coverage Report

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html

# Open report
open coveragereport/index.html
```

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Test Count:** 355 tests (280 unit, 50 integration, 20 E2E, 5 load)
**Coverage Target:** 85%+
