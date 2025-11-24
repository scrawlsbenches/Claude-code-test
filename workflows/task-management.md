# Task Management with task-manager.sh

**Purpose**: Guide for using task-manager.sh to manage TASK_LIST.md

**Last Updated**: 2025-11-24

---

## Overview

**TASK_LIST.md** is the project's comprehensive task roadmap. Use **task-manager.sh** to interact with it - don't manually parse the file.

**Key principle:** Always use `./task-manager.sh` commands instead of manual `grep`/`cat` operations.

---

## Quick Reference

| When | Command | Why |
|------|---------|-----|
| **Start of session** | `./task-manager.sh summary` | Quick overview + next task |
| **Check progress** | `./task-manager.sh stats` | See completion rate |
| **"What should I work on?"** | `./task-manager.sh next` | Get highest priority pending task |
| **Find specific work** | `./task-manager.sh list pending` | See all available tasks |
| **Planning work** | `./task-manager.sh show <id>` | Read task requirements |
| **Starting work** | `./task-manager.sh start <id>` | Mark as in-progress |
| **After completing** | `./task-manager.sh complete <id>` | Mark as completed |
| **Before pushing** | `./task-manager.sh pre-push` | Interactive documentation |

---

## All Commands

### Status & Overview

```bash
./task-manager.sh stats              # Full statistics with progress bar
./task-manager.sh summary            # Quick overview + next recommended task
./task-manager.sh next               # Show next task to work on (by priority)
```

### Listing Tasks

```bash
./task-manager.sh list all           # All task titles
./task-manager.sh list pending       # Tasks not yet started
./task-manager.sh list completed     # Finished tasks
./task-manager.sh list progress      # Tasks in progress
./task-manager.sh list blocked       # Blocked tasks
./task-manager.sh list critical      # Critical priority only
./task-manager.sh list high          # High priority only
./task-manager.sh list medium        # Medium priority only
./task-manager.sh list low           # Low priority only
```

### Finding & Viewing Tasks

```bash
./task-manager.sh search "MinIO"     # Search by keyword
./task-manager.sh show 25            # Show detailed task info
```

### Updating Task Status

```bash
./task-manager.sh start 25           # Mark as in-progress
./task-manager.sh start 5 6 7        # Bulk: mark multiple as in-progress
./task-manager.sh complete 25        # Mark as completed (with notes prompt)
./task-manager.sh complete 1 2 3     # Bulk: mark multiple as completed
./task-manager.sh update 25          # Interactive status change
./task-manager.sh reject 14          # Mark as won't-do
```

### Adding & Pre-Push

```bash
./task-manager.sh add                # Add new task interactively
./task-manager.sh pre-push           # Interactive pre-push documentation
```

---

## Status Categories

The script recognizes both emoji and text-based statuses:

| Status | Emoji | Text Patterns |
|--------|-------|---------------|
| Pending | ⏳ | "Not Implemented", "Not Created", "Pending" |
| In Progress | 🔄 | "In Progress", "WIP" |
| Completed | ✅ | "Completed", "Complete", "COMPLETED" |
| Blocked | ⚠️ | "Blocked", "On Hold" |
| Rejected | ❌ | "Rejected", "Won't Do", "Cancelled" |

---

## Priority Levels

| Priority | Emoji | Text | When to Use |
|----------|-------|------|-------------|
| Critical | 🔴 | "Critical" | Required for production |
| High | 🟡 | "High" | Important for enterprise |
| Medium | 🟢 | "Medium" | Valuable enhancements |
| Low | ⚪ | "Low" | Nice-to-have features |

---

## Workflow Examples

### Example 1: Starting a Session

```bash
# Quick overview of where things stand
./task-manager.sh summary

# Output:
# === Session Summary ===
#   Progress: 21/28 tasks (75%)
#   Pending:  5 | In Progress: 0
#
# === Next Recommended Task ===
#   #25: MinIO Object Storage Implementation
#   **Priority:** 🟢 Medium
#   To start: ./task-manager.sh start 25
```

