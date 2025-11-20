# Reality Check Skill - Project Manager Role

## Purpose
This skill prevents over-commitment and unrealistic plans. It embodies the Project Manager role to estimate effort realistically, identify risks, and validate that plans are achievable given constraints.

## When to Use This Skill

**CRITICAL: Use this skill AFTER architecture is designed but BEFORE committing to timelines or starting implementation.**

### Triggers (Use this skill when):
1. **Stakeholder asks** "When will this be done?"
2. **About to commit** to a deadline or timeline
3. **Planning sprint/iteration** (what can we actually finish?)
4. **Scope feels large** (more than 1 week of work)
5. **Multiple unknowns** exist in the project
6. **Request feels ambitious** (can we really build this?)

### Red Flags (STOP and use this skill if you think):
- 🚨 "This will be quick" (famous last words)
- 🚨 "Just a few lines of code" (ignoring tests, docs, deployment)
- 🚨 "I can finish this in a day" (underestimating)
- 🚨 "No problem, I'll build all of this" (over-committing)
- 🚨 "How hard can it be?" (very hard, usually)
- 🚨 Stakeholder says "simple change" (rarely is)

## Project Manager Role: The Realistic Estimation Framework

### The Three Estimation Principles

#### Principle 1: Hofstadter's Law
**"It always takes longer than you expect, even when you take into account Hofstadter's Law"**

```
Initial Estimate: X hours
Reality: 2-3X hours (even for experienced developers)

Why:
- Underestimate complexity
- Forget about edge cases
- Forget about testing time
- Forget about documentation
- Forget about debugging
- Forget about integration issues
- Forget about deployment
- Interruptions and context switching
```

**Solution: 3x Multiplier Rule**
```
Your initial estimate: X hours
Realistic estimate: 3X hours

Example:
"This feature will take 4 hours"
→ Realistic: 12 hours (1.5 days)

"This project will take 1 week"
→ Realistic: 3 weeks
```

#### Principle 2: Break Down and Sum Up
**Large estimates are always wrong. Break into small pieces.**

```
❌ WRONG: "This project will take 2 weeks"
→ Too vague, what's included?

✅ RIGHT: Break into tasks:
- Task 1: Write tests (4 hours)
- Task 2: Implement core (8 hours)
- Task 3: Integration (4 hours)
- Task 4: Documentation (2 hours)
- Task 5: Code review fixes (2 hours)
- Task 6: Deployment (2 hours)
- Buffer (20% = 4 hours)
→ TOTAL: 26 hours = 3.25 days

Then apply 3x multiplier: 9.75 days (~2 weeks)
```

#### Principle 3: Identify Unknowns
**Unknowns are where estimates fail. Make unknowns visible.**

```
Known-Knowns: Things we know and understand
→ Estimate with confidence

Known-Unknowns: Things we know we don't know
→ Add research/spike time, high variance

Unknown-Unknowns: Things we don't know we don't know
→ Add large buffer (50-100%), high risk

Example:
"Add authentication to API"
- Known-Known: JWT token generation (4 hours)
- Known-Unknown: Integration with existing user store (8 hours? 16? Need to investigate)
- Unknown-Unknown: Edge cases with concurrent logins (? hours - add 50% buffer)

Estimate: 4 + 12 (avg) + 8 (buffer) = 24 hours → 3 days
```

### The Reality Check Process

**Run this process for every significant piece of work:**

