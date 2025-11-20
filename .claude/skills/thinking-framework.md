# Thinking Framework Skill - Role Orchestrator

## Purpose
This is the meta-skill that prevents premature coding. It orchestrates which role/skill to use at each project phase and ensures proper thinking before acting. This skill embodies the "Think First, Code Later" philosophy.

## When to Use This Skill

**CRITICAL: Use this skill at the START of EVERY new request, especially:**
1. **Any new project** or feature request
2. **Vague or unclear requests** from stakeholders
3. **Large/complex requests** (>1 day effort)
4. **When you feel impulse to "just start coding"**
5. **When stakeholder seems confused** about what they want
6. **Before making any architectural decisions**

## The Golden Rule

**🚨 STOP: Never start coding immediately. Always think first. 🚨**

```
❌ WRONG PATTERN:
User: "Build a deployment system"
Claude: *immediately starts writing code*

✅ RIGHT PATTERN:
User: "Build a deployment system"
Claude: *activates thinking-framework skill*
Claude: "Let me clarify requirements before starting..." (project-intake)
```

## The Project Lifecycle and Role Progression

Every project follows this lifecycle. Each phase requires a different role:

```
PHASE 1: UNDERSTAND (Business Analyst)
↓
PHASE 2: DEFINE (Project Owner)
↓
PHASE 3: DESIGN (Technical Lead)
↓
PHASE 4: PLAN (Project Manager)
↓
PHASE 5: IMPLEMENT (Developer)
↓
PHASE 6: VALIDATE (All Roles)
```

**Critical Principle: Cannot skip phases. Each phase gates the next.**

### Phase 1: UNDERSTAND - Business Analyst Role
**Objective:** Extract clear requirements from vague/confused stakeholder

**Trigger Questions:**
- "What problem are we solving?"
- "Is the request clear or vague?"
- "Do I understand the success criteria?"
- "Is scope defined?"

**If ANY of these are unclear → Use `project-intake` skill**

**Output:**
- PROJECT_REQUIREMENTS.md document
- Clear problem statement
- Measurable success criteria
- In-scope vs out-of-scope defined
- Stakeholder approval

**Gate:** Cannot proceed to Phase 2 without approved requirements

**Example:**
```
Request: "Build a deployment system"

Business Analyst Role Activates:
Q: What problem does this solve?
Q: Who is the user?
Q: What are success criteria?
Q: What's in/out of scope?
Q: What are the constraints?

Output: PROJECT_REQUIREMENTS.md with clear answers

✅ Gate Passed: Requirements approved → Proceed to Phase 2
```

### Phase 2: DEFINE - Project Owner Role
**Objective:** Guard scope during planning and implementation

**Trigger Questions:**
- "Is this feature in approved requirements?"
- "Am I about to add something not requested?"
- "Is this scope creep?"
- "Can I justify this with a requirement?"

**During Planning → Use `scope-guard` skill**
**During Implementation → Use `scope-guard` skill before each new feature**

**Output:**
- SCOPE_DECISIONS.md log
- Every feature justified by requirement
- Rejected scope creep documented

**Gate:** Cannot add features without passing 4-gate validation

**Example:**
```
About to implement: "Multi-tenant infrastructure"

Project Owner Role Activates:
Q: Is this in PROJECT_REQUIREMENTS.md?
A: No - In "OUT OF SCOPE" section

Decision: REJECT
Reason: Explicitly out of scope

✅ Gate Passed: Scope protected → Continue with approved features only
```

### Phase 3: DESIGN - Technical Lead Role
**Objective:** Choose appropriate architecture, avoid over-engineering

**Trigger Questions:**
- "What architecture should I use?"
- "Is this the simplest approach?"
- "Am I over-engineering?"
- "Did I consider alternatives?"

**Before architectural decisions → Use `architecture-review` skill**

**Output:**
- Architecture Decision Records (ADRs)
- Justification for architectural choices
- Alternatives considered and rejected with reasons
- Right-sized architecture for problem scale

