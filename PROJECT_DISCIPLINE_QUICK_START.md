# Project Discipline Skills - Quick Start Guide

**Created:** 2025-11-20
**Purpose:** Quick reference for the 5 new project discipline skills that prevent over-engineering, scope creep, and failed projects

---

## The Problem These Skills Solve

The HotSwap.Distributed project was intentionally built WITHOUT these skills to test what happens when Claude builds autonomously without project discipline.

**Result:**
- ❌ 60% scope creep (12 of 20 features unplanned)
- ❌ 26,750 lines of code (3x needed)
- ❌ 38,237 lines of docs (trying to explain the mess)
- ❌ Unclear product identity
- ❌ 6/10 quality score

**Root Cause:** No requirements clarification, no scope validation, no architecture review, no realistic estimation, jumped straight to coding.

---

## The Solution: 5 Project Discipline Skills

These skills codify proper software project management into reusable workflows:

| Skill | Role | When | Prevents |
|-------|------|------|----------|
| [thinking-framework](#1-thinking-framework) | Meta-Orchestrator | **EVERY new request** | Premature coding |
| [project-intake](#2-project-intake) | Business Analyst | Start of project | Building wrong thing |
| [scope-guard](#3-scope-guard) | Project Owner | During implementation | Feature creep |
| [architecture-review](#4-architecture-review) | Technical Lead | Before design | Over-engineering |
| [reality-check](#5-reality-check) | Project Manager | Before timelines | Over-commitment |

---

## How They Work Together

```
NEW REQUEST
     ↓
[1] thinking-framework ← Meta-skill (starts here)
     ↓
     Are requirements clear?
     ├─ NO → [2] project-intake (Business Analyst)
     │       ↓
     │       Create PROJECT_REQUIREMENTS.md
     │       Get approval
     │       ↓
     └─ YES → Continue
             ↓
     Is architecture designed?
     ├─ NO → [4] architecture-review (Technical Lead)
     │       ↓
     │       Create ADRs
     │       Choose simplest approach
     │       ↓
     └─ YES → Continue
             ↓
     Is timeline estimated?
     ├─ NO → [5] reality-check (Project Manager)
     │       ↓
     │       Break into tasks
     │       Apply 3x multiplier
     │       Realistic estimate
     │       ↓
     └─ YES → START IMPLEMENTATION
                     ↓
             [3] scope-guard (continuous)
                 ↓
             Before each new feature:
             - Validate in requirements
             - Check 4 gates
             - Reject if not approved
```

---

## 1. thinking-framework

**File:** `.claude/skills/thinking-framework.md` (9.5K)
**Role:** Meta-Orchestrator
**Use:** **EVERY new project or request**

### Quick Summary
The master skill that prevents jumping straight to code. Routes you to the right role at each project phase.

### The 6-Phase Lifecycle
1. **UNDERSTAND** → Business Analyst (`project-intake`)
2. **DEFINE** → Project Owner (`scope-guard`)
3. **DESIGN** → Technical Lead (`architecture-review`)
4. **PLAN** → Project Manager (`reality-check`)
5. **IMPLEMENT** → Developer (TDD, coding)
6. **VALIDATE** → All roles review

**Golden Rule:** Cannot skip phases. Each phase gates the next.

### When to Use
- 🚨 **EVERY** new project or feature request
- When you feel impulse to "just start coding"
- When request is vague/unclear
- Before making architectural decisions

### Example
```
User: "Build a deployment system"

❌ WRONG: *immediately starts coding*

✅ RIGHT:
- Activate thinking-framework
- Identify phase: Phase 0 (no requirements)
- Route to: project-intake (Business Analyst)
- Clarify requirements first
- Then proceed through phases sequentially
```

---

## 2. project-intake

**File:** `.claude/skills/project-intake.md` (10K)
**Role:** Business Analyst
**Use:** At VERY START of any project

### Quick Summary
Extracts clear requirements from vague/confused stakeholders BEFORE any code is written.

### The Five Critical Questions
1. **PROBLEM STATEMENT:** What problem are we solving?
2. **SUCCESS CRITERIA:** How will we know we've solved it?
3. **CONSTRAINTS & ASSUMPTIONS:** What are the limits?
4. **SCOPE BOUNDARIES:** What's in/out of scope?
5. **RISK ASSESSMENT:** What could go wrong?

### Workflow
1. Ask clarifying questions (don't assume)
2. Document in PROJECT_REQUIREMENTS.md
3. Get stakeholder approval
4. **GATE:** Cannot proceed without approval

### Example
```
Request: "Build authentication"

❌ WRONG:
*starts building JWT + OAuth2 + SAML + 2FA*

✅ RIGHT:
"Let me clarify requirements first:
- What auth methods needed? (JWT? OAuth2? SAML?)
- Who are the users? (Internal? External?)
- What's the threat model?
- What's explicitly OUT of scope?"

Document answers → Get approval → Then design
```

---

## 3. scope-guard

**File:** `.claude/skills/scope-guard.md` (8.5K)
**Role:** Project Owner
**Use:** DURING implementation, before adding ANY new feature

### Quick Summary
Prevents feature creep using 4-Gate Validation. Every feature must pass ALL four gates or be rejected.

### The 4 Gates
**Gate 1:** Requirements Justification
- Is this in PROJECT_REQUIREMENTS.md?

**Gate 2:** Complexity Justification
- Does complexity justify value?
- Score = (LOC × Maintenance) / Value
- Target: < 1.0

**Gate 3:** Dependency Justification
- Is new dependency necessary?

**Gate 4:** Maintenance Justification
- Are we willing to maintain this forever?

### Example
```
About to add: "Multi-tenancy"

Checkpoint:
- Gate 1: In requirements? → NO (in OUT_OF_SCOPE)
- Decision: REJECT immediately

Log in SCOPE_DECISIONS.md
Continue with approved features only
```

---

## 4. architecture-review

**File:** `.claude/skills/architecture-review.md` (10K)
**Role:** Technical Lead
**Use:** AFTER requirements, BEFORE implementation

### Quick Summary
Ensures architecture matches requirements. Prevents over-engineering using KISS principle.

### The Three Principles
1. **Right-Sized Architecture** - Match complexity to scale
2. **Vertical Slice Over Horizontal** - Organize by features
3. **YAGNI** - Don't build for hypothetical future

### Architecture Review Checklist
1. Problem-Architecture Fit
2. Scale Appropriateness
3. Team Capability
4. YAGNI Validation
5. Alternatives Considered

**Output:** Architecture Decision Records (ADRs)

### Example
```
Question: "Should I use microservices?"

Analysis:
- Scale: 100 servers, 10 deploys/day
- Team: 1-2 developers
- Complexity: Rolling deployment

Answer: NO
- Monolith is sufficient for scale
- Small team can't maintain microservices
- Unnecessary operational complexity

Decision: Monolith with layered architecture
Document: ADR-001: Monolith Architecture
```

---

## 5. reality-check

**File:** `.claude/skills/reality-check.md` (8K)
**Role:** Project Manager
**Use:** AFTER architecture, BEFORE committing to timelines

### Quick Summary
Prevents unrealistic promises using scientific estimation methods.

### The Three Estimation Principles
1. **Hofstadter's Law** - Apply 3x multiplier to all estimates
2. **Break Down and Sum Up** - Large estimates always wrong
3. **Identify Unknowns** - Add buffers for unknowns

### Reality Check Process
1. Break into ≤1-day tasks
2. Sum estimates
3. Apply 3x multiplier
4. Add unknowns buffer (50-100%)
5. Check vs capacity
6. Commit to realistic timeline

### Example
```
Stakeholder: "Can you finish this in 3 days?"

Analysis:
- Tasks: 70 hours estimated
- 3x multiplier: 210 hours
- Unknowns: +50% = 315 hours
- Capacity: 240 hours (3 days × 8 hours × 10 days)
- Ratio: 315/240 = 1.3 → NOT FEASIBLE

Response: "Not possible in 3 days. Options:
A) Full scope: 8 weeks (realistic)
B) Core only: 5 weeks (reduced scope)
C) MVP: 2 weeks (minimal features)

Which fits your priorities?"
```

---

## Real-World Comparison

### Without These Skills (What Happened)
```
Request: "Build deployment system"
→ No requirements clarification
→ Assumed: multi-tenancy, billing, websites, plugins needed
→ No scope validation during implementation
→ No architecture review (built for enterprise scale)
→ No realistic estimation ("a few days")

Result:
- 26,750 lines of code
- 38,237 lines of docs
- 60% scope creep
- 3 weeks actual (vs "few days" estimate)
- 6/10 quality score
- Unclear product identity
```

### With These Skills (What Should Have Happened)
```
Request: "Build deployment system"

Phase 1: project-intake
→ Clarify: Deploy to 100 servers, rolling/blue-green strategies
→ Document: Core deployment only, NO multi-tenancy/billing
→ Approve: PROJECT_REQUIREMENTS.md signed off

Phase 2: scope-guard
→ About to add multi-tenancy? → REJECT (out of scope)
→ About to add billing? → REJECT (out of scope)
→ Stay focused on core deployment

Phase 3: architecture-review
→ Microservices? → NO (monolith sufficient for 100 servers)
→ Message queue? → NO (sync deployment is fine)
→ Decision: Monolith with 4 deployment strategies

Phase 4: reality-check
→ Estimate: 70 hours × 3 = 210 hours
→ With unknowns: 315 hours = 8 weeks
→ OR reduce scope to core: 5 weeks
→ Commit: 5 weeks with core features

Phase 5: Implementation with continuous scope-guard
→ Build only approved features
→ Validate each addition against requirements

Result:
- 8,000-10,000 lines of code (focused)
- 8,000 lines of docs (matches implementation)
- 0% scope creep
- 5 weeks (as estimated)
- 9/10 quality score
- Clear product identity
```

---

## Quick Start Checklist

**For EVERY new project or major feature:**

- [ ] 1. Activate `/thinking-framework` (meta-skill)
- [ ] 2. Run `/project-intake` if requirements unclear
- [ ] 3. Create PROJECT_REQUIREMENTS.md with:
  - [ ] Problem statement
  - [ ] Success criteria
  - [ ] In-scope vs out-of-scope
  - [ ] Constraints
- [ ] 4. Get stakeholder approval
- [ ] 5. Run `/architecture-review` before designing
- [ ] 6. Create ADRs for major decisions
- [ ] 7. Run `/reality-check` before committing timeline
- [ ] 8. Break into ≤1-day tasks, apply 3x multiplier
- [ ] 9. During implementation: `/scope-guard` before each feature
- [ ] 10. Validate feature is in PROJECT_REQUIREMENTS.md

**If you skip any step → High risk of failure**

---

## Common Mistakes to Avoid

### ❌ Mistake 1: Jumping Straight to Code
**Wrong:** User says "Build X" → Start coding
**Right:** User says "Build X" → Activate thinking-framework → Clarify requirements → Design → Estimate → Then code

### ❌ Mistake 2: Assuming Requirements
**Wrong:** "User wants authentication so I'll add OAuth2 + SAML + JWT + 2FA"
**Right:** "Let me ask: Which auth methods are needed? What's in scope?"

### ❌ Mistake 3: Accepting Scope Creep
**Wrong:** "While I'm at it, let me add multi-tenancy"
**Right:** "Multi-tenancy is out of scope per requirements. Should we update requirements first?"

### ❌ Mistake 4: Over-Engineering
**Wrong:** "Best practice says use microservices"
**Right:** "Scale requirement is 100 servers. Monolith is sufficient. ADR documents why."

### ❌ Mistake 5: Unrealistic Estimates
**Wrong:** "This will take a few days"
**Right:** "70 hours × 3 = 210 hours = 5 weeks realistically"

---

## Success Metrics

**You're using these skills successfully if:**
- ✅ Never start coding without approved requirements
- ✅ Can point to requirement for every feature
- ✅ Scope creep < 10%
- ✅ Architecture choices documented in ADRs
- ✅ Actual time within 25% of estimate
- ✅ Stakeholder says "You delivered exactly what I asked for"

**You're failing to use these skills if:**
- ❌ Jump straight to coding
- ❌ Build features not requested
- ❌ Scope creep > 25%
- ❌ Can't explain why architecture chosen
- ❌ Consistently miss deadlines by 2-3x
- ❌ Stakeholder says "This isn't what I wanted"

---

## Next Steps

1. **Read the full skills:**
   - `.claude/skills/thinking-framework.md`
   - `.claude/skills/project-intake.md`
   - `.claude/skills/scope-guard.md`
   - `.claude/skills/architecture-review.md`
   - `.claude/skills/reality-check.md`

2. **See full documentation:** `SKILLS.md` (updated with all skills)

3. **Learn from failure:** Review assessment reports:
   - `PROJECT_ASSESSMENT_EXECUTIVE_SUMMARY.md`
   - `PROJECT_ASSESSMENT_DETAILED_FILES.md`

4. **Practice:** Use `/thinking-framework` on next project request

---

**Remember: The best code is the code you don't write because you thought first and realized it wasn't needed.**
