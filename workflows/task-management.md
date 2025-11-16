# Task Management with TASK_LIST.md

**Purpose**: Guide for using TASK_LIST.md to track project work

**Last Updated**: 2025-11-16

---

## Overview

**TASK_LIST.md** is the project's comprehensive task roadmap containing 20+ prioritized tasks derived from analyzing all project documentation. It serves as the **single source of truth** for:
- Planned enhancements
- Known gaps
- Future work
- Sprint planning

---

## Quick Reference

| When | Action | Why |
|------|--------|-----|
| **Start of session** | Read TASK_LIST.md | Understand project priorities |
| **User asks "what to do"** | Reference task list | Show planned work |
| **Before new feature** | Check if task exists | Avoid duplicates |
| **Planning work** | Review priorities/estimates | Plan efficiently |
| **After completing** | Update task status | Keep list current |
| **Discover new work** | Add new task | Document for future |

---

## Task List Structure

```
TASK_LIST.md
├── Major Tasks (High Priority)
│   ├── Task #1-5: Critical items (Auth, Security, HTTPS)
│   └── Detailed requirements, acceptance criteria, effort estimates
│
├── Minor Tasks (Medium Priority)
│   ├── Task #6-10: Enhancements (WebSocket, Prometheus, Helm)
│   └── Implementation guidance and benefits
│
├── Low Priority Tasks
│   ├── Task #11-14: Nice-to-have features (GraphQL, ML, multi-tenancy)
│   └── Optional enhancements
│
└── Summary Statistics
    ├── Tasks by priority, status, effort
    └── Recommended sprint planning
```

---

## Task Status Indicators

| Indicator | Status | Meaning | When to Use |
|-----------|--------|---------|-------------|
| ⏳ | **Pending** | Not yet started | Default for new tasks |
| 🔄 | **In Progress** | Currently being worked on | When starting work |
| ✅ | **Completed** | Fully implemented and tested | After successful deployment |
| ⚠️ | **Blocked** | Waiting on dependency/decision | Can't proceed without resolution |

---

## Task Priority Levels

| Priority | Level | Examples | When to Use |
|----------|-------|----------|-------------|
| 🔴 | **Critical** | Security, Auth, HTTPS | Required for production |
| 🟡 | **High** | Approval workflow, Audit logs | Important for enterprise |
| 🟢 | **Medium** | WebSocket, Prometheus, Helm | Valuable enhancements |
| ⚪ | **Low** | GraphQL, ML, multi-tenancy | Nice-to-have features |

---

## How to Use TASK_LIST.md

### 1. Read Before Starting Work

**Always review the task list at the start of a session:**

```bash
# Read the task list
cat TASK_LIST.md

# Or search for specific topics
grep -i "authentication" TASK_LIST.md
grep -i "🔴 Critical" TASK_LIST.md
```

**Look for**:
- Tasks relevant to your work area
- High priority items that need attention
- Dependencies between tasks
- Estimated effort for planning

---

### 2. Reference When Planning

**Before starting a new feature:**

```bash
# Check if task already exists
grep -i "rate limiting" TASK_LIST.md
grep -i "websocket" TASK_LIST.md
```

**Use task information**:
- ✅ **Priorities**: Work on Critical (🔴) before Low (⚪)
- ✅ **Effort estimates**: Plan time accordingly (1-7 days)
- ✅ **Dependencies**: Complete prerequisites first
- ✅ **Requirements**: Follow documented acceptance criteria

**Example Planning**:
```
User asks: "Add rate limiting to the API"

1. Check TASK_LIST.md
   → Find: Task #5: API Rate Limiting (🟢 Medium, ⏳ Pending, 1 day)

2. Review requirements:
   - Configurable limits per endpoint
   - Sliding window algorithm
   - Rate limit headers

3. Check dependencies:
   - None listed

4. Start work:
   - Update status: ⏳ → 🔄
   - Commit: "docs: update TASK_LIST.md - mark rate limiting as in progress"
```

---

### 3. Update After Implementation

**When you complete a task from the list:**

```bash
# 1. Mark task as completed
# Change: ⏳ Pending → ✅ Completed

# 2. Add implementation notes
Example:
### 5. API Rate Limiting
**Status**: ✅ **Completed** (2025-11-16)
**Implementation**: Added RateLimitingMiddleware in src/Middleware/
**Tests**: 8 unit tests added, all passing
**Notes**: Used AspNetCoreRateLimit package v5.0.0

# 3. Document any issues discovered
**Known Issues**: Rate limiting doesn't work with WebSocket endpoints (see Task #12)

# 4. Add new tasks if needed
**Follow-up**: Task #15: Extend rate limiting to WebSocket endpoints
```

