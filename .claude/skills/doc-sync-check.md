# Documentation Sync Checker Skill

**Description**: Validates that documentation is synchronized with code changes, preventing stale documentation that misleads developers.

**When to use**:
- Before EVERY commit (especially if code changed)
- Monthly documentation audit
- Before major releases
- When updating documentation

## Instructions

This skill implements the comprehensive documentation validation process from CLAUDE.md to ensure documentation stays current and accurate.

---

## Phase 1: Identify What Changed

### Step 1.1: Check Git Changes

```bash
# Show all changed files
git diff --staged --name-only

# Categorize changes
echo "=== Code Changes ==="
git diff --staged --name-only | grep -E "\.cs$|\.csproj$"

echo "=== Docker Changes ==="
git diff --staged --name-only | grep -E "Dockerfile|docker-compose"

echo "=== Documentation Changes ==="
git diff --staged --name-only | grep -E "\.md$"

echo "=== Test Changes ==="
git diff --staged --name-only | grep -E "Tests/.*\.cs$"
```

### Step 1.2: Determine Documentation Update Requirements

Based on file types changed, determine which docs need updating:

| Change Type | Docs to Update |
|-------------|----------------|
| Public API changed | XML docs, README.md, CLAUDE.md |
| Package added/removed | CLAUDE.md (Technology Stack) |
| Build/test process changed | CLAUDE.md (Dev Setup, Pre-Commit) |
| Test count changed | CLAUDE.md (multiple locations), README.md |
| Project structure changed | CLAUDE.md (Project Structure) |
| Task completed | TASK_LIST.md, ENHANCEMENTS.md |
| Docker config changed | CLAUDE.md, README.md, docker-compose.yml |
| Environment variables changed | CLAUDE.md, README.md, .env.example |

---

## Phase 2: Validate Specific Documentation

### Check 1: Test Count Synchronization

**Run if**: Tests were added, removed, or modified

```bash
# Get actual test count
ACTUAL_TESTS=$(dotnet test --verbosity quiet 2>&1 | grep -oP "Passed:\s+\K\d+")
ACTUAL_SKIPPED=$(dotnet test --verbosity quiet 2>&1 | grep -oP "Skipped:\s+\K\d+")
ACTUAL_TOTAL=$(dotnet test --verbosity quiet 2>&1 | grep -oP "Total:\s+\K\d+")

echo "Actual test counts: Passed=$ACTUAL_TESTS, Skipped=$ACTUAL_SKIPPED, Total=$ACTUAL_TOTAL"

# Check documented test counts
echo "=== Documented Test Counts in CLAUDE.md ==="
grep -n "Passed.*tests\|Total.*tests\|Build Status.*tests" CLAUDE.md | head -10
```

**Locations to update in CLAUDE.md**:
- Line 16: Build Status
- Line 115: Project Metrics table (Quick Reference)
- Line 388: First Time Build expected output
- Line 435: Run All Tests expected output
- Line 473: Critical Path Tests expected output

**Action**:
```bash
# If counts don't match, update CLAUDE.md
# Search and replace old count with new count in all locations
```

### Check 2: Package Version Synchronization

**Run if**: .csproj files changed (packages added/removed/updated)

```bash
# List all packages with versions
echo "=== Actual Packages ===" dotnet list package | grep ">" | awk '{print $2, $4}'

# Check documented packages in CLAUDE.md
echo "=== Documented Packages ==="
grep -A 100 "### Core Infrastructure" CLAUDE.md | grep -E "^\*\*|^- \*\*" | head -20
```

**Locations to update**:
- CLAUDE.md: Technology Stack section (lines 65-100)

**Action**:
- Verify all packages are documented with correct versions
- Add new packages to appropriate section (Primary Framework, Core Infrastructure, API & Documentation, etc.)
- Remove obsolete packages

### Check 3: Project Structure Synchronization

**Run if**: Files/folders were added, removed, or moved

```bash
# Check actual structure
echo "=== Actual Structure ==="
tree -L 2 -d -I 'bin|obj|TestResults|.git' .

# Compare with documented structure in CLAUDE.md (lines 20-50)
echo "=== Documented Structure ==="
sed -n '20,50p' CLAUDE.md
```

**Action**:
- Update Project Structure ASCII tree in CLAUDE.md if structure changed
- Update Key Components section if projects added/removed

### Check 4: API Signature Changes

**Run if**: Public APIs changed (interfaces, public methods, models)

```bash
# Find public API changes
git diff --staged | grep -E "^\+.*public (class|interface|enum|async Task|[A-Z].*\()"

# Check if XML documentation exists
echo "=== Checking XML Documentation ==="
git diff --staged | grep -A 5 "^\+.*public " | grep -B 2 "///"
```

**Action**:
- Ensure all public APIs have XML documentation (`///`)
- Update README.md if user-facing API changed
- Update architecture docs if interfaces changed

### Check 5: Build/Test Process Changes

