# CRITICAL GitHub Issues - Remediation Sprint Phase 1

**Created:** 2025-11-20
**Purpose:** 15 detailed issue specifications for Phase 1 remediation sprint
**Total Estimated Effort:** 109 hours (~3 weeks)
**Milestone:** Phase 1 - Critical Remediation

---

## 📋 Overview

This directory contains comprehensive specifications for all 15 CRITICAL issues identified in the comprehensive code review (CODE_REVIEW_REPORT.md). Each issue includes:

- **Problem Description** - What's wrong and where
- **Impact Assessment** - Why it's critical
- **Code Locations** - Exact files and line numbers
- **Recommended Fix** - Code examples and implementation approach
- **Acceptance Criteria** - How to verify it's fixed
- **Test Cases** - 3-4 tests with full assertions
- **Definition of Done** - Checklist for completion
- **Effort Estimate** - Hours to complete
- **Related Issues** - Cross-references

---

## 📁 File Structure

```
.github/ISSUE_TEMPLATE/
├── README.md                    # This file
├── CRITICAL_ISSUES_01-05.md     # Issues 1-5
├── CRITICAL_ISSUES_06-10.md     # Issues 6-10
└── CRITICAL_ISSUES_11-15.md     # Issues 11-15
```

---

## 🔴 Issue Summary by Category

### Security Issues (8 issues, 55 hours)

| # | Title | Effort | Priority |
|---|-------|--------|----------|
| 1 | Missing Authorization on Schema Endpoints | 4h | Week 1 |
| 2 | Tenant Isolation Middleware Not Registered | 2h | Week 1 |
| 4 | Hardcoded Demo Credentials | 6h | Week 1 |
| 9 | IDOR Vulnerabilities | 12h | Week 2-3 |
| 11 | Missing CSRF Protection | 16h | Week 3 |
| 12 | Weak JWT Secret Key | 4h | Week 3 |
| 13 | SignalR Hub Missing Authentication | 4h | Week 3 |
| 14 | Production Environment Detection Weakness | 4h | Week 3 |
| 15 | Permissive CORS Configuration | 3h | Week 3 |

### Stability/Concurrency Issues (7 issues, 54 hours)

| # | Title | Effort | Priority |
|---|-------|--------|----------|
| 3 | Async/Await Blocking Call | 8h | Week 1 |
| 5 | Static Dictionary Memory Leak | 12h | Week 2 |
| 6 | Race Condition in LoadBalanced Routing | 6h | Week 2 |
| 7 | Division by Zero in Canary Metrics | 4h | Week 2 |
| 8 | Pipeline State Management Race Condition | 10h | Week 2 |
| 10 | Unchecked Rollback Failures | 10h | Week 3 |

**Total:** 15 issues, 109 hours, ~3 weeks

---

## 🎯 How to Use These Issues

### Option 1: Import to GitHub Issues (Recommended)

**Automated Import:**

```bash
# Install GitHub CLI if not already installed
# brew install gh (macOS)
# sudo apt install gh (Linux)
# winget install GitHub.cli (Windows)

# Authenticate
gh auth login

# Navigate to repository
cd /path/to/Claude-code-test

# Create issues from templates
gh issue create --title "[CRITICAL] Missing Authorization on Schema Endpoints" \
  --body-file .github/ISSUE_TEMPLATE/CRITICAL_ISSUES_01-05.md \
  --label "blocker,security,P0" \
  --milestone "Phase 1 - Critical Remediation"

# Repeat for all 15 issues
```

**Manual Import:**

1. Go to: https://github.com/scrawlsbenches/Claude-code-test/issues/new
2. Copy content from issue files
3. Paste into issue body
4. Add labels: `blocker`, `security` (if applicable), `P0`
5. Set milestone: `Phase 1 - Critical Remediation`
6. Assign to appropriate track lead

### Option 2: Create from GitHub Web UI