### Example 2: Implementing a Task

```bash
# 1. Find the next task to work on
./task-manager.sh next

# 2. Review task details
./task-manager.sh show 25

# 3. Mark it as in-progress
./task-manager.sh start 25

# 4. Implement the feature (TDD workflow)
# ... write tests, implement, refactor ...

# 5. Mark as completed
./task-manager.sh complete 25
# (Enter implementation notes when prompted)

# 6. Commit the updated TASK_LIST.md
git add TASK_LIST.md
git commit -m "docs: mark Task #25 as completed"
```

### Example 3: Bulk Operations

```bash
# Mark multiple tasks as in-progress
./task-manager.sh start 5 6 7

# Mark multiple tasks as completed (no interactive prompts)
./task-manager.sh complete 1 2 3
```

### Example 4: Finding Specific Work

```bash
# Search for authentication-related tasks
./task-manager.sh search "authentication"

# List all critical priority tasks
./task-manager.sh list critical

# List all pending tasks
./task-manager.sh list pending
```

### Example 5: Pre-Push Documentation

```bash
# Before pushing, document completed work
./task-manager.sh pre-push

# This will:
# - Show tasks in progress
# - Ask which tasks you completed
# - Prompt for implementation notes
# - Update TASK_LIST.md automatically
# - Show summary
```

---

## Best Practices

### Always Use the Script ✅

```bash
# ✅ CORRECT: Use task-manager.sh
./task-manager.sh search "MinIO"
./task-manager.sh show 25
./task-manager.sh complete 25

# ❌ AVOID: Manual grep/cat operations
grep -i "MinIO" TASK_LIST.md
cat TASK_LIST.md | grep "Status"
```

### Start Sessions with Summary ✅

```bash
# ✅ CORRECT: Quick context at session start
./task-manager.sh summary

# This gives you:
# - Current progress
# - What's pending
# - Next recommended task
```

### Mark Tasks In-Progress ✅

```bash
# ✅ CORRECT: Mark task before starting work
./task-manager.sh start 25
# ... do the work ...
./task-manager.sh complete 25

# ❌ AVOID: Only marking complete at the end
./task-manager.sh complete 25  # Others don't know you're working on it
```

### Use Bulk Operations for Efficiency ✅

```bash
# ✅ CORRECT: Bulk operations when appropriate
./task-manager.sh complete 1 2 3

# ❌ AVOID: One at a time when not needed
./task-manager.sh complete 1
./task-manager.sh complete 2
./task-manager.sh complete 3
```

---

## Integration with Other Workflows

### With TDD Workflow

```bash
# 1. Find task
./task-manager.sh next

# 2. Start task
./task-manager.sh start 25

# 3. Follow TDD workflow:
#    - 🔴 Write failing test
#    - 🟢 Implement to pass
#    - 🔵 Refactor

# 4. Complete task
./task-manager.sh complete 25
```

### With Pre-Commit Checklist

```bash
# Before committing:
# 1. Run pre-commit checks (build, test)
dotnet build && dotnet test

# 2. Update task status if completed
./task-manager.sh complete 25

# 3. Commit code + TASK_LIST.md together
git add . && git commit -m "feat: implement feature (Task #25)"
```

### With Git Workflow

```bash
# When creating feature branch:
git checkout -b claude/implement-task25-minio-sessionid

# After completing work:
./task-manager.sh complete 25
git add TASK_LIST.md
git commit -m "docs: mark Task #25 as completed"
```

---

## See Also

- [TDD Workflow](tdd-workflow.md) - Test-driven development process
- [Pre-Commit Checklist](pre-commit-checklist.md) - Verify before committing
- [Git Workflow](git-workflow.md) - Git conventions and branching

**Main Project Task List**: [TASK_LIST.md](../TASK_LIST.md)

**Back to**: [Main CLAUDE.md](../CLAUDE.md#task-management-with-task-managersh)