```
REALITY CHECK QUESTIONNAIRE:
────────────────────────────────

Project/Feature: [Name]
Requested by: [Stakeholder]
Initial impression: [Quick/Medium/Large]

STEP 1: SCOPE CLARITY
Q: Are requirements clear and documented?
A: [Yes/No] - [Reference to PROJECT_REQUIREMENTS.md]

Q: Are success criteria measurable?
A: [Yes/No] - [Specific criteria]

Q: Is "out of scope" defined?
A: [Yes/No] - [What we're NOT building]

✅ PASS: Clear requirements, defined scope
⚠️ RISK: Vague requirements → Add 50% buffer
❌ FAIL: No requirements → Do project-intake first

STEP 2: TASK BREAKDOWN
Q: Can I break this into ≤1-day tasks?
A: [Yes/No] - [List of tasks]

Tasks:
1. [Task 1] - [Estimate hours]
2. [Task 2] - [Estimate hours]
3. [Task 3] - [Estimate hours]
...

Subtotal: [Sum of all tasks]
Buffer (20%): [20% of subtotal]
TOTAL: [Subtotal + buffer]

Apply 3x multiplier: [TOTAL × 3]
REALISTIC ESTIMATE: [Final days/weeks]

STEP 3: UNKNOWNS ASSESSMENT
Q: What are the known-unknowns?
A: [List items we know we need to figure out]

Q: How much research/investigation needed?
A: [Hours per unknown]

Q: What are potential unknown-unknowns?
A: [Risks we might hit]

Q: What's the confidence level?
A: High (90%) | Medium (70%) | Low (50%)

Unknown multiplier:
- High confidence: 1.0x (no adjustment)
- Medium confidence: 1.5x (50% buffer)
- Low confidence: 2.0x (100% buffer)

ADJUSTED ESTIMATE: [Realistic estimate × unknown multiplier]

STEP 4: DEPENDENCY CHECK
Q: What external dependencies exist?
A: [List: APIs, services, people, approvals, data]

Q: Are all dependencies available?
A: [Yes/No/Partially] - [Details]

Q: What's the risk of dependency delays?
A: High | Medium | Low

Dependency risk adjustment:
- Low risk: No adjustment
- Medium risk: Add 25%
- High risk: Add 50-100%

ADJUSTED ESTIMATE: [Previous estimate × dependency risk]

STEP 5: EFFORT REALITY CHECK
Q: Total estimated effort?
A: [X person-days]

Q: Available capacity?
A: [Y person-days] (account for meetings, email, other work)

Q: Ratio: Effort / Capacity
A: [Ratio]

Reality check:
- Ratio < 0.7: Feasible
- Ratio 0.7-1.0: Tight but doable
- Ratio > 1.0: NOT FEASIBLE without cutting scope

Q: Is this achievable with current capacity?
A: [Yes/No/Partial]

STEP 6: RISK ASSESSMENT
List all risks:
1. [Risk 1] - Likelihood: [H/M/L] - Impact: [H/M/L]
2. [Risk 2] - Likelihood: [H/M/L] - Impact: [H/M/L]

High-risk items (High likelihood + High impact):
- [Count]

If >2 high-risk items → Add 50% buffer or defer

FINAL ESTIMATE:
────────────────
Best case: [Optimistic]
Likely case: [Realistic]
Worst case: [Pessimistic]

Commitment: [What we promise to stakeholder]
Internal buffer: [Hidden buffer for unknowns]

CONFIDENCE: [%]

GO/NO-GO DECISION:
- [ ] GO: Achievable with acceptable risk
- [ ] NO-GO: Too risky, cut scope or defer
- [ ] CONDITIONAL: Go if [conditions met]
```

### Estimation Guidelines by Task Type

#### Feature Development
```
Base estimate formula:
- Requirements analysis: 10% of implementation time
- Implementation: [Your estimate]
- Unit tests: 50% of implementation time
- Integration tests: 25% of implementation time
- Documentation: 15% of implementation time
- Code review & fixes: 20% of implementation time
- Buffer: 20% of total

Total: Implementation × 2.4 × 3 (multiplier) = 7.2× initial estimate

Example:
"Implement authentication" - initial estimate: 8 hours
Reality: 8 × 2.4 × 3 = 57.6 hours = 7.2 days
```

#### Bug Fix
```
Base estimate formula:
- Reproduction: 1-2 hours
- Root cause analysis: 2-4 hours
- Fix: [Your estimate]
- Test fix: 100% of fix time
- Regression testing: 1-2 hours
- Documentation: 0.5 hours
- Buffer: 20%

Total: Fix × 3 + 8 hours overhead

Example:
"Fix deployment failure" - fix estimate: 2 hours
Reality: (2 × 3) + 8 = 14 hours = 2 days
```

