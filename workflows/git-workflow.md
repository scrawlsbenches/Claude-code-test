# Git Workflow

**Purpose**: Git conventions and workflows for Claude-code-test repository

**Last Updated**: 2025-11-16

---

## Quick Reference

| Task | Command | Notes |
|------|---------|-------|
| Create branch | `git checkout -b claude/feature-sessionid` | MUST start with `claude/` |
| Stage changes | `git add .` or `git add <file>` | Review with `git status` |
| Commit | `git commit -m "type: message"` | Use conventional commits |
| Push first time | `git push -u origin claude/branch-name` | `-u` sets upstream |
| Push updates | `git push` | After upstream is set |
| Check status | `git status` | See what's staged/unstaged |
| View diff | `git diff` | Unstaged changes |
| View staged | `git diff --cached` | Staged changes |

---

## Branch Strategy

### Main Branch

- **Name**: `main` (or `master`)
- **Protection**: Never force push
- **Direct commits**: Not allowed (use pull requests)

### Feature Branches

**Naming Convention**: `claude/[descriptive-name]-[session-id]`

**Examples**:
```bash
# ✅ CORRECT
claude/add-rate-limiting-01XYZ123ABC
claude/fix-auth-bug-01ABC789DEF
claude/refactor-deployment-01GHI456JKL

# ❌ WRONG
feature/add-rate-limiting    # Missing claude/ prefix and session ID
claude/add-rate-limiting      # Missing session ID
add-rate-limiting-01XYZ123    # Missing claude/ prefix
```

**Why This Convention?**:
- `claude/` prefix: Required for push authentication
- Session ID suffix: Tracks which AI session created the branch
- Descriptive name: Explains what the branch does

**Creating a Feature Branch**:
```bash
# From main branch
git checkout main
git pull origin main

# Create new feature branch
git checkout -b claude/your-feature-name-sessionid

# Verify you're on the new branch
git branch --show-current
```

---

## Commit Guidelines

### Conventional Commit Format

**Format**: `type: description`

**Types**:
- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation changes
- `refactor:` - Code refactoring (no behavior change)
- `test:` - Test additions/modifications
- `chore:` - Maintenance tasks (dependencies, scripts)
- `perf:` - Performance improvements
- `style:` - Code style changes (formatting, no logic change)
- `ci:` - CI/CD pipeline changes

**Examples**:
```bash
# ✅ GOOD commit messages
git commit -m "feat: add JWT authentication to API endpoints"
git commit -m "fix: resolve null reference exception in deployment tracker"
git commit -m "docs: update CLAUDE.md with new testing requirements"
git commit -m "refactor: extract deployment validation into separate service"
git commit -m "test: add integration tests for deployment pipeline"

# ❌ BAD commit messages
git commit -m "updated files"           # Too vague
git commit -m "fix stuff"               # Not descriptive
git commit -m "WIP"                     # Work in progress, not ready
git commit -m "asdf"                    # Meaningless
```

### Multi-Line Commit Messages

For complex changes, use extended description:

```bash
git commit -m "feat: implement rate limiting middleware

- Add configurable rate limits per endpoint
- Implement sliding window algorithm
- Add rate limit headers to responses
- Include comprehensive unit tests

Closes #123"
```

Or use editor for multi-line:
```bash
# Opens default editor
git commit

# In editor:
feat: implement rate limiting middleware

- Add configurable rate limits per endpoint
- Implement sliding window algorithm
- Add rate limit headers to responses
- Include comprehensive unit tests

Closes #123
```

---

## Common Workflow Scenarios

### Scenario 1: New Feature Development

```bash
# 1. Create feature branch
git checkout -b claude/add-feature-01ABC123

# 2. Make changes (follow TDD workflow)
# Write tests, implement code, refactor

# 3. Stage changes
git add .

# 4. Review what will be committed
git status
git diff --cached

# 5. Run pre-commit checklist (CRITICAL!)
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test

# 6. Commit if all checks pass
git commit -m "feat: add new feature description"

# 7. Push to remote
git push -u origin claude/add-feature-01ABC123

# 8. Create pull request (if needed)
# Visit: https://github.com/scrawlsbenches/Claude-code-test/pull/new/claude/add-feature-01ABC123
```