**Commit the update:**
```bash
git add TASK_LIST.md
git commit -m "docs: update TASK_LIST.md - mark rate limiting as completed"
```

---

### 4. Add New Tasks

**When you discover new work:**

```markdown
### N. New Task Name
**Priority:** 🟢 Medium
**Status:** ⏳ Not Implemented
**Effort:** 2-3 days
**Dependencies:** Task #5 (Rate Limiting)
**References:** README.md:238, ENHANCEMENTS.md:42

**Requirements:**
- [ ] Requirement 1
- [ ] Requirement 2
- [ ] Requirement 3

**Acceptance Criteria:**
- Feature works as described
- Tests pass (>80% coverage)
- Documentation updated

**Impact:** Medium - Improves user experience for X use case

**Notes:**
- Consider using library Y for implementation
- May require database migration
```

---

## Workflow Examples

### Example 1: Implementing Existing Task

```bash
# Scenario: User asks to implement JWT authentication (Task #1)

# Step 1: Check TASK_LIST.md
grep -A 20 "JWT Authentication" TASK_LIST.md

# Found:
# Task #1: JWT Authentication
# Priority: 🔴 Critical
# Status: ⏳ Pending
# Effort: 2 days

# Step 2: Update status to In Progress
# Edit TASK_LIST.md: ⏳ → 🔄
git add TASK_LIST.md
git commit -m "docs: update TASK_LIST.md - start JWT authentication task"

# Step 3: Implement following TDD workflow
# - Write tests
# - Implement feature
# - Refactor

# Step 4: After completion, update status
# Edit TASK_LIST.md: 🔄 → ✅
# Add implementation notes
git add TASK_LIST.md
git commit -m "docs: update TASK_LIST.md - JWT authentication completed

Implemented in:
- src/Api/Services/AuthenticationService.cs
- tests/AuthenticationServiceTests.cs

Tests: 12 passing, 0 failing
Coverage: 92%"
```

### Example 2: Discovering New Task During Work

```bash
# Scenario: While implementing rate limiting, discover need for metrics

# Step 1: Finish current task (rate limiting)
# Step 2: Add new task to TASK_LIST.md

### 15. Rate Limiting Metrics Dashboard
**Priority:** 🟢 Medium
**Status:** ⏳ Pending
**Effort:** 1 day
**Dependencies:** Task #5 (Rate Limiting - completed)

**Requirements:**
- [ ] Expose rate limit metrics via /metrics endpoint
- [ ] Track requests per endpoint
- [ ] Track rate limit violations
- [ ] Add Prometheus integration

**Acceptance Criteria:**
- Metrics endpoint returns Prometheus format
- Grafana dashboard template included
- Documentation updated

**Impact:** Medium - Enables monitoring of rate limiting effectiveness

# Step 3: Commit new task
git add TASK_LIST.md
git commit -m "docs: add Task #15 - rate limiting metrics dashboard

Discovered during implementation of Task #5.
Will be needed for production monitoring."
```

### Example 3: Blocked Task

```bash
# Scenario: Can't implement approval workflow without user management

# Update task status
### 7. Multi-Stage Approval Workflow
**Priority:** 🟡 High
**Status:** ⚠️ **Blocked**
**Effort:** 3-4 days
**Blocked By:** Task #1 (JWT Authentication) must be completed first

**Reason for Block:**
Approval workflow requires user roles and permissions,
which depend on JWT authentication system (Task #1).

**Unblock Criteria:**
- Task #1 completed
- User repository supports role queries
- Integration tests for auth pass

# Commit update
git add TASK_LIST.md
git commit -m "docs: update TASK_LIST.md - mark approval workflow as blocked

Blocked by Task #1 (JWT Authentication).
Cannot implement without user role system."
```

---

## Best Practices

### Keep It Current ✅

```bash
# ✅ DO: Update immediately after completing work
git commit -m "feat: add rate limiting
docs: update TASK_LIST.md - mark Task #5 complete"

# ❌ DON'T: Let task list get stale
# (Forgetting to update leads to duplicate work)
```

### Maintain Quality ✅

```bash
# ✅ DO: Include clear requirements
Requirements:
- [ ] Add JWT token generation
- [ ] Add token validation middleware
- [ ] Add refresh token support

# ❌ DON'T: Vague requirements
Requirements:
- [ ] Do authentication stuff
```

### Communicate Changes ✅

