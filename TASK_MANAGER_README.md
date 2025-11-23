# Task Manager CLI

A bash script for managing `TASK_LIST.md` automatically, helping document work before pushes.

## Features

- ✅ Add new tasks interactively
- ✅ List tasks by status (pending, in progress, completed, blocked, rejected)
- ✅ List tasks by priority (critical, high, medium, low)
- ✅ Update task status
- ✅ Mark tasks as completed with implementation notes
- ✅ Mark tasks as rejected/won't do with rejection reason
- ✅ Search tasks by keyword
- ✅ View task statistics with visual progress bar
- ✅ Pre-push wizard for documenting work
- ✅ Show detailed task information
- ✅ Color-coded output for better readability

## Quick Start

```bash
# Make the script executable (if not already)
chmod +x task-manager.sh

# Show help
./task-manager.sh help

# View task statistics
./task-manager.sh stats

# List pending tasks
./task-manager.sh list pending

# Run pre-push documentation wizard
./task-manager.sh pre-push
```

## Commands

### Add a New Task

```bash
./task-manager.sh add
```

Interactively prompts for:
- Task name
- Priority (Critical, High, Medium, Low)
- Effort estimate
- Dependencies
- Description

Example:
```bash
$ ./task-manager.sh add
=== Add New Task ===
Task name: Implement rate limiting middleware
Priority:
  1) 🔴 Critical
  2) 🟡 High
  3) 🟢 Medium
  4) ⚪ Low
Select (1-4): 3
Effort estimate (e.g., 1-2 days): 1 day
Dependencies (e.g., Task #5, or leave empty): None
Brief description: Add rate limiting to API endpoints to prevent abuse
✓ Added task #27: Implement rate limiting middleware
```

### List Tasks

```bash
# List all tasks
./task-manager.sh list all

# List by status
./task-manager.sh list pending
./task-manager.sh list progress
./task-manager.sh list completed
./task-manager.sh list blocked
./task-manager.sh list rejected

# List by priority
./task-manager.sh list critical
./task-manager.sh list high
./task-manager.sh list medium
./task-manager.sh list low
```

Example output:
```bash
$ ./task-manager.sh list pending
=== Pending Tasks ===
  #25: MinIO Object Storage Implementation
    **Status:** ⏳ Not Implemented
  #27: Implement rate limiting middleware
    **Status:** ⏳ Pending

✓ Found 2 task(s)
```

### Update Task Status

```bash
./task-manager.sh update <task_id>
```

Example:
```bash
$ ./task-manager.sh update 27
=== Update Task #27 ===
Current: **Status:** ⏳ Pending

New status:
  1) ⏳ Pending
  2) 🔄 In Progress
  3) ✅ Completed
  4) ⚠️ Blocked
Select (1-4): 2
✓ Updated task #27 status to: 🔄 In Progress
```

### Complete a Task

```bash
./task-manager.sh complete <task_id>
```

Marks task as completed and prompts for implementation notes:

```bash
$ ./task-manager.sh complete 27
=== Complete Task #27 ===

Add implementation notes (optional, press Ctrl+D when done):
Example: Implemented in src/Services/AuthService.cs, added 12 tests

Implemented RateLimitingMiddleware in src/API/Middleware/
Added 8 unit tests, all passing
Updated README with configuration examples
^D
✓ Marked task #27 as completed
```

### Reject a Task

```bash
./task-manager.sh reject <task_id>
```

Marks task as rejected/won't do and prompts for rejection reason:

```bash
$ ./task-manager.sh reject 28
=== Reject Task #28 ===

Add rejection reason (optional, press Ctrl+D when done):
Example: Out of scope for current roadmap, superseded by Task #15

Out of scope - feature not aligned with product roadmap
Superseded by Task #30 which provides better implementation
^D
✓ Marked task #28 as rejected
```

### Search Tasks

```bash
./task-manager.sh search <keyword>
```

Example:
```bash
$ ./task-manager.sh search "authentication"
=== Search Results for: authentication ===
  #1: Authentication & Authorization
  #15: HTTPS/TLS Configuration

✓ Found 2 task(s)
```