#### Refactoring
```
Base estimate formula:
- Analysis: 20% of code time
- Refactoring: [Your estimate]
- Test updates: 50% of refactor time
- Validation: 25% of refactor time
- Documentation: 10% of refactor time
- Buffer: 25% (higher risk)

Total: Refactor × 2.3

Example:
"Refactor authentication service" - estimate: 16 hours
Reality: 16 × 2.3 = 36.8 hours = 4.6 days
```

#### Infrastructure/DevOps
```
Base estimate formula:
- Research/investigation: 25% of implementation
- Implementation: [Your estimate]
- Testing: 50% of implementation
- Documentation: 20% of implementation
- Rollback plan: 10% of implementation
- Buffer: 30% (high unknowns)

Total: Implementation × 2.65

Example:
"Set up CI/CD pipeline" - estimate: 8 hours
Reality: 8 × 2.65 = 21.2 hours = 2.6 days
```

### The Commitment Conversation

**When stakeholder asks "When will this be done?"**

```
WRONG RESPONSE:
"This will be quick, maybe a day or two"
→ Under-promise, over-deliver violated
→ Sets unrealistic expectations
→ Pressure to cut corners

RIGHT RESPONSE:
"Let me break this down to give you an accurate estimate.

[Run reality check process]

Based on my analysis:
- Core work: [X hours]
- Tests: [Y hours]
- Documentation: [Z hours]
- Buffer for unknowns: [B hours]
- Total: [T hours]

Realistic timeline:
- Best case: [Optimistic date]
- Likely case: [Realistic date] ← COMMIT TO THIS
- Worst case: [Pessimistic date]

I'm comfortable committing to [Likely case]. This includes:
✓ [Deliverable 1]
✓ [Deliverable 2]
✓ [Deliverable 3]

This does NOT include:
✗ [Out of scope item]
✗ [Future consideration]

Does this timeline work for you? If we need it sooner, we can reduce scope to [Core features only] and deliver in [Shorter timeline]."
```

### Warning Signs: When to Push Back

**Say NO or negotiate scope when:**

```
RED FLAGS:
1. "Need this by tomorrow" for multi-day work
   → Response: "Not possible without severe quality issues. Minimum [X days]. Can we prioritize features?"

2. "Just a quick change" that touches core logic
   → Response: "Core logic requires careful testing. Real estimate: [X days]."

3. "Add this while you're at it" (scope creep)
   → Response: "That's a separate feature. Current scope is [X]. New feature would add [Y] time."

4. Vague requirements with tight deadline
   → Response: "Need 2 hours to clarify requirements before I can commit. Otherwise high risk of building wrong thing."

5. Multiple high-risk unknowns
   → Response: "Too many unknowns for reliable estimate. Need [X] day spike to investigate, then re-estimate."

6. Dependencies outside our control
   → Response: "This depends on [Team/System]. I can commit to our part in [X] days, but total timeline depends on [Dependency]."
```

**Pushing Back Diplomatically:**
```
STAKEHOLDER: "Can you finish this by Friday?"

BAD RESPONSE: "No, impossible."

GOOD RESPONSE:
"I want to set realistic expectations. Based on similar work:
- Task A: 2 days
- Task B: 1.5 days
- Task C: 1 day
- Testing & review: 1 day
Total: 5.5 days

Friday is 3 days away. We have three options:

Option 1: Full scope, realistic timeline (Next Wednesday)
→ All features, properly tested, documented

Option 2: Core scope, Friday deadline (Tight)
→ Core features only [List], deferred [List]
→ No buffer, higher risk

Option 3: MVP, Thursday (Safe)
→ Bare minimum [List]
→ Iterate after

Which option fits your priorities?"
```

## Integration with Other Skills

**Use AFTER these skills:**
- `project-intake` - Requirements must be clear before estimating
- `scope-guard` - Scope must be locked before estimating
- `architecture-review` - Architecture affects estimation

**Use BEFORE these skills:**
- `sprint-planner` - Can't plan sprint without realistic estimates
- `tdd-helper` - Estimates include testing time
- Development - Don't commit to timeline without reality check