**Run if**: Build commands, test commands, or workflow changed

```bash
# Check if Pre-Commit Checklist needs updating
echo "=== Pre-Commit Steps in CLAUDE.md ==="
grep -A 3 "dotnet clean\|dotnet restore\|dotnet build\|dotnet test" CLAUDE.md | head -20

# Verify commands still work
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test --verbosity quiet
```

**Action**:
- Update Pre-Commit Checklist if commands changed
- Update Development Environment Setup if prerequisites changed
- Update CI/CD docs if workflow changed

### Check 6: Docker Configuration Synchronization

**Run if**: Dockerfile or docker-compose.yml changed

```bash
# Check Docker-related documentation
echo "=== Docker Documentation Checks ==="

# 1. Verify base image versions match
grep "FROM" Dockerfile
grep -A 5 "Docker" README.md

# 2. Check port mappings
grep "ports:" docker-compose.yml
grep -E "localhost:[0-9]{4}" CLAUDE.md README.md

# 3. Check environment variables
grep -E "environment:|ENV" docker-compose.yml Dockerfile
grep -A 10 "Environment Variables" README.md || echo "No environment variable docs found"

# 4. Check service names
grep "services:" docker-compose.yml -A 20 | grep -E "^\s+[a-z-]+:"
grep -E "docker-compose|service" README.md
```

**Locations to update**:
- CLAUDE.md: Docker Development and Maintenance section
- CLAUDE.md: Running with Docker Compose section
- README.md: Docker Quickstart section
- README.md: Environment Variables section (if applicable)
- docker-compose.yml: Inline comments

**Action**:
- Update port numbers if changed
- Update service URLs if changed
- Update environment variable documentation
- Add inline comments to docker-compose.yml for new configuration
- Update base image versions in documentation

---

## Phase 3: Validate Documentation Freshness

### Check 7: Last Updated Dates

```bash
# Check "Last Updated" dates in all documentation
echo "=== Documentation Last Updated Dates ==="
grep -n "Last Updated:" *.md | while read line; do
    file=$(echo "$line" | cut -d: -f1)
    date=$(echo "$line" | grep -oP "\d{4}-\d{2}-\d{2}")
    days_old=$(( ($(date +%s) - $(date -d "$date" +%s 2>/dev/null || date -j -f "%Y-%m-%d" "$date" +%s 2>/dev/null || echo 0)) / 86400 ))
    echo "$file: $date ($days_old days old)"
done
```

**Action**:
- Update "Last Updated" date if file was modified
- Flag files >90 days old for review

### Check 8: Changelog Entries

```bash
# Check if Changelog was updated
echo "=== Recent Changelog Entries ==="
grep -A 5 "^### $(date +%Y)" CLAUDE.md | head -20
```

**Action**:
- Add changelog entry for significant changes
- Include date and description of changes

### Check 9: Broken File References

```bash
# Check for references to files that don't exist
echo "=== Checking File References ==="
grep -oP "src/[^\s)]+" *.md | sort -u | while read ref; do
    if [ ! -e "$ref" ]; then
        echo "❌ Broken reference: $ref"
    fi
done

grep -oP "tests/[^\s)]+" *.md | sort -u | while read ref; do
    if [ ! -e "$ref" ]; then
        echo "❌ Broken reference: $ref"
    fi
done
```

**Action**:
- Update or remove broken file references
- Use relative references when possible

---

## Phase 4: Validate Code Examples

### Check 10: Code Examples Compile

```bash
# Extract C# code blocks from markdown (manual review for now)
echo "=== Code Examples in Documentation ==="
grep -n '```csharp' *.md | cut -d: -f1-2

echo ""
echo "⚠️  Manual check required: Verify code examples in docs still compile"
echo "TODO: Automated code example extraction and compilation"
```

**Action**:
- Manually verify code examples are current
- Update examples if APIs changed

### Check 11: Command Examples Work

```bash
# Test documented commands actually work
echo "=== Testing Documented Commands ==="

# Common commands from Quick Reference
echo "Testing: dotnet --version"
dotnet --version

echo "Testing: dotnet restore"
dotnet restore > /dev/null 2>&1 && echo "✅ Success" || echo "❌ Failed"

echo "Testing: dotnet build"
dotnet build --no-incremental > /dev/null 2>&1 && echo "✅ Success" || echo "❌ Failed"

echo "Testing: dotnet test"
dotnet test --verbosity quiet > /dev/null 2>&1 && echo "✅ Success" || echo "❌ Failed"
```

**Action**:
- Update commands if they fail or produce different output
- Update expected output in documentation

---

## Phase 5: Task List Synchronization

### Check 12: TASK_LIST.md Updates

**Run if**: Feature completed or new work identified

```bash
# Check for completed tasks that need status update
echo "=== Checking TASK_LIST.md ==="
grep -n "Status.*Pending\|Status.*In Progress" TASK_LIST.md | head -10
```

**Action**:
- Update task status from ⏳ to ✅ if completed
- Add completion notes
- Add new tasks discovered during implementation
- Update ENHANCEMENTS.md with completed features

---

## Phase 6: Generate Report

### Summary Report

```bash
echo "===================================="
echo "DOCUMENTATION SYNC CHECK REPORT"
echo "===================================="
echo ""
echo "Date: $(date +%Y-%m-%d)"
echo ""

