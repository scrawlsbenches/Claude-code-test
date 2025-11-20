# Git Hooks

This directory contains git hooks that enforce code quality standards before commits.

## Available Hooks

### pre-commit

The pre-commit hook enforces the following checks before allowing a commit:
1. Clean build artifacts (`dotnet clean`)
2. Restore NuGet packages (`dotnet restore`)
3. Build solution with no incremental builds (`dotnet build --no-incremental`)
4. Run all tests (`dotnet test`)

If any step fails, the commit will be blocked.

## Installation

### Option 1: Copy hook manually (Recommended)

```bash
# From the repository root
cp scripts/hooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

### Option 2: Use git config (Git 2.9+)

```bash
# Set hooks directory for this repository
git config core.hooksPath scripts/hooks

# Ensure hooks are executable
chmod +x scripts/hooks/pre-commit
```

### Option 3: Automated installation script

Run the installation script:

```bash
./scripts/install-hooks.sh
```

## Verifying Installation

Test the hook manually:

```bash
.git/hooks/pre-commit
```

Expected output:
```
🔍 Running pre-commit checks...

1️⃣  Cleaning build artifacts...
✅ Clean completed

2️⃣  Restoring NuGet packages...
✅ Restore completed

3️⃣  Building solution (non-incremental)...
✅ Build succeeded

4️⃣  Running all tests...
✅ All tests passed

✨ Pre-commit checks passed! Proceeding with commit...
```

## Bypassing the Hook (Emergency Only)

In rare cases where you need to bypass the hook:

```bash
git commit --no-verify -m "your message"
```

**⚠️ WARNING**: Only use `--no-verify` in emergencies. Bypassing the hook can lead to broken builds in CI/CD.

## Troubleshooting

### Hook not running

1. Verify hook is executable:
   ```bash
   ls -l .git/hooks/pre-commit
   # Should show: -rwxr-xr-x
   ```

2. If not executable, make it executable:
   ```bash
   chmod +x .git/hooks/pre-commit
   ```

### Hook fails with "dotnet: command not found"

Ensure .NET SDK 8.0 is installed:

```bash
dotnet --version
# Expected: 8.0.x or later
```

See [CLAUDE.md Development Environment Setup](../../CLAUDE.md#development-environment-setup) for installation instructions.

### Tests fail during hook

Fix the failing tests before committing. Run tests manually to debug:

```bash
dotnet test --verbosity normal
```

## Updating Hooks

When hooks are updated in `scripts/hooks/`, reinstall them:

```bash
# If using Option 1 (copy):
cp scripts/hooks/pre-commit .git/hooks/pre-commit

# If using Option 2 (core.hooksPath):
# No action needed, hooks are already linked

# If using Option 3 (install script):
./scripts/install-hooks.sh
```

## CI/CD Integration

The same checks enforced by this hook are also run in GitHub Actions CI/CD pipeline. The hook ensures you catch issues locally before pushing.
