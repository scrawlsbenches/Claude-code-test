# Architecture Review Skill - Technical Lead Role

## Purpose
This skill prevents over-engineering and ensures architecture matches requirements. It embodies the Technical Lead role to review designs before implementation and validate they're appropriate for the problem scale.

## When to Use This Skill

**CRITICAL: Use this skill AFTER requirements are clear but BEFORE writing implementation code.**

### Triggers (Use this skill when you're about to):
1. **Design system architecture** for a new project/feature
2. **Choose technology stack** (frameworks, databases, message queues)
3. **Create abstraction layers** (interfaces, strategies, factories)
4. **Make architectural decisions** (monolith vs microservices, sync vs async)
5. **Add infrastructure components** (databases, caches, queues, etc.)

### Red Flags (STOP and use this skill if you're thinking):
- 🚨 "Let's use microservices" (when monolith might suffice)
- 🚨 "Add a message queue" (when direct calls work)
- 🚨 "Need a schema registry" (when simple contracts work)
- 🚨 "Implement CQRS" (when CRUD is enough)
- 🚨 "Add event sourcing" (when state snapshots work)
- 🚨 "Make everything async" (when sync is simpler)

## Technical Lead Role: The KISS Architecture Framework

**KISS = Keep It Simple, Stupid**

**Principle:** Choose the simplest architecture that solves the problem.  Start simple, add complexity only when forced by real requirements.

### The Three Architecture Principles

#### Principle 1: Right-Sized Architecture
**Match architecture complexity to problem complexity**

```
Problem Complexity → Architecture Choice

Simple CRUD (1-10 entities):
→ Monolith + SQL database + REST API
→ DON'T: Microservices, event sourcing, CQRS

Medium Scale (10-50 entities, <100 req/sec):
→ Layered monolith + caching + background jobs
→ DON'T: Distributed systems, message queues (unless async requirement)

Large Scale (>50 entities, >100 req/sec, multiple teams):
→ Consider microservices, events, distributed patterns
→ BUT: Start with modular monolith first

Enterprise Scale (>1M users, >1000 req/sec, global):
→ Microservices, event-driven, CQRS, distributed caching
→ BUT: Only if you have team expertise and requirements justify
```

**Example - HotSwap.Distributed:**
```
PROBLEM: Deploy kernel modules to 100-500 servers with rolling strategy
SCALE: 10 deployments/day, single organization, 1-2 developers

RIGHT-SIZED ARCHITECTURE:
✅ Monolith API (single deployment)
✅ Direct deployment (no message queue)
✅ In-memory state (with optional Redis for HA)
✅ Simple HTTP API
✅ 4 deployment strategies (direct need)

OVER-ENGINEERED ARCHITECTURE (what was built):
❌ Message routing strategies (5 strategies, no requirement)
❌ Schema registry (enterprise pattern, not needed)
❌ Multi-tenancy (single org use case)
❌ Event-driven messaging (sync deployment is fine)
❌ Separate KnowledgeGraph system (unrelated)

RESULT: 3x more complexity than needed
```

#### Principle 2: Vertical Slice Over Horizontal Layers
**Organize by features, not by technical layers**

```
❌ WRONG (Horizontal Layers):
/Controllers
  - DeploymentsController
  - ClustersController
  - TenantsController
  - WebsitesController (wait, why websites?)
/Services
  - DeploymentService
  - TenantService
  - WebsiteService (scope creep!)
/Repositories
  - DeploymentRepo
  - TenantRepo
  - WebsiteRepo

Problem: Feature spread across 3+ locations, scope creep hidden

✅ RIGHT (Vertical Slices):
/Deployment (core feature)
  - DeploymentController
  - DeploymentService
  - DeploymentRepo
  - Tests
/Monitoring (core feature)
  - HealthController
  - MetricsService
  - Tests
/Tenants (wait, is this in scope?)
  - STOP: Check PROJECT_REQUIREMENTS.md first

Problem: Obvious when feature isn't in scope
```

**Benefits of Vertical Slices:**
- New features obvious (new folder = new feature)
- Scope creep visible (unplanned folder = question it)
- Features can be removed easily (delete folder)
- Testing focused (tests with feature)

#### Principle 3: YAGNI - You Aren't Gonna Need It
**Don't build for hypothetical future needs**

