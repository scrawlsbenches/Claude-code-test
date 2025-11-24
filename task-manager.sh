#!/bin/bash

# task-manager.sh - Task Management CLI for task_list.md
# Helps maintain task_list.md automatically and document work before pushes

set -euo pipefail

# Configuration
TASK_FILE="${TASK_FILE:-TASK_LIST.md}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TASK_PATH="$SCRIPT_DIR/$TASK_FILE"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Priority emoji mappings
PRIORITY_CRITICAL="🔴"
PRIORITY_HIGH="🟡"
PRIORITY_MEDIUM="🟢"
PRIORITY_LOW="⚪"

# Text-based priority patterns (more robust matching)
PRIORITY_CRITICAL_TEXT="Critical"
PRIORITY_HIGH_TEXT="High"
PRIORITY_MEDIUM_TEXT="Medium"
PRIORITY_LOW_TEXT="Low"

# Status emoji mappings
STATUS_PENDING="⏳"
STATUS_IN_PROGRESS="🔄"
STATUS_COMPLETED="✅"
STATUS_BLOCKED="⚠️"
STATUS_REJECTED="❌"

# Text-based status patterns (for tasks without emojis)
STATUS_PENDING_TEXT="Not Implemented|Not Created|Pending"
STATUS_COMPLETED_TEXT="Completed|Complete|COMPLETED"
STATUS_IN_PROGRESS_TEXT="In Progress|In-Progress|WIP"
STATUS_BLOCKED_TEXT="Blocked|On Hold"
STATUS_REJECTED_TEXT="Rejected|Won't Do|Wont Do|Cancelled"

# Usage information
usage() {
    cat << EOF
Task Manager - CLI for managing task_list.md

Usage: $0 <command> [options]

Commands:
  add              Add a new task interactively
  list [filter]    List tasks (all, pending, progress, completed, blocked, rejected)
  update <id>      Update task status
  complete <id...> Mark task(s) as completed (supports multiple IDs)
  start <id...>    Mark task(s) as in-progress (supports multiple IDs)
  reject <id>      Mark task as rejected/won't do
  search <term>    Search tasks by keyword
  stats            Show task statistics
  next             Show the next recommended task to work on
  summary          Quick overview: stats + next task (ideal for session start)
  pre-push         Interactive pre-push task documentation
  show <id>        Show detailed task information
  help             Show this help message

Examples:
  $0 add                      # Add a new task interactively
  $0 list pending             # List all pending tasks
  $0 update 5                 # Update status of task #5
  $0 complete 3               # Mark task #3 as completed
  $0 complete 1 2 3           # Mark multiple tasks as completed
  $0 start 5 6                # Mark tasks #5 and #6 as in-progress
  $0 reject 7                 # Mark task #7 as rejected/won't do
  $0 search "authentication"  # Search for authentication tasks
  $0 stats                    # Show task statistics
  $0 next                     # Show next recommended task
  $0 summary                  # Quick session start overview
  $0 pre-push                 # Document work before push
  $0 show 2                   # Show details of task #2

Environment Variables:
  TASK_FILE        Path to task list file (default: task_list.md)

EOF
    exit 0
}