### Show Task Details

```bash
./task-manager.sh show <task_id>
```

Example:
```bash
$ ./task-manager.sh show 25
=== Task #25 Details ===
### 25. MinIO Object Storage Implementation
**Priority:** 🟢 Medium
**Status:** ⏳ Not Implemented
**Effort:** 2-3 days
...
```

### View Statistics

```bash
./task-manager.sh stats
```

Example output:
```bash
=== Task Statistics ===

Total Tasks: 26

By Status:
  ⏳ Pending:      2
  🔄 In Progress:  1
  ✅ Completed:    12
  ⚠️ Blocked:      0
  ❌ Rejected:     1

By Priority:
  🔴 Critical:     3
  🟡 High:         5
  🟢 Medium:       10
  ⚪ Low:          2

Completion Rate: 46%
[=======================-------------------------]
```

### Pre-Push Documentation Wizard

```bash
./task-manager.sh pre-push
```

Interactive wizard that helps document work before pushing:

1. Shows tasks currently in progress
2. Prompts to mark completed tasks
3. Prompts to mark newly started tasks
4. Prompts to add new tasks
5. Shows summary statistics
6. Suggests commit message

Example workflow:
```bash
$ ./task-manager.sh pre-push
=== Pre-Push Task Documentation ===

This wizard helps document work completed before pushing.

ℹ Current tasks in progress:
  #27: Implement rate limiting middleware
    **Status:** 🔄 In Progress

Did you complete any tasks? (y/n)
y
Enter task ID(s) to mark as completed (space-separated): 27

ℹ Completing task #27
...

Did you start any new tasks? (y/n)
n

Would you like to add any new tasks? (y/n)
n

=== Summary ===
...

✓ Task documentation complete!
ℹ Don't forget to commit TASK_LIST.md with your changes

Suggested commit message:
  git add TASK_LIST.md
  git commit -m 'docs: update TASK_LIST.md - document completed work'
```

## Workflow Integration

### Before Starting Work

```bash
# Check what needs to be done
./task-manager.sh list pending
./task-manager.sh list critical

# Find a specific task
./task-manager.sh search "rate limiting"

# Mark as in progress
./task-manager.sh update 5
```

### During Development

```bash
# Check task details
./task-manager.sh show 5

# Follow TDD workflow as usual
# Write tests, implement, refactor
```

### Before Committing/Pushing

```bash
# Run pre-push wizard
./task-manager.sh pre-push

# Or manually complete tasks
./task-manager.sh complete 5

# Commit task list with changes
git add TASK_LIST.md
git commit -m "docs: update TASK_LIST.md - mark rate limiting as completed"
```

### Planning Sprints

```bash
# View statistics
./task-manager.sh stats

# List high priority tasks
./task-manager.sh list critical
./task-manager.sh list high

# Check effort estimates
./task-manager.sh show 1
./task-manager.sh show 2
```

## Status Indicators

| Emoji | Status | Description |
|-------|--------|-------------|
| ⏳ | Pending | Task not yet started |
| 🔄 | In Progress | Currently being worked on |
| ✅ | Completed | Task finished successfully |
| ⚠️ | Blocked | Waiting on dependency/decision |
| ❌ | Rejected | Task rejected/won't do |

## Priority Levels

| Emoji | Priority | When to Use |
|-------|----------|-------------|
| 🔴 | Critical | Required for production, security-related |
| 🟡 | High | Important for enterprise features |
| 🟢 | Medium | Valuable enhancements |
| ⚪ | Low | Nice-to-have features |

## Configuration

### Environment Variables

```bash
# Use a different task file
TASK_FILE=custom_tasks.md ./task-manager.sh list all

# Or export for session
export TASK_FILE=custom_tasks.md
./task-manager.sh stats
```

### Default File

By default, the script uses `TASK_LIST.md` in the same directory as the script.

## Tips and Best Practices

### Keep It Current

```bash
# ✅ DO: Update immediately after completing work
./task-manager.sh complete 5
git add TASK_LIST.md
git commit -m "feat: add rate limiting
docs: update TASK_LIST.md - mark Task #5 complete"

# ❌ DON'T: Let task list get stale
```