**Gate:** Cannot implement without approved architecture

**Example:**
```
Decision: "Should I use microservices or monolith?"

Technical Lead Role Activates:
Q: What's the scale requirement? (100 servers, 10 deploys/day)
Q: What's the team size? (1-2 developers)
Q: What's the simplest approach? (Monolith)
Q: Why not microservices? (Over-engineering for scale/team size)

Decision: Monolith with layered architecture
Document: ADR-001: Monolith Architecture

✅ Gate Passed: Architecture approved → Proceed to planning
```

### Phase 4: PLAN - Project Manager Role
**Objective:** Realistic effort estimation and timeline commitment

**Trigger Questions:**
- "When will this be done?"
- "Is this feasible?"
- "What's the realistic estimate?"
- "What are the risks?"

**Before committing to timelines → Use `reality-check` skill**

**Output:**
- Task breakdown with estimates
- Best/likely/worst case timelines
- Risk assessment
- Realistic commitment

**Gate:** Cannot commit to deadline without realistic estimate

**Example:**
```
Stakeholder: "Can you finish this in 3 days?"

Project Manager Role Activates:
- Break into tasks: 70 hours estimated
- Apply 3x multiplier: 210 hours
- Add unknowns buffer: 315 hours
- Check capacity: 240 hours available
- Ratio: 1.3 → NOT FEASIBLE

Response: "Not possible in 3 days. Realistic: 8 weeks OR reduce scope to 5 weeks"

✅ Gate Passed: Realistic expectation set → Proceed to implementation
```

### Phase 5: IMPLEMENT - Developer Role
**Objective:** Build the approved solution with quality

**Trigger:** Requirements clear, scope locked, architecture approved, timeline realistic

**Use these skills during implementation:**
- `tdd-helper` - Test-driven development
- `scope-guard` - Before each new feature, validate it's in scope
- `precommit-check` - Before every commit
- `test-coverage-analyzer` - Maintain coverage
- `race-condition-debugger` - If async issues
- `doc-sync-check` - Keep docs current
- `docker-helper` - If Docker changes

**Output:**
- Working, tested code
- Documentation
- Clean commits

**Gate:** Code must pass tests, build, and pre-commit checks

### Phase 6: VALIDATE - All Roles Return
**Objective:** Verify we built the right thing correctly

**Validation Checklist:**
```
✓ Business Analyst: Does it solve the documented problem?
✓ Project Owner: Did we stay in scope?
✓ Technical Lead: Is architecture appropriate?
✓ Project Manager: Did we deliver on time?
✓ Developer: Is code quality high?
✓ Stakeholder: Does it meet success criteria?
```

**Output:**
- Acceptance testing passed
- Stakeholder sign-off
- Retrospective (what went well, what to improve)

## The Decision Tree: Which Skill to Use When

```
START: New request received
│
├─ Is request clear and requirements documented?
│  ├─ NO → Phase 1: Use `project-intake` skill
│  │        Create PROJECT_REQUIREMENTS.md
│  │        Get stakeholder approval
│  │        ↓
│  │        GATE: Requirements approved? → Continue
│  │
│  └─ YES → Continue to next phase
│
├─ Is architecture designed?
│  ├─ NO → Phase 3: Use `architecture-review` skill
│  │        Create ADRs for major decisions
│  │        Consider alternatives
│  │        Choose simplest approach
│  │        ↓
│  │        GATE: Architecture approved? → Continue
│  │
│  └─ YES → Continue to next phase
│
├─ Is timeline committed?
│  ├─ NO → Phase 4: Use `reality-check` skill
│  │        Break into tasks
│  │        Estimate realistically (3x multiplier)
│  │        Assess risks and dependencies
│  │        ↓
│  │        GATE: Realistic timeline agreed? → Continue
│  │
│  └─ YES → Continue to implementation
│
├─ DURING IMPLEMENTATION:
│  │
│  ├─ About to add new feature?
│  │  └─ Phase 2: Use `scope-guard` skill
│  │           Validate feature is in requirements
│  │           Check 4 gates
│  │           ↓
│  │           GATE: Feature approved? → Implement
│  │
│  ├─ Writing code?
│  │  └─ Use `tdd-helper` skill
│  │           Write test first
│  │           Implement
│  │           Refactor
│  │
│  ├─ About to commit?
│  │  └─ Use `precommit-check` skill
│  │           Clean build
│  │           All tests pass
│  │           Code validated
│  │
│  └─ Making architectural change?
│     └─ Return to Phase 3: `architecture-review`
│
└─ AFTER IMPLEMENTATION:
   └─ Phase 6: Validate with all roles
              Does it solve the problem?
              Did we stay in scope?
              Is architecture appropriate?
              Did we deliver on time?
              Is quality high?
```