```
❌ YAGNI VIOLATIONS:
"Let me add multi-tenancy in case we go SaaS later"
→ Wait until you actually go SaaS (may never happen)

"Let me make this async in case we need to scale"
→ Wait until scale requirement appears (may never need it)

"Let me add caching layer in case performance becomes an issue"
→ Wait until you measure performance problem (premature optimization)

"Let me use microservices so we can scale independently"
→ Wait until you have multiple teams and actual scale needs

✅ YAGNI COMPLIANCE:
"Requirements say sync deployment, so I'll build sync"
→ If async is needed later, refactor then (requirements will be clearer)

"Requirements say single org, so I'll build for one"
→ If multi-tenancy is needed, add it when requirement is real

"Requirements say 100 servers, so I'll optimize for that"
→ If scale to 10,000 servers, refactor when need is proven
```

### The Architecture Review Checklist

**Run this checklist for every architectural decision:**

```
ARCHITECTURE REVIEW CHECKLIST:
─────────────────────────────────

Feature/System: [Name]
Requirements: [Reference to PROJECT_REQUIREMENTS.md section]

1. PROBLEM-ARCHITECTURE FIT
   Q: What problem does this architectural choice solve?
   A: [Specific problem from requirements]

   Q: Is there a simpler way to solve this?
   A: [Yes/No] - [If yes, what's the simpler way?]

   ✅ PASS: Simplest approach that solves the problem
   ❌ FAIL: Overengineered solution, simpler approach exists

2. SCALE APPROPRIATENESS
   Q: What's the expected scale? (users, requests, data)
   A: [From requirements constraints]

   Q: Does this architecture handle that scale?
   A: [Yes/No] - [Capacity analysis]

   Q: Does this architecture over-engineer for scale we don't need?
   A: [Yes/No] - [If yes, what's simpler approach for actual scale?]

   ✅ PASS: Matches required scale, not over-engineered
   ❌ FAIL: Built for 1M users when we have 100

3. TEAM CAPABILITY
   Q: Does team have expertise in this architecture?
   A: [Yes/No] - [Specific technologies]

   Q: Can team maintain this long-term?
   A: [Yes/No] - [Maintenance concerns]

   Q: Is this a learning experiment or production system?
   A: [Production/Learning]

   ✅ PASS: Team confident, production-appropriate
   ❌ FAIL: Experimental tech for critical system

4. YAGNI VALIDATION
   Q: Is this needed NOW for current requirements?
   A: [Yes/No] - [Requirement reference]

   Q: Or is this "in case we need it later"?
   A: [Now/Later]

   Q: If later: What's the cost to add it when actually needed?
   A: [Effort estimate - often less than cost of premature implementation]

   ✅ PASS: Needed now for current requirements
   ❌ FAIL: Building for hypothetical future

5. ALTERNATIVES CONSIDERED
   Q: What's the simplest approach?
   A: [Description]

   Q: What's the current approach?
   A: [Description]

   Q: Why not use simplest?
   A: [Justification - must be specific requirement, not assumption]

   ✅ PASS: Considered simpler alternatives, justified current approach
   ❌ FAIL: Didn't consider alternatives, or weak justification

DECISION: [APPROVE / REJECT / SIMPLIFY]
REASON: [Explanation]
ALTERNATIVE: [If rejected, what's the simpler approach?]
```

### Common Over-Engineering Patterns

#### Pattern 1: Premature Distribution
```
SYMPTOM: "Let's use microservices from day one"

WHY IT'S WRONG:
- Microservices add: network latency, distributed debugging, deployment complexity
- Martin Fowler: "Start with monolith, extract microservices when needed"
- Netflix, Amazon started with monoliths

WHEN TO USE MICROSERVICES:
✅ Multiple teams working independently
✅ Different parts need different scaling (proven bottleneck)
✅ Different deployment cycles required (proven need)
✅ Team has microservice expertise

WHEN NOT TO USE:
❌ "Best practice says microservices"
❌ Single team, single deployment
❌ No proven scaling bottleneck
❌ Complexity not justified by requirements

HotSwap.Distributed: ✅ Monolith is correct (single team, moderate scale)
```

