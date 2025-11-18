# Autonomous Agent Onboarding Guide

**Version**: 1.0
**Last Updated**: 2025-11-17
**Target Audience**: Fully autonomous AI agents working on Claude-code-test
**Environment**: Claude Code (Ubuntu 24.04 LTS, root access)

---

## 🤖 Agent Identity & Mission

**You are**: A fully autonomous AI agent with code modification, testing, and git commit authority.

**Your mission**: Execute assigned tasks with **zero human intervention** by following strict protocols that guarantee code quality and prevent CI/CD failures.

**Key principle**: **Build + Test = Ship**. Never commit code without verification.

---

## ⚡ Quick Start Protocol (First 60 Seconds)

### Step 0: Environment Verification (MANDATORY)

```bash
# Check if .NET SDK is installed
dotnet --version
```

**If command fails**: Install .NET SDK immediately (see [.NET SDK Installation](#net-sdk-installation-protocol))

**If version < 8.0**: Install .NET SDK 8.0+

**Expected output**: `8.0.121` or later

### Step 1: Initial State Assessment (30 seconds)

```bash
# 1. Check git status
git status
git log --oneline -5

# 2. Verify branch matches session ID
git branch --show-current
# MUST start with "claude/" and end with session ID

# 3. Verify build works
dotnet restore
dotnet build --no-incremental

# 4. Verify all tests pass
dotnet test --verbosity quiet

# 5. Check current test count
# Expected: "Passed! - Failed: 0, Passed: 568, Skipped: 14, Total: 582"
```

**If ANY command fails**: STOP. Fix the issue before proceeding.

### Step 2: Read Task Assignment

**Before coding, read these files IN ORDER**:

1. **CLAUDE.md** (lines 1-150) - Critical rules and quick reference
2. **TASK_LIST.md** - Check if task is already documented
3. **Relevant source files** - Never guess contracts (property/method names)

---

## 🚨 CRITICAL: Pre-Commit Checklist (Non-Negotiable)

**Run this BEFORE EVERY commit**. No exceptions.

```bash
# The Golden Rule: All must succeed
dotnet clean && \
dotnet restore && \
dotnet build --no-incremental && \
dotnet test

# Expected results:
# - Build: 0 errors, 0-1 warnings (System.Text.Json warning is OK)
# - Tests: 568 passing, 14 skipped, 582 total

# If ANY step fails → DO NOT COMMIT
```

**Commit rejection criteria** (NEVER commit if):
- ❌ Build has errors
- ❌ ANY test is failing
- ❌ You skipped running tests
- ❌ You didn't verify contracts before using types
- ❌ You haven't updated documentation (if API/package changes)

---

## 📋 Test-Driven Development (TDD) - MANDATORY

**All code changes MUST follow Red-Green-Refactor**:

```
🔴 RED → 🟢 GREEN → 🔵 REFACTOR
```

### TDD Workflow (Non-Negotiable)

```bash
# 1. 🔴 RED - Write failing test
cd tests/HotSwap.Distributed.Tests/
# Create/edit test file with failing test
dotnet test --filter "FullyQualifiedName~YourNewTest"
# MUST FAIL (if it passes, test is wrong)

# 2. 🟢 GREEN - Write minimal implementation
cd ../../src/[appropriate-project]/
# Write ONLY enough code to pass the test
dotnet test --filter "FullyQualifiedName~YourNewTest"
# MUST PASS

# 3. 🔵 REFACTOR - Improve code quality
# Improve implementation while keeping tests green
dotnet test
# ALL tests MUST PASS

# 4. Repeat for next test case
```

**Test naming convention**:
```csharp
[Fact]
public async Task MethodName_StateUnderTest_ExpectedBehavior()
{
    // Arrange - Setup
    var mock = new Mock<IDependency>();
    var sut = new SystemUnderTest(mock.Object);

    // Act - Execute
    var result = await sut.MethodAsync(parameters);

    // Assert - Verify (use FluentAssertions)
    result.Should().NotBeNull();
    result.Property.Should().Be(expectedValue);
}
```

**Never**:
- ❌ Write implementation before tests
- ❌ Skip tests "because it's a small change"
- ❌ Comment out failing tests
- ❌ Commit with failing tests

---

## 🔐 Contract Verification Protocol (Critical)

**⚠️ NEVER guess property/method/parameter names**

### Before Using ANY Type

```bash
# 1. Find the type definition
grep -r "class TypeName\|interface ITypeName" src/

# 2. Read the COMPLETE definition
cat path/to/file.cs

# 3. Note:
#    - Exact property names (case-sensitive!)
#    - Required vs optional (required keyword, ?)
#    - Method signatures (parameters, return types)
#    - Enum values
```

### Common Contract Mistakes (Avoid These)

```csharp
// ❌ WRONG: Guessing property names
var obj = new SomeModel { Message = "..." };

// ✅ CORRECT: Read definition first, use exact names
// (After reading class definition in file)
var obj = new SomeModel { Error = "..." };

// ❌ WRONG: Mock doesn't match method signature
mock.Setup(x => x.GetAsync(It.IsAny<string>()))
    .ReturnsAsync(result);

// ✅ CORRECT: Verify method has CancellationToken parameter
mock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(result);
```

---

## 📦 Package Management

### Adding New Packages

```bash
# 1. Add package to appropriate project
dotnet add [project-path] package [PackageName]

# 2. Update CLAUDE.md Technology Stack section
# Add package with version number

# 3. If test project needs package:
# - Add to tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj
# - Any package whose types are DIRECTLY used in test code

# 4. Verify build still works
dotnet build --no-incremental
dotnet test
```

### Common Test Package Dependencies

```xml
<!-- If tests use BCrypt.Net.BCrypt directly -->
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />

<!-- If tests use ILogger<T> directly -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

---

## 📝 Documentation Update Requirements

**Update documentation when you**:

1. **Change public APIs** → Update XML comments, README.md
2. **Add/remove packages** → Update CLAUDE.md Technology Stack
3. **Change build/test process** → Update CLAUDE.md Pre-Commit Checklist
4. **Add/remove tests** → Update test counts in CLAUDE.md (multiple locations)
5. **Change project structure** → Update CLAUDE.md Project Structure section
6. **Complete tasks** → Update TASK_LIST.md status (⏳ → ✅)

### Documentation Update Checklist

```bash
# After changing code, check:
git diff --staged | grep -E "public|internal|protected"
# If API changed → Update docs

git diff --staged | grep -E "PackageReference|TargetFramework"
# If packages changed → Update CLAUDE.md Technology Stack

git diff --staged tests/
# If tests changed → Run dotnet test, update test counts

# Update "Last Updated" dates in modified .md files
# Add changelog entry to CLAUDE.md if significant changes
```

---

## 🔧 Git Workflow

### Branch Naming (Strictly Enforced)

```bash
# Branch MUST match this pattern:
claude/[descriptive-name]-[session-id]

# Examples:
# ✅ claude/add-rate-limiting-012tfd2rZHhP51C8e1hGs5nm
# ❌ feature/add-rate-limiting (missing claude/ prefix)
# ❌ claude/add-rate-limiting (missing session ID)
```

### Commit Message Format

```bash
# Use conventional commits:
git commit -m "feat: add user authentication service"

# Prefixes:
# feat:     - New features
# fix:      - Bug fixes
# docs:     - Documentation changes
# refactor: - Code refactoring (no behavior change)
# test:     - Test additions/modifications
# chore:    - Maintenance tasks (dependency updates, etc.)
```

### Push Protocol (With Retry Logic)

```bash
# ALWAYS use -u flag
git push -u origin [branch-name]

# If push fails with network error:
# Retry up to 4 times with exponential backoff (2s, 4s, 8s, 16s)

# Retry script:
for i in 1 2 3 4; do
    if git push -u origin [branch]; then
        break
    fi
    sleep $((2 ** i))
done
```

### Complete Commit Workflow

```bash
# 1. Pre-commit verification
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test

# 2. Review changes
git status
git diff --staged

# 3. Commit with message
git commit -m "feat: your feature description"

# 4. Push with retry logic
git push -u origin claude/[branch-name]

# 5. Monitor CI/CD
# Check GitHub Actions immediately after push
```

---

## 🏗️ Project Architecture

### Project Structure

```
Claude-code-test/
├── src/
│   ├── HotSwap.Distributed.Domain/          # Core domain (no dependencies)
│   ├── HotSwap.Distributed.Infrastructure/  # Cross-cutting (depends on Domain)
│   ├── HotSwap.Distributed.Orchestrator/    # Business logic (depends on Domain)
│   └── HotSwap.Distributed.Api/             # REST API (depends on all)
├── tests/
│   ├── HotSwap.Distributed.Tests/           # Unit tests (582 tests: 568 passing, 14 skipped)
│   └── HotSwap.Distributed.SmokeTests/      # Integration tests
└── examples/
    └── ApiUsageExample/                      # API usage examples
```

### Dependency Rules

```
API → Orchestrator, Infrastructure, Domain
Infrastructure → Domain
Orchestrator → Domain
Domain → (no dependencies)

Tests → All projects whose types are used in tests
```

### Namespace Convention

```csharp
// Namespace MUST match folder structure
// File: src/HotSwap.Distributed.Domain/Models/User.cs
namespace HotSwap.Distributed.Domain.Models;

// ❌ WRONG: Namespace doesn't match folder
namespace HotSwap.Domain.Models;
```

---

## 🧪 Testing Standards

### Test Coverage Requirements

- **Happy path** - Normal successful execution
- **Edge cases** - Boundary conditions, empty inputs, null values
- **Error cases** - Invalid input, exceptions, failure scenarios
- **Target coverage**: >80% (current: ~85%)

### Test Project References

```xml
<!-- Test project MUST reference all projects whose types appear in tests -->
<ItemGroup>
  <ProjectReference Include="..\..\src\HotSwap.Distributed.Domain\..." />
  <ProjectReference Include="..\..\src\HotSwap.Distributed.Infrastructure\..." />
  <ProjectReference Include="..\..\src\HotSwap.Distributed.Orchestrator\..." />
  <ProjectReference Include="..\..\src\HotSwap.Distributed.Api\..." />
</ItemGroup>
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName"

# Run tests with detailed output
dotnet test --verbosity normal

# Critical path validation
./test-critical-paths.sh

# Code validation
./validate-code.sh
```

---

## 🚀 Common Tasks

### 1. Implementing a New Feature

```bash
# Step 1: Check TASK_LIST.md
cat TASK_LIST.md | grep -i "feature-name"

# Step 2: 🔴 Write failing test
cd tests/HotSwap.Distributed.Tests/
# Create test file
dotnet test --filter "FullyQualifiedName~NewFeature"
# Verify it FAILS

# Step 3: 🟢 Implement feature
cd ../../src/[appropriate-project]/
# Write implementation
dotnet test --filter "FullyQualifiedName~NewFeature"
# Verify it PASSES

# Step 4: 🔵 Refactor
# Improve code quality
dotnet test
# Verify ALL tests PASS

# Step 5: Update documentation
# Update CLAUDE.md if needed
# Update TASK_LIST.md status
# Update README.md if user-facing

# Step 6: Pre-commit check
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test

# Step 7: Commit and push
git add .
git commit -m "feat: implement new feature"
git push -u origin claude/[branch]
```

### 2. Fixing a Bug

```bash
# Step 1: 🔴 Write test that reproduces bug
cd tests/HotSwap.Distributed.Tests/
# Write test that FAILS (demonstrates bug)
dotnet test --filter "FullyQualifiedName~BugTest"

# Step 2: 🟢 Fix the bug
cd ../../src/[project]/
# Fix implementation
dotnet test --filter "FullyQualifiedName~BugTest"
# Verify test now PASSES

# Step 3: Run all tests
dotnet test
# Ensure no regressions

# Step 4: Pre-commit check
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test

# Step 5: Commit with fix
git add .
git commit -m "fix: resolve issue with [description]"
git push -u origin claude/[branch]
```

### 3. Refactoring Code

```bash
# Step 1: Ensure tests exist
dotnet test
# If no tests → Write tests first (TDD workflow)

# Step 2: Refactor (keep behavior identical)
# Modify code structure only

# Step 3: Run tests continuously
dotnet test
# Tests MUST stay green throughout refactoring

# Step 4: Pre-commit check
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test

# Step 5: Commit
git add .
git commit -m "refactor: improve [component] structure"
git push -u origin claude/[branch]
```

### 4. Adding a New Package

```bash
# Step 1: Add package to project
dotnet add [project-path] package [PackageName]

# Step 2: Update CLAUDE.md Technology Stack section
# Add: **[PackageName] [version]** - Description

# Step 3: If test project needs it, add there too
dotnet add tests/HotSwap.Distributed.Tests/... package [PackageName]

# Step 4: Verify build
dotnet build --no-incremental
dotnet test

# Step 5: Commit
git add .
git commit -m "chore: add [PackageName] dependency"
git push -u origin claude/[branch]
```

---

## 🛠️ .NET SDK Installation Protocol

**If `dotnet --version` fails**, install immediately:

```bash
# Step 1: Download Microsoft repository configuration
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

# Step 2: Install repository (running as root, no sudo)
dpkg -i packages-microsoft-prod.deb

# Step 3: Clean up
rm packages-microsoft-prod.deb

# Step 4: Fix /tmp permissions
chmod 1777 /tmp

# Step 5: Update package lists
apt-get update
# Note: 403 errors from PPA repositories are non-critical

# Step 6: Install .NET SDK 8.0
apt-get install -y dotnet-sdk-8.0

# Step 7: Verify installation
dotnet --version
# Expected: 8.0.121 or later

dotnet --list-sdks
# Expected: 8.0.121 [/usr/lib/dotnet/sdk]

dotnet --list-runtimes
# Expected:
#   Microsoft.AspNetCore.App 8.0.21
#   Microsoft.NETCore.App 8.0.21
```

**Installation time**: ~30-60 seconds
**Disk space**: ~500 MB

---

## 🔍 Troubleshooting

### Build Failures

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build --no-incremental

# Check for:
# - Missing using statements
# - Namespace mismatches (must match folder structure)
# - Missing project references
# - Type not found (verify type exists and is public)
```

### Test Failures

```bash
# Run individual test for detailed output
dotnet test --filter "FullyQualifiedName~FailingTest" --verbosity normal

# Common issues:
# - NullReferenceException → Check mock setup
# - Timeout → Increase timeout or optimize code
# - Type not found → Ensure test project references needed projects
```

### Package Reference Errors

```bash
# If test uses type directly but package not referenced:
# Add package to test project .csproj

# Example: Tests use BCrypt but package missing
dotnet add tests/HotSwap.Distributed.Tests/... package BCrypt.Net-Next

# Rebuild
dotnet restore
dotnet build --no-incremental
```

### Git Push Failures

```bash
# Network errors → Retry with backoff
for i in 1 2 3 4; do
    if git push -u origin claude/[branch]; then
        echo "Push successful"
        break
    fi
    echo "Retry $i/4 after $((2 ** i))s..."
    sleep $((2 ** i))
done

# 403 Forbidden → Branch name must start with "claude/" and end with session ID
git branch -m [current] claude/[descriptive-name]-[session-id]
git push -u origin claude/[descriptive-name]-[session-id]
```

---

## 📊 Quality Gates (Autonomous Decision Making)

### When to Proceed (All conditions met)

- ✅ Build succeeds with 0 errors
- ✅ All tests pass (0 failures, 0 skips)
- ✅ Test coverage >80%
- ✅ Documentation updated
- ✅ TASK_LIST.md updated
- ✅ No hardcoded secrets/credentials
- ✅ No breaking API changes (or documented)

### When to STOP and Report (Any condition met)

- 🛑 Tests fail after multiple fix attempts
- 🛑 Circular dependency detected
- 🛑 Breaking change to public API (requires approval)
- 🛑 Security vulnerability detected
- 🛑 Task requires external resource (database, external API)
- 🛑 Task ambiguity cannot be resolved from documentation

---

## 🎯 Task Completion Checklist

**Before marking any task as complete**:

```bash
# 1. ✅ Code implemented
# 2. ✅ Tests written BEFORE implementation (TDD)
# 3. ✅ All tests pass
dotnet test

# 4. ✅ Build succeeds
dotnet build --no-incremental

# 5. ✅ Documentation updated
# - CLAUDE.md (if setup/testing/API changed)
# - README.md (if user-facing)
# - TASK_LIST.md (status updated)
# - XML comments (public APIs)

# 6. ✅ Code review (self)
git diff --staged

# 7. ✅ Pre-commit checklist passed
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test

# 8. ✅ Committed and pushed
git commit -m "feat: description"
git push -u origin claude/[branch]

# 9. ✅ CI/CD passing (check GitHub Actions)

# If ALL checks pass → Task complete
# If ANY check fails → Task NOT complete
```

---

## 📚 Essential Reading

**Read these files BEFORE starting work**:

1. **CLAUDE.md** - Complete development guide (84KB, ~2000 lines)
   - Quick Reference section ⭐⭐⭐ CRITICAL
   - Pre-Commit Checklist section ⭐⭐⭐ CRITICAL
   - TDD Workflow section ⭐⭐⭐ CRITICAL

2. **TASK_LIST.md** - Task roadmap (20+ tasks with priorities)

3. **README.md** - Project overview

4. **PROJECT_STATUS_REPORT.md** - Current status (95% spec compliance, 582 tests)

**Reference as needed**:

- **ENHANCEMENTS.md** - Recent enhancements documentation
- **TESTING.md** - Testing documentation
- **BUILD_STATUS.md** - Build validation report
- **SPEC_COMPLIANCE_REVIEW.md** - Specification compliance

---

## 🤖 Autonomous Agent Best Practices

### Decision-Making Framework

1. **Check documentation first** (CLAUDE.md, TASK_LIST.md)
2. **Verify contracts** (read type definitions)
3. **Follow TDD** (Red-Green-Refactor)
4. **Run pre-commit check** (build + test)
5. **Update documentation** (when needed)
6. **Commit and push** (with retry logic)
7. **Monitor CI/CD** (verify success)

### Error Handling Strategy

```bash
# When error occurs:
# 1. Read error message carefully
# 2. Check if documented in CLAUDE.md Troubleshooting
# 3. Attempt automated fix (if safe)
# 4. Verify fix with build + test
# 5. If unfixable → STOP and report

# Never:
# ❌ Ignore warnings
# ❌ Comment out failing tests
# ❌ Skip pre-commit checks
# ❌ Commit broken code
# ❌ Guess at fixes without understanding
```

### Communication Protocol

**Log all actions** in this format:

```
[TIMESTAMP] [ACTION] [STATUS] [DETAILS]

Examples:
[2025-11-17 10:00:00] BUILD STARTED Building solution...
[2025-11-17 10:00:20] BUILD SUCCESS 0 errors, 1 warning
[2025-11-17 10:00:25] TEST STARTED Running 582 tests...
[2025-11-17 10:00:35] TEST SUCCESS All tests passed
[2025-11-17 10:00:40] COMMIT CREATED feat: add rate limiting
[2025-11-17 10:00:45] PUSH SUCCESS Pushed to claude/add-rate-limiting-012tfd
```

---

## 📋 Daily Autonomous Workflow

### Session Start

```bash
# 1. Verify environment (30 seconds)
dotnet --version  # Must be 8.0.121+
git status
git log --oneline -5

# 2. Verify branch
git branch --show-current
# Must start with "claude/" and end with session ID

# 3. Baseline verification
dotnet restore
dotnet build --no-incremental
dotnet test --verbosity quiet
# All must succeed

# 4. Read assignment
# Review task description
# Check TASK_LIST.md for related tasks
# Read relevant source files
```

### During Work

```bash
# Continuous verification cycle (every 15 minutes or after significant change)
dotnet build --no-incremental
dotnet test

# Before each commit
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test
```

### Session End

```bash
# Final verification
dotnet clean
dotnet restore
dotnet build --no-incremental
dotnet test --verbosity quiet

# Commit all work
git status
git add .
git commit -m "feat: description of work completed"
git push -u origin claude/[branch]

# Verify CI/CD
# Check GitHub Actions build status
```

---

## 🎓 Learning Resources

### Official Documentation

- **.NET 8.0 Docs**: https://docs.microsoft.com/en-us/dotnet/
- **C# Guide**: https://docs.microsoft.com/en-us/dotnet/csharp/
- **ASP.NET Core**: https://docs.microsoft.com/en-us/aspnet/core/
- **xUnit**: https://xunit.net/
- **Moq**: https://github.com/moq/moq4
- **FluentAssertions**: https://fluentassertions.com/

### Project-Specific Docs

- **CLAUDE.md** - Complete development guide
- **workflows/** directory - Detailed workflow guides
  - `workflows/tdd-workflow.md`
  - `workflows/git-workflow.md`
  - `workflows/pre-commit-checklist.md`
  - `workflows/task-management.md`
- **appendices/** directory - Deep-dive guides
  - `appendices/A-DETAILED-SETUP.md`
  - `appendices/B-NO-SDK-CHECKLIST.md`
  - `appendices/C-STALE-DOCS-GUIDE.md`
  - `appendices/D-TDD-PATTERNS.md`

---

## 🔐 Security Considerations

**Never commit**:
- ❌ Secrets, API keys, passwords
- ❌ .env files with credentials
- ❌ Connection strings
- ❌ Private keys or certificates
- ❌ Hardcoded tokens

**Always**:
- ✅ Validate all input
- ✅ Use parameterized queries
- ✅ Sanitize output (prevent XSS)
- ✅ Follow OWASP Top 10 guidelines
- ✅ Use async/await for I/O operations
- ✅ Dispose resources properly (using statements)

---

## 📈 Success Metrics

### Key Performance Indicators

- **Build success rate**: 100% (0 failures)
- **Test pass rate**: 100% (0 failures)
- **Code coverage**: >80% (current: ~85%)
- **CI/CD success rate**: 100%
- **Documentation freshness**: <30 days
- **Task completion**: Track in TASK_LIST.md

### Self-Monitoring

```bash
# After each task completion, verify:
# 1. Build succeeds
dotnet build --no-incremental

# 2. All tests pass
dotnet test

# 3. Documentation updated
git diff --staged | grep "\.md$"

# 4. Git commit created
git log -1

# 5. Push successful
git status | grep "Your branch is up to date"

# 6. CI/CD passing
# Check GitHub Actions
```

---

## 🆘 Emergency Procedures

### If CI/CD Fails After Push

```bash
# 1. Pull latest
git pull origin claude/[branch]

# 2. Check CI/CD error logs
# Identify the failure reason

# 3. Fix locally
dotnet clean
dotnet restore
dotnet build --no-incremental
dotnet test

# 4. Commit fix
git add .
git commit -m "fix: resolve CI/CD failure - [description]"

# 5. Push
git push -u origin claude/[branch]

# 6. Monitor CI/CD again
```

### If Tests Fail Unexpectedly

```bash
# 1. Run specific failing test with detailed output
dotnet test --filter "FullyQualifiedName~FailingTest" --verbosity normal

# 2. Check for recent changes
git diff HEAD~1

# 3. Verify contracts (read type definitions)
grep -r "class TypeName" src/

# 4. Fix the issue

# 5. Verify fix
dotnet test

# 6. Commit
git add .
git commit -m "fix: resolve test failure in [test-name]"
git push -u origin claude/[branch]
```

### If Build Breaks

```bash
# 1. Clean everything
dotnet clean

# 2. Restore packages
dotnet restore

# 3. Build with detailed output
dotnet build --verbosity detailed

# 4. Identify error (read carefully!)

# 5. Common fixes:
# - Add missing using statement
# - Fix namespace mismatch
# - Add missing project reference
# - Verify type exists and is public

# 6. Rebuild
dotnet build --no-incremental

# 7. Commit fix
git add .
git commit -m "fix: resolve build error - [description]"
git push -u origin claude/[branch]
```

---

## ✅ Final Checklist (Before Each Commit)

**Print this checklist and follow it religiously**:

```
□ Code builds successfully (dotnet build --no-incremental)
□ All tests pass (dotnet test)
□ Test coverage >80%
□ TDD followed (tests written first)
□ Contracts verified (read type definitions)
□ Documentation updated (if needed)
□ TASK_LIST.md updated (if task completed)
□ No secrets in code
□ No hardcoded values (use configuration)
□ Namespace matches folder structure
□ XML comments for public APIs
□ Git commit message follows convention
□ Branch name correct (claude/[name]-[session-id])
□ Pre-commit checklist completed
□ Ready to push
```

**If ALL checked → COMMIT**
**If ANY unchecked → DO NOT COMMIT**

---

## 📝 Version History

### v1.0 (2025-11-17)
- Initial autonomous agent onboarding document
- Based on CLAUDE.md comprehensive development guide
- Tailored for fully autonomous AI agents
- Emphasizes zero-human-intervention workflows
- Includes .NET SDK installation protocol
- Complete TDD workflow integration
- Contract verification protocols
- Emergency procedures and troubleshooting

---

**Remember**: You are autonomous. Make decisions confidently based on documentation. Follow protocols strictly. Never commit broken code. Build + Test = Ship.

**Good luck, Agent! 🤖🚀**
