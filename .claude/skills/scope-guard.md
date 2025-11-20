# Scope Guard Skill - Project Owner Role

## Purpose
This skill prevents feature creep and scope expansion during implementation. It embodies the Project Owner/Product Owner role to validate that every feature, file, and line of code is justified by approved requirements.

## When to Use This Skill

**CRITICAL: Use this skill DURING implementation whenever you're about to add something new.**

### Triggers (Use this skill when you're about to):
1. **Add a new feature** not explicitly in requirements
2. **Add a new dependency** (NuGet package, library, service)
3. **Create a new project** in the solution
4. **Add a new controller/endpoint** to an API
5. **Implement a pattern** (multi-tenancy, messaging, caching, etc.)
6. **Add infrastructure** (database, queue, cache, etc.)
7. **Build abstraction layers** (interfaces, strategies, factories)

### Red Flags (STOP and use this skill if you think):
- 🚨 "We might need this later" (YAGNI violation)
- 🚨 "This is a best practice" (not always applicable)
- 🚨 "Enterprise systems have this" (over-engineering)
- 🚨 "Let me make this more flexible" (premature abstraction)
- 🚨 "Users probably want this too" (assumption)
- 🚨 "Since I'm here, I'll add..." (scope creep)

## Project Owner Role: The Scope Validation Framework

### The Four Gates

Every feature addition must pass through **ALL FOUR** gates. If it fails any gate, **DO NOT IMPLEMENT IT**.

#### Gate 1: Requirements Justification
**Question:** "Is this explicitly required in PROJECT_REQUIREMENTS.md?"

**Pass criteria:**
```
✅ Feature is listed in "IN SCOPE (Core)" section
✅ Feature is listed in "IN SCOPE (Optional)" with appropriate priority
✅ Feature directly enables a success criterion
✅ Feature solves the documented problem statement
```

**Fail criteria:**
```
❌ Feature is listed in "OUT OF SCOPE" section
❌ Feature is in "FUTURE CONSIDERATION" (not now)
❌ Feature is not mentioned in requirements at all
❌ "We might need it someday" (YAGNI)
❌ "Best practice says we should" (not justified by requirements)
```

**Example - HotSwap.Distributed Project:**
```
✅ PASS: Rolling deployment strategy
   → Listed in "IN SCOPE (Core)" as "Deploy across N servers with rolling strategy"

✅ PASS: Health monitoring
   → Enables success criterion: "Detect failures and rollback in <60 seconds"

❌ FAIL: Multi-tenancy
   → Listed in "OUT OF SCOPE: Multi-tenancy - single organization use case"

❌ FAIL: Cost tracking/billing
   → Not mentioned in requirements at all
   → Assumption: "Enterprise systems need billing"

❌ FAIL: Website management
   → Problem statement: "Deploy kernel modules"
   → Websites are not kernel modules
```

#### Gate 2: Complexity Justification
**Question:** "Does the complexity added justify the value gained?"

**Pass criteria:**
```
✅ Implementation is simple and minimal
✅ Complexity is proportional to benefit
✅ No simpler alternative exists
✅ Complexity is isolated and testable
```

**Fail criteria:**
```
❌ Adding 1,000+ lines for uncertain benefit
❌ Creating 5 abstraction layers for 1 use case
❌ Implementing enterprise pattern for simple problem
❌ Adding framework when standard library suffices
```

**Complexity Scoring:**
```
Lines of Code Added × Maintenance Burden
─────────────────────────────────────── = Complexity Score
          Value Delivered

Target: <1.0 (more value than complexity)
Warning: 1.0-3.0 (review carefully)
Reject: >3.0 (too complex for value)
```

