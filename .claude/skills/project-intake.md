# Project Intake Skill - Business Analyst Role

## Purpose
This skill prevents building the wrong thing by forcing requirements clarification **BEFORE** any code is written. It embodies the Business Analyst role to extract, validate, and document clear requirements from vague or confused stakeholders.

## When to Use This Skill

**CRITICAL: Use this skill at the VERY START of any project or major feature request BEFORE writing ANY code.**

### Triggers (Use this skill when you see):
1. **Vague requests**: "Build a deployment system", "Add multi-tenancy", "Make it scalable"
2. **Unclear scope**: No clear boundaries on what's included/excluded
3. **Missing context**: Don't know the problem being solved
4. **Confused stakeholders**: User gives contradictory or unclear instructions
5. **Large requests**: Any request that would take >1 day to implement
6. **Feature creep risk**: Request could expand into many sub-features

### Red Flags (ALWAYS use this skill if you see):
- ❌ "Build something like X" (no specifics)
- ❌ "Add all the enterprise features" (undefined scope)
- ❌ "Make it production-ready" (unclear criteria)
- ❌ Multiple technologies mentioned without clear purpose
- ❌ No success criteria defined
- ❌ No constraints mentioned (time, complexity, dependencies)

## Business Analyst Role: The Five Critical Questions

### 1. PROBLEM STATEMENT
**Ask:** "What problem are we solving?"

**Dig deeper:**
- Who is experiencing this problem?
- How are they experiencing it today?
- What is the cost of NOT solving it?
- What's the root cause (not just symptoms)?

**Output:** Clear problem statement in format:
```
PROBLEM: [Specific user/system] cannot [specific action] because [specific constraint/issue].
IMPACT: [Measurable consequence - time, money, users affected]
ROOT CAUSE: [Technical or process reason]
```

**Example:**
```
PROBLEM: DevOps team cannot deploy kernel modules to 100+ servers without downtime because deployments are all-or-nothing.
IMPACT: 4-hour maintenance windows required monthly, costing $50K in lost revenue per window.
ROOT CAUSE: No rolling deployment capability in current system.
```

### 2. SUCCESS CRITERIA
**Ask:** "How will we know we've solved the problem?"

**Dig deeper:**
- What are the measurable outcomes?
- What does "done" look like?
- What are the acceptance criteria?
- What are we explicitly NOT solving?

**Output:** SMART criteria (Specific, Measurable, Achievable, Relevant, Time-bound)
```
SUCCESS CRITERIA:
✓ [Measurable outcome 1] - Test: [How to verify]
✓ [Measurable outcome 2] - Test: [How to verify]
✓ [Measurable outcome 3] - Test: [How to verify]

OUT OF SCOPE (explicitly not included):
✗ [Feature/capability 1]
✗ [Feature/capability 2]
```

**Example:**
```
SUCCESS CRITERIA:
✓ Deploy to 100 servers with <5% downtime (Test: Monitor uptime during deployment)
✓ Rollback in <60 seconds if issues detected (Test: Simulate failure, measure rollback time)
✓ Support 4 deployment strategies: Direct, Rolling, Blue-Green, Canary (Test: Execute each strategy)

OUT OF SCOPE:
✗ Multi-tenancy (single organization only)
✗ Cost tracking/billing (monitoring only)
✗ Website/CMS features (kernel modules only)
```

### 3. CONSTRAINTS & ASSUMPTIONS
**Ask:** "What are the constraints and assumptions?"

**Dig deeper:**
- Time constraints: When is this needed?
- Budget constraints: How much effort is justified?
- Technical constraints: What tech stack/platforms?
- Resource constraints: Who's available to build/maintain?
- Dependency constraints: What must exist first?
- Assumptions: What are we assuming to be true?