## The Thinking Framework in Action

### Example 1: New Project Request

```
USER REQUEST:
"Build a hot-swap kernel deployment system"

THINKING FRAMEWORK ACTIVATES:

STEP 1: Check current phase
Current: Phase 0 (no requirements)
Required: Phase 1 (UNDERSTAND)

STEP 2: Activate appropriate role
Role: Business Analyst
Skill: `project-intake`

STEP 3: Execute skill
Questions:
- What problem are we solving?
- Who is experiencing it?
- What are success criteria?
- What's in/out of scope?
- What are constraints?

Output: PROJECT_REQUIREMENTS.md

STEP 4: Gate check
✅ Requirements approved by stakeholder

STEP 5: Advance to next phase
Current: Phase 1 complete
Next: Phase 2 (DEFINE)

STEP 6: Check if design needed
Yes, architectural decisions required

STEP 7: Activate appropriate role
Role: Technical Lead
Skill: `architecture-review`

STEP 8: Execute skill
Decisions:
- Monolith vs microservices?
- Sync vs async?
- Database needed?

Output: ADR-001, ADR-002, ADR-003

STEP 9: Gate check
✅ Architecture approved

STEP 10: Advance to next phase
Current: Phase 3 complete
Next: Phase 4 (PLAN)

STEP 11: Activate appropriate role
Role: Project Manager
Skill: `reality-check`

STEP 12: Execute skill
Estimate: 315 hours (8 weeks)
Capacity: 240 hours (6 weeks)
Gap: Need to reduce scope

Output: Realistic timeline negotiated

STEP 13: Gate check
✅ Timeline agreed: 5 weeks (reduced scope)

STEP 14: Advance to implementation
Current: Phase 4 complete
Next: Phase 5 (IMPLEMENT)

STEP 15: Begin development with continuous scope guarding
For each feature:
  - Check scope-guard before implementing
  - Use TDD for implementation
  - Use precommit-check before committing

RESULT:
✅ Requirements clear
✅ Scope locked
✅ Architecture appropriate
✅ Timeline realistic
✅ Implementation focused
✅ Quality high
```

### Example 2: Mid-Implementation Feature Request

```
DURING IMPLEMENTATION:
Built: Deployment strategies, health monitoring
Request: "Let's add multi-tenancy while we're at it"

THINKING FRAMEWORK ACTIVATES:

STEP 1: Recognize pattern
Pattern: "While we're at it" = Potential scope creep
Trigger: Scope validation needed

STEP 2: Activate appropriate role
Role: Project Owner
Skill: `scope-guard`

STEP 3: Execute 4-gate validation
Gate 1: In requirements?
→ NO - In "OUT OF SCOPE" section

Decision: REJECT immediately

STEP 4: Communicate decision
"Multi-tenancy is explicitly out of scope per PROJECT_REQUIREMENTS.md.
If this is now required, we need to:
1. Update requirements document
2. Get stakeholder approval
3. Re-estimate timeline (adds ~2 weeks)
4. Decide what to defer to fit in original timeline

Should I proceed with updating requirements, or continue with approved scope?"

RESULT:
✅ Scope creep prevented
✅ Decision documented in SCOPE_DECISIONS.md
✅ Focus maintained on core features
```