**Example - HotSwap.Distributed Project:**
```
✅ PASS: 4 deployment strategies (~1,200 lines)
   → Complexity Score: 1,200 lines ÷ 4 strategies = 300 lines each
   → Value: Core requirement, different use cases
   → Verdict: Reasonable

❌ FAIL: 5 routing strategies (~1,200 lines)
   → Complexity Score: 1,200 lines ÷ 0 use cases = ∞
   → Value: Not in requirements, no use case
   → Verdict: Pure overhead

❌ FAIL: Multi-tenant infrastructure (~700 lines)
   → Complexity Score: 700 lines ÷ 0 organizations = ∞
   → Value: Single organization use case
   → Verdict: Wasted effort
```

#### Gate 3: Dependency Justification
**Question:** "Is this new dependency necessary, or can we use what we have?"

**Pass criteria:**
```
✅ Solves problem that standard library cannot
✅ Well-maintained package (recent updates, good community)
✅ Adds significant value (saves >100 lines of code)
✅ No overlap with existing dependencies
```

**Fail criteria:**
```
❌ Reinventing standard library functionality
❌ Adding 5 packages when 1 would suffice
❌ Unmaintained or abandoned package
❌ Adds <50 lines of value
❌ Duplicates existing dependency capability
```

**Dependency Review Checklist:**
```
For each new dependency:
1. Why can't standard library solve this?
2. What's the maintenance status? (Last update, GitHub stars, issues)
3. How much code does it save us?
4. Does it overlap with existing dependencies?
5. What's the security risk? (Supply chain, CVEs)
```

**Example - HotSwap.Distributed Project:**
```
✅ PASS: OpenTelemetry (~1.9.0)
   → Justification: Industry-standard observability, saves 1,000+ lines
   → Status: Well-maintained, official Microsoft support
   → Value: High (tracing, metrics, logging unified)

✅ PASS: Moq (~4.20.0) for tests
   → Justification: Test mocking library, standard in .NET
   → Value: High (enables proper unit testing)

❌ FAIL: Entity Framework for simple in-memory cache
   → Justification: "Might need database later"
   → Reality: ConcurrentDictionary is sufficient
   → Value: Low (adds complexity for uncertain future)
```

#### Gate 4: Maintenance Justification
**Question:** "Are we willing to maintain this forever?"

**Pass criteria:**
```
✅ Code is simple and self-explanatory
✅ Tests cover critical paths
✅ Documentation explains why it exists
✅ Team has expertise in this technology
✅ Clear ownership and support plan
```

**Fail criteria:**
```
❌ "I don't fully understand this, but it works"
❌ No tests (how will we know if it breaks?)
❌ No documentation (how will next person understand?)
❌ Experimental technology (who will support?)
❌ "Copy-pasted from Stack Overflow" (not understood)
```

**Maintenance Cost Formula:**
```
Annual Maintenance Hours =
  (Lines of Code ÷ 100) × Complexity Factor × Churn Rate

Complexity Factor:
- Simple logic: 0.5
- Business logic: 1.0
- Framework code: 2.0
- Distributed systems: 3.0
- Machine learning: 5.0

Churn Rate: % of code changed per year
```

**Example - HotSwap.Distributed Project:**
```
✅ PASS: Deployment strategies
   → Simple, testable, well-documented
   → Maintenance: ~12 hours/year (reasonable)

⚠️ WARNING: Schema compatibility checker
   → Complex logic, edge cases
   → Maintenance: ~40 hours/year
   → Question: Is this worth it for our use case?

❌ FAIL: Knowledge graph query engine
   → Complex, experimental, not tested
   → Maintenance: ~80 hours/year
   → No clear owner or use case
```

## The Scope Guard Workflow

### Step 1: Checkpoint Before Adding Features
**Before implementing ANY new feature, run this checkpoint:**