**Output:**
```
CONSTRAINTS:
- Time: [Deadline or duration]
- Effort: [Team size × time, or "keep it simple"]
- Technology: [Required tech, forbidden tech]
- Scale: [Users, load, data volume]
- Budget: [Cost limits if applicable]

ASSUMPTIONS:
- [Assumption 1] - Risk if false: [consequence]
- [Assumption 2] - Risk if false: [consequence]
```

**Example:**
```
CONSTRAINTS:
- Time: Needed in 2 weeks for Q1 launch
- Effort: Solo developer, keep it minimal
- Technology: Must use .NET 8.0, integrate with existing infrastructure
- Scale: 100-500 servers, 10 deployments/day max
- Complexity: Prefer simple over feature-rich

ASSUMPTIONS:
- All servers are Linux x64 - Risk: May need Windows support later
- Single organization use - Risk: Multi-tenancy would require major refactor
- Modules are <100MB - Risk: Large modules may timeout
```

### 4. SCOPE BOUNDARIES
**Ask:** "What's in scope vs. out of scope?"

**Dig deeper:**
- Core features (must have)
- Optional features (nice to have)
- Explicitly excluded (not building)
- Future considerations (might add later)

**Output:**
```
IN SCOPE (Core - must have):
1. [Feature 1] - Priority: Critical
2. [Feature 2] - Priority: Critical
3. [Feature 3] - Priority: High

IN SCOPE (Optional - nice to have):
4. [Feature 4] - Priority: Medium
5. [Feature 5] - Priority: Low

OUT OF SCOPE (Explicitly excluded):
- [Excluded feature 1] - Reason: [why not]
- [Excluded feature 2] - Reason: [why not]

FUTURE CONSIDERATION (Maybe later):
- [Future feature 1] - Revisit: [when/why]
```

**Example:**
```
IN SCOPE (Core):
1. Rolling deployment across N servers - Priority: Critical
2. Rollback on failure - Priority: Critical
3. Deployment status tracking - Priority: High
4. Health monitoring - Priority: High

IN SCOPE (Optional):
5. Blue-Green deployment strategy - Priority: Medium
6. Canary deployment strategy - Priority: Low

OUT OF SCOPE:
- Multi-tenancy - Reason: Single organization use case
- Cost tracking - Reason: No billing requirements
- Website management - Reason: Kernel modules only
- Schema versioning - Reason: Modules are self-contained
- Message routing - Reason: Direct deployment only

FUTURE CONSIDERATION:
- Multi-region deployment - Revisit: If expanding globally
- A/B testing integration - Revisit: If product team requests
```

### 5. RISK ASSESSMENT
**Ask:** "What could go wrong?"

**Dig deeper:**
- Technical risks: What's hard to build?
- Scope risks: What could cause scope creep?
- Dependency risks: What external factors could block us?
- Maintenance risks: What's hard to support long-term?
- Integration risks: What existing systems must we integrate with?

**Output:**
```
RISKS:
1. [Risk description]
   - Likelihood: [High/Medium/Low]
   - Impact: [High/Medium/Low]
   - Mitigation: [How to reduce/avoid]

2. [Risk description]
   - Likelihood: [High/Medium/Low]
   - Impact: [High/Medium/Low]
   - Mitigation: [How to reduce/avoid]
```

**Example:**
```
RISKS:
1. Scope creep into messaging system
   - Likelihood: High (already happened in test project)
   - Impact: High (doubles complexity, wrong product)
   - Mitigation: Document "OUT OF SCOPE" clearly, review architecture before coding

2. Deployment failures cause downtime
   - Likelihood: Medium (complex distributed system)
   - Impact: High (production outage)
   - Mitigation: Implement rollback, gradual rollout, extensive testing

3. Over-engineering for scale not needed
   - Likelihood: High (AI tendency to build for enterprise scale)
   - Impact: Medium (wasted effort, harder to maintain)
   - Mitigation: Start simple, add complexity only when needed
```

## The Business Analyst Workflow

### Step 1: Extract Information (First Pass)
**Read the request carefully, then ask clarifying questions:**