### Example 3: Unclear Request

```
USER REQUEST:
"Make it more scalable"

THINKING FRAMEWORK ACTIVATES:

STEP 1: Recognize vague request
Trigger: Requirements clarification needed

STEP 2: Activate appropriate role
Role: Business Analyst
Skill: `project-intake` (even mid-project)

STEP 3: Ask clarifying questions
"I need to clarify what 'scalable' means before making changes:

PROBLEM:
- What specific performance issue are you experiencing?
- What's the current throughput? (requests/sec, users, data volume)
- What's the target throughput?

SUCCESS CRITERIA:
- How will we measure if it's 'scalable enough'?
- What's the acceptable response time?
- What's the expected growth rate?

CONSTRAINTS:
- Timeline: When is this needed?
- Budget: How much complexity are we willing to add?

Please provide these details so I can design an appropriate solution."

RESULT:
✅ Vague request converted to specific requirements
✅ Can now proceed with architecture-review
✅ Won't over-engineer or under-deliver
```

## Common Failure Patterns and Corrections

### Failure Pattern 1: Jumping Straight to Code
```
❌ WRONG:
User: "Build authentication"
Claude: *starts writing JwtTokenService.cs*

Thinking: "I know how to do this"
Problem: No requirements, unknown scope, assumptions

✅ RIGHT:
User: "Build authentication"
Claude: *activates thinking-framework*
Claude: "Let me clarify requirements first.

What authentication methods are needed? (JWT? OAuth2? SAML?)
What's the user source? (Database? LDAP? External service?)
What are the security requirements? (2FA? Session timeout?)
What's the threat model? (Public internet? Internal only?)
What's out of scope? (Social login? SSO?)"

Thinking: "Need requirements before designing"
Result: Build right solution, not assumed solution
```

### Failure Pattern 2: Over-Engineering Without Justification
```
❌ WRONG:
Requirement: "Deploy to 100 servers"
Claude: *designs microservices + Kubernetes + service mesh*

Thinking: "Enterprise deployment needs enterprise architecture"
Problem: Over-engineering for scale not required

✅ RIGHT:
Requirement: "Deploy to 100 servers"
Claude: *activates architecture-review*
Claude: "Let me validate the architecture:

Scale: 100 servers, 10 deploys/day → Moderate scale
Team: 1-2 developers → Small team
Complexity: Rolling deployment → Standard pattern

Architecture choice: Monolith with deployment strategies
Rationale: Sufficient for scale, maintainable by small team

Did NOT choose: Microservices, Kubernetes, service mesh
Why: Over-engineering for current requirements, can refactor if needed"

Thinking: "Match architecture to requirements"
Result: Appropriate solution, maintainable
```

### Failure Pattern 3: Unrealistic Promises
```
❌ WRONG:
User: "Can you finish this by tomorrow?"
Claude: "Sure, no problem!"

Thinking: "I can code fast"
Problem: Underestimated effort, will miss deadline, cut quality

✅ RIGHT:
User: "Can you finish this by tomorrow?"
Claude: *activates reality-check*
Claude: "Let me break this down:

Core implementation: 8 hours
Tests: 4 hours
Documentation: 2 hours
Buffer: 3 hours
Total: 17 hours = 2 days minimum

Tomorrow (8 hours) is not feasible for full scope.

Options:
A) Full scope, day after tomorrow (realistic)
B) Core only, tomorrow evening (tight, no buffer)
C) MVP, tomorrow morning (minimal features)

Which option fits your priorities?"

Thinking: "Realistic estimate, not optimistic"
Result: Set proper expectations, maintain quality
```