#### Pattern 2: Premature Abstraction
```
SYMPTOM: "Let me create interfaces for everything"

WHY IT'S WRONG:
- Abstractions add indirection, harder to understand
- "Don't abstract until you have 2+ implementations" - Rule of Three

WHEN TO ABSTRACT:
✅ 2+ actual implementations exist or certain (e.g., InMemory + Redis)
✅ Dependency injection for testing (mock dependencies)
✅ Plugin architecture required by requirements

WHEN NOT TO ABSTRACT:
❌ "We might swap this later" (YAGNI)
❌ Single implementation, no second one planned
❌ Over-layering (IService → Service → IRepo → Repo → ICache → Cache)

HotSwap.Distributed:
✅ Good: IDeploymentStrategy interface (4 implementations needed)
❌ Bad: IMessageRouter (1 implementation, over-abstraction)
```

#### Pattern 3: Premature Async
```
SYMPTOM: "Everything should be async and event-driven"

WHY IT'S WRONG:
- Async adds: eventual consistency, harder debugging, more complex code
- Sync is simpler, easier to reason about, easier to debug

WHEN TO USE ASYNC:
✅ Long-running operations (>30 seconds)
✅ Fire-and-forget operations (notifications, logging)
✅ Scale requirement (millions of requests)
✅ UI responsiveness requirement

WHEN NOT TO USE:
❌ Operations complete in <1 second
❌ User needs immediate response
❌ "Best practice says event-driven" (not always)
❌ No actual performance requirement

HotSwap.Distributed:
✅ Sync is correct (deployments complete in seconds, user waits for result)
❌ Adding message queue for sync deployments is over-engineering
```

#### Pattern 4: Premature Optimization
```
SYMPTOM: "Let me add caching, sharding, and load balancing"

WHY IT'S WRONG:
- Donald Knuth: "Premature optimization is the root of all evil"
- Optimize when you measure performance problem, not before

WHEN TO OPTIMIZE:
✅ Measured performance problem (profiler data)
✅ User-reported slow performance
✅ Specific requirement (e.g., <100ms response time)
✅ Clear bottleneck identified

WHEN NOT TO OPTIMIZE:
❌ "This might be slow" (measure first)
❌ "At scale this won't work" (you're not at scale yet)
❌ "Best practice says cache everything" (cache when needed)

APPROACH:
1. Make it work (correct)
2. Make it testable (quality)
3. Make it clean (maintainable)
4. Make it fast (if measured problem) ← Most projects never reach this

HotSwap.Distributed:
✅ In-memory state is fine for 10 deployments/day
❌ Redis/distributed cache not needed until proven bottleneck
```

### Architecture Decision Record (ADR)

**Document every significant architectural decision:**

```markdown
# ADR-001: [Decision Title]

**Date**: [YYYY-MM-DD]
**Status**: Proposed | Accepted | Rejected | Superseded
**Deciders**: [Who made this decision]

## Context
What is the issue we're facing? What are the constraints?
[Describe problem, requirements reference, constraints]

## Decision
What architecture/approach did we choose?
[Describe decision]

## Consequences
What are the trade-offs? What does this enable? What does this prevent?

**Positive:**
- [Benefit 1]
- [Benefit 2]

**Negative:**
- [Cost 1]
- [Cost 2]

**Neutral:**
- [Trade-off 1]

## Alternatives Considered

### Alternative 1: [Name]
- **Pros**: [List]
- **Cons**: [List]
- **Why not chosen**: [Reason]

### Alternative 2: [Name]
- **Pros**: [List]
- **Cons**: [List]
- **Why not chosen**: [Reason]

## References
- PROJECT_REQUIREMENTS.md: [Section]
- External resources: [Links]
```

**Example ADR:**
```markdown
# ADR-001: Use Monolith Architecture for HotSwap.Distributed

**Date**: 2025-11-20
**Status**: Accepted
**Deciders**: Technical Lead (Claude), Stakeholder

## Context
Building hot-swap kernel deployment system for 100-500 servers, 10 deployments/day, single organization, 1-2 developers.

Requirements:
- Deploy kernel modules with rolling/blue-green/canary strategies
- Health monitoring and rollback
- Support 100-500 servers
- Single organization (no multi-tenancy requirement)

## Decision
Use monolithic ASP.NET Core API with layered architecture:
- API layer: REST controllers
- Orchestrator layer: Deployment logic
- Infrastructure layer: Cross-cutting concerns
- Domain layer: Models and contracts

## Consequences

**Positive:**
- Simple deployment (single binary)
- Easy debugging (all code in one process)
- Fast development (no distributed complexity)
- Low operational overhead (one app to monitor)

**Negative:**
- Can't scale parts independently (not a requirement)
- Single deployment (acceptable for 1-2 developers)

**Neutral:**
- Can extract to microservices later if needed (refactor when requirement emerges)

## Alternatives Considered

### Alternative 1: Microservices
- **Pros**: Independent scaling, independent deployment
- **Cons**: Network latency, distributed debugging, ops complexity
- **Why not chosen**: Single team, no independent scaling requirement, unnecessary complexity

### Alternative 2: Serverless (AWS Lambda/Azure Functions)
- **Pros**: Auto-scaling, pay-per-use
- **Cons**: Cold starts, state management complexity, vendor lock-in
- **Why not chosen**: Deployment is stateful process, cold starts unacceptable

## References
- PROJECT_REQUIREMENTS.md: Deployment System Requirements
- [Martin Fowler: Monolith First](https://martinfowler.com/bliki/MonolithFirst.html)
```