1. **Create Milestone First:**
   - Go to: https://github.com/scrawlsbenches/Claude-code-test/milestones
   - Click "New milestone"
   - Title: "Phase 1 - Critical Remediation"
   - Due date: 3 weeks from start
   - Description: "Resolve all 15 CRITICAL issues from code review"

2. **Create Issues:**
   - For each issue in the files:
     - Copy the markdown content
     - Create new issue
     - Paste content
     - Add labels
     - Set milestone
     - Assign owner

### Option 3: Use GitHub Projects (Recommended for Tracking)

1. **Create Project Board:**
   - Go to: https://github.com/scrawlsbenches/Claude-code-test/projects
   - Click "New project"
   - Template: "Kanban"
   - Name: "Remediation Sprint Phase 1"

2. **Add Columns:**
   - Backlog
   - Ready
   - In Progress
   - In Review
   - Done

3. **Import Issues:**
   - Create all 15 issues from templates
   - Add to project board
   - Organize by priority/week

4. **Configure Automation:**
   - Auto-move to "In Progress" when assigned
   - Auto-move to "In Review" on PR creation
   - Auto-move to "Done" on issue close

---

## 👥 Recommended Team Assignment

### Security Track (Lead + 1 engineer)
**Issues:** 1, 2, 4, 9, 11, 12, 13, 14, 15
**Effort:** 55 hours (~2 weeks)

**Week 1 Focus:**
- Issue #1: Schema authorization (4h)
- Issue #2: Tenant isolation (2h)
- Issue #4: Demo credentials (6h)

**Week 2-3 Focus:**
- Issue #9: IDOR fixes (12h)
- Issue #11: CSRF protection (16h)
- Issues #12, #13, #14, #15: Remaining security (15h)

### Infrastructure Track (Lead + 1 engineer)
**Issues:** 5, 10
**Effort:** 22 hours (~1 week)

**Week 2 Focus:**
- Issue #5: ApprovalService memory leak (12h)

**Week 3 Focus:**
- Issue #10: Rollback failure handling (10h)

### Application Track (Lead + 1 engineer)
**Issues:** 3, 6, 7, 8
**Effort:** 28 hours (~1 week)

**Week 1 Focus:**
- Issue #3: Async/await blocking (8h)

**Week 2 Focus:**
- Issue #6: LoadBalanced race condition (6h)
- Issue #7: Division by zero (4h)
- Issue #8: Pipeline state race condition (10h)

---

## 📊 Tracking Progress

### Daily Standup Questions

For each engineer:
1. **What issue(s) did you complete yesterday?**
2. **What issue(s) are you working on today?**
3. **Any blockers?**

### Weekly Metrics to Track

- **Issues Resolved:** X/15
- **Effort Spent:** Y/109 hours
- **Blockers:** Count and description
- **On Track:** Yes / At Risk / Behind

### Phase Gate 1 Criteria (Week 3)

Before moving to Phase 2, verify:
- [ ] All 15 CRITICAL issues closed
- [ ] All acceptance criteria met
- [ ] All tests passing (582 total)
- [ ] Zero regression bugs
- [ ] Security review approved
- [ ] Code review approved (2+ reviewers per PR)
- [ ] Documentation updated

---

## 🧪 Testing Strategy

### Test Categories per Issue

Each issue requires:

1. **Unit Tests (3-4 per issue)**
   - Happy path
   - Edge cases
   - Error conditions
   - Performance/concurrency tests

2. **Integration Tests (1-2 per issue)**
   - End-to-end workflow
   - Cross-component interaction

3. **Security Tests (for security issues)**
   - Attack scenario simulation
   - Authorization bypass attempts
   - IDOR exploitation attempts

### Total Test Count Increase

- **Before:** 582 tests
- **New Tests:** ~60 tests (4 per issue average)
- **After:** ~642 tests expected

---

## 🔍 Code Review Checklist

For each PR closing an issue:

### Security Review (Dr. Priya Sharma)
- [ ] No new security vulnerabilities introduced
- [ ] Authentication/authorization correct
- [ ] Input validation comprehensive
- [ ] No sensitive data in logs
- [ ] OWASP Top 10 compliance

### Technical Review (Marcus Rodriguez)
- [ ] Code follows C# conventions
- [ ] Async/await used correctly
- [ ] No race conditions introduced
- [ ] Error handling comprehensive
- [ ] Performance acceptable

### Test Review (Lisa Park)
- [ ] All test cases from issue implemented
- [ ] Tests follow AAA pattern
- [ ] FluentAssertions used
- [ ] Edge cases covered
- [ ] No flaky tests

### Documentation Review
- [ ] XML documentation updated
- [ ] README.md updated (if needed)
- [ ] CLAUDE.md updated (if needed)
- [ ] API docs updated (if needed)

---

## 📞 Escalation Path

### Blockers
- **Owner:** Issue assignee
- **Escalate to:** Track lead (within 4 hours)
- **Escalate to:** Marcus Rodriguez (within 24 hours)
- **Escalate to:** Sarah Chen (if >1 day blocked)

### Security Concerns
- **Any security questions:** Dr. Priya Sharma (immediate)
- **Security review required:** Within 24 hours
- **No compromise:** Security has veto power

### Timeline Risks
- **If >1 week slip likely:** Escalate to Marcus Rodriguez
- **If Phase 1 at risk:** Emergency stakeholder meeting

---

## 📝 Issue Template Format

Each issue follows this structure:

```markdown
## Issue #X: [CRITICAL] Title

**Priority:** 🔴 CRITICAL
**Category:** Security/Concurrency/etc.
**Assigned To:** Track Lead
**Estimated Effort:** X hours
**Phase:** 1 - Week X

### Problem Description
[What's wrong]

### Impact
[Why it's critical]

### Code Location
[Files and lines]

### Recommended Fix
[Solution with code examples]

### Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2

### Test Cases
**Test 1:** [Description]
[Code example]

### Definition of Done
- [x] Code changes implemented
- [x] Tests passing
- [x] Code review approved
- [x] Security review approved (if applicable)
- [x] Merged to main

### Related Issues
[Cross-references]
```

---

## 🚀 Getting Started

**For Project Manager (Alex Kumar):**

1. Review all 15 issues
2. Create GitHub issues (manual or automated)
3. Set up GitHub Projects board
4. Assign to track leads
5. Schedule first standup (tomorrow 9 AM)

**For Engineering Leads:**

1. Review assigned issues
2. Clarify any questions with code review author
3. Break down into tasks if needed
4. Estimate completion dates
5. Commit to Phase 1 timeline (3 weeks)

**For Team Members:**

1. Read assigned issues thoroughly
2. Review code locations
3. Understand acceptance criteria
4. Ask questions before starting
5. Follow test-driven development (tests first!)

---

## 📚 Related Documents

- **CODE_REVIEW_REPORT.md** - Full code review findings
- **REMEDIATION_ACTION_PLAN.md** - 4-phase timeline and goals
- **SKILLS.md** - Claude Skills for automation
- **CLAUDE.md** - Development guidelines

---

## ✅ Success Criteria

Phase 1 is complete when:

1. ✅ All 15 issues closed in GitHub
2. ✅ All acceptance criteria met
3. ✅ All new tests passing
4. ✅ Zero regression in existing 582 tests
5. ✅ Security review sign-off (Dr. Priya Sharma)
6. ✅ Code review approved (2+ reviewers per PR)
7. ✅ Documentation updated
8. ✅ Production deployment ready

**Target:** Week 3, December 11, 2025

---

**Questions?**
- Slack: #code-remediation
- PM: Alex Kumar (@alex.kumar)
- Engineering Lead: Marcus Rodriguez (@marcus.r)
- Security: Dr. Priya Sharma (@priya.sharma)