### Scenario 2: Bug Fix

```bash
# 1. Create bug fix branch
git checkout -b claude/fix-bug-description-01DEF456

# 2. Write test that reproduces bug (TDD Red)
# 3. Fix the bug (TDD Green)
# 4. Refactor if needed (TDD Refactor)

# 5. Stage and commit
git add .
git commit -m "fix: resolve bug description

- Add test that reproduces the issue
- Implement fix in ComponentName
- Verify all tests pass"

# 6. Push
git push -u origin claude/fix-bug-description-01DEF456
```

### Scenario 3: Documentation Update

```bash
# 1. Create docs branch
git checkout -b claude/update-docs-01GHI789

# 2. Update documentation files

# 3. Stage and commit
git add CLAUDE.md README.md
git commit -m "docs: update setup instructions and testing guide"

# 4. Push
git push -u origin claude/update-docs-01GHI789
```

### Scenario 4: Amending Last Commit (Use Carefully)

```bash
# If you forgot to include a file in the last commit
git add forgotten-file.cs
git commit --amend --no-edit

# If you want to change the commit message
git commit --amend -m "New commit message"

# ⚠️ WARNING: Only amend commits that haven't been pushed
# ⚠️ If already pushed, create a new commit instead
```

---

## Git Push Requirements

### Standard Push

```bash
# First time pushing a branch (sets upstream)
git push -u origin claude/branch-name-sessionid

# Subsequent pushes (upstream already set)
git push
```

### Critical Requirements

1. **Branch naming**: MUST start with `claude/` and end with session ID
2. **Authentication**: Branch name format required for push authentication
3. **Network retry**: If network error, retry up to 4 times with exponential backoff
4. **Never force push** to main/master without explicit permission

### Retry Logic for Network Errors

```bash
# Automatic retry with exponential backoff
attempt=1
max_attempts=4
delay=2

while [ $attempt -le $max_attempts ]; do
    if git push -u origin claude/branch-name; then
        echo "Push successful"
        break
    else
        if [ $attempt -lt $max_attempts ]; then
            echo "Push failed, retrying in ${delay}s (attempt $attempt/$max_attempts)"
            sleep $delay
            delay=$((delay * 2))  # Exponential backoff: 2s, 4s, 8s, 16s
            attempt=$((attempt + 1))
        else
            echo "Push failed after $max_attempts attempts"
            exit 1
        fi
    fi
done
```

---

## Git Best Practices

### Before Committing

1. ✅ **Run pre-commit checklist** (see [Pre-Commit Checklist](pre-commit-checklist.md))
2. ✅ **Review changes**: `git diff --cached`
3. ✅ **Verify staged files**: `git status`
4. ✅ **Check for secrets**: Don't commit `.env`, `appsettings.Development.json`, API keys
5. ✅ **Test changes**: Ensure build and tests pass

### During Development

```bash
# Check current status frequently
git status

# View unstaged changes
git diff

# View staged changes
git diff --cached

# View commit history
git log --oneline -10

# View changes in specific file
git diff path/to/file.cs
```

### Staging Files Selectively

```bash
# Stage all changes
git add .

# Stage specific file
git add path/to/file.cs

# Stage multiple files
git add file1.cs file2.cs file3.cs

# Stage all files in directory
git add src/MyProject/

# Interactive staging (patch mode)
git add -p

# Unstage file
git restore --staged path/to/file.cs

# Discard unstaged changes
git restore path/to/file.cs
```

---

## Handling Conflicts

### When Pulling Changes

```bash
# Pull latest from main
git checkout main
git pull origin main

# Merge main into feature branch
git checkout claude/your-feature-branch
git merge main

# If conflicts occur:
# 1. Git will mark conflicted files
# 2. Open conflicted files and resolve markers:
#    <<<<<<< HEAD
#    Your changes
#    =======
#    Changes from main
#    >>>>>>> main
# 3. Remove conflict markers, keep desired code
# 4. Stage resolved files
git add resolved-file.cs

# 5. Complete merge
git commit -m "merge: resolve conflicts with main"
```