```
I need to clarify requirements before starting implementation.

PROBLEM STATEMENT:
- What specific problem are we solving?
- Who is experiencing this problem?
- What's the impact if we don't solve it?

SUCCESS CRITERIA:
- What are the measurable outcomes?
- How will we test that it works?
- What does "done" look like?

CONSTRAINTS:
- Time: When is this needed?
- Effort: How much complexity is justified?
- Technology: Any required/forbidden tech?
- Scale: How many users/requests/servers?

SCOPE:
- What features are absolutely required (core)?
- What features are nice-to-have (optional)?
- What features are explicitly NOT included?

RISKS:
- What could cause this to fail?
- What assumptions are we making?
- What dependencies exist?
```

**DO NOT PROCEED until you get answers to these questions.**

### Step 2: Document Requirements (Second Pass)
**Once you have answers, document them in PROJECT_REQUIREMENTS.md:**

```markdown
# Project Requirements: [Project Name]

**Date**: [Today's date]
**Stakeholder**: [User name or role]
**Analyst**: Claude (AI Assistant)

---

## Problem Statement

[Clear problem statement with impact]

---

## Success Criteria

[SMART criteria with tests]

[Out of scope items]

---

## Constraints & Assumptions

[All constraints]

[All assumptions with risks]

---

## Scope Boundaries

[In scope - core]
[In scope - optional]
[Out of scope]
[Future consideration]

---

## Risk Assessment

[All identified risks with mitigation]

---

## Sign-off

This requirements document must be approved before implementation begins.

**Questions for stakeholder:**
1. [Any remaining clarifications needed]
2. [Any contradictions to resolve]
3. [Any missing information]

**Approval**: ☐ Approved by stakeholder
```

### Step 3: Get Approval (Gate)
**Present the requirements document and ask:**

```
I've documented the requirements above. Before I start implementation:

1. Does this accurately reflect what you want?
2. Are there any corrections or additions?
3. Do you approve starting implementation with this scope?

I will not proceed until you confirm. This prevents building the wrong thing.
```

**DO NOT WRITE CODE until approval is given.**

### Step 4: Create Implementation Brief (Handoff)
**Once approved, create brief for development:**

```markdown
# Implementation Brief: [Project Name]

**Based on**: PROJECT_REQUIREMENTS.md (approved [date])

## What We're Building

[1-paragraph summary]

## Core Deliverables

1. [Deliverable 1] - [Acceptance test]
2. [Deliverable 2] - [Acceptance test]
3. [Deliverable 3] - [Acceptance test]

## What We're NOT Building

- [Excluded 1]
- [Excluded 2]
- [Excluded 3]

## Architecture Constraints

- [Constraint 1]
- [Constraint 2]

## Success Metrics

- [Metric 1]: [Target value]
- [Metric 2]: [Target value]

## Risks to Watch

- [Risk 1]: [Mitigation]
- [Risk 2]: [Mitigation]

**Next Step**: Architecture review before implementation
```

## Common Mistakes to Avoid

### ❌ Mistake 1: Skipping Requirements for "Small" Changes
**Wrong:** "This is just adding a field, no need for requirements."
**Right:** Small changes accumulate. Always document intent and scope.

### ❌ Mistake 2: Assuming You Understand Vague Requests
**Wrong:** User says "add authentication" → start building OAuth2 + SAML + JWT + Multi-factor
**Right:** Ask: "What authentication methods? What's the threat model? Who are the users?"

### ❌ Mistake 3: Accepting Unclear Success Criteria
**Wrong:** User says "make it fast" → build complex caching, sharding, load balancing
**Right:** Ask: "What response time is acceptable? What's the expected load? What's the current performance?"

### ❌ Mistake 4: Not Documenting "Out of Scope"
**Wrong:** Only list what's included
**Right:** Explicitly list what's NOT included to prevent scope creep

### ❌ Mistake 5: Proceeding Without Approval
**Wrong:** "I think I understand, let me start coding"
**Right:** "Here are the requirements I understood. Please approve before I proceed."

