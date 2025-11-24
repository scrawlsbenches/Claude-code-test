# Git Hooks for HotSwap Distributed Kernel

This directory contains Git hooks that automate quality checks and enforce best practices from `CLAUDE.md`.

## Available Hooks

### `pre-commit`
Runs before each commit to ensure code quality.

**Checks performed:**
1. ✅ .NET SDK availability
2. ✅ Clean build artifacts
3. ✅ Restore NuGet packages
4. ✅ Build solution (non-incremental)
5. ✅ Check for warnings (fails on ANY warnings)
6. ✅ Run all tests
7. ✅ Verify zero test failures
8. ✅ Verify staged files

**Execution time:** ~30-60 seconds (depending on test suite size)

### `pre-push`
Runs before pushing to remote repository.

**Checks performed:**
1. ✅ Branch naming convention (`claude/*` recommended)
2. ✅ No uncommitted changes
3. ✅ Tests still pass (quick sanity check)
4. ✅ Prevent direct push to main/master
5. 📋 Task documentation reminder (non-blocking)

**Execution time:** ~20-40 seconds

## Installation

### Quick Install

```bash
# From repository root
.githooks/install-hooks.sh
```

### Manual Install

```bash
# Copy hooks to .git/hooks/
cp .githooks/pre-commit .git/hooks/pre-commit
cp .githooks/pre-push .git/hooks/pre-push

# Make them executable
chmod +x .git/hooks/pre-commit
chmod +x .git/hooks/pre-push
```

## Usage

### Normal Workflow

Once installed, hooks run automatically:

```bash
# Hooks run automatically
git commit -m "feat: add new feature"
git push origin claude/my-branch
```

### Bypassing Hooks (Not Recommended)

**Only use when absolutely necessary:**

```bash
# Bypass pre-commit hook
git commit --no-verify -m "emergency fix"

# Bypass pre-push hook
git push --no-verify origin claude/my-branch
```

**⚠️ Warning:** Bypassing hooks can lead to:
- Failing CI/CD builds
- Breaking changes in main branch
- Poor code quality
- Test failures in production

## Customization

### Adjusting Test Speed

To run only unit tests (faster pre-commit):

Edit `.githooks/pre-commit` test command:

```bash
# Run only unit tests (faster)
dotnet test --filter "FullyQualifiedName!~IntegrationTests" --verbosity quiet

# Or run all tests (comprehensive, slower)
dotnet test --verbosity quiet
```

### Disabling Specific Checks

Comment out unwanted checks in the hook scripts:

```bash
# Example: Disable warning check
# WARNING_COUNT=$(echo "$BUILD_OUTPUT" | grep -c "Warning(s)" || true)
```

## Troubleshooting

### Hook Not Running

```bash
# Check if hooks are executable
ls -la .git/hooks/

# If not executable, fix with:
chmod +x .git/hooks/pre-commit
chmod +x .git/hooks/pre-push
```

### Hook Fails with "dotnet: command not found"

Install .NET SDK 8.0. See `CLAUDE.md` "Development Environment Setup" section.

```bash
# Verify .NET SDK installation
dotnet --version
# Expected: 8.0.121 or later
```

### Hook Takes Too Long

For large test suites, consider:

1. **Skip integration tests in pre-commit:**
   ```bash
   # In .githooks/pre-commit, change:
   dotnet test --verbosity quiet
   # To:
   dotnet test --filter "Category!=Integration" --verbosity quiet
   ```

2. **Run full tests only in pre-push:**
   - Keep pre-commit fast (unit tests only)
   - Run full suite in pre-push hook

### False Positives

If hook fails but you believe it's incorrect:

1. Run manually to debug:
   ```bash
   .githooks/pre-commit
   ```

2. Check actual error:
   ```bash
   dotnet build --no-incremental
   dotnet test --verbosity normal
   ```

3. Fix the underlying issue (don't bypass hook)

## CI/CD Integration

These hooks mirror the GitHub Actions CI/CD pipeline. If hooks pass locally, CI/CD should pass too.

**CI/CD runs:**
- All unit tests
- All integration tests
- Code coverage analysis
- Security scanning

**Local hooks run:**
- All unit tests
- All integration tests
- Build verification

## Best Practices

1. **Never bypass hooks without good reason**
   - Hooks prevent broken builds
   - Save time by catching issues early

2. **Update hooks when workflow changes**
   - Keep `.githooks/` in sync with `CLAUDE.md`
   - Update after changing test structure

3. **Test hooks after modifying**
   ```bash
   # Test pre-commit
   .githooks/pre-commit

   # Test pre-push
   .githooks/pre-push origin https://github.com/user/repo
   ```

4. **Share hooks with team**
   - Hooks are version controlled
   - All team members should install them
   - Document any customizations

## Performance Optimization

### Caching NuGet Packages

Hooks restore packages each time. Speed up with global cache:

```bash
# Set NuGet cache location (if not already set)
export NUGET_PACKAGES="$HOME/.nuget/packages"
```

### Parallel Test Execution

Enable parallel test execution in `.githooks/pre-commit`:

```bash
dotnet test --parallel --verbosity quiet
```

## Maintenance

### Updating Hooks

After modifying hooks in `.githooks/`:

```bash
# Reinstall to apply changes
.githooks/install-hooks.sh
```

### Removing Hooks

```bash
# Remove hooks
rm .git/hooks/pre-commit
rm .git/hooks/pre-push

# Or disable without removing
mv .git/hooks/pre-commit .git/hooks/pre-commit.disabled
mv .git/hooks/pre-push .git/hooks/pre-push.disabled
```

## Task Management Integration

The `pre-push` hook includes a reminder to run `./task-manager.sh pre-push` for task documentation.

### Why Document Tasks Before Pushing?

When you complete work on tasks from `TASK_LIST.md`, updating the task status helps:
- Track project progress
- Communicate completion to team members
- Maintain accurate roadmap documentation

### Task Documentation Workflow

```bash
# Before pushing (optional but recommended)
./task-manager.sh pre-push

# This interactive command will:
# - Show tasks currently in progress
# - Ask which tasks you completed
# - Prompt for implementation notes
# - Update TASK_LIST.md automatically
```

### Quick Task Commands

```bash
# See current progress
./task-manager.sh stats

# Mark task as completed
./task-manager.sh complete <task-id>

# See what to work on next
./task-manager.sh next
```

For full task-manager.sh documentation, see `workflows/task-management.md`.

## Related Documentation

- `CLAUDE.md` - Complete development guide and pre-commit checklist
- `SKILLS.md` - Claude Skills (includes `/precommit-check`)
- `TESTING.md` - Testing standards and patterns
- `workflows/task-management.md` - Task management workflow
- `.github/workflows/build-and-test.yml` - CI/CD pipeline

## Questions?

See `CLAUDE.md` or ask the team lead.

---

**Created:** 2025-11-20
**Last Updated:** 2025-11-24
**Maintainer:** Development Team
