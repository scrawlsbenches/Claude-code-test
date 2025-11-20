# Git Hooks for Distributed Kernel Orchestration System

This directory contains git hooks that enforce code quality standards based on CLAUDE.md guidelines.

## Available Hooks

### pre-commit
Automatically runs before each commit to ensure:
- ✅ Clean build (no errors, no warnings)
- ✅ All tests pass
- ✅ No compilation errors
- ✅ Code quality standards met

## Installation

### One-Time Setup (Recommended)

```bash
# Make the hook executable
chmod +x .githooks/pre-commit

# Copy to .git/hooks/
cp .githooks/pre-commit .git/hooks/pre-commit
```

### Automatic Setup for All Developers

Add this to your repository setup script:

```bash
#!/bin/bash
# setup-git-hooks.sh

echo "Installing git hooks..."
cp .githooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
echo "✅ Git hooks installed successfully!"
```

## Bypassing Hooks (Emergency Only)

If you absolutely must bypass the pre-commit hook (NOT recommended):

```bash
git commit --no-verify -m "your message"
```

**⚠️ WARNING:** Only use `--no-verify` in emergencies. Bypassing hooks can lead to broken builds in CI/CD.

## Hook Behavior

The pre-commit hook will:
1. Run `dotnet clean && dotnet restore`
2. Build with `dotnet build --no-incremental`
3. Run all tests with `dotnet test`
4. **Block the commit** if any step fails

Total execution time: ~30-60 seconds (depending on test suite size)

## Troubleshooting

### "Permission denied" error
```bash
chmod +x .git/hooks/pre-commit
```

### Hook not running
```bash
# Verify hook is installed
ls -l .git/hooks/pre-commit

# Should show: -rwxr-xr-x (executable)
```

### Tests take too long
If the pre-commit hook takes >2 minutes, consider:
- Running only unit tests: Modify hook to use `--filter "FullyQualifiedName!~IntegrationTests"`
- Optimizing slow tests (see TASK_LIST_WORKER_2_TESTING_QUALITY.md Task #24)

## Maintenance

Created by: Claude Code Task List Worker 2
Date: 2025-11-20
Based on: CLAUDE.md Pre-Commit Checklist
