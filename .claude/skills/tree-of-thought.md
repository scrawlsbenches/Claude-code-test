# Tree-of-Thought Problem-Solving Framework

**Description**: Systematic problem-solving framework for complex investigations, debugging, and multi-step implementations. Prevents analysis paralysis, circular reasoning, and getting stuck while maintaining execution bias.

**When to use**:
- ✅ Complex investigations (expected >30 minutes)
- ✅ Stuck/deadlocked after 3-5 attempts on a problem
- ✅ Unfamiliar problem domain requiring systematic exploration
- ✅ Multi-step feature implementation with many unknowns
- ✅ User explicitly requests thorough/systematic approach
- ❌ **DON'T USE** for simple bugs (just fix it)
- ❌ **DON'T USE** for straightforward features (TDD Red-Green-Blue is enough)
- ❌ **DON'T USE** for code reviews (just review)
- ❌ **DON'T USE** for quick investigations (<15 minutes)

**Core Philosophy**: 🎯 **80/20 RULE** - You only need Phase -1 + TDD for 80% of tasks. This framework is for the complex 20%.

---

## Instructions

**Purpose**: This guide helps you work systematically on the Claude-code-test distributed hot-swap orchestration project while avoiding common pitfalls like analysis paralysis, circular reasoning, and getting stuck.

**⚡ MOST IMPORTANT**: Bias toward ACTION. When in doubt, write code. You can course-correct faster by doing than by overthinking.

---

## Table of Contents