# Code changes
CODE_CHANGES=$(git diff --staged --name-only | grep -E "\.cs$|\.csproj$" | wc -l)
echo "Code files changed: $CODE_CHANGES"

# Doc changes
DOC_CHANGES=$(git diff --staged --name-only | grep -E "\.md$" | wc -l)
echo "Documentation files changed: $DOC_CHANGES"

# Test counts
ACTUAL_TESTS=$(dotnet test --verbosity quiet 2>&1 | grep -oP "Passed:\s+\K\d+" || echo "N/A")
echo "Current test count: $ACTUAL_TESTS"

echo ""
echo "=== Documentation Update Requirements ==="

if [ $CODE_CHANGES -gt 0 ]; then
    echo "⚠️  Code changed - verify XML documentation"
    echo "⚠️  Check if README.md needs updating"
fi

# Check if packages changed
if git diff --staged --name-only | grep -q "\.csproj$"; then
    echo "⚠️  .csproj changed - update CLAUDE.md Technology Stack"
fi

# Check if tests changed
if git diff --staged --name-only | grep -q "Tests/.*\.cs$"; then
    echo "⚠️  Tests changed - verify test counts in CLAUDE.md"
fi

# Check if Docker changed
if git diff --staged --name-only | grep -qE "Dockerfile|docker-compose"; then
    echo "⚠️  Docker config changed - update Docker documentation"
fi

echo ""
echo "=== Next Steps ==="
echo "1. Review warnings above"
echo "2. Update required documentation"
echo "3. Update 'Last Updated' dates"
echo "4. Add changelog entries"
echo "5. Run pre-commit checklist"
echo ""
```

---

## Quick Checklist

Before committing, verify:

- ✅ Test counts match in all CLAUDE.md locations
- ✅ Package versions match in Technology Stack section
- ✅ Project structure matches actual structure
- ✅ Public APIs have XML documentation
- ✅ README.md updated if user-facing changes
- ✅ Docker docs updated if Dockerfile/compose changed
- ✅ "Last Updated" dates are current
- ✅ Changelog entries added for significant changes
- ✅ File references are valid (not broken)
- ✅ Code examples still compile (manual check)
- ✅ Command examples produce expected output
- ✅ TASK_LIST.md updated if tasks completed

---

## Common Documentation Debt Patterns

### ❌ WRONG: Outdated Test Counts
```markdown
❌ Build Status: ✅ Passing (65/65 tests)
   (Actual: 582 tests)
```

### ✅ CORRECT: Current Test Counts
```markdown
✅ Build Status: ✅ Passing (582 tests: 568 passing, 14 skipped)
   (Updated: 2025-11-19)
```

### ❌ WRONG: Missing Package Documentation
```markdown
❌ No mention of BCrypt.Net-Next package
   (Package exists in .csproj but not in docs)
```

### ✅ CORRECT: All Packages Documented
```markdown
✅ **BCrypt.Net-Next 4.0.3** - Password hashing
```

### ❌ WRONG: Stale "Last Updated" Date
```markdown
❌ Last Updated: 2025-01-15
   (Today is 2025-11-19, 10 months old!)
```

### ✅ CORRECT: Current Date
```markdown
✅ Last Updated: 2025-11-19
```

---

## Automation

Run this skill before committing:
```
/doc-sync-check
```

Or integrate into pre-commit workflow:
```bash
# After code changes, before commit
/doc-sync-check
# Review report
# Update documentation as needed
/precommit-check
```

---

## Success Criteria

Documentation is in sync when:

- ✅ All test counts match actual counts
- ✅ All package versions documented correctly
- ✅ Project structure matches actual structure
- ✅ No broken file references
- ✅ "Last Updated" dates are current (within 30 days for active files)
- ✅ Changelog has entries for recent changes
- ✅ Code examples compile and work
- ✅ Command examples produce expected output
- ✅ Task list reflects current status
- ✅ Docker documentation matches configuration

---

## Performance Notes

- Full documentation sync check: ~2-3 minutes
- Quick check (test counts only): ~30 seconds
- Should be run before every commit with code changes

---

## Reference

Based on:
- CLAUDE.md: "Avoiding Stale Documentation" section (500+ lines)
- CLAUDE.md: "Documentation Update Triggers" (8 triggers)
- CLAUDE.md: "Monthly Documentation Audit" process

**Key Principle**:
> "Stale documentation is worse than no documentation. Outdated docs mislead developers, waste time, and cause bugs."