```
SCOPE CHECKPOINT:
─────────────────

Feature: [Name of feature you're about to add]

GATE 1 - Requirements Justification:
Q: Is this in PROJECT_REQUIREMENTS.md?
A: [Yes/No] - [Where in requirements]

GATE 2 - Complexity Justification:
Q: Estimated lines of code? [Number]
Q: Value delivered? [Describe]
Q: Complexity score: [LOC ÷ Value]
Q: Pass? [Yes/No]

GATE 3 - Dependency Justification:
Q: New dependencies needed? [List or "None"]
Q: Can existing code/libraries handle this? [Yes/No]
Q: If new dependency: Why? [Justification]

GATE 4 - Maintenance Justification:
Q: Estimated maintenance hours/year? [Number]
Q: Do we have expertise? [Yes/No]
Q: Is it tested? [Yes/No]
Q: Is it documented? [Yes/No]

DECISION: [PROCEED / REJECT / DEFER]
REASON: [Explanation]
```

**Example - Multi-Tenancy Decision:**
```
SCOPE CHECKPOINT:
─────────────────

Feature: Multi-tenant infrastructure

GATE 1 - Requirements Justification:
Q: Is this in PROJECT_REQUIREMENTS.md?
A: No - Explicitly listed in "OUT OF SCOPE: Single organization use case"

GATE 2 - Complexity Justification:
Q: Estimated lines of code? 700
Q: Value delivered? Supports multiple organizations
Q: Complexity score: 700 ÷ 0 (no requirement) = ∞
Q: Pass? NO

GATE 3 - Dependency Justification:
Q: New dependencies needed? None
Q: Can existing code handle this? N/A

GATE 4 - Maintenance Justification:
Q: Estimated maintenance hours/year? 40
Q: Do we have expertise? Yes
Q: Is it tested? Would need to be
Q: Is it documented? Would need to be

DECISION: REJECT
REASON: Explicitly out of scope, adds 700 lines with 0 value for current requirements.
If multi-tenancy is needed in future, revisit requirements and add to scope.
```

### Step 2: Log Scope Decisions
**Create SCOPE_DECISIONS.md to track all scope decisions:**

```markdown
# Scope Decisions Log

This file tracks all features considered and whether they were approved/rejected.

## Legend
- ✅ APPROVED - Passed all four gates
- ❌ REJECTED - Failed one or more gates
- ⏸️ DEFERRED - Good idea, but not now (add to FUTURE_CONSIDERATION)

---

## Decision Log

### [Date] - [Feature Name]
**Status**: [APPROVED/REJECTED/DEFERRED]
**Proposed by**: [Person/AI/Stakeholder]
**Decision by**: Scope Guard (Project Owner role)

**Gates:**
- Gate 1 (Requirements): [PASS/FAIL] - [Reason]
- Gate 2 (Complexity): [PASS/FAIL] - [Score: X]
- Gate 3 (Dependency): [PASS/FAIL] - [Reason]
- Gate 4 (Maintenance): [PASS/FAIL] - [Reason]

**Justification**: [Why approved/rejected/deferred]

**Alternative Considered**: [If rejected, what's the simpler approach?]

---
```

### Step 3: Challenge Feature Creep
**If you notice feature creep happening, challenge it:**

```
SCOPE CREEP ALERT:
──────────────────

I notice we're building: [Feature X]

This was not in the original PROJECT_REQUIREMENTS.md.

QUESTIONS:
1. Has the problem statement changed?
2. Did new requirements emerge?
3. Is this truly necessary, or "nice to have"?
4. Should we update PROJECT_REQUIREMENTS.md and get approval first?

CURRENT SCOPE: [Summarize approved scope]
CREEP RISK: [What we're about to add that wasn't approved]

RECOMMENDATION:
Option A: Add to PROJECT_REQUIREMENTS.md, get stakeholder approval, then implement
Option B: Defer to FUTURE_CONSIDERATION in requirements
Option C: Reject as out of scope

Which option do you prefer?
```

### Step 4: Weekly Scope Review
**At end of each week, review what was built vs. what was planned:**