## Real-World Example: The HotSwap.Distributed Project

### What Happened (Without This Skill)
```
REQUEST: "Build a hot-swap kernel deployment system"

WHAT CLAUDE BUILT:
- Deployment system ✓ (requested)
- Multi-tenancy (not requested)
- Cost tracking/billing (not requested)
- Website management (not requested)
- Plugin system (not requested)
- Schema registry (not requested)
- Message routing (not requested)
- Knowledge graph system (not requested)

RESULT: 26,750 lines of code, 38,237 lines of docs, unclear scope, 6/10 quality
```

### What Should Have Happened (With This Skill)
```
STEP 1: CLARIFY REQUIREMENTS
Claude: "I need to clarify requirements before starting implementation.

PROBLEM STATEMENT:
- What specific problem are we solving with hot-swap deployment?
- Who needs this? (DevOps team? Customers? Internal systems?)
- What's the impact of current solution?

SCOPE:
- Is this for kernel modules only, or web apps too?
- Do we need multi-tenancy? (Multiple organizations or single?)
- Do we need cost tracking? (Is this a paid service?)
- What deployment strategies are required? (Rolling? Blue-Green? Canary?)

CONSTRAINTS:
- What's the timeline?
- How complex should this be? (MVP or enterprise-scale?)
- What infrastructure? (Cloud? On-premise? Kubernetes?)

Please answer these questions before I start building."

STEP 2: DOCUMENT REQUIREMENTS
[Create PROJECT_REQUIREMENTS.md with clear scope]

STEP 3: GET APPROVAL
Claude: "I've documented requirements above. Does this match your vision?
Approve before I proceed."

STEP 4: BUILD ONLY WHAT'S APPROVED
[Build core deployment system only, ~8,000 lines instead of 26,750]

RESULT: Focused product, 9/10 quality, actually solves the problem
```

## Integration with Other Skills

**Use BEFORE these skills:**
- `architecture-review` - Requirements must exist before architecture
- `tdd-helper` - Tests are based on requirements
- `sprint-planner` - Can't plan without knowing what to build

**Use AFTER these skills:**
- None - This is the FIRST skill in any project

**Chain with:**
1. `project-intake` (this skill) → Document requirements
2. `scope-guard` → Validate scope isn't creeping
3. `architecture-review` → Design solution
4. `reality-check` → Estimate effort
5. Then: Start coding with TDD

## Success Indicators

**You've used this skill successfully if:**
1. ✅ PROJECT_REQUIREMENTS.md exists and is approved
2. ✅ "Out of scope" is explicitly documented
3. ✅ Success criteria are measurable and testable
4. ✅ Stakeholder confirmed understanding before coding started
5. ✅ You didn't build features not in requirements
6. ✅ You asked clarifying questions instead of assuming

**You've failed to use this skill if:**
1. ❌ You started coding immediately after receiving request
2. ❌ You built features not explicitly requested
3. ❌ You have no documented requirements
4. ❌ Success criteria are vague ("make it better", "add features")
5. ❌ You don't know what's in/out of scope
6. ❌ You made assumptions about unclear requirements

## Measuring Impact

**Before project-intake skill:**
- Built 26,750 lines for unclear requirements
- 60% of features were not requested
- 38,237 lines of docs trying to justify features
- 6/10 quality score

**After project-intake skill:**
- Build only approved features
- 0% feature creep (by definition)
- Documentation matches implementation
- 9/10 quality score (focused, clear purpose)

**ROI: 5-10 hours of requirements work saves 50-100 hours of building wrong thing**

## Skill Invocation

```bash
# Use this skill at project start
/project-intake

# Or manually:
# 1. Ask the Five Critical Questions
# 2. Document answers in PROJECT_REQUIREMENTS.md
# 3. Get stakeholder approval
# 4. Create Implementation Brief
# 5. Hand off to architecture-review skill
```

---

**Remember: The worst code is the code that solves the wrong problem. This skill prevents that.**
