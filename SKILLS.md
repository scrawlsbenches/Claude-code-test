# Claude Skills for .NET Development

**Last Updated**: 2025-11-19

This document describes all available Claude Skills for this .NET distributed systems project. Skills are specialized tools that automate common workflows, enforce best practices, and guide you through complex tasks.

## Table of Contents

1. [Overview](#overview)
2. [Quick Reference](#quick-reference)
3. [Core Development Skills](#core-development-skills)
4. [Quality & Testing Skills](#quality--testing-skills)
5. [Documentation & Infrastructure Skills](#documentation--infrastructure-skills)
6. [Typical Workflows](#typical-workflows)
7. [How to Use Skills](#how-to-use-skills)
8. [Creating New Skills](#creating-new-skills)

---

## Overview

**What are Claude Skills?**

Claude Skills are markdown-based instruction sets that guide AI assistants through complex, multi-step workflows. Each skill encapsulates best practices, checklists, and automation for specific development tasks.

**Benefits:**
- ✅ Enforces project standards and best practices
- ✅ Reduces errors through systematic checklists
- ✅ Saves time by automating repetitive tasks
- ✅ Provides consistent workflows across sessions
- ✅ Documents institutional knowledge

**Location:** `.claude/skills/*.md`

---

## Quick Reference

### Skills by Category

| Skill | Size | When to Use | Frequency |
|-------|------|-------------|-----------|
| **Core Development** ||||
| [dotnet-setup](#dotnet-setup) | 2.6K | New session, .NET SDK missing | Per session |
| [tdd-helper](#tdd-helper) | 10K | Any code changes | Daily |
| [precommit-check](#precommit-check) | 4.6K | Before EVERY commit | Daily |
| **Quality & Testing** ||||
| [test-coverage-analyzer](#test-coverage-analyzer) | 14K | After features, quality audits | Weekly |
| [race-condition-debugger](#race-condition-debugger) | 8.4K | Intermittent test failures | As needed |
| **Documentation & Infrastructure** ||||
| [doc-sync-check](#doc-sync-check) | 14K | Before commits, monthly audits | Daily/Monthly |
| [docker-helper](#docker-helper) | 18K | Docker changes, maintenance | As needed |

**Total:** 7 skills, ~71K lines of comprehensive guidance

### Decision Tree: Which Skill to Use?

```
Starting new session?
  └─> /dotnet-setup

Writing code?
  └─> /tdd-helper (MANDATORY)

Tests failing intermittently?
  └─> /race-condition-debugger

Feature complete?
  ├─> /test-coverage-analyzer
  └─> /doc-sync-check

About to commit?
  └─> /precommit-check (MANDATORY)

Updating Docker?
  └─> /docker-helper

Monthly maintenance?
  ├─> /test-coverage-analyzer
  ├─> /doc-sync-check
  └─> /docker-helper
```

---

## Core Development Skills

### dotnet-setup

**File:** `.claude/skills/dotnet-setup.md`
**Size:** 2.6K
**Purpose:** Automatically sets up .NET 8.0 SDK and verifies the development environment

#### When to Use
- Starting a new Claude Code session
- .NET SDK is not installed
- Environment verification needed

#### What It Does
1. Checks if .NET SDK is already installed
2. Installs .NET SDK 8.0 for Ubuntu 24.04
3. Fixes common installation issues (permissions, GPG errors)
4. Verifies installation (SDK, runtimes)
5. Restores project dependencies
6. Validates build succeeds

#### Expected Duration
- Fresh install: 30-60 seconds
- Already installed: 5-10 seconds

#### Success Criteria
- ✅ `dotnet --version` shows 8.0.x or later
- ✅ `dotnet restore` completes without errors
- ✅ `dotnet build` completes with 0 errors

#### Usage
```bash
# Via slash command (if configured)
/dotnet-setup

# Or manually invoke
# The skill will guide through installation steps
```

---

### tdd-helper

**File:** `.claude/skills/tdd-helper.md`
**Size:** 10K
**Purpose:** Guides through the mandatory Red-Green-Refactor TDD workflow

#### When to Use
- **MANDATORY** for ANY code changes
- Starting new features
- Fixing bugs
- Refactoring existing code

#### What It Does
Enforces the Test-Driven Development cycle:

**Phase 1: 🔴 RED - Write Failing Test**
- Guides test structure (AAA pattern)
- Provides templates for xUnit, Moq, FluentAssertions
- Ensures test fails before implementation

**Phase 2: 🟢 GREEN - Make Test Pass**
- Implements minimal code to pass test
- Verifies test passes
- Avoids over-engineering

**Phase 3: 🔵 REFACTOR - Improve Quality**
- Extracts methods
- Improves naming
- Adds error handling and documentation
- Ensures ALL tests still pass

**Phase 4: 🔄 REPEAT**
- Guides through edge cases, error cases, alternatives

#### Expected Duration
- Per feature: 15-35 minutes
- Saves debugging time later

#### Success Criteria
- ✅ Tests written BEFORE implementation
- ✅ Tests initially failed (RED)
- ✅ Implementation makes tests pass (GREEN)
- ✅ Code refactored for quality (REFACTOR)
- ✅ ALL tests pass (zero failures)
- ✅ Coverage includes happy path, edge cases, errors

#### Key Features
- Test naming convention: `MethodName_StateUnderTest_ExpectedBehavior`
- AAA pattern enforcement (Arrange-Act-Assert)
- FluentAssertions templates (`.Should()` syntax)
- Mock setup validation (matches actual signatures)
- Common TDD mistakes to avoid
- Integration with pre-commit checklist

#### Usage
```bash
/tdd-helper

# Follow the guided workflow:
# 1. 🔴 Write failing test
# 2. 🟢 Write minimal implementation
# 3. 🔵 Refactor for quality
# 4. Run ALL tests
# 5. Repeat for next test case
```

#### Example Workflow
```csharp
// 1. 🔴 RED - Write failing test
[Fact]
public async Task AuthenticateAsync_WithValidCredentials_ReturnsToken()
{
    // Arrange
    var mockRepo = new Mock<IUserRepository>();
    var service = new AuthenticationService(mockRepo.Object);

    // Act
    var result = await service.AuthenticateAsync("user", "pass");

    // Assert
    result.Should().NotBeNull();
}

// Run test - FAILS (no implementation yet) ✅

// 2. 🟢 GREEN - Implement
public async Task<Token> AuthenticateAsync(string username, string password)
{
    // Minimal implementation
    return new Token { Value = "test-token" };
}

// Run test - PASSES ✅

// 3. 🔵 REFACTOR - Improve quality
public async Task<Token> AuthenticateAsync(string username, string password)
{
    ValidateInput(username, password);
    var user = await _repository.GetUserAsync(username);
    return GenerateToken(user);
}

// Run ALL tests - ALL PASS ✅
```

---

### precommit-check

**File:** `.claude/skills/precommit-check.md`
**Size:** 4.6K
**Purpose:** Automates the mandatory pre-commit checklist to prevent CI/CD failures

#### When to Use
- **MANDATORY** before EVERY commit
- Never skip this step

#### What It Does
Executes the critical pre-commit validation:

**Step 1: Clean Build Artifacts**
```bash
dotnet clean
```

**Step 2: Restore Dependencies**
```bash
dotnet restore
```

**Step 3: Build Non-Incrementally**
```bash
dotnet build --no-incremental
```
- ✅ Must succeed with 0 warnings, 0 errors

**Step 4: Run All Tests**
```bash
dotnet test --verbosity normal
```
- ✅ Must pass with 0 failures (568 passing, 14 skipped expected)

**Step 5: Verify Modified Files**
```bash
git status --short
git diff --staged --stat
```

**Step 6: Security Check**
- Scans for potential secrets in staged files

**Step 7: Generate Report**
- ✅ PASS: Safe to commit
- ❌ FAIL: Do NOT commit until fixed

#### Expected Duration
- Clean build + tests: ~30-40 seconds
- Worth it to prevent CI/CD failures

#### Success Criteria
- ✅ `dotnet clean` completes
- ✅ `dotnet restore` completes
- ✅ `dotnet build --no-incremental` succeeds (0 warnings, 0 errors)
- ✅ `dotnet test` passes (0 failures)
- ✅ No suspicious patterns in staged changes

#### The Golden Rule
```bash
# This MUST succeed before committing:
dotnet clean && \
dotnet restore && \
dotnet build --no-incremental && \
dotnet test

# If ALL succeed → Safe to commit
# If ANY fails → DO NOT commit until fixed
```

#### Usage
```bash
/precommit-check

# If all checks pass:
git commit -m "feat: your feature"

# If any check fails:
# Fix the issue, then run /precommit-check again
```

#### Special Cases
- Docker changes: Additional validation required
- Integration tests: Run separately if modified
- New packages: Check for vulnerabilities

---

## Quality & Testing Skills

### test-coverage-analyzer

**File:** `.claude/skills/test-coverage-analyzer.md`
**Size:** 14K
**Purpose:** Analyzes test coverage to maintain the required 85%+ coverage target

#### When to Use
- After implementing new features
- During code reviews
- Monthly quality audits
- Before major releases

#### What It Does

**Phase 1: Generate Coverage Report**
```bash
dotnet test --collect:"XPlat Code Coverage"
```
- Generates `coverage.cobertura.xml` report

**Phase 2: Analyze Overall Coverage**
- Calculates line coverage percentage
- Calculates branch coverage percentage
- Compares against 85% target
- Shows coverage by project

**Phase 3: Identify Untested Code**
- Lists files with <80% coverage
- Finds methods with 0% coverage
- Prioritizes by impact

**Phase 4: Analyze Critical Paths**
- Checks core components (Orchestrator, Infrastructure, API, Domain)
- Ensures critical paths have >90% coverage

**Phase 5: Generate Recommendations**
- Priority 1: Critical components below 85%
- Priority 2: Completely untested files
- Priority 3: Complex methods without tests

**Phase 6: Human-Readable Report**
- Summary with metrics
- Status (PASS/NEEDS IMPROVEMENT)
- Next steps and action items

#### Expected Duration
- Test execution: ~20-30 seconds
- Analysis: ~10-15 seconds
- Total: ~30-50 seconds

#### Success Criteria
- ✅ Coverage report generated successfully
- ✅ Overall line coverage >= 85%
- ✅ Core components >= 90%
- ✅ No critical paths with 0% coverage
- ✅ All public APIs have basic tests

#### Usage
```bash
/test-coverage-analyzer

# Review the report output:
# - Overall coverage percentage
# - Files with low coverage
# - Uncovered methods
# - Recommendations
```

#### Integration with TDD
```bash
# 1. Run coverage analysis
/test-coverage-analyzer

# 2. Identify gaps

# 3. For each gap, use TDD
/tdd-helper

# 4. Re-run coverage to verify
/test-coverage-analyzer
```

---

### race-condition-debugger

**File:** `.claude/skills/race-condition-debugger.md`
**Size:** 8.4K
**Purpose:** Specialized skill for debugging async/await race conditions and timing issues

#### When to Use
- Tests pass locally but fail on CI/CD
- Intermittent test failures
- `KeyNotFoundException` or `NullReferenceException` in async code
- "Sometimes works, sometimes doesn't" behavior

#### What It Does

**Phase 1: Reproduce and Characterize**
- Compares local vs CI/CD behavior
- Analyzes timing patterns
- Runs tests multiple times to check consistency

**Phase 2: Identify Race Condition Patterns**
- Pattern 1: Task.Run() fire-and-forget
- Pattern 2: Missing status updates
- Pattern 3: Cache/state lookup timing

**Phase 3: Verify Dependency Injection Lifetimes**
- Checks Singleton vs Scoped vs Transient
- Ensures shared state services are Singleton

**Phase 4: Async/Await Anti-Patterns**
- Finds `async void` methods
- Detects blocking on async code (`.Result`, `.Wait()`)
- Identifies missing awaits

**Phase 5: Implement Defensive Fixes**
- Layer 1: Fix operation ordering
- Layer 2: Add status updates
- Layer 3: Add retry logic
- Layer 4: Add timeout protection

**Phase 6: Testing Strategy**
- Local testing (run test 10+ times)
- CI/CD verification
- Load testing

**Phase 7: Documentation**
- Documents root cause in code comments
- Adds reference to investigation

#### Expected Duration
- Investigation: 30-60 minutes
- Fix implementation: 15-30 minutes
- Verification: 10-20 minutes

#### Success Criteria
- ✅ Test passes consistently locally (10+ runs)
- ✅ Test passes consistently on CI/CD
- ✅ Timing logs show correct operation order
- ✅ No race conditions under load
- ✅ Root cause documented in code

#### Usage
```bash
/race-condition-debugger

# Follow systematic investigation:
# 1. Reproduce and characterize
# 2. Identify patterns
# 3. Verify DI lifetimes
# 4. Check async anti-patterns
# 5. Implement fixes
# 6. Test thoroughly
# 7. Document findings
```

---

## Documentation & Infrastructure Skills

### doc-sync-check

**File:** `.claude/skills/doc-sync-check.md`
**Size:** 14K
**Purpose:** Validates that documentation is synchronized with code changes

#### When to Use
- Before EVERY commit (especially if code changed)
- Monthly documentation audit
- Before major releases
- When updating documentation

#### What It Does

**Phase 1: Identify What Changed**
- Checks git changes (code, Docker, docs, tests)
- Determines which docs need updating

**Phase 2: Validate Specific Documentation**

**Check 1: Test Count Synchronization**
- Compares actual vs documented test counts
- Updates 5 locations in CLAUDE.md:
  - Line 16: Build Status
  - Line 115: Project Metrics table
  - Line 388: First Time Build
  - Line 435: Run All Tests
  - Line 473: Critical Path Tests

**Check 2: Package Version Synchronization**
- Lists all packages with versions
- Compares with documented versions in CLAUDE.md
- Updates Technology Stack section

**Check 3: Project Structure Synchronization**
- Compares actual vs documented structure
- Updates Project Structure ASCII tree

**Check 4: API Signature Changes**
- Finds public API changes
- Ensures XML documentation exists
- Updates README.md if user-facing

**Check 5: Build/Test Process Changes**
- Verifies pre-commit commands still work
- Updates instructions if changed

**Check 6: Docker Configuration Synchronization**
- Checks base image versions
- Verifies port mappings
- Validates environment variables
- Updates Docker documentation

**Phase 3: Validate Documentation Freshness**
- Checks "Last Updated" dates (flags >90 days)
- Verifies Changelog entries exist
- Finds broken file references

**Phase 4: Validate Code Examples**
- Reviews C# code examples in docs
- Tests command examples work

**Phase 5: Task List Synchronization**
- Updates TASK_LIST.md statuses
- Adds completion notes

**Phase 6: Generate Report**
- Summary of what changed
- Documentation update requirements
- Next steps

#### Expected Duration
- Full check: 2-3 minutes
- Quick check (test counts only): 30 seconds

#### Success Criteria
- ✅ Test counts match actual counts
- ✅ Package versions documented correctly
- ✅ Project structure matches reality
- ✅ No broken file references
- ✅ "Last Updated" dates current
- ✅ Changelog has recent entries
- ✅ Code examples compile
- ✅ Command examples work

#### Key Principle
> "Stale documentation is worse than no documentation. Outdated docs mislead developers, waste time, and cause bugs."

#### Usage
```bash
/doc-sync-check

# Review the report
# Update documentation as needed
# Then run pre-commit check
/precommit-check
```

---

### docker-helper

**File:** `.claude/skills/docker-helper.md`
**Size:** 18K
**Purpose:** Assists with Docker configuration maintenance following security and optimization best practices

#### When to Use
- Updating base images or .NET SDK version
- Adding new services to docker-compose.yml
- Optimizing Docker build performance
- Conducting security reviews
- Monthly Docker maintenance tasks

#### What It Does

**Phase 1: Initial Assessment**
- Checks current Dockerfile and docker-compose.yml
- Identifies update triggers
- Reviews last modification dates

**Phase 2: Dockerfile Optimization**
- Validates multi-stage build
- Checks layer caching optimization
- Security best practices check:
  - Non-root user
  - Alpine images
  - No secrets in Dockerfile

**Phase 3: docker-compose.yml Validation**
- Validates syntax (`docker-compose config`)
- Checks service configuration
- Verifies resource limits
- Reviews network configuration

**Phase 4: Build and Test**
- Builds Docker image
- Verifies image size (<500MB for runtime)
- Tests container startup
- Health check validation
- Cleans up test resources

**Phase 5: docker-compose Stack Test**
- Starts full stack
- Verifies all services
- Tests service connectivity (API, Redis, Jaeger)
- Runs tests in container (optional)
- Cleans up stack

**Phase 6: Security Scanning**
- Scans with Docker Scout
- Scans with Trivy (if available)
- Reports HIGH/CRITICAL vulnerabilities

**Phase 7: Generate Recommendations**
- Summary report
- Best practices checklist
- Optimization recommendations

#### Expected Duration
- Dockerfile validation: ~1 minute
- Image build and test: ~2-3 minutes
- docker-compose stack test: ~1-2 minutes
- Security scan: ~1-2 minutes
- Total: ~5-8 minutes

#### Success Criteria
- ✅ Dockerfile builds successfully
- ✅ Multi-stage build implemented
- ✅ Image size reasonable (<500MB)
- ✅ Non-root user configured
- ✅ docker-compose.yml valid
- ✅ All services start successfully
- ✅ Health endpoints respond
- ✅ No HIGH/CRITICAL vulnerabilities
- ✅ Documentation updated

#### Monthly Maintenance Tasks
```bash
# 1. Check for base image updates
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull mcr.microsoft.com/dotnet/aspnet:8.0

# 2. Scan for vulnerabilities
trivy image hotswap-orchestrator:latest --severity HIGH,CRITICAL

# 3. Review logs for errors
docker-compose logs --tail=100 orchestrator-api | grep -i error

# 4. Clean up unused resources
docker system prune -a --volumes

# 5. Verify .dockerignore is current
du -sh .

# 6. Test build performance
time docker build --no-cache -t hotswap-test .
```

#### Usage
```bash
/docker-helper

# Follow the comprehensive validation
# Address any warnings or errors
# Update documentation if needed
```

---

## Typical Workflows

### Daily Development Workflow

```bash
# Morning: Setup environment
/dotnet-setup

# Development: TDD for each feature
/tdd-helper
# 🔴 Write failing test
# 🟢 Implement feature
# 🔵 Refactor code

# Before commit: Validation
/doc-sync-check      # If documentation affected
/precommit-check     # Always mandatory

# Commit
git commit -m "feat: your feature"
```

### Feature Completion Workflow

```bash
# 1. Implement with TDD
/tdd-helper

# 2. Check test coverage
/test-coverage-analyzer

# 3. If coverage <85%, add more tests
/tdd-helper  # For each gap

# 4. Validate documentation
/doc-sync-check

# 5. Pre-commit validation
/precommit-check

# 6. Commit
git commit -m "feat: complete feature X (coverage: 87%)"
```

### Bug Fix Workflow

```bash
# 1. If intermittent/async bug
/race-condition-debugger

# 2. Write failing test (TDD)
/tdd-helper
# 🔴 Test reproduces bug

# 3. Fix bug
/tdd-helper
# 🟢 Test passes
# 🔵 Refactor

# 4. Pre-commit check
/precommit-check

# 5. Commit
git commit -m "fix: resolve race condition in deployment rollback"
```

### Docker Update Workflow

```bash
# 1. Update Dockerfile/docker-compose.yml
# (edit files)

# 2. Validate Docker configuration
/docker-helper

# 3. Update documentation
/doc-sync-check

# 4. Pre-commit check
/precommit-check

# 5. Commit
git commit -m "feat: update Docker to .NET 8.0.121"
```

### Monthly Maintenance Workflow

```bash
# 1. Test coverage audit
/test-coverage-analyzer

# 2. Documentation audit
/doc-sync-check

# 3. Docker maintenance
/docker-helper

# 4. Address findings from audits

# 5. Commit improvements
git commit -m "chore: monthly maintenance - coverage and docs"
```

---

## How to Use Skills

### Method 1: Slash Commands (if configured)

```bash
# Simply type the skill name with a slash
/tdd-helper
/precommit-check
/doc-sync-check
```

### Method 2: Via Claude Code Tool

If slash commands aren't configured, skills can be invoked programmatically:

```python
# The assistant can invoke skills directly
Skill(command="tdd-helper")
```

### Method 3: Manual Execution

Open the skill file and follow the instructions manually:

```bash
# Read the skill
cat .claude/skills/tdd-helper.md

# Follow the step-by-step instructions
```

---

## Creating New Skills

### Skill Template

```markdown
# Skill Name

**Description**: Brief one-line description

**When to use**:
- Scenario 1
- Scenario 2
- Scenario 3

## Instructions

This skill helps you [purpose].

### Phase 1: [Name]

#### Step 1.1: [Action]

```bash
# Commands to run
command --with-args
```

**Expected output**:
```
Sample output here
```

**On failure**:
- Troubleshooting step 1
- Troubleshooting step 2

### Phase 2: [Name]

...continue phases...

## Success Criteria

All of the following must be true:
- ✅ Criterion 1
- ✅ Criterion 2
- ✅ Criterion 3

## Reference

Based on: [CLAUDE.md sections]
```

### Best Practices for Skills

1. **Clear Purpose**: One primary purpose per skill
2. **Step-by-Step**: Break down complex tasks into phases and steps
3. **Executable**: Include actual commands to run
4. **Validation**: Define success criteria
5. **Examples**: Provide code examples where helpful
6. **Expected Output**: Show what success looks like
7. **Troubleshooting**: Address common failures
8. **Performance**: Document expected duration
9. **Integration**: Reference other skills and workflows
10. **Maintenance**: Update as project evolves

### Skill Categories

- **Core Development**: Setup, coding, committing
- **Quality & Testing**: Coverage, debugging, validation
- **Documentation**: Sync checking, generation
- **Infrastructure**: Docker, deployment, CI/CD
- **Security**: Scanning, validation, compliance

---

## Statistics

**Current Skills (as of 2025-11-19)**:

| Metric | Value |
|--------|-------|
| Total Skills | 7 |
| Total Lines | ~71,000 |
| Categories | 3 |
| Average Skill Size | ~10K lines |
| Creation Date | 2025-11-19 |

**Coverage**:
- ✅ Environment Setup: dotnet-setup
- ✅ Test-Driven Development: tdd-helper
- ✅ Pre-Commit Validation: precommit-check
- ✅ Test Coverage Analysis: test-coverage-analyzer
- ✅ Race Condition Debugging: race-condition-debugger
- ✅ Documentation Sync: doc-sync-check
- ✅ Docker Management: docker-helper

**Future Skill Ideas**:
- Package security scanner
- API documentation generator
- Performance profiling assistant
- Database migration helper
- Deployment orchestration guide

---

## References

- **CLAUDE.md**: Primary project documentation (2,000+ lines)
- **TASK_LIST.md**: Project roadmap and priorities
- **ENHANCEMENTS.md**: Recent improvements
- **README.md**: User-facing project overview

---

## Changelog

### 2025-11-19 (Initial Release - 7 Skills)

**Skills Added**:
1. **dotnet-setup** (2.6K) - Environment setup
2. **tdd-helper** (10K) - TDD workflow guidance
3. **precommit-check** (4.6K) - Pre-commit validation
4. **test-coverage-analyzer** (14K) - Coverage analysis
5. **race-condition-debugger** (8.4K) - Async debugging
6. **doc-sync-check** (14K) - Documentation synchronization
7. **docker-helper** (18K) - Docker management

**Impact**:
- Enforces mandatory TDD workflow
- Prevents CI/CD failures with pre-commit checks
- Maintains 85%+ test coverage requirement
- Prevents stale documentation
- Ensures Docker security and optimization

---

**For questions or suggestions about Claude Skills, see CLAUDE.md or open an issue.**