```markdown
# Weekly Scope Review - Week of [Date]

## What We Planned to Build (from PROJECT_REQUIREMENTS.md)
- [ ] Feature 1
- [ ] Feature 2
- [ ] Feature 3

## What We Actually Built
- [x] Feature 1 ✅ (in scope)
- [x] Feature 2 ✅ (in scope)
- [x] Feature X ❌ (out of scope - why?)

## Scope Violations
1. **Feature X** (300 lines)
   - Justification: [Why was this added?]
   - Impact: [Time spent, maintenance burden]
   - Action: [Keep/Remove/Refactor]

## Scope Changes Approved
1. **Feature Y**
   - Reason: [New requirement from stakeholder]
   - Approved: [Date]
   - Status: Implemented

## Metrics
- Features in scope: [X]
- Features out of scope: [Y]
- Scope creep percentage: [Y/(X+Y) × 100%]
- Target: <10% scope creep

## Actions
- [ ] Remove scope creep features
- [ ] Update requirements if legitimately needed
- [ ] Communicate scope violations to stakeholder
```

## Common Scope Creep Patterns

### Pattern 1: "Since I'm Here" Syndrome
**Symptom:** Adding features while working on nearby code
```
❌ "Since I'm adding authentication, let me add OAuth2 + SAML + LDAP"
✅ "Requirements say JWT authentication. Implement only that."

❌ "Since I'm building deployment API, let me add tenant management"
✅ "Requirements say deployment API only. Tenants are out of scope."
```

### Pattern 2: "Best Practice" Over-Engineering
**Symptom:** Adding patterns because "enterprise systems do this"
```
❌ "Best practice says add multi-tenancy"
✅ "Requirements say single organization. Don't add multi-tenancy."

❌ "Enterprise systems use message queues"
✅ "Requirements say direct deployment. No message queue needed."
```

### Pattern 3: "Future Proofing" Trap
**Symptom:** Building for hypothetical future needs
```
❌ "We might need multi-region deployment someday"
✅ "Requirements don't mention multi-region. YAGNI - You Aren't Gonna Need It."

❌ "Let me make this pluggable in case we want to swap implementations"
✅ "One implementation needed now. Add abstraction only if second implementation is required."
```

### Pattern 4: "While I'm at It" Expansion
**Symptom:** Adding related but unnecessary features
```
❌ "While building deployment system, let me add cost tracking"
✅ "Cost tracking not in requirements. Focus on deployment only."

❌ "While adding health checks, let me add full APM monitoring"
✅ "Basic health check is sufficient for requirements. APM is scope creep."
```

### Pattern 5: "User Might Want" Assumptions
**Symptom:** Assuming features without asking
```
❌ "Users probably want dashboards"
✅ "Requirements don't mention dashboards. Ask stakeholder first."

❌ "Users will need email notifications"
✅ "Notifications not in requirements. Defer to future consideration."
```

## Measuring Scope Creep

### Scope Creep Metrics
```
Scope Creep % = (Unplanned Features / Total Features) × 100%

Target: <10%
Warning: 10-25%
Critical: >25%

Example - HotSwap.Distributed Project:
Total Features Built: ~20
Planned Features: ~8
Unplanned Features: ~12
Scope Creep: (12/20) × 100% = 60% ❌ CRITICAL
```

### Feature Justification Ratio
```
Justification Ratio = Features in Requirements / Features Implemented

Target: 1.0 (all features justified)
Warning: 0.7-0.9 (some creep)
Critical: <0.7 (major creep)

Example - HotSwap.Distributed:
Features in Requirements: 8
Features Implemented: 20
Justification Ratio: 8/20 = 0.4 ❌ CRITICAL
```

### Lines of Code Waste
```
Wasted LOC = Lines Implementing Out-of-Scope Features

Target: <10% of total LOC
Warning: 10-30%
Critical: >30%

Example - HotSwap.Distributed:
Total LOC: 26,750
Out-of-Scope LOC: ~16,000 (tenants, websites, analytics, routing, etc.)
Waste %: (16,000/26,750) × 100% = 60% ❌ CRITICAL
```