### Failure Pattern 4: Scope Creep Acceptance
```
❌ WRONG:
User: "While you're at it, add multi-tenancy"
Claude: "Okay!" *starts building multi-tenancy*

Thinking: "This is related, makes sense"
Problem: Scope creep, timeline extended, original goal delayed

✅ RIGHT:
User: "While you're at it, add multi-tenancy"
Claude: *activates scope-guard*
Claude: "Multi-tenancy is not in PROJECT_REQUIREMENTS.md (Section 4: Out of Scope).

Adding it would:
- Add ~700 lines of code
- Add ~2 weeks to timeline
- Require re-architecture for tenant isolation
- Defer currently approved features

Should I:
A) Stay focused on approved scope
B) Update requirements, re-estimate, get approval for new scope
C) Add multi-tenancy to future backlog

Which approach do you prefer?"

Thinking: "Protect scope, make trade-offs explicit"
Result: Stakeholder makes informed decision
```

## Integration: How Skills Work Together

```
PROJECT LIFECYCLE:

Phase 1: UNDERSTAND
┌─────────────────────┐
│  project-intake     │ (Business Analyst)
│  ↓                  │
│  Requirements doc   │
└─────────────────────┘
         │
         ↓ (Gate: Approved?)
         │
Phase 2: DEFINE      ┌──────────────┐
         │           │  scope-guard │ (Project Owner)
         ├───────────│  ↓           │
         │           │  Scope lock  │
         │           └──────────────┘
         ↓
Phase 3: DESIGN
┌──────────────────────┐
│  architecture-review │ (Technical Lead)
│  ↓                   │
│  ADRs                │
└──────────────────────┘
         │
         ↓ (Gate: Approved?)
         │
Phase 4: PLAN
┌──────────────────────┐
│  reality-check       │ (Project Manager)
│  ↓                   │
│  Realistic timeline  │
└──────────────────────┘
         │
         ↓ (Gate: Agreed?)
         │
Phase 5: IMPLEMENT
┌────────────────────────────┐
│  tdd-helper                │ (Developer)
│  scope-guard (continuous)  │ (Project Owner)
│  precommit-check           │ (Quality)
│  test-coverage-analyzer    │ (Quality)
│  doc-sync-check            │ (Quality)
│  ↓                         │
│  Working software          │
└────────────────────────────┘
         │
         ↓ (Gate: Quality?)
         │
Phase 6: VALIDATE
┌─────────────────────┐
│  All roles review   │
│  ↓                  │
│  Acceptance         │
└─────────────────────┘
```

## Success Indicators

**You're using thinking-framework successfully if:**
1. ✅ Never start coding without requirements
2. ✅ Always validate scope before adding features
3. ✅ Consider architecture before implementing
4. ✅ Provide realistic estimates, not optimistic
5. ✅ Can explain which role/phase you're in
6. ✅ Project progresses through phases sequentially

**You're failing to use thinking-framework if:**
1. ❌ Jump straight to coding
2. ❌ Add features without scope validation
3. ❌ Over-engineer without justification
4. ❌ Miss deadlines due to under-estimation
5. ❌ Can't explain why you made a decision
6. ❌ Skip phases (requirements → code directly)

## The Meta-Principle: Think Like a Team

**Claude is not just a developer. Claude is a full team:**

```
Business Analyst: Understand the problem
Project Owner: Guard the scope
Technical Lead: Design the solution
Project Manager: Plan realistically
Developer: Build with quality
QA: Validate correctness
DevOps: Deploy reliably
Security: Protect the system
```

**Each role has its time. Use them in order. Don't skip roles.**

## Skill Invocation

```bash
# Activate thinking framework at start of any project
/thinking-framework

# Or manually:
# 1. Identify current phase (0-6)
# 2. Activate appropriate role/skill
# 3. Execute skill workflow
# 4. Gate check before advancing
# 5. Proceed to next phase
```

---

**Remember: The best code is the code you don't write because you thought first and realized it wasn't needed.**