---

## Undoing Changes

### Undo Last Commit (Keep Changes)

```bash
# Undo commit, keep changes staged
git reset --soft HEAD~1

# Undo commit, keep changes unstaged
git reset HEAD~1

# Undo commit, discard changes (DANGEROUS!)
git reset --hard HEAD~1
```

### Discard Local Changes

```bash
# Discard changes to specific file
git restore path/to/file.cs

# Discard all unstaged changes
git restore .

# Discard all changes including staged (DANGEROUS!)
git reset --hard HEAD
```

### Revert a Commit (Create New Commit)

```bash
# Safer than reset - creates new commit that undoes changes
git revert <commit-hash>

# Revert last commit
git revert HEAD
```

---

## Working with Remote

### Fetching vs Pulling

```bash
# Fetch: Downloads changes without merging
git fetch origin

# Pull: Downloads and merges changes
git pull origin main

# Pull with rebase (cleaner history)
git pull --rebase origin main
```

### Checking Remote Status

```bash
# View remote branches
git branch -r

# View all branches (local + remote)
git branch -a

# Show remote URL
git remote -v

# Check if local is behind remote
git fetch origin
git status
```

---

## CI/CD Integration

### GitHub Actions Workflow

After pushing, GitHub Actions automatically:
1. Builds the solution
2. Runs all tests
3. Validates code quality
4. Reports results

**Monitor builds**:
- Visit: https://github.com/scrawlsbenches/Claude-code-test/actions
- Check status of your branch
- Review logs if build fails

### If CI/CD Fails

```bash
# 1. Check GitHub Actions logs for error details
# 2. Fix issues locally
# 3. Test locally before pushing
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test

# 4. Commit fix
git add .
git commit -m "fix: resolve CI/CD build failure - <description>"

# 5. Push fix
git push
```

---

## Common Git Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| "Permission denied" | Branch name format wrong | Ensure branch starts with `claude/` and ends with session ID |
| "Network error" | Transient connection issue | Retry push with exponential backoff (2s, 4s, 8s, 16s) |
| "Merge conflict" | Concurrent changes | Resolve conflicts manually, test, then commit |
| "Detached HEAD" | Checked out commit instead of branch | `git checkout main` or `git checkout -b new-branch` |
| "Nothing to commit" | No changes staged | Use `git add` to stage files first |
| "Untracked files" | New files not staged | `git add .` or `git add <file>` |

---

## Git Cheat Sheet

```bash
# Status and Info
git status                          # Show working tree status
git log --oneline -10              # Show recent commits
git branch --show-current          # Show current branch name
git diff                           # Show unstaged changes
git diff --cached                  # Show staged changes

# Branching
git branch                         # List local branches
git checkout -b branch-name        # Create and switch to branch
git checkout branch-name           # Switch to existing branch
git branch -d branch-name          # Delete local branch

# Staging
git add .                          # Stage all changes
git add file.cs                    # Stage specific file
git restore --staged file.cs       # Unstage file
git restore file.cs                # Discard unstaged changes

# Committing
git commit -m "message"            # Commit with message
git commit --amend                 # Amend last commit
git commit --amend --no-edit       # Amend without changing message

# Remote Operations
git fetch origin                   # Fetch remote changes
git pull origin main               # Pull and merge from main
git push -u origin branch          # Push and set upstream
git push                           # Push to upstream

# Undoing
git reset HEAD~1                   # Undo last commit, keep changes
git reset --hard HEAD~1            # Undo last commit, discard changes
git revert HEAD                    # Create new commit that undoes last commit
git restore file.cs                # Discard changes to file
```

---

## See Also

- [Pre-Commit Checklist](pre-commit-checklist.md) - CRITICAL steps before every commit
- [TDD Workflow](tdd-workflow.md) - Test-driven development process
- [Task Management](task-management.md) - Using TASK_LIST.md

**Back to**: [Main CLAUDE.md](../CLAUDE.md#git-workflow)