## Integration with Other Skills

**Use AFTER these skills:**
- `project-intake` - Scope guard enforces requirements from intake

**Use DURING these skills:**
- `tdd-helper` - Before writing test, validate feature is in scope
- Development work - Checkpoint before each new feature

**Use BEFORE these skills:**
- `architecture-review` - Don't architect out-of-scope features
- `precommit-check` - Don't commit scope creep

**Chain with:**
1. `project-intake` → Define scope
2. `scope-guard` (this skill) → Enforce scope during development
3. Weekly review → Measure and correct scope creep

## Success Indicators

**You've used this skill successfully if:**
1. ✅ Every feature can point to justification in PROJECT_REQUIREMENTS.md
2. ✅ SCOPE_DECISIONS.md tracks all scope decisions
3. ✅ Weekly scope creep <10%
4. ✅ You rejected features not in requirements
5. ✅ You challenged "while I'm at it" additions
6. ✅ You questioned "best practice" features

**You've failed to use this skill if:**
1. ❌ Built features not in requirements
2. ❌ "Just added" features without checkpoint
3. ❌ Scope creep >25%
4. ❌ No SCOPE_DECISIONS.md log
5. ❌ Can't explain why each feature exists
6. ❌ Added complexity "just in case"

## Real-World Example: HotSwap.Distributed

### Without Scope Guard (What Happened)
```
APPROVED SCOPE (PROJECT_REQUIREMENTS.md):
- Rolling deployment
- Health monitoring
- Deployment strategies (4 types)
- Rollback capability
Total: ~8 core features

ACTUAL IMPLEMENTATION:
- Deployment system ✅
- Multi-tenancy ❌ (not requested)
- Cost tracking ❌ (not requested)
- Website management ❌ (not requested)
- Message routing ❌ (not requested)
- Schema registry ❌ (not requested)
- Knowledge graph ❌ (not requested)
- Plugin system ❌ (not requested)
Total: ~20 features (60% scope creep)

RESULT:
- 26,750 lines of code
- 16,000 lines out of scope (60% waste)
- Unclear product identity
- 6/10 quality score
```

### With Scope Guard (What Should Have Happened)
```
WEEK 1: Deployment Core
Checkpoint: "About to add rolling deployment strategy"
Gate 1: ✅ In requirements (core feature)
Gate 2: ✅ Complexity reasonable (~300 lines for core feature)
Gate 3: ✅ No new dependencies
Gate 4: ✅ Maintainable, testable
Decision: PROCEED

WEEK 2: Additional Strategies
Checkpoint: "About to add Blue-Green deployment"
Gate 1: ✅ In requirements (optional, priority medium)
Gate 2: ✅ Complexity reasonable (~350 lines)
Gate 3: ✅ No new dependencies
Gate 4: ✅ Similar to rolling, maintainable
Decision: PROCEED

WEEK 3: Feature Creep Attempt
Checkpoint: "About to add multi-tenancy"
Gate 1: ❌ FAIL - In "OUT OF SCOPE" section
Decision: REJECT immediately

Checkpoint: "About to add cost tracking"
Gate 1: ❌ FAIL - Not in requirements at all
Decision: REJECT immediately

Checkpoint: "About to add website management"
Gate 1: ❌ FAIL - Scope is kernel modules, not websites
Decision: REJECT immediately

RESULT:
- 8,000-10,000 lines of code (focused)
- 0% scope creep
- Clear product identity
- 9/10 quality score
```

## Skill Invocation

```bash
# Use this skill during implementation
/scope-guard

# Or manually before each feature:
# 1. Run Scope Checkpoint (4 gates)
# 2. Log decision in SCOPE_DECISIONS.md
# 3. Proceed only if all gates pass
# 4. Weekly scope review
```

---

**Remember: Scope creep is the silent killer of projects. Guard the scope ruthlessly.**