# Utility functions
print_header() {
    echo -e "${CYAN}=== $1 ===${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}" >&2
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

# Check if task file exists
check_task_file() {
    if [[ ! -f "$TASK_PATH" ]]; then
        print_error "Task file not found: $TASK_PATH"
        echo "Would you like to create a new task_list.md? (y/n)"
        read -r response
        if [[ "$response" == "y" ]]; then
            create_task_file
        else
            exit 1
        fi
    fi
}

# Create new task file with template
create_task_file() {
    cat > "$TASK_PATH" << 'EOF'
# Task List

**Generated:** $(date +%Y-%m-%d)
**Last Updated:** $(date +%Y-%m-%d)

---

## Overview

This document tracks all tasks, enhancements, and work items for the project.

## Tasks

EOF
    print_success "Created new task file: $TASK_PATH"
}

# Get next task number
get_next_task_number() {
    if [[ ! -f "$TASK_PATH" ]]; then
        echo "1"
        return
    fi

    # Find the highest task number
    local max_num=$(grep -oP '^### \K\d+(?=\.)' "$TASK_PATH" 2>/dev/null | sort -n | tail -1)
    if [[ -z "$max_num" ]]; then
        echo "1"
    else
        echo $((max_num + 1))
    fi
}

# Add a new task
add_task() {
    print_header "Add New Task"

    # Get task details
    echo -n "Task name: "
    read -r task_name

    if [[ -z "$task_name" ]]; then
        print_error "Task name cannot be empty"
        exit 1
    fi

    echo ""
    echo "Priority:"
    echo "  1) 🔴 Critical"
    echo "  2) 🟡 High"
    echo "  3) 🟢 Medium"
    echo "  4) ⚪ Low"
    echo -n "Select (1-4): "
    read -r priority_choice

    case $priority_choice in
        1) priority="$PRIORITY_CRITICAL Critical";;
        2) priority="$PRIORITY_HIGH High";;
        3) priority="$PRIORITY_MEDIUM Medium";;
        4) priority="$PRIORITY_LOW Low";;
        *) priority="$PRIORITY_MEDIUM Medium";;
    esac

    echo -n "Effort estimate (e.g., 1-2 days): "
    read -r effort
    [[ -z "$effort" ]] && effort="TBD"

    echo -n "Dependencies (e.g., Task #5, or leave empty): "
    read -r dependencies
    [[ -z "$dependencies" ]] && dependencies="None"

    echo -n "Brief description: "
    read -r description

    # Get next task number
    local task_num=$(get_next_task_number)

    # Create task entry
    local date_str=$(date +%Y-%m-%d)

    cat >> "$TASK_PATH" << EOF

---

### ${task_num}. ${task_name}
**Priority:** ${priority}
**Status:** $STATUS_PENDING Pending
**Effort:** ${effort}
**Dependencies:** ${dependencies}
**Added:** ${date_str}

**Description:**
${description}

**Requirements:**
- [ ] Requirement 1
- [ ] Requirement 2
- [ ] Requirement 3

**Acceptance Criteria:**
- Feature works as described
- Tests pass (>80% coverage)
- Documentation updated

**Impact:** TBD

EOF

    # Update last updated date in header
    if grep -q "Last Updated:" "$TASK_PATH"; then
        sed -i "s/\*\*Last Updated:\*\*.*/\*\*Last Updated:\*\* ${date_str}/" "$TASK_PATH"
    fi

    print_success "Added task #${task_num}: ${task_name}"
    print_info "Edit $TASK_PATH to add specific requirements and acceptance criteria"
}

# List tasks with optional filter
list_tasks() {
    check_task_file

    local filter="${1:-all}"

    case $filter in
        all)
            print_header "All Tasks"
            grep -E "^### [0-9]+\." "$TASK_PATH" | while read -r line; do
                echo "$line"
            done
            ;;
        pending|⏳)
            print_header "Pending Tasks"
            show_tasks_by_status "$STATUS_PENDING" "$STATUS_PENDING_TEXT"
            ;;
        progress|in-progress|🔄)
            print_header "Tasks In Progress"
            show_tasks_by_status "$STATUS_IN_PROGRESS" "$STATUS_IN_PROGRESS_TEXT"
            ;;
        completed|done|✅)
            print_header "Completed Tasks"
            show_tasks_by_status "$STATUS_COMPLETED" "$STATUS_COMPLETED_TEXT"
            ;;
        blocked|⚠️)
            print_header "Blocked Tasks"
            show_tasks_by_status "$STATUS_BLOCKED" "$STATUS_BLOCKED_TEXT"
            ;;
        rejected|wont-do|wontdo|❌)
            print_header "Rejected Tasks"
            show_tasks_by_status "$STATUS_REJECTED" "$STATUS_REJECTED_TEXT"
            ;;
        critical|🔴)
            print_header "Critical Priority Tasks"
            show_tasks_by_priority "$PRIORITY_CRITICAL_TEXT"
            ;;
        high|🟡)
            print_header "High Priority Tasks"
            show_tasks_by_priority "$PRIORITY_HIGH_TEXT"
            ;;
        medium|🟢)
            print_header "Medium Priority Tasks"
            show_tasks_by_priority "$PRIORITY_MEDIUM_TEXT"
            ;;
        low|⚪)
            print_header "Low Priority Tasks"
            show_tasks_by_priority "$PRIORITY_LOW_TEXT"
            ;;
        *)
            print_error "Unknown filter: $filter"
            echo "Valid filters: all, pending, progress, completed, blocked, rejected, critical, high, medium, low"
            exit 1
            ;;
    esac
}