## Integration with Other Skills

**Use AFTER these skills:**
- `project-intake` - Requirements must be clear before architecture
- `scope-guard` - Scope must be validated before designing architecture

**Use BEFORE these skills:**
- `tdd-helper` - Architecture drives test structure
- `reality-check` - Architecture affects effort estimates
- Development - Don't code without approved architecture

**Chain with:**
1. `project-intake` → Define requirements
2. `scope-guard` → Validate scope
3. `architecture-review` (this skill) → Design solution
4. `reality-check` → Estimate effort
5. Then: Implement with TDD

## Success Indicators

**You've used this skill successfully if:**
1. ✅ Architecture Decision Records (ADRs) exist for major decisions
2. ✅ Architecture matches problem scale (not over/under-engineered)
3. ✅ Considered simpler alternatives and documented why not chosen
4. ✅ Team can maintain the architecture
5. ✅ Architecture solves current requirements, not hypothetical future
6. ✅ Can explain every architectural choice with specific requirement

**You've failed to use this skill if:**
1. ❌ Used microservices for single-team project
2. ❌ Added caching without measuring performance
3. ❌ Made everything async without performance requirement
4. ❌ Created abstractions with single implementation
5. ❌ Can't justify architectural choices with requirements
6. ❌ Architecture is "best practice" without fit-for-purpose validation

## Real-World Example: HotSwap.Distributed

### Without Architecture Review (What Happened)
```
DECISION: "Let's build enterprise-grade deployment system"

ARCHITECTURE CHOSEN:
- ✅ Monolith API (correct)
- ✅ Layered architecture (correct)
- ✅ 4 deployment strategies (correct)
- ❌ Message routing (5 strategies, no requirement)
- ❌ Schema registry (enterprise pattern, not needed)
- ❌ Multi-tenancy (single org use case)
- ❌ Separate KnowledgeGraph system (unrelated)

NO ADRs: No documentation of why these choices made
NO ALTERNATIVES: Didn't consider simpler approaches
NO VALIDATION: Didn't check if choices match requirements

RESULT: 3x more complex than needed, unclear why
```

### With Architecture Review (What Should Have Happened)
```
STEP 1: Review Requirements
- Deploy to 100-500 servers
- Rolling/Blue-Green/Canary strategies
- Health monitoring, rollback
- Single organization, 10 deployments/day

STEP 2: Architecture Decision
[Run Architecture Review Checklist]

Q: What's the simplest architecture?
A: Monolith API + direct deployment

Q: Do we need message queue?
A: No - Sync deployment, user waits for result

Q: Do we need multi-tenancy?
A: No - Single organization (in OUT_OF_SCOPE)

Q: Do we need schema registry?
A: No - Kernel modules are self-contained

DECISION: Monolith + Direct Deployment + 4 Strategies

STEP 3: Document Decision
[Create ADR-001: Monolith Architecture]
[Create ADR-002: Direct Deployment (No Message Queue)]
[Create ADR-003: In-Memory State (No Redis Initially)]

RESULT: Focused architecture, 1/3 the complexity, clear rationale
```

## Skill Invocation

```bash
# Use this skill for architectural decisions
/architecture-review

# Or manually:
# 1. Run Architecture Review Checklist
# 2. Consider simpler alternatives
# 3. Create ADR for decision
# 4. Get stakeholder/team approval
# 5. Then proceed to implementation
```

---

**Remember: The best architecture is the simplest one that solves the problem. Start simple, add complexity only when forced by real requirements.**