```bash
# ✅ DO: Descriptive commit messages
git commit -m "docs: update TASK_LIST.md - mark rate limiting as completed

- Implemented RateLimitingMiddleware
- Added 8 unit tests, all passing
- Updated README with rate limiting configuration
- Discovered need for metrics (added Task #15)"

# ❌ DON'T: Vague commit messages
git commit -m "update docs"
```

### Reference in Commits ✅

```bash
# ✅ DO: Reference task in feature commits
git commit -m "feat: implement JWT authentication (Task #1 from TASK_LIST.md)

- Add JwtTokenGenerator service
- Add authentication middleware
- Add 12 comprehensive tests
- Update README with auth setup instructions"

# ❌ DON'T: No reference to task
git commit -m "add auth"
```

---

## Integration with Other Workflows

### With TDD Workflow

```bash
# 1. Check TASK_LIST.md for task details
# 2. Follow TDD workflow:
#    - 🔴 Write failing test
#    - 🟢 Implement to pass
#    - 🔵 Refactor
# 3. Update TASK_LIST.md after completion
```

### With Pre-Commit Checklist

```bash
# Before committing:
# 1. Run pre-commit checks (build, test)
# 2. Update TASK_LIST.md if task completed
# 3. Commit code + TASK_LIST.md together
```

### With Git Workflow

```bash
# When creating feature branch:
git checkout -b claude/implement-task5-rate-limiting-sessionid

# When committing:
git commit -m "feat: implement rate limiting (Task #5)

docs: update TASK_LIST.md - mark Task #5 as completed"
```

---

## Task List Maintenance

### Monthly Review

**At the start of each month**, review TASK_LIST.md:

```bash
# 1. Check for stale "In Progress" tasks
grep "🔄" TASK_LIST.md

# 2. Update effort estimates based on reality
# If task took 3 days but estimated 1 day, update estimate

# 3. Re-prioritize based on current needs
# Move tasks up/down based on business priorities

# 4. Remove obsolete tasks
# Tasks that are no longer relevant

# 5. Add new tasks discovered
# From user feedback, bug reports, tech debt
```

### Sprint Planning

**Use TASK_LIST.md for sprint planning**:

```bash
# 1. Filter by priority and status
grep "🔴 Critical.*⏳" TASK_LIST.md   # Critical pending tasks
grep "🟡 High.*⏳" TASK_LIST.md       # High priority pending

# 2. Sum effort estimates
# Example: 3 tasks × 2 days each = 6 days of work

# 3. Check dependencies
# Ensure prerequisite tasks are completed

# 4. Assign to sprint
# Update task notes with sprint number
```

---

## Common Mistakes to Avoid

| Mistake | Problem | Solution |
|---------|---------|----------|
| Not reading task list | Duplicate work, missed priorities | Always check TASK_LIST.md first |
| Forgetting to update | Task list gets stale | Update immediately after work |
| Vague task descriptions | Unclear what needs to be done | Include specific requirements |
| No effort estimates | Can't plan sprints | Add time estimates (1-7 days) |
| Ignoring priorities | Critical work delayed | Work on 🔴 before ⚪ |
| Not documenting blockers | Work stalls without clarity | Mark as ⚠️ Blocked with reason |

---

## Task Entry Template

**Copy this template when adding new tasks**:

```markdown
### N. Task Name Here
**Priority:** 🔴 Critical / 🟡 High / 🟢 Medium / ⚪ Low
**Status:** ⏳ Pending
**Effort:** X-Y days
**Dependencies:** Task #Z (if any)
**References:** [File:line references, documentation links]

**Requirements:**
- [ ] Specific requirement 1
- [ ] Specific requirement 2
- [ ] Specific requirement 3

**Acceptance Criteria:**
- Feature works as described
- All tests pass (>80% coverage)
- Documentation updated (CLAUDE.md, README.md)
- Code reviewed and approved

**Impact:** [High/Medium/Low] - [Brief description of impact]

**Implementation Notes:**
- Consider using [library/pattern name]
- May require [database migration/API changes]
- Reference implementation: [link or file]

**Testing Notes:**
- Unit tests: [what to test]
- Integration tests: [what to test]
- Manual testing: [what to verify]
```

---

## See Also

- [TDD Workflow](tdd-workflow.md) - Test-driven development process
- [Pre-Commit Checklist](pre-commit-checklist.md) - Verify before committing
- [Git Workflow](git-workflow.md) - Git conventions and branching

**Main Project Task List**: [TASK_LIST.md](../TASK_LIST.md)

**Back to**: [Main CLAUDE.md](../CLAUDE.md#working-with-task_listmd)