### Use Pre-Push Wizard

```bash
# Before every push
./task-manager.sh pre-push
```

This ensures you:
- Document completed work
- Update task statuses
- Track new work discovered
- Maintain accurate task list

### Reference Tasks in Commits

```bash
# ✅ DO: Reference task in commit messages
git commit -m "feat: implement JWT authentication (Task #1)

- Add JwtTokenGenerator service
- Add authentication middleware
- Add 12 comprehensive tests"

# ❌ DON'T: No reference
git commit -m "add auth"
```

### Regular Reviews

```bash
# Weekly: Review pending tasks
./task-manager.sh list pending

# Monthly: Check statistics
./task-manager.sh stats

# Quarterly: Review and reprioritize
./task-manager.sh list all
```

## Integration with Git Hooks

You can integrate the pre-push wizard with git hooks:

### Pre-Push Hook

Create `.git/hooks/pre-push`:

```bash
#!/bin/bash

echo "Running task documentation check..."

# Check if TASK_LIST.md has uncommitted changes
if git diff --name-only | grep -q "TASK_LIST.md"; then
    echo "✓ TASK_LIST.md has been updated"
    exit 0
fi

# If no changes, prompt user
echo "⚠️  TASK_LIST.md hasn't been updated"
echo "Would you like to document your work? (y/n)"
read -r response

if [[ "$response" == "y" ]]; then
    ./task-manager.sh pre-push
    echo ""
    echo "Please stage and commit TASK_LIST.md before pushing"
    exit 1
fi

exit 0
```

Make it executable:
```bash
chmod +x .git/hooks/pre-push
```

## Troubleshooting

### Task file not found

```bash
$ ./task-manager.sh stats
✗ Task file not found: /path/to/TASK_LIST.md

Would you like to create a new task_list.md? (y/n)
```

**Solution:** Either create the file by answering 'y', or set `TASK_FILE` to the correct path.

### Task ID not found

```bash
$ ./task-manager.sh show 99
✗ Task #99 not found
```

**Solution:** Use `./task-manager.sh list all` to see available task IDs.

### Permission denied

```bash
$ ./task-manager.sh stats
bash: ./task-manager.sh: Permission denied
```

**Solution:** Make the script executable:
```bash
chmod +x task-manager.sh
```

## Examples

### Complete Workflow Example

```bash
# 1. Start of day - check what to work on
./task-manager.sh list pending
./task-manager.sh list critical

# 2. Start working on task #5
./task-manager.sh show 5
./task-manager.sh update 5  # Mark as in progress

# 3. Implement following TDD
# ... write tests, implement, refactor ...

# 4. Before committing
./task-manager.sh complete 5
# Add implementation notes when prompted

# 5. Commit both code and task list
git add .
git add TASK_LIST.md
git commit -m "feat: implement rate limiting (Task #5)

- Implemented RateLimitingMiddleware
- Added 8 unit tests, all passing
- Updated README with configuration

docs: update TASK_LIST.md - mark Task #5 as completed"

# 6. Push
git push
```

### Sprint Planning Example

```bash
# 1. Review current status
./task-manager.sh stats

# 2. Identify high-priority work
./task-manager.sh list critical
./task-manager.sh list high

# 3. Check effort estimates
./task-manager.sh show 1
./task-manager.sh show 2
./task-manager.sh show 3

# 4. Plan 2-week sprint (assume 10 working days)
# - Task #1: 2 days (Critical)
# - Task #2: 3 days (Critical)
# - Task #6: 2 days (High)
# - Task #7: 2 days (High)
# Total: 9 days (fits in sprint)
```

## Related Documentation

- [TASK_LIST.md](TASK_LIST.md) - Main task list
- [workflows/task-management.md](workflows/task-management.md) - Task management workflow guide
- [workflows/git-workflow.md](workflows/git-workflow.md) - Git conventions
- [workflows/pre-commit-checklist.md](workflows/pre-commit-checklist.md) - Pre-commit checks

## License

Same as project license.