# Show tasks by status (supports both emoji and text patterns)
show_tasks_by_status() {
    local status_emoji="$1"
    local status_text="${2:-}"  # Optional text pattern
    local count=0

    # Read task file and extract tasks with matching status
    local in_task=0
    local task_num=""
    local task_name=""

    while IFS= read -r line; do
        if [[ $line =~ ^###\ ([0-9]+)\.\ (.+)$ ]]; then
            task_num="${BASH_REMATCH[1]}"
            task_name="${BASH_REMATCH[2]}"
            in_task=1
        elif [[ $in_task -eq 1 && $line =~ ^\*\*Status:\*\* ]]; then
            # Check for emoji match OR text pattern match
            local matches=0
            if [[ -n "$status_emoji" && $line =~ $status_emoji ]]; then
                matches=1
            elif [[ -n "$status_text" && $line =~ ($status_text) ]]; then
                matches=1
            fi

            if [[ $matches -eq 1 ]]; then
                echo "  #${task_num}: ${task_name}"
                echo "    ${line}"
                count=$((count + 1))
            fi
            in_task=0
        elif [[ $line =~ ^### ]]; then
            in_task=0
        fi
    done < "$TASK_PATH"

    if [[ $count -eq 0 ]]; then
        print_info "No tasks found with this status"
    else
        echo ""
        print_success "Found $count task(s)"
    fi
}

# Show tasks by priority (uses text-based matching for robustness)
show_tasks_by_priority() {
    local priority_text="$1"
    local count=0

    local in_task=0
    local task_num=""
    local task_name=""

    while IFS= read -r line; do
        if [[ $line =~ ^###\ ([0-9]+)\.\ (.+)$ ]]; then
            task_num="${BASH_REMATCH[1]}"
            task_name="${BASH_REMATCH[2]}"
            in_task=1
        elif [[ $in_task -eq 1 && $line =~ ^\*\*Priority:\*\* ]]; then
            # Use text-based matching (e.g., "Critical" not just emoji)
            if [[ $line =~ $priority_text ]]; then
                echo "  #${task_num}: ${task_name}"
                echo "    ${line}"
                count=$((count + 1))
            fi
            in_task=0
        elif [[ $line =~ ^### ]]; then
            in_task=0
        fi
    done < "$TASK_PATH"

    if [[ $count -eq 0 ]]; then
        print_info "No tasks found with this priority"
    else
        echo ""
        print_success "Found $count task(s)"
    fi
}

# Update task status
update_task_status() {
    check_task_file

    local task_id="$1"

    if [[ -z "$task_id" ]]; then
        print_error "Task ID required"
        echo "Usage: $0 update <task_id>"
        exit 1
    fi

    # Find the task
    if ! grep -q "^### ${task_id}\." "$TASK_PATH"; then
        print_error "Task #${task_id} not found"
        exit 1
    fi

    print_header "Update Task #${task_id}"

    # Show current status
    local current_status=$(grep -A 5 "^### ${task_id}\." "$TASK_PATH" | grep "^\*\*Status:\*\*" | head -1)
    echo "Current: $current_status"
    echo ""

    # Prompt for new status
    echo "New status:"
    echo "  1) $STATUS_PENDING Pending"
    echo "  2) $STATUS_IN_PROGRESS In Progress"
    echo "  3) $STATUS_COMPLETED Completed"
    echo "  4) $STATUS_BLOCKED Blocked"
    echo "  5) $STATUS_REJECTED Rejected/Won't Do"
    echo -n "Select (1-5): "
    read -r status_choice

    case $status_choice in
        1) new_status="$STATUS_PENDING Pending";;
        2) new_status="$STATUS_IN_PROGRESS In Progress";;
        3) new_status="$STATUS_COMPLETED Completed ($(date +%Y-%m-%d))";;
        4) new_status="$STATUS_BLOCKED Blocked";;
        5) new_status="$STATUS_REJECTED Rejected ($(date +%Y-%m-%d))";;
        *)
            print_error "Invalid choice"
            exit 1
            ;;
    esac

    # Update the status using awk for precise replacement
    awk -v task="### ${task_id}." -v new_status="**Status:** ${new_status}" '
        /^###/ { in_task = ($0 ~ task) }
        in_task && /^\*\*Status:\*\*/ {
            print new_status
            in_task = 0
            next
        }
        { print }
    ' "$TASK_PATH" > "${TASK_PATH}.tmp" && mv "${TASK_PATH}.tmp" "$TASK_PATH"

    # Update last updated date
    local date_str=$(date +%Y-%m-%d)
    sed -i "s/\*\*Last Updated:\*\*.*/\*\*Last Updated:\*\* ${date_str}/" "$TASK_PATH"

    print_success "Updated task #${task_id} status to: ${new_status}"
}

# Mark task as completed with notes
complete_task() {
    check_task_file

    local task_id="${1:-}"

    if [[ -z "$task_id" ]]; then
        print_error "Task ID required"
        echo "Usage: $0 complete <task_id>"
        exit 1
    fi

    # Find the task
    if ! grep -q "^### ${task_id}\." "$TASK_PATH"; then
        print_error "Task #${task_id} not found"
        exit 1
    fi

    print_header "Complete Task #${task_id}"

    # Update status to completed
    local date_str=$(date +%Y-%m-%d)
    local new_status="$STATUS_COMPLETED **Completed** (${date_str})"

    awk -v task="### ${task_id}." -v new_status="**Status:** ${new_status}" '
        /^###/ { in_task = ($0 ~ task) }
        in_task && /^\*\*Status:\*\*/ {
            print new_status
            in_task = 0
            next
        }
        { print }
    ' "$TASK_PATH" > "${TASK_PATH}.tmp" && mv "${TASK_PATH}.tmp" "$TASK_PATH"

    # Prompt for implementation notes
    echo ""
    echo "Add implementation notes (optional, press Ctrl+D when done):"
    echo "Example: Implemented in src/Services/AuthService.cs, added 12 tests"
    echo ""

    local notes=""
    while IFS= read -r line; do
        notes="${notes}${line}\n"
    done

    if [[ -n "$notes" ]]; then
        # Find the task and add implementation summary after Status
        awk -v task="### ${task_id}." -v notes="$notes" '
            /^###/ { in_task = ($0 ~ task) }
            in_task && /^\*\*Status:\*\*/ {
                print $0
                if (notes != "") {
                    print ""
                    print "**Implementation Summary:**"
                    printf "%b", notes
                }
                in_task = 0
                next
            }
            in_task && /^\*\*Implementation Summary:\*\*/ {
                # Skip old implementation summary
                while (getline && !/^\*\*[A-Z]/) { }
                print $0
                in_task = 0
                next
            }
            { print }
        ' "$TASK_PATH" > "${TASK_PATH}.tmp" && mv "${TASK_PATH}.tmp" "$TASK_PATH"
    fi

    # Update last updated date
    sed -i "s/\*\*Last Updated:\*\*.*/\*\*Last Updated:\*\* ${date_str}/" "$TASK_PATH"

    print_success "Marked task #${task_id} as completed"
}

# Bulk complete tasks (no interactive prompts)
complete_tasks_bulk() {
    check_task_file

    if [[ $# -eq 0 ]]; then
        print_error "At least one task ID required"
        echo "Usage: $0 complete <id> [id2] [id3] ..."
        exit 1
    fi

    local date_str=$(date +%Y-%m-%d)
    local new_status="$STATUS_COMPLETED **Completed** (${date_str})"
    local completed_count=0

    for task_id in "$@"; do
        # Find the task
        if ! grep -q "^### ${task_id}\." "$TASK_PATH"; then
            print_warning "Task #${task_id} not found, skipping"
            continue
        fi

        # Update status to completed
        awk -v task="### ${task_id}." -v new_status="**Status:** ${new_status}" '
            /^###/ { in_task = ($0 ~ task) }
            in_task && /^\*\*Status:\*\*/ {
                print new_status
                in_task = 0
                next
            }
            { print }
        ' "$TASK_PATH" > "${TASK_PATH}.tmp" && mv "${TASK_PATH}.tmp" "$TASK_PATH"

        print_success "Marked task #${task_id} as completed"
        completed_count=$((completed_count + 1))
    done

    # Update last updated date once
    sed -i "s/\*\*Last Updated:\*\*.*/\*\*Last Updated:\*\* ${date_str}/" "$TASK_PATH"

    echo ""
    print_success "Completed $completed_count task(s)"
}

# Mark tasks as in-progress (bulk support)
start_tasks() {
    check_task_file

    if [[ $# -eq 0 ]]; then
        print_error "At least one task ID required"
        echo "Usage: $0 start <id> [id2] [id3] ..."
        exit 1
    fi

    local date_str=$(date +%Y-%m-%d)
    local new_status="$STATUS_IN_PROGRESS **In Progress** (${date_str})"
    local started_count=0

    for task_id in "$@"; do
        # Find the task
        if ! grep -q "^### ${task_id}\." "$TASK_PATH"; then
            print_warning "Task #${task_id} not found, skipping"
            continue
        fi

        # Update status to in-progress
        awk -v task="### ${task_id}." -v new_status="**Status:** ${new_status}" '
            /^###/ { in_task = ($0 ~ task) }
            in_task && /^\*\*Status:\*\*/ {
                print new_status
                in_task = 0
                next
            }
            { print }
        ' "$TASK_PATH" > "${TASK_PATH}.tmp" && mv "${TASK_PATH}.tmp" "$TASK_PATH"

        print_success "Marked task #${task_id} as in-progress"
        started_count=$((started_count + 1))
    done

    # Update last updated date once
    sed -i "s/\*\*Last Updated:\*\*.*/\*\*Last Updated:\*\* ${date_str}/" "$TASK_PATH"

    echo ""
    print_success "Started $started_count task(s)"
}

# Show next recommended task (highest priority pending)
next_task() {
    check_task_file

    print_header "Next Recommended Task"

    local found=0
    local in_main_task=0
    local task_num=""
    local task_name=""
    local task_priority=""
    local task_effort=""

    # Priority order: Critical > High > Medium > Low
    for priority in "Critical" "High" "Medium" "Low"; do
        while IFS= read -r line; do
            if [[ $line =~ ^###\ ([0-9]+)\.\ (.+)$ ]]; then
                task_num="${BASH_REMATCH[1]}"
                task_name="${BASH_REMATCH[2]}"
                in_main_task=1
                task_priority=""
                task_effort=""
            elif [[ $in_main_task -eq 1 && $line =~ ^\*\*Priority:\*\* ]]; then
                task_priority="$line"
            elif [[ $in_main_task -eq 1 && $line =~ ^\*\*Effort:\*\* ]]; then
                task_effort="$line"
            elif [[ $in_main_task -eq 1 && $line =~ ^\*\*Status:\*\* ]]; then
                # Check if pending AND matches current priority level
                if [[ $line =~ ($STATUS_PENDING|Not\ Implemented|Not\ Created|Pending) ]]; then
                    if [[ $task_priority =~ $priority ]]; then
                        echo ""
                        echo -e "  ${YELLOW}#${task_num}${NC}: ${task_name}"
                        echo "    $task_priority"
                        [[ -n "$task_effort" ]] && echo "    $task_effort"
                        echo ""
                        echo -e "  To start: ${GREEN}./task-manager.sh start ${task_num}${NC}"
                        echo -e "  Details:  ${BLUE}./task-manager.sh show ${task_num}${NC}"
                        found=1
                        break 2  # Exit both loops
                    fi
                fi
                in_main_task=0
            elif [[ $line =~ ^### ]]; then
                in_main_task=0
            fi
        done < "$TASK_PATH"
    done

    if [[ $found -eq 0 ]]; then
        print_info "No pending tasks found - all done!"
    fi
}

# Quick summary for session start
show_summary() {
    check_task_file

    print_header "Session Summary"

    # Quick stats (inline, not full stats output)
    local total=$(grep -c "^### [0-9]\+\." "$TASK_PATH" 2>/dev/null || echo "0")
    local pending=0
    local in_progress=0
    local completed=0

    local in_main_task=0
    while IFS= read -r line; do
        if [[ $line =~ ^###\ [0-9]+\. ]]; then
            in_main_task=1
        elif [[ $line =~ ^#### ]]; then
            in_main_task=0
        elif [[ $in_main_task -eq 1 && $line =~ ^\*\*Status:\*\* ]]; then
            if [[ $line =~ ($STATUS_PENDING|Not\ Implemented|Not\ Created|Pending) ]]; then
                pending=$((pending + 1))
            elif [[ $line =~ ($STATUS_IN_PROGRESS|In\ Progress) ]]; then
                in_progress=$((in_progress + 1))
            elif [[ $line =~ ($STATUS_COMPLETED|Completed|Complete|COMPLETED) ]]; then
                completed=$((completed + 1))
            fi
            in_main_task=0
        fi
    done < "$TASK_PATH"

    local completion_rate=0
    if [[ $total -gt 0 ]]; then
        completion_rate=$((completed * 100 / total))
    fi

    echo ""
    echo -e "  Progress: ${GREEN}${completed}${NC}/${total} tasks (${completion_rate}%)"
    echo -e "  Pending:  ${YELLOW}${pending}${NC} | In Progress: ${BLUE}${in_progress}${NC}"
    echo ""

    # Show next task
    next_task
}

# Mark task as rejected/won't do
reject_task() {
    check_task_file

    local task_id="$1"

    if [[ -z "$task_id" ]]; then
        print_error "Task ID required"
        echo "Usage: $0 reject <task_id>"
        exit 1
    fi

    # Find the task
    if ! grep -q "^### ${task_id}\." "$TASK_PATH"; then
        print_error "Task #${task_id} not found"
        exit 1
    fi

    print_header "Reject Task #${task_id}"

    # Update status to rejected
    local date_str=$(date +%Y-%m-%d)
    local new_status="$STATUS_REJECTED **Rejected** (${date_str})"

    awk -v task="### ${task_id}." -v new_status="**Status:** ${new_status}" '
        /^###/ { in_task = ($0 ~ task) }
        in_task && /^\*\*Status:\*\*/ {
            print new_status
            in_task = 0
            next
        }
        { print }
    ' "$TASK_PATH" > "${TASK_PATH}.tmp" && mv "${TASK_PATH}.tmp" "$TASK_PATH"

    # Prompt for rejection reason
    echo ""
    echo "Add rejection reason (optional, press Ctrl+D when done):"
    echo "Example: Out of scope for current roadmap, superseded by Task #15"
    echo ""

    local reason=""
    while IFS= read -r line; do
        reason="${reason}${line}\n"
    done

    if [[ -n "$reason" ]]; then
        # Find the task and add rejection reason after Status
        awk -v task="### ${task_id}." -v reason="$reason" '
            /^###/ { in_task = ($0 ~ task) }
            in_task && /^\*\*Status:\*\*/ {
                print $0
                if (reason != "") {
                    print ""
                    print "**Rejection Reason:**"
                    printf "%b", reason
                }
                in_task = 0
                next
            }
            in_task && /^\*\*Rejection Reason:\*\*/ {
                # Skip old rejection reason
                while (getline && !/^\*\*[A-Z]/) { }
                print $0
                in_task = 0
                next
            }
            { print }
        ' "$TASK_PATH" > "${TASK_PATH}.tmp" && mv "${TASK_PATH}.tmp" "$TASK_PATH"
    fi

    # Update last updated date
    sed -i "s/\*\*Last Updated:\*\*.*/\*\*Last Updated:\*\* ${date_str}/" "$TASK_PATH"

    print_success "Marked task #${task_id} as rejected"
}

# Search tasks by keyword
search_tasks() {
    check_task_file

    local search_term="$1"

    if [[ -z "$search_term" ]]; then
        print_error "Search term required"
        echo "Usage: $0 search <term>"
        exit 1
    fi

    print_header "Search Results for: $search_term"

    local count=0
    local in_task=0
    local task_num=""
    local task_name=""
    local task_content=""

    while IFS= read -r line; do
        if [[ $line =~ ^###\ ([0-9]+)\.\ (.+)$ ]]; then
            # Check if previous task matches
            if [[ $in_task -eq 1 && -n "$task_content" ]]; then
                if echo "$task_content" | grep -iq "$search_term"; then
                    echo "  #${task_num}: ${task_name}"
                    count=$((count + 1))
                fi
            fi

            # Start new task
            task_num="${BASH_REMATCH[1]}"
            task_name="${BASH_REMATCH[2]}"
            task_content="$task_name"
            in_task=1
        elif [[ $in_task -eq 1 ]]; then
            task_content="${task_content} ${line}"
        fi
    done < "$TASK_PATH"

    # Check last task
    if [[ $in_task -eq 1 && -n "$task_content" ]]; then
        if echo "$task_content" | grep -iq "$search_term"; then
            echo "  #${task_num}: ${task_name}"
            count=$((count + 1))
        fi
    fi

    echo ""
    if [[ $count -eq 0 ]]; then
        print_info "No tasks found matching '$search_term'"
    else
        print_success "Found $count task(s)"
    fi
}

# Show task statistics
show_stats() {
    check_task_file

    print_header "Task Statistics"

    local total=$(grep -c "^### [0-9]\+\." "$TASK_PATH" 2>/dev/null || echo "0")

    # Count statuses only for main tasks (### N.) - excludes sub-tasks (#### N.N)
    local pending=0
    local in_progress=0
    local completed=0
    local blocked=0
    local rejected=0
    local critical=0
    local high=0
    local medium=0
    local low=0

    local in_main_task=0

    while IFS= read -r line; do
        # Detect main task header (### N. not #### N.N)
        if [[ $line =~ ^###\ [0-9]+\. ]]; then
            in_main_task=1
        elif [[ $line =~ ^#### ]]; then
            in_main_task=0  # Sub-task, skip its status
        elif [[ $in_main_task -eq 1 && $line =~ ^\*\*Status:\*\* ]]; then
            # Count status for main task only
            if [[ $line =~ ($STATUS_PENDING|Not\ Implemented|Not\ Created|Pending) ]]; then
                pending=$((pending + 1))
            elif [[ $line =~ ($STATUS_IN_PROGRESS|In\ Progress) ]]; then
                in_progress=$((in_progress + 1))
            elif [[ $line =~ ($STATUS_COMPLETED|Completed|Complete|COMPLETED) ]]; then
                completed=$((completed + 1))
            elif [[ $line =~ ($STATUS_BLOCKED|Blocked) ]]; then
                blocked=$((blocked + 1))
            elif [[ $line =~ ($STATUS_REJECTED|Rejected) ]]; then
                rejected=$((rejected + 1))
            fi
            in_main_task=0
        elif [[ $in_main_task -eq 1 && $line =~ ^\*\*Priority:\*\* ]]; then
            # Count priority for main task (text-based matching)
            if [[ $line =~ $PRIORITY_CRITICAL_TEXT ]]; then
                critical=$((critical + 1))
            elif [[ $line =~ $PRIORITY_HIGH_TEXT ]]; then
                high=$((high + 1))
            elif [[ $line =~ $PRIORITY_MEDIUM_TEXT ]]; then
                medium=$((medium + 1))
            elif [[ $line =~ $PRIORITY_LOW_TEXT ]]; then
                low=$((low + 1))
            fi
        fi
    done < "$TASK_PATH"

    echo ""
    echo "Total Tasks: $total"
    echo ""
    echo "By Status:"
    echo "  $STATUS_PENDING Pending:      $pending"
    echo "  $STATUS_IN_PROGRESS In Progress:  $in_progress"
    echo "  $STATUS_COMPLETED Completed:    $completed"
    echo "  $STATUS_BLOCKED Blocked:      $blocked"
    echo "  $STATUS_REJECTED Rejected:     $rejected"
    echo ""
    echo "By Priority:"
    echo "  $PRIORITY_CRITICAL Critical:     $critical"
    echo "  $PRIORITY_HIGH High:         $high"
    echo "  $PRIORITY_MEDIUM Medium:       $medium"
    echo "  $PRIORITY_LOW Low:          $low"
    echo ""

    if [[ $total -gt 0 ]]; then
        local completion_rate=$((completed * 100 / total))
        echo "Completion Rate: ${completion_rate}%"

        # Progress bar
        local bar_length=50
        local filled=$((completion_rate * bar_length / 100))
        local empty=$((bar_length - filled))

        printf "["
        printf "${GREEN}%${filled}s${NC}" | tr ' ' '='
        printf "%${empty}s" | tr ' ' '-'
        printf "]\n"
    fi
}

# Show detailed task information
show_task() {
    check_task_file

    local task_id="$1"

    if [[ -z "$task_id" ]]; then
        print_error "Task ID required"
        echo "Usage: $0 show <task_id>"
        exit 1
    fi

    print_header "Task #${task_id} Details"

    # Extract task details
    awk -v task="### ${task_id}." '
        BEGIN { in_task=0 }
        /^###/ {
            if (in_task) exit
            in_task = ($0 ~ task)
        }
        in_task { print }
    ' "$TASK_PATH"

    if ! grep -q "^### ${task_id}\." "$TASK_PATH"; then
        print_error "Task #${task_id} not found"
        exit 1
    fi
}

# Pre-push interactive documentation
pre_push_documentation() {
    print_header "Pre-Push Task Documentation"

    echo ""
    echo "This wizard helps document work completed before pushing."
    echo ""

    # Show tasks in progress
    print_info "Current tasks in progress:"
    show_tasks_by_status "$STATUS_IN_PROGRESS"

    echo ""
    echo "Did you complete any tasks? (y/n)"
    read -r has_completed

    if [[ "$has_completed" == "y" ]]; then
        echo -n "Enter task ID(s) to mark as completed (space-separated): "
        read -r task_ids

        for task_id in $task_ids; do
            echo ""
            print_info "Completing task #${task_id}"
            complete_task "$task_id"
        done
    fi

    echo ""
    echo "Did you start any new tasks? (y/n)"
    read -r has_started

    if [[ "$has_started" == "y" ]]; then
        echo -n "Enter task ID(s) to mark as in progress (space-separated): "
        read -r task_ids

        for task_id in $task_ids; do
            echo ""
            print_info "Marking task #${task_id} as in progress"

            local date_str=$(date +%Y-%m-%d)
            local new_status="$STATUS_IN_PROGRESS In Progress"

            awk -v task="### ${task_id}." -v new_status="**Status:** ${new_status}" '
                /^###/ { in_task = ($0 ~ task) }
                in_task && /^\*\*Status:\*\*/ {
                    print new_status
                    in_task = 0
                    next
                }
                { print }
            ' "$TASK_PATH" > "${TASK_PATH}.tmp" && mv "${TASK_PATH}.tmp" "$TASK_PATH"

            print_success "Updated task #${task_id}"
        done
    fi

    echo ""
    echo "Would you like to add any new tasks? (y/n)"
    read -r has_new

    if [[ "$has_new" == "y" ]]; then
        echo -n "How many tasks would you like to add? "
        read -r num_tasks

        for ((i=1; i<=num_tasks; i++)); do
            echo ""
            print_info "Adding task $i of $num_tasks"
            add_task
        done
    fi

    echo ""
    print_header "Summary"
    show_stats

    echo ""
    print_success "Task documentation complete!"
    print_info "Don't forget to commit task_list.md with your changes"
    echo ""
    echo "Suggested commit message:"
    echo "  git add $TASK_FILE"
    echo "  git commit -m 'docs: update task_list.md - document completed work'"
}

# Main command dispatcher
main() {
    if [[ $# -eq 0 ]]; then
        usage
    fi

    local command="$1"
    shift

    case "$command" in
        add)
            add_task "$@"
            ;;
        list|ls)
            list_tasks "$@"
            ;;
        update)
            update_task_status "$@"
            ;;
        complete|done)
            # Use bulk complete for multiple args, single complete for one
            if [[ $# -gt 1 ]]; then
                complete_tasks_bulk "$@"
            else
                complete_task "$@"
            fi
            ;;
        start|begin)
            start_tasks "$@"
            ;;
        reject|wont-do|wontdo)
            reject_task "$@"
            ;;
        search|find)
            search_tasks "$@"
            ;;
        stats|statistics)
            show_stats
            ;;
        next)
            next_task
            ;;
        summary|overview)
            show_summary
            ;;
        pre-push|prepush|doc)
            pre_push_documentation
            ;;
        show|view)
            show_task "$@"
            ;;
        help|--help|-h)
            usage
            ;;
        *)
            print_error "Unknown command: $command"
            echo ""
            usage
            ;;
    esac
}

# Run main function
main "$@"
