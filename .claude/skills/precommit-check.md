# Pre-Commit Validation Skill

**Description**: Automates the mandatory pre-commit checklist from CLAUDE.md to ensure code quality before commits.

**When to use**: Before EVERY commit to prevent CI/CD failures and maintain code quality.

## Instructions

This skill implements the critical pre-commit checklist that MUST be completed before any commit. Follow these steps in order and STOP if any step fails.

### Step 1: Clean Build Artifacts

```bash
dotnet clean
```

**Expected**: Should complete in 2-5 seconds
**On failure**: Report error and STOP

### Step 2: Restore Dependencies

```bash
dotnet restore
```

**Expected**: "All projects are up-to-date for restore" or successful restoration
**On failure**:
- Check network connectivity
- Clear NuGet cache: `dotnet nuget locals all --clear`
- Report error and STOP

### Step 3: Build Non-Incrementally

```bash
dotnet build --no-incremental 2>&1 | tee /tmp/build-output.log
```

**Critical checks**:
- ✅ Build succeeded
- ✅ **0 Warning(s)** (warnings are future errors)
- ✅ **0 Error(s)**

**On failure**:
- Show last 20 lines of build output
- Report specific errors/warnings
- STOP - DO NOT PROCEED

**Common build issues**:
- Missing using statements
- Namespace mismatches
- Type not found errors
- Missing project references

### Step 4: Run All Tests

```bash
dotnet test --verbosity normal 2>&1 | tee /tmp/test-output.log
```

**Critical checks**:
- ✅ **0 Failed tests** (MANDATORY)
- ✅ Check skipped tests are expected (14 skipped is normal)
- ✅ No test timeouts or hangs

**Expected output**:
```
Passed!  - Failed:     0, Passed:   568, Skipped:    14, Total:   582
```

**On failure**:
- Show which tests failed
- Show failure reasons
- STOP - DO NOT COMMIT

**Common test failures**:
- NullReferenceException (check mock setup)
- Timeouts (optimize or increase timeout)
- Missing dependencies in test project

### Step 5: Verify Modified Files

```bash
git status --short
git diff --staged --stat
```

**Check**:
- Are the correct files staged?
- Any unintended changes?
- Any generated files that should be .gitignored?

### Step 6: Security Check

```bash
# Check for potential secrets in staged files
git diff --staged | grep -iE "password|secret|api[_-]?key|token|credential" || echo "No potential secrets found"
```

**On match**: Review carefully to ensure no actual secrets are being committed

### Step 7: Final Summary Report

If ALL steps passed, provide summary:

```
✅ PRE-COMMIT VALIDATION PASSED

Build:     0 warnings, 0 errors
Tests:     XXX passed, YY skipped, 0 failed
Duration:  ~XX seconds
Status:    SAFE TO COMMIT

Staged files:
[list of staged files]

Next step: git commit -m "your message"
```

If ANY step failed:

```
❌ PRE-COMMIT VALIDATION FAILED

Failed at: [step name]
Reason:    [specific error]

DO NOT COMMIT until this is fixed.

Troubleshooting:
[relevant troubleshooting steps]
```

## The Golden Rule

**BUILD + TEST = SUCCESS**

```bash
# This MUST succeed before committing:
dotnet clean && \
dotnet restore && \
dotnet build --no-incremental && \
dotnet test

# If ALL steps succeed → Safe to commit
# If ANY step fails → DO NOT commit until fixed
```

## Special Cases

### Docker Changes
If Dockerfile or docker-compose.yml were modified:
```bash
# Additional validation required
docker build -t test-build .
docker-compose config
```

### Integration Test Changes
If files in `tests/HotSwap.Distributed.IntegrationTests/` were modified:
```bash
# Run integration tests specifically
dotnet test tests/HotSwap.Distributed.IntegrationTests/ --verbosity normal
```

### New Package Added
If .csproj files were modified:
```bash
# Check for vulnerable or outdated packages
dotnet list package --vulnerable
dotnet list package --outdated
```

## What NOT to Do

❌ **NEVER commit if**:
- Build has warnings or errors
- ANY tests are failing
- You haven't run `dotnet test`
- Code contains `// TODO: Fix this before commit`
- Secrets or credentials are in staged files

## Automation

This skill can be called manually before commits:
```
/precommit-check
```

Or integrated into git hooks (see CLAUDE.md for git hook setup).

## Performance Notes

- Typical duration: 30-40 seconds
- Clean: ~3s
- Restore: ~5s (cached)
- Build: ~20s
- Test: ~18s

## Success Criteria

All of the following MUST be true:
- ✅ `dotnet clean` completes
- ✅ `dotnet restore` completes
- ✅ `dotnet build --no-incremental` succeeds with 0 warnings, 0 errors
- ✅ `dotnet test` passes with 0 failures
- ✅ No suspicious patterns in staged changes
- ✅ Changes are intentional and reviewed

Only then is it safe to commit.