- [⚡ Phase -1: Execution Bias](#-phase--1-execution-bias-read-this-first) ⭐⭐⭐ **START HERE**
- [🎯 Quick Reference: 80% of Tasks](#-quick-reference-80-of-tasks) ⭐⭐⭐
- [📋 Full Tree-of-Thought Framework](#-full-tree-of-thought-framework) ⭐
- [🔄 Iteration & Context Management](#-iteration--context-management) ⭐
- [🔓 Deadlock Detection & Escape Sequences](#-deadlock-detection--escape-sequences) ⭐
- [🧠 Advanced Topics](#-advanced-topics)

---

## ⚡ Phase -1: Execution Bias (READ THIS FIRST!)

**BIAS TOWARD ACTION: When in doubt, execute. Code is cheap, overthinking is expensive.**

```
EXECUTION BIAS RULES
│
├─ Rule 1: START FAST
│  └─ If task is clear (>80% confidence) → START IMMEDIATELY
│     └─ Don't plan, don't read docs, just write the test (TDD)
│     └─ You can course-correct faster by doing than by thinking
│
├─ Rule 2: PHASE TIME LIMITS (HARD LIMITS)
│  ├─ Phase 0 (Doc Skepticism): 30 seconds - "Is doc obviously wrong?"
│  ├─ Phase 1 (Understanding): 1 minute - "Do I get what's being asked?"
│  ├─ Phase 2 (Environment): 30 seconds - "Do I have .NET SDK?"
│  ├─ Phase 3 (Context): 2 minutes - "What code do I need to read?"
│  ├─ Phase 4 (Planning): 2 minutes - "Red/Green/Blue, what's the test?"
│  └─ Phase 5+ (Execution): UNLIMITED - Actually doing the work
│
│  🚨 If you exceed time limits → STOP PLANNING, START DOING
│
├─ Rule 3: "WHEN IN DOUBT" DEFAULTS
│  ├─ Unclear requirement? → Write simplest test you CAN write, refine later
│  ├─ Multiple approaches? → Pick the simplest, don't deliberate
│  ├─ Missing info? → Make reasonable assumption, note it, proceed
│  ├─ Doc vs code conflict? → Trust code, fix doc in same commit
│  └─ Complex task? → Start with smallest piece, expand from there
│
├─ Rule 4: STOP SIGNALS (Execute immediately when you catch yourself)
│  ├─ "Let me think about this more..." → NO, write a test NOW
│  ├─ "I should check one more thing..." → NO, check WHILE coding
│  ├─ "Maybe I should read..." → NO, read the MINIMUM, start coding
│  ├─ "What if X or Y or Z..." → NO, pick X, try it, pivot if wrong
│  └─ Reading this framework for >2 minutes → STOP, go write code
│
└─ Rule 5: DONE IS BETTER THAN PERFECT
   ├─ Write the imperfect test → Makes progress
   ├─ Write the naive implementation → Can refactor in BLUE phase
   ├─ Ship the minimal feature → Can enhance in next iteration
   └─ Make the "good enough" commit → Can improve in next PR
```

**⏱️ EXECUTION TRIGGER: If you've been thinking >5 minutes without writing code/files → You're stuck in planning. Execute NOW.**

**🎯 80/20 RULE: You only need Phase -1 + TDD Red-Green-Blue for 80% of tasks. The rest is for complex debugging only.**

---

## 🎯 Quick Reference: 80% of Tasks

**For straightforward tasks (new feature, simple bug fix, refactoring):**

```
FAST PATH (5 minutes from request to first test)
│
1. ⚡ UNDERSTAND (30 seconds)
   └─ What exactly am I building/fixing?
   └─ If clear → Proceed. If unclear → Ask user ONE clarifying question.

2. 🔧 ENVIRONMENT (30 seconds)
   └─ Do I have .NET SDK? (dotnet --version)
      ├─ YES → Can build/test locally
      └─ NO → Use alternative checklist (verify contracts carefully)

3. 📚 CONTEXT (2 minutes MAX)
   └─ What existing code/tests relate to this?
   └─ Use Grep/Glob to find relevant files
   └─ Read ONLY what's directly relevant (not the whole codebase!)

4. 🔴 RED - Write Failing Test (5-10 minutes)
   └─ tests/HotSwap.Distributed.Tests/[Component]Tests.cs
   └─ Test name: MethodName_StateUnderTest_ExpectedBehavior
   └─ AAA pattern: Arrange → Act → Assert (FluentAssertions)
   └─ Run test: dotnet test --filter "TestName"
   └─ MUST FAIL (if passes, test is wrong!)

5. 🟢 GREEN - Minimal Implementation (10-20 minutes)
   └─ Write ONLY enough code to make test pass
   └─ Don't worry about perfection yet
   └─ Run test: MUST PASS

6. 🔵 BLUE - Refactor for Quality (5-10 minutes)
   └─ Improve naming, extract methods, add docs
   └─ Run ALL tests: dotnet test (must still pass!)

7. ✅ VERIFY & COMMIT (5 minutes)
   └─ dotnet clean && dotnet restore && dotnet build --no-incremental
   └─ dotnet test (all tests pass?)
   └─ Update docs if needed (CLAUDE.md, README.md)
   └─ git add . && git commit -m "feat: ..."
   └─ git pull origin <branch> --no-rebase (handle conflicts if any)
   └─ git push -u origin claude/<branch-name>

TOTAL TIME: ~30-40 minutes for typical feature
```

**If you're taking longer than this → You're overthinking. Simplify or ask for help.**

---

## 📋 Full Tree-of-Thought Framework

**Use this for complex investigations, debugging, or unfamiliar areas:**

### 🎯 Phase 0: Documentation Skepticism

**⚠️ CRITICAL ASSUMPTION: Documentation can be outdated, incorrect, or misleading.**

**Philosophy: "Trust but Verify" - Code is the source of truth, documentation is a guide.**

```
DOCUMENTATION SKEPTICISM (30 second check)
│
├─ When reading ANY documentation, ask:
│  ├─ When was this last updated? (Check "Last Updated" date)
│  ├─ Does this match what I see in the code?
│  └─ If mismatch → Code is right, doc is wrong
│
├─ Sources of truth (in priority order):
│  1. **The actual code** (.cs files) → ALWAYS AUTHORITATIVE
│  2. **Test files** → Show actual usage and behavior
│  3. **Build/test output** → Shows current state
│  4. **Type definitions** → Compiler-verified contracts
│  5. **Git history** → Recent changes not yet documented
│  6. **Documentation** → Helpful guide, but verify critical info
│
├─ When I find a documentation error, classify severity:
│  │
│  ├─ 🔴 BLOCKER (Fix immediately, blocks current work)
│  │  ├─ Doc says class/method exists, but it doesn't
│  │  ├─ Doc says to use API that's been removed
│  │  ├─ Setup instructions don't work at all
│  │  └─ Action: MUST resolve NOW to proceed
│  │
│  ├─ 🟠 CRITICAL (Could cause bugs/security issues)
│  │  ├─ Doc shows insecure code example
│  │  ├─ Doc misleads about error handling
│  │  ├─ Doc contradicts actual method signatures
│  │  └─ Action: Fix in current commit if <10 min, else high-priority task
│  │
│  ├─ 🟡 MAJOR (Outdated but not immediately dangerous)
│  │  ├─ Test counts are wrong (doc: 65, actual: 582)
│  │  ├─ Package versions are outdated
│  │  ├─ File references point to old structure
│  │  └─ Action: Fix if in same area I'm modifying, else create task
│  │
│  └─ 🟢 MINOR (Cosmetic, low impact)
│     ├─ Typos, formatting issues
│     ├─ Slightly outdated wording
│     └─ Action: Batch with other doc updates, or ignore if trivial
│
└─ Ground Truth Verification:
   ├─ For Type definitions: Use Glob + Read actual .cs file
   ├─ For Method signatures: Use Grep for method definition
   ├─ For Test counts: Run dotnet test --verbosity quiet
   ├─ For Package versions: Run dotnet list package
   └─ For Project structure: Run ls -R src/ tests/
```

**Decision: Fix doc error now or defer?**

```
Found doc error → Does it BLOCK my current task?
│
├─ YES (BLOCKER) → Fix immediately, proceed with correct info
├─ Could cause bugs? (CRITICAL) → Fix if <10 min, else create task
├─ Already modifying this area? (MAJOR) → Fix in current commit
└─ Minor/cosmetic? (MINOR) → Add TODO comment, defer to monthly cleanup
```

---

### 🎯 Phase 1: Understanding & Scoping

**(1 minute time limit - if taking longer, ask user for clarification)**

```
1. REQUEST CLARITY
   ├─ What exactly is being requested?
   │  ├─ New feature? → Identify acceptance criteria
   │  ├─ Bug fix? → Can I reproduce it? What's expected behavior?
   │  ├─ Refactoring? → What's the goal?
   │  └─ Documentation? → What changed that triggered this?
   │
   ├─ Is the request clear and unambiguous?
   │  ├─ YES → Proceed to Phase 2
   │  └─ NO → Ask ONE specific clarifying question, don't guess
   │
   └─ What's the scope?
      ├─ Single file/component? → Straightforward
      ├─ Multiple components? → Break into smaller tasks, use TodoWrite
      └─ Cross-cutting concern? → Check Domain/Infrastructure/Orchestrator/API layers
```

**⏱️ Time limit exceeded? → Make your best assumption, note it, proceed. You can pivot later if wrong.**

---

### 🎯 Phase 2: Environment & Prerequisites

**(30 second check)**

```
2. ENVIRONMENT CHECK
   ├─ Do I have .NET SDK 8.0+ installed?
   │  ├─ YES → I can build and test locally (use standard checklist)
   │  │  └─ Verify: dotnet --version (Expected: 8.0.121+)
   │  │
   │  └─ NO → I'm in a restricted environment (use alternative checklist)
   │     ├─ MUST verify contracts extra carefully (read all type definitions)
   │     ├─ MUST manually check package references
   │     └─ MUST rely on CI/CD for verification
   │
   └─ Is there a relevant Claude Skill I should use?
      ├─ /tdd-helper → For test-driven development workflow
      ├─ /precommit-check → For pre-commit validation
      ├─ /test-coverage-analyzer → For coverage analysis
      └─ See SKILLS.md for full list
```

---

### 🎯 Phase 3: Context Gathering

**(2 minute time limit - gather MINIMUM needed context)**

```
3. CODE CONTEXT (Read ONLY what's directly relevant)
   │
   ├─ What contracts (interfaces, models, enums) will I use?
   │  ├─ Use Grep to find: grep -r "interface IUserService"
   │  ├─ Use Read to examine: Read the actual .cs file
   │  ├─ Check: Property names, method signatures, nullability
   │  └─ ⚠️ NEVER GUESS property/method names - always verify!
   │
   ├─ What patterns exist in this codebase?
   │  ├─ Find similar code: Use Grep for similar functionality
   │  ├─ Check test patterns: How are other services tested?
   │  └─ Follow existing conventions (don't reinvent)
   │
   └─ What layers are involved?
      ├─ Domain → Core models, enums (no dependencies)
      ├─ Infrastructure → Cross-cutting (telemetry, security)
      ├─ Orchestrator → Core orchestration logic
      └─ API → REST controllers (depends on all above)
```

**⏱️ Time limit exceeded? → You're reading too much. Start with what you have, read more WHILE coding if needed.**

---

### 🎯 Phase 4: Planning & Task Management

**(2 minute time limit - simple plan only)**

```
4. WORK PLANNING
   │
   ├─ Is this complex enough for TodoWrite?
   │  ├─ YES (3+ steps) → Create todo list:
   │  │  ├─ 🔴 Write test for [feature]
   │  │  ├─ 🟢 Implement [feature] to pass test
   │  │  ├─ 🔵 Refactor [feature] implementation
   │  │  └─ ✅ Verify all tests pass
   │  │
   │  └─ NO (1-2 simple steps) → Just do it, skip TodoWrite
   │
   └─ What's my TDD strategy?
      ├─ New Feature → Write tests first for expected behavior
      │  ├─ Happy path test
      │  ├─ Edge case tests (null, empty, boundary)
      │  └─ Error case tests (invalid input, exceptions)
      │
      ├─ Bug Fix → Write test that reproduces bug (should fail)
      └─ Refactoring → Ensure existing tests exist and pass first
```

**⏱️ Time limit exceeded? → Stop planning. Write the first test NOW. Plan adjusts as you go.**

---

### 🎯 Phase 5-7: Test-Driven Development (TDD)

**This is where you spend most of your time (actual work!):**

#### 🔴 Phase 5: RED - Write Failing Test

```
WRITE FAILING TESTS (NO TIME LIMIT - this is actual work)
│
├─ Test file location:
│  └─ tests/HotSwap.Distributed.Tests/[ComponentName]Tests.cs
│
├─ Test naming convention:
│  └─ MethodName_StateUnderTest_ExpectedBehavior
│     Example: AuthenticateAsync_WithValidCredentials_ReturnsToken
│
├─ Test structure (AAA pattern):
│  ├─ // Arrange - Set up test data, mocks, system under test
│  ├─ // Act - Execute the method being tested
│  └─ // Assert - Verify expected behavior (use FluentAssertions)
│
├─ Mock setup patterns:
│  ├─ Read the ACTUAL interface/method signature (don't guess!)
│  ├─ Mock ALL parameters exactly (including CancellationToken)
│  └─ Use It.IsAny<T>() for parameters you don't care about
│
├─ Run the test - it MUST FAIL:
│  ├─ Command: dotnet test --filter "FullyQualifiedName~[TestName]"
│  ├─ Expected: Test fails (implementation doesn't exist yet)
│  └─ If test passes without implementation → TEST IS WRONG, fix it!
│
└─ Package references for tests:
   ├─ Does test use BCrypt? → Test project needs BCrypt.Net-Next
   ├─ Does test use ILogger? → Test project needs Microsoft.Extensions.Logging.Abstractions
   └─ Check: tests/HotSwap.Distributed.Tests/HotSwap.Distributed.Tests.csproj
```

#### 🟢 Phase 6: GREEN - Minimal Implementation

```
IMPLEMENT TO PASS (NO TIME LIMIT - this is actual work)
│
├─ Implementation location:
│  ├─ Controllers → src/HotSwap.Distributed.Api/Controllers/
│  ├─ Services → src/HotSwap.Distributed.Orchestrator/Services/
│  ├─ Models → src/HotSwap.Distributed.Domain/Models/
│  └─ Infrastructure → src/HotSwap.Distributed.Infrastructure/
│
├─ Implementation checklist:
│  ├─ Use EXACT property/method names from contracts (no guessing!)
│  ├─ Follow namespace conventions (match folder structure)
│  ├─ Add required using statements
│  ├─ Use async/await for I/O operations
│  ├─ Add proper error handling (try-catch where appropriate)
│  └─ Don't optimize yet - just make it work!
│
├─ Run the test - it MUST PASS:
│  ├─ Command: dotnet test --filter "FullyQualifiedName~[TestName]"
│  ├─ Expected: Test passes (implementation is correct)
│  └─ If test fails → Fix implementation, NOT the test!
│
└─ Dependency injection (if needed):
   ├─ Register new services in Program.cs
   ├─ Use appropriate lifetime (Singleton/Scoped/Transient)
   └─ Follow existing registration patterns
```

#### 🔵 Phase 7: BLUE - Refactor for Quality

```
REFACTOR FOR QUALITY (NO TIME LIMIT - this is actual work)
│
├─ Code quality improvements:
│  ├─ Extract methods for complex logic
│  ├─ Improve variable/method naming
│  ├─ Remove duplication (DRY principle)
│  ├─ Apply SOLID principles
│  ├─ Add XML documentation for public APIs
│  └─ Add inline comments for complex logic
│
├─ Run ALL tests continuously:
│  ├─ Command: dotnet test
│  ├─ Expected: ALL tests pass (including existing tests)
│  └─ If ANY test fails → Revert and try smaller refactoring steps
│
├─ Security considerations:
│  ├─ No hardcoded secrets or credentials
│  ├─ Input validation for user input
│  ├─ Parameterized queries (avoid SQL injection)
│  ├─ Sanitize output (avoid XSS)
│  └─ Use using statements for IDisposable resources
│
└─ Performance considerations:
   ├─ Use async/await for I/O-bound operations
   ├─ Avoid blocking calls (no .Result or .Wait())
   ├─ Consider appropriate collection types
   └─ Profile before optimizing (don't guess)
```

---

### 🎯 Phase 8: Documentation Updates

```
DOCUMENTATION SYNC (5 minutes)
│
├─ What documentation needs updating?
│  ├─ Changed public APIs? → Update XML comments, README.md
│  ├─ Added/removed packages? → Update CLAUDE.md Technology Stack
│  ├─ Changed build/test process? → Update CLAUDE.md setup instructions
│  ├─ Added/removed tests? → Update test counts in CLAUDE.md
│  ├─ Changed project structure? → Update CLAUDE.md Project Structure
│  ├─ Completed TASK_LIST.md task? → Update status, add to ENHANCEMENTS.md
│  └─ Changed Docker files? → Update Docker sections
│
├─ Documentation quality checks:
│  ├─ Are code examples still accurate?
│  ├─ Do command examples still work?
│  ├─ Are file references correct?
│  ├─ Is "Last Updated" date current?
│  └─ Is Changelog updated with changes?
│
└─ Can I use /doc-sync-check skill?
   └─ Automates validation of documentation synchronization
```

---

### 🎯 Phase 9: Pre-Commit Verification

**⚠️ NEVER commit without completing this checklist.**

#### If .NET SDK is Available (STANDARD CHECKLIST)

```
PRE-COMMIT CHECKLIST (5 minutes)
│
├─ Step 1: Clean build
│  └─ dotnet clean && dotnet restore && dotnet build --no-incremental
│     Expected: 0 errors, 0 warnings
│
├─ Step 2: ALL tests pass
│  └─ dotnet test
│     Expected: 568 passing, 14 skipped, 0 failed
│
├─ Step 3: Verify new files compile
│  └─ git status (check what's staged)
│
├─ Step 4: Check for common issues
│  ├─ No hardcoded paths (C:\, /Users/, localhost)
│  ├─ No missing XML documentation warnings
│  └─ Builds in both Debug and Release configurations
│
├─ Step 5: Final verification
│  ├─ git status (review what will be committed)
│  ├─ git diff --staged (review exact changes)
│  └─ Only THEN commit
│
└─ Step 6: Docker verification (if Dockerfile/docker-compose.yml changed)
   ├─ docker build -t hotswap-test:local .
   ├─ docker-compose up -d
   ├─ curl http://localhost:5000/health
   └─ docker-compose down -v
```

#### If .NET SDK NOT Available (ALTERNATIVE CHECKLIST)

```
ALTERNATIVE CHECKLIST (10 minutes)
│
├─ Step 1: Verify contracts before use (READ definitions, don't guess!)
│  └─ For every type used, Read the actual .cs file
│     Check: Property names, method parameters, nullability
│
├─ Step 2: Verify package references in .csproj files
│  └─ Test project has packages for types used in test code
│     Example: If using BCrypt.Net.BCrypt, need BCrypt.Net-Next reference
│
├─ Step 3: Review all code changes
│  └─ git diff (check for syntax errors, missing using statements)
│
├─ Step 4: Verify project references correct
│  └─ Test project references all projects whose types are used
│
├─ Step 5: Check for common build errors
│  ├─ Namespaces match folder structure
│  ├─ All using statements present
│  ├─ Async methods return Task
│  └─ Mock setups match actual method signatures
│
├─ Step 6: Document CI/CD dependency in commit message
│  └─ Note: "Build/tests will run in GitHub Actions"
│
└─ Step 7: Monitor CI/CD after push
   └─ Check GitHub Actions immediately, fix if fails
```

---

### 🎯 Phase 10: Git Operations

```
GIT WORKFLOW (5 minutes)
│
├─ Pre-push: Pull latest changes (MANDATORY)
│  ├─ git fetch origin <branch-name>
│  ├─ git pull origin <branch-name> --no-rebase
│  ├─ If conflicts: Resolve → git add → git commit
│  ├─ Then rebuild and test: dotnet build && dotnet test
│  └─ Only then push
│
├─ Commit message format:
│  ├─ feat: [description] → New features
│  ├─ fix: [description] → Bug fixes
│  ├─ docs: [description] → Documentation changes
│  ├─ refactor: [description] → Code refactoring
│  ├─ test: [description] → Test additions/modifications
│  └─ chore: [description] → Maintenance tasks
│
├─ Push with retry logic:
│  ├─ git push -u origin claude/[branch-name]
│  ├─ Branch MUST start with claude/ and end with session ID
│  └─ On network failure: Retry up to 4 times (2s, 4s, 8s, 16s backoff)
│
└─ Post-push:
   ├─ Monitor GitHub Actions for build status
   └─ If build fails: Follow emergency fix procedure
```

---

## 🔄 Iteration & Context Management

**For complex investigations spanning multiple cycles:**

### 🔄 Iterative Problem-Solving Framework

**Use OODA Loop: Observe → Orient → Decide → Act**

```
INVESTIGATION CYCLE (Internal - Don't Narrate)
│
├─ OBSERVE: Gather specific data for current question
│  └─ Run test, read code, check logs - get ONE piece of info
│
├─ ORIENT: Update mental model
│  └─ Does this confirm/refute hypothesis? What's next?
│
├─ DECIDE: Choose action
│  └─ Solved? → Implement. Stuck? → Pivot or escape. Progress? → Continue.
│
└─ ACT: Execute
   └─ Write code, run command, read file - DO something
```

**Key Principles:**
- **Don't narrate**: No "Iteration 1, 2, 3..." in output
- **Track internally**: Mental log is fine, written log wastes tokens
- **Deliver results**: Show findings, not investigation process
- **Set limits**: If >10-15 cycles without progress → Use escape sequence

**Balancing Speed vs Thoroughness:**
- **Simple tasks** (80%): Fast path (5 min to first test), minimal narration
- **Complex investigations** (20%): Take time needed (10-30 min OK), but still deliver concise results
- **Rule of thumb**: If investigation takes >30 min, create 50-100 line summary (not 300-line play-by-play)

---

### 🧹 Context Pruning Strategy

**Manage cognitive load by discarding irrelevant information:**

```
INFORMATION TRIAGE
│
├─ 🟢 KEEP (Core Facts - Always Relevant)
│  ├─ Project architecture (layers)
│  ├─ Current task objective
│  ├─ Confirmed root cause (if found)
│  ├─ Active constraints
│  └─ Verified ground truth
│
├─ 🟡 PARK (Might Be Relevant Later)
│  ├─ Alternative hypotheses not yet tested
│  ├─ Tangential issues discovered
│  ├─ Optimization opportunities
│  └─ Action: Add to TASK_LIST.md or TODO comments
│
├─ 🔴 DISCARD (Proven Irrelevant)
│  ├─ Disproven hypotheses ("Thought it was X, but it's not")
│  ├─ Dead-end exploration paths
│  ├─ Red herrings (looked suspicious, actually fine)
│  └─ Action: Explicitly acknowledge "X is NOT the issue"
│
└─ 🔵 SUMMARIZE (Compress for Efficiency)
   ├─ Long file reads → Extract key facts only
   ├─ Multiple similar tests → General pattern
   └─ Repeated observations → "Consistently seeing X"
```

**Pruning Triggers (Internal - Don't Output):**
- After finding answer → Discard search details
- When pivoting → Discard failed approach
- When overwhelmed → Mental reset (see below)
- **Don't document pruning process** - just do it

---

### 📝 Mental Reset (When Overwhelmed)

**If losing focus after many search cycles, mentally reset (don't write it out):**

```
INTERNAL CHECKPOINT (Keep in your head, don't output)
├─ Goal: What am I solving?
├─ Facts: 2-3 confirmed truths
├─ Dead ends: What failed?
├─ Next: One specific action
└─ Discard rest
```

**Don't create written summaries unless absolutely necessary** - they waste tokens.

---

### 🎯 Breadth-First vs Depth-First Exploration

```
EXPLORATION STRATEGY
│
├─ Breadth-First (Survey → Narrow Down)
│  │
│  ├─ When to use:
│  │  ├─ Problem area is unfamiliar
│  │  ├─ Many potential root causes
│  │  └─ Unclear where to start
│  │
│  └─ Approach:
│     1. Quick survey: Grep for keywords
│     2. Skim multiple files (don't deep-dive yet)
│     3. Identify 2-3 most likely areas
│     4. Switch to depth-first on most promising
│
└─ Depth-First (Hypothesis → Verify → Drill Down)
   │
   ├─ When to use:
   │  ├─ Have strong hypothesis about root cause
   │  ├─ Problem is in specific, known area
   │  └─ Following clear chain of causality
   │
   └─ Approach:
      1. Form specific hypothesis
      2. Read relevant code thoroughly
      3. Verify hypothesis with tests/logs
      4. If confirmed → Fix. If not → Backtrack
```

---

### 🔀 When to Pivot vs Persist

```
PIVOT vs PERSIST DECISION
│
├─ Persist if:
│  ├─ Making measurable progress each iteration
│  ├─ Each iteration narrows down problem space
│  ├─ Current approach is theoretically sound
│  └─ Time invested is reasonable (<30 min)
│
├─ Pivot if:
│  ├─ Stuck on same question for 3+ iterations
│  ├─ Observations contradict fundamental assumptions
│  ├─ Time spent exceeds expected value (>30 min, no progress)
│  └─ Gut feeling says "this doesn't make sense"
│
└─ How to Pivot:
   1. Acknowledge: "Current approach isn't working because..."
   2. Prune: Discard all information specific to failed approach
   3. Keep: Core facts about the problem itself
   4. Rethink: "What if the problem is actually Y, not X?"
   5. New hypothesis: Choose fundamentally different angle
   6. Fresh start: Begin new iteration with new approach
```

---

## 🔓 Deadlock Detection & Escape Sequences

**Preventing and breaking free from stuck states:**

### 🚨 Common Deadlock Patterns

```
DEADLOCK PATTERN CATALOG
│
├─ 1. CIRCULAR REASONING LOOP
│  │  Signature: Testing same hypothesis multiple times
│  │  Detection: "Have I already eliminated X? YES → Don't test again"
│  │  Escape: Force completely new angle, or ask for help
│
├─ 2. ANALYSIS PARALYSIS
│  │  Signature: Gathering info without deciding (>3 observation iterations)
│  │  Detection: "Have I gathered enough to decide? YES but still not deciding"
│  │  Escape: Timebox 5 minutes, choose "good enough" option, commit
│
├─ 3. MISSING INFORMATION DEADLOCK
│  │  Signature: Can't proceed without info X, can't get X myself
│  │  Detection: "Can I get info myself? NO → Missing info deadlock"
│  │  Escape: Ask user for info, or park task and work on something else
│
├─ 4. TOOL LIMITATION DEADLOCK
│  │  Signature: Need to do X, but tools can't do X
│  │  Detection: "Trying workarounds that don't quite work"
│  │  Escape: Reframe goal without X, or ask user to do X manually
│
├─ 5. COMPLEXITY OVERFLOW DEADLOCK
│  │  Signature: Problem keeps expanding, TodoWrite >10 items
│  │  Detection: "Task scope exceeded original estimate by 3x+"
│  │  Escape: Define MVP, implement minimal version, defer rest
│
├─ 6. CONFLICTING CONSTRAINTS DEADLOCK
│  │  Signature: Requirement A says X, Requirement B says NOT X
│  │  Detection: "No solution satisfies both constraints"
│  │  Escape: Ask user which takes priority
│
└─ 7. FALSE ASSUMPTION DEADLOCK
   │  Signature: All approaches fail, reality doesn't match expectations
   │  Detection: "This SHOULD work but doesn't" (3+ iterations)
   │  Escape: List all assumptions, verify each, rebuild mental model
```

---

### 🔓 Universal Escape Sequences

**These work for ANY deadlock:**

```
UNIVERSAL ESCAPE PROTOCOL
│
├─ ESCAPE #1: THE RESET
│  │  When: Completely stuck, nothing working
│  │  Steps:
│  │  1. STOP ALL WORK
│  │  2. SAVE STATE: Create Working Memory Summary
│  │  3. CLEAR MIND: Discard speculation, keep only facts
│  │  4. BREAK: Move to different task
│  │  5. RETURN FRESH: Begin from Phase 1 as if new task
│
├─ ESCAPE #2: ASK FOR HELP
│  │  When: Stuck after trying reset, or missing critical info
│  │  Template:
│  │     "I'm stuck on [problem]. Tried:
│  │      - [Approach A] → [Result]
│  │      - [Approach B] → [Result]
│  │
│  │      Learned: [Facts]
│  │      Stuck on: [Specific blocker]
│  │
│  │      Options:
│  │      1. [Option A - pros/cons]
│  │      2. [Option B - pros/cons]
│  │
│  │      What would you recommend?"
│
├─ ESCAPE #3: SIMPLIFY RADICALLY
│  │  When: Problem seems too complex
│  │  Steps:
│  │  1. IDENTIFY CORE: What's the ONE thing I'm really trying to do?
│  │  2. STRIP AWAY: Remove all nice-to-haves
│  │  3. ABSOLUTE MINIMUM: Simplest version that could work?
│  │  4. IMPLEMENT THAT: Just the core
│  │  5. TEST: Does minimal version work?
│
├─ ESCAPE #4: CHANGE PERSPECTIVE
│  │  When: Stuck in one way of thinking
│  │  Perspectives:
│  │  - User perspective: "What does user experience?"
│  │  - Data perspective: "What happens to the data?"
│  │  - Timeline perspective: "What happens in what order?"
│  │  - Reverse perspective: "What if I start from the end?"
│
└─ ESCAPE #5: DIVIDE AND CONQUER
   │  When: Problem has multiple interacting parts
   │  Steps:
   │  1. IDENTIFY COMPONENTS: What are the moving parts?
   │  2. ISOLATE: Test each component independently
   │  3. VERIFY: Which work? Which don't?
   │  4. NARROW: Focus only on failing component
```

---

### ⏱️ Deadlock Detection Checklist

**Run this every 3-5 iterations:**

```
DEADLOCK SELF-CHECK
│
Ask yourself:
│
├─ 1. Am I making progress?
│  └─ Each iteration should teach something new
│     If last 3 iterations taught nothing → DEADLOCK
│
├─ 2. Am I repeating myself?
│  └─ Have I tested this hypothesis before?
│     If YES → CIRCULAR REASONING
│
├─ 3. Can I make a decision?
│  └─ Do I have enough info to choose?
│     If YES but still gathering → ANALYSIS PARALYSIS
│
├─ 4. Do I have what I need?
│  └─ Is there critical info I can't access?
│     If YES → MISSING INFORMATION
│
├─ 5. Is scope expanding?
│  └─ Is problem bigger than when I started?
│     If 3x+ bigger → COMPLEXITY OVERFLOW
│
├─ 6. Are requirements consistent?
│  └─ Can all constraints be satisfied?
│     If NO → CONFLICTING CONSTRAINTS
│
├─ 7. Does reality match expectations?
│  └─ Are observations matching predictions?
│     If consistently NO → FALSE ASSUMPTION
│
└─ 8. Am I fighting my tools?
   └─ Trying to do something tools can't do?
      If YES → TOOL LIMITATION

IF ANY DEADLOCK DETECTED:
└─ STOP immediately
└─ Identify pattern (1-7 above)
└─ Execute pattern-specific escape
└─ If still stuck → Execute universal escape
```

---

### 📊 Deadlock Prevention Strategies

```
PREVENTION CHECKLIST (Run at start of complex task)
│
├─ Before Starting:
│  ├─ [ ] Define success criteria (what does "done" look like?)
│  ├─ [ ] Set iteration limit (max 10 before reset)
│  ├─ [ ] Identify required information
│  ├─ [ ] Define MVP scope
│  ├─ [ ] Check for conflicting constraints
│  ├─ [ ] List assumptions explicitly
│  ├─ [ ] Plan escape route (if stuck, I'll do X)
│  └─ [ ] Set time limit (max 1 hour before checkpoint)
│
├─ During Work:
│  ├─ [ ] Track eliminated hypotheses
│  ├─ [ ] Update iteration log
│  ├─ [ ] Prune context regularly
│  ├─ [ ] Question assumptions periodically
│  ├─ [ ] Check progress every 3 iterations
│  └─ [ ] Use TodoWrite for complex tasks
│
└─ Early Warning Signs:
   ├─ ⚠️ Revisiting same file 3+ times
   ├─ ⚠️ Reading more than coding (10:1 ratio)
   ├─ ⚠️ "Just one more check" thoughts
   ├─ ⚠️ Uncertainty increasing instead of decreasing
   └─ ⚠️ Time spent exceeds estimate by 2x
      ACTION: Run deadlock self-check immediately
```

---

## 🧠 Advanced Topics

### 🧪 Minimal Experiment Design

**When exploring unknowns:**

```
EXPERIMENT PRINCIPLES
│
Each experiment should:
├─ Test ONE specific hypothesis
├─ Be reversible (easy to undo)
├─ Take <5 minutes to set up and run
├─ Produce clear pass/fail result
└─ Teach you something regardless of outcome

Good example:
  Hypothesis: "Method X is never called during test Y"
  Experiment: Add Console.WriteLine("X called") at start of X
  Run: dotnet test --filter Y
  Observe: If no output → Confirmed. If output → Refuted.
  Cleanup: Remove Console.WriteLine
```

---

### 📊 Investigation Documentation (When Needed)

**ONLY create investigation docs for:**
- User-requested investigations ("why are X tests failing?")
- Findings that affect future work (documented decisions)
- Complex root cause analysis that should be referenced later

**DON'T create docs for:**
- Routine debugging (just fix it)
- Simple searches (just deliver answer)
- Code exploration (just write the code)

**If documenting, keep it CONCISE (<100 lines):**

```markdown
# [Problem] Investigation

**Root Cause**: [One sentence]
**Found**: [2-3 key discoveries]
**Decision**: [What action was taken]
**Impact**: [Risk level + justification]

## Recommendations
1. [Action A] - [Effort estimate]
2. [Action B] - [Effort estimate]

## Tasks Created
- Task #N: [Description] ([Effort])
```

**Focus on findings and decisions, NOT investigation process. No "Iteration" logs.**

---

## 🚨 Critical Reminders

**These are NON-NEGOTIABLE:**

```
❌ NEVER commit without running pre-commit checklist
❌ NEVER guess property/method names (always Read definitions)
❌ NEVER skip writing tests first (TDD is mandatory)
❌ NEVER commit with failing tests
❌ NEVER push without pulling first
❌ NEVER ignore build warnings
❌ NEVER skip documentation updates
❌ NEVER commit secrets or environment-specific values

✅ ALWAYS follow Red-Green-Refactor cycle
✅ ALWAYS verify contracts before use (read definitions)
✅ ALWAYS run dotnet build && dotnet test before commit
✅ ALWAYS update documentation with code changes
✅ ALWAYS pull before push
✅ ALWAYS use proper git branch naming (claude/*-sessionid)
✅ ALWAYS monitor CI/CD after pushing
✅ ALWAYS check package references in test projects
```

---

## 💡 Quick Decision Tree

```
START
  │
  ├─ Is request clear? NO → Ask clarifying questions
  │                    YES ↓
  ├─ Do I have .NET SDK? NO → Use alternative checklist
  │                      YES ↓
  ├─ Have I read relevant contracts? NO → Use Read tool
  │                                  YES ↓
  ├─ Have I written tests first? NO → Write failing tests (RED)
  │                              YES ↓
  ├─ Do tests pass? NO → Implement code (GREEN)
  │                 YES ↓
  ├─ Is code quality good? NO → Refactor (BLUE)
  │                        YES ↓
  ├─ Are docs updated? NO → Update documentation
  │                    YES ↓
  ├─ Does build pass? NO → Fix errors, don't commit
  │                   YES ↓
  ├─ Do ALL tests pass? NO → Fix failures, don't commit
  │                     YES ↓
  ├─ Have I pulled latest? NO → git pull origin <branch>
  │                        YES ↓
  ├─ Am I ready to commit? YES → Commit with proper message
  │
  └─ Push with retry logic → Monitor CI/CD → DONE ✅
```

---

**Remember**: This is a high-quality, production-ready codebase. Maintain that standard in every contribution! 🎯

**When in doubt**: Bias toward ACTION. Write code, iterate quickly, course-correct as you go.