**Chain with:**
1. `project-intake` → Define requirements
2. `scope-guard` → Lock scope
3. `architecture-review` → Design solution
4. `reality-check` (this skill) → Estimate effort
5. Then: Commit to timeline and start implementation

## Success Indicators

**You've used this skill successfully if:**
1. ✅ Break work into ≤1-day tasks with estimates
2. ✅ Apply 3x multiplier to initial estimates
3. ✅ Identify and quantify unknowns
4. ✅ Document best/likely/worst case scenarios
5. ✅ Commit to realistic timelines (not optimistic)
6. ✅ Actual time within 25% of estimate

**You've failed to use this skill if:**
1. ❌ "This will be quick" without breakdown
2. ❌ Commit to timeline without analysis
3. ❌ Consistently 2-3x over estimate
4. ❌ Surprise delays due to missed dependencies
5. ❌ Cut corners to meet unrealistic deadline
6. ❌ Pressure to "just get it done" without time for quality

## Real-World Example: HotSwap.Distributed

### Without Reality Check (What Happened)
```
REQUEST: "Build hot-swap kernel deployment system"

CLAUDE'S THINKING:
"Sure, this will be straightforward. I'll build:
- Deployment system
- Multi-tenancy (enterprise feature)
- Message routing (best practice)
- Schema registry (proper architecture)
- Cost tracking (useful feature)
- Website management (nice to have)
- Knowledge graph (interesting!)

Estimated time: A few days"

ACTUAL RESULT:
- 26,750 lines of code (3x expected)
- 38,237 lines of docs (trying to explain it all)
- 3 weeks+ of work
- 60% scope creep
- Unclear product
- 6/10 quality

LESSON: No reality check → Over-commitment → Failed execution
```

### With Reality Check (What Should Have Happened)
```
REQUEST: "Build hot-swap kernel deployment system"

CLAUDE'S RESPONSE:
"Let me run a reality check before committing.

[Run Reality Check Process]

STEP 1: Scope Clarity
Requirements: [Review PROJECT_REQUIREMENTS.md]
Core: Deployment strategies, health monitoring, rollback
Out of scope: Multi-tenancy, billing, website management

STEP 2: Task Breakdown
1. Domain models - 4 hours
2. Deployment strategies (4 types) - 16 hours
3. Health monitoring - 4 hours
4. API endpoints - 8 hours
5. Tests (50% of implementation) - 16 hours
6. Documentation - 6 hours
7. Deployment setup - 4 hours
8. Buffer (20%) - 12 hours
Subtotal: 70 hours

STEP 3: Apply 3x Multiplier
70 × 3 = 210 hours = 26 days = 5-6 weeks

STEP 4: Unknowns
- Integration with existing systems (Known-Unknown) - Add 25%
- Edge cases in distributed deployment (Unknown-Unknown) - Add 25%
Adjusted: 210 × 1.5 = 315 hours = 39 days = 8 weeks

STEP 5: Capacity Check
Available capacity: 1 developer, 6 hours/day effective
8 weeks × 5 days × 6 hours = 240 hours
Ratio: 315 / 240 = 1.3 → NOT FEASIBLE

OPTIONS:
A) Reduce scope to core features → 5 weeks (feasible)
B) Add developer → 4 weeks (if possible)
C) Accept 8-week timeline

RECOMMENDATION: Option A (Core features only)
- 4 deployment strategies
- Health & rollback
- Basic monitoring
- NO: Multi-tenancy, routing, schemas, websites

Revised estimate: 5 weeks
Commitment: 6 weeks (with buffer)

Does this work?"

RESULT:
- Realistic timeline communicated upfront
- Scope negotiated based on capacity
- Core features delivered on time
- No surprise delays
- High quality
```

## Skill Invocation

```bash
# Use this skill for effort estimation
/reality-check

# Or manually:
# 1. Break work into ≤1-day tasks
# 2. Sum estimates and apply 3x multiplier
# 3. Identify unknowns and add buffer
# 4. Check dependencies
# 5. Compare to capacity
# 6. Commit to realistic timeline
```

---

**Remember: It's better to under-promise and over-deliver than to over-promise and disappoint. Be realistic, not optimistic.**
