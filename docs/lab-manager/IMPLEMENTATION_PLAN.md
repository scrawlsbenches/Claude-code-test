# Implementation Plan - Epics, Stories & Tasks

**Version:** 1.0.0
**Total Duration:** 30-38 days (6-8 weeks)
**Team Size:** 1-2 developers
**Sprint Length:** 2 weeks

---

## Table of Contents

1. [Overview](#overview)
2. [Epic 1: Core Lab Infrastructure](#epic-1-core-lab-infrastructure)
3. [Epic 2: Environment Provisioning](#epic-2-environment-provisioning)
4. [Epic 3: Submission & Grading](#epic-3-submission--grading)
5. [Epic 4: Progress Tracking](#epic-4-progress-tracking)
6. [Epic 5: LMS Integration & Production Hardening](#epic-5-lms-integration--production-hardening)
7. [Sprint Planning](#sprint-planning)
8. [Risk Mitigation](#risk-mitigation)

---

## Overview

### Implementation Approach

This implementation follows **Test-Driven Development (TDD)** and builds on the existing HotSwap platform infrastructure.

**Key Principles:**
- ✅ Write tests BEFORE implementation (Red-Green-Refactor)
- ✅ Reuse existing components (auth, tracing, metrics, deployment strategies)
- ✅ Clean architecture (Domain → Infrastructure → Orchestrator → API)
- ✅ 85%+ test coverage requirement
- ✅ Zero-downtime deployments

### Effort Estimation

| Epic | Effort | Complexity | Dependencies |
|------|--------|------------|--------------|
| Epic 1: Core Infrastructure | 8-10 days | Medium | None |
| Epic 2: Environment Provisioning | 9-11 days | High | Epic 1 |
| Epic 3: Submission & Grading | 6-8 days | Medium | Epic 1, Epic 2 |
| Epic 4: Progress Tracking | 4-5 days | Low | Epic 1, Epic 2, Epic 3 |
| Epic 5: LMS Integration | 3-4 days | Medium | All epics |

**Total:** 30-38 days (6-8 weeks with buffer)

---

## Epic 1: Core Lab Infrastructure

**Goal:** Establish foundational lab management components

**Duration:** 8-10 days
**Priority:** Critical (must complete first)
**Dependencies:** None

### User Stories

#### Story 1.1: Create Course Domain Model

**As a** platform developer
**I want to** create the Course domain model
**So that** I can represent academic courses in the system

**Acceptance Criteria:**
- Course class created with all required fields
- Validation logic implemented
- Archive functionality working
- Unit tests pass (12+ tests)

**Tasks:**
- [ ] Create `Course.cs` in Domain/Models
- [ ] Add required properties (CourseName, Title, Term, Instructor, etc.)
- [ ] Implement `IsValid()` validation method
- [ ] Implement `Archive()` method
- [ ] Write 12+ unit tests (validation, archival, edge cases)
- [ ] Add JSON serialization attributes

**Estimated Effort:** 1 day

---

#### Story 1.2: Create Lab Domain Model

**As a** platform developer
**I want to** create the Lab domain model
**So that** I can represent lab exercises in the system

**Acceptance Criteria:**
- Lab class created with configuration
- AutograderConfig value object created
- Validation logic implemented
- Publish functionality working
- Unit tests pass (15+ tests)

**Tasks:**
- [ ] Create `Lab.cs` in Domain/Models
- [ ] Create `AutograderConfig.cs` value object
- [ ] Add properties (LabId, CourseName, Title, Type, etc.)
- [ ] Implement `IsValid()` validation
- [ ] Implement `Publish()` and `IsPastDue()` methods
- [ ] Write 15+ unit tests
- [ ] Add support for lab versioning

**Estimated Effort:** 1.5 days

---

#### Story 1.3: Create StudentEnvironment, Submission, and Supporting Models

**As a** platform developer
**I want to** create environment and submission models
**So that** I can manage student environments and submissions

**Acceptance Criteria:**
- StudentEnvironment class created
- Submission and GradingResult classes created
- ResourceQuota and ResourceUsage value objects created
- Unit tests pass (25+ tests total)

**Tasks:**
- [ ] Create `StudentEnvironment.cs` in Domain/Models
- [ ] Create `Submission.cs` in Domain/Models
- [ ] Create `GradingResult.cs` in Domain/Models
- [ ] Create `ResourceQuota.cs` and `ResourceUsage.cs` value objects
- [ ] Implement validation logic for all models
- [ ] Implement environment lifecycle methods
- [ ] Write 25+ unit tests

**Estimated Effort:** 2 days

---

#### Story 1.4: Create Course & Lab Management APIs

**As an** instructor
**I want to** create and manage courses and labs via API
**So that** I can set up my course structure

**Acceptance Criteria:**
- CoursesController created with CRUD endpoints
- LabsController created with CRUD endpoints
- Authorization enforced (Instructor, Admin only)
- Unit tests pass (20+ tests)

**Tasks:**
- [ ] Create `CoursesController.cs` in API layer
- [ ] Implement POST /api/v1/courses
- [ ] Implement GET /api/v1/courses
- [ ] Implement PUT /api/v1/courses/{name}
- [ ] Implement DELETE /api/v1/courses/{name}
- [ ] Create `LabsController.cs` in API layer
- [ ] Implement POST /api/v1/labs
- [ ] Implement GET /api/v1/labs
- [ ] Implement PUT /api/v1/labs/{labId}
- [ ] Implement POST /api/v1/labs/{labId}/publish
- [ ] Add authorization middleware
- [ ] Write 20+ integration tests

**Estimated Effort:** 2.5 days

---

#### Story 1.5: Implement Course & Lab Persistence

**As a** platform developer
**I want to** persist courses and labs to PostgreSQL
**So that** data survives restarts

**Acceptance Criteria:**
- ICourseRepository interface created
- ILabRepository interface created
- PostgreSQL implementations working
- Integration tests pass (15+ tests)

**Tasks:**
- [ ] Create `ICourseRepository.cs` interface
- [ ] Create `ILabRepository.cs` interface
- [ ] Create `PostgresCourseRepository.cs` implementation
- [ ] Create `PostgresLabRepository.cs` implementation
- [ ] Add database migrations (EF Core)
- [ ] Implement CRUD operations
- [ ] Write 15+ integration tests

**Estimated Effort:** 2 days

---

## Epic 2: Environment Provisioning

**Goal:** Implement student environment provisioning and management

**Duration:** 9-11 days
**Priority:** Critical
**Dependencies:** Epic 1

### User Stories

#### Story 2.1: Create ResourceTemplate Management

**As a** platform administrator
**I want to** define resource templates
**So that** instructors can use predefined environment configurations

**Acceptance Criteria:**
- ResourceTemplate model created
- Templates stored in database
- Predefined templates available (dotnet-basic, jupyter-datascience, etc.)
- Unit tests pass (10+ tests)

**Tasks:**
- [ ] Create `ResourceTemplate.cs` in Domain/Models
- [ ] Create `IResourceTemplateRepository.cs` interface
- [ ] Create `PostgresResourceTemplateRepository.cs` implementation
- [ ] Seed predefined templates (migration script)
- [ ] Create `ResourceTemplatesController.cs` API
- [ ] Write 10+ unit tests

**Estimated Effort:** 1.5 days

---

#### Story 2.2: Implement Docker Container Provisioning

**As a** student
**I want to** have an isolated environment provisioned for my lab
**So that** I can work on the lab exercise

**Acceptance Criteria:**
- IEnvironmentProvisioner interface created
- DockerEnvironmentProvisioner implementation working
- Containers created with resource quotas
- Starter code mounted into container
- Unit tests pass (15+ tests)

**Tasks:**
- [ ] Create `IEnvironmentProvisioner.cs` interface
- [ ] Create `DockerEnvironmentProvisioner.cs` implementation
- [ ] Integrate with Docker API (Docker.DotNet)
- [ ] Implement `ProvisionAsync(Lab, Student)` method
- [ ] Apply resource quotas (--cpus, --memory, --storage)
- [ ] Mount starter code volume
- [ ] Implement health checks
- [ ] Write 15+ integration tests

**Estimated Effort:** 3 days

---

#### Story 2.3: Implement Web-Based Environment Access

**As a** student
**I want to** access my lab environment via web browser
**So that** I can work on labs without installing software

**Acceptance Criteria:**
- Web IDE integrated (code-server for VS Code, JupyterLab)
- Access URL generated with authentication token
- HTTPS enforced
- Session management working
- Unit tests pass (10+ tests)

**Tasks:**
- [ ] Integrate code-server (VS Code Server) into Docker images
- [ ] Integrate JupyterLab into notebook templates
- [ ] Generate secure access URLs (JWT token in URL)
- [ ] Implement reverse proxy routing (Nginx or Traefik)
- [ ] Add HTTPS/TLS certificates
- [ ] Implement session timeout
- [ ] Write 10+ integration tests

**Estimated Effort:** 2.5 days

---

#### Story 2.4: Implement Environment Lifecycle Management

**As a** platform
**I want to** manage environment lifecycle (suspend, resume, delete)
**So that** I can optimize resource usage

**Acceptance Criteria:**
- Auto-suspend working (after 30 min inactivity)
- Manual suspend/resume working
- Environment deletion working
- State transitions validated
- Unit tests pass (12+ tests)

**Tasks:**
- [ ] Create `EnvironmentLifecycleManager.cs` orchestrator
- [ ] Implement auto-suspend logic (background job)
- [ ] Implement `SuspendAsync(environmentId)` method
- [ ] Implement `ResumeAsync(environmentId)` method
- [ ] Implement `DeleteAsync(environmentId)` method
- [ ] Add state validation (can't resume deleted environment)
- [ ] Write 12+ unit tests

**Estimated Effort:** 2 days

---

## Epic 3: Submission & Grading

**Goal:** Implement lab submission and autograding functionality

**Duration:** 6-8 days
**Priority:** High
**Dependencies:** Epic 1, Epic 2

### User Stories

#### Story 3.1: Implement Lab Submission

**As a** student
**I want to** submit my lab work for grading
**So that** I can receive a grade

**Acceptance Criteria:**
- SubmissionsController created
- Files uploaded to MinIO/S3
- Submission receipt generated
- Late submission detection working
- Unit tests pass (15+ tests)

**Tasks:**
- [ ] Create `SubmissionsController.cs` API
- [ ] Implement POST /api/v1/submissions
- [ ] Implement file upload to MinIO/S3 (reuse existing MinioStorageService)
- [ ] Calculate lateness and penalty
- [ ] Generate submission receipt (PDF or email)
- [ ] Validate submission attempts (max attempts)
- [ ] Write 15+ integration tests

**Estimated Effort:** 2 days

---

#### Story 3.2: Implement Docker-Based Autograding

**As an** instructor
**I want to** autograde student submissions
**So that** I can provide fast feedback

**Acceptance Criteria:**
- IAutograder interface created
- DockerAutograder implementation working
- Test results captured and stored
- Grading timeout enforced
- Unit tests pass (12+ tests)

**Tasks:**
- [ ] Create `IAutograder.cs` interface
- [ ] Create `DockerAutograder.cs` implementation
- [ ] Implement `GradeAsync(Submission, Lab)` method
- [ ] Run autograder Docker container (isolated)
- [ ] Capture test results (stdout, stderr, exit code)
- [ ] Parse test results (JSON format)
- [ ] Enforce grading timeout (default: 5 minutes)
- [ ] Write 12+ integration tests

**Estimated Effort:** 2.5 days

---

#### Story 3.3: Implement Grading Results & Feedback

**As a** student
**I want to** view my grading results and feedback
**So that** I can understand my score

**Acceptance Criteria:**
- GradingController created
- Test results displayed
- Feedback rendered (Markdown)
- Instructor override working
- Unit tests pass (10+ tests)

**Tasks:**
- [ ] Create `GradingController.cs` API
- [ ] Implement GET /api/v1/grading/{submissionId}/results
- [ ] Implement PUT /api/v1/grading/{submissionId}/override (instructor only)
- [ ] Store grading results in PostgreSQL
- [ ] Render Markdown feedback
- [ ] Add audit logging for manual overrides
- [ ] Write 10+ integration tests

**Estimated Effort:** 1.5 days

---

## Epic 4: Progress Tracking

**Goal:** Implement student progress tracking and analytics

**Duration:** 4-5 days
**Priority:** Medium
**Dependencies:** Epic 1, Epic 2, Epic 3

### User Stories

#### Story 4.1: Implement Progress Metrics Collection

**As a** platform
**I want to** track student progress metrics
**So that** instructors can monitor student engagement

**Acceptance Criteria:**
- ProgressMetrics model created
- Metrics collected on environment access
- Metrics collected on submission
- Metrics stored in PostgreSQL
- Unit tests pass (12+ tests)

**Tasks:**
- [ ] Create `ProgressMetrics.cs` in Domain/Models
- [ ] Create `IProgressTracker.cs` interface
- [ ] Create `ProgressTracker.cs` implementation
- [ ] Hook into environment access events
- [ ] Hook into submission events
- [ ] Calculate completion percentage
- [ ] Store metrics in PostgreSQL
- [ ] Write 12+ unit tests

**Estimated Effort:** 2 days

---

#### Story 4.2: Implement Progress Analytics & Reports

**As an** instructor
**I want to** view progress analytics and reports
**So that** I can identify struggling students

**Acceptance Criteria:**
- ProgressController created
- Course-level analytics working
- Student-level analytics working
- Struggling students identified
- Unit tests pass (10+ tests)

**Tasks:**
- [ ] Create `ProgressController.cs` API
- [ ] Implement GET /api/v1/progress/course/{courseName}
- [ ] Implement GET /api/v1/progress/student/{studentId}
- [ ] Implement GET /api/v1/progress/struggling
- [ ] Calculate aggregate metrics (completion rate, average score)
- [ ] Export to CSV/Excel
- [ ] Write 10+ integration tests

**Estimated Effort:** 2 days

---

## Epic 5: LMS Integration & Production Hardening

**Goal:** Integrate with LMS and prepare for production deployment

**Duration:** 3-4 days
**Priority:** Medium
**Dependencies:** All epics

### User Stories

#### Story 5.1: Implement LTI 1.3 Integration

**As a** student
**I want to** access labs from my LMS (Canvas, Moodle)
**So that** I have a seamless learning experience

**Acceptance Criteria:**
- LTI 1.3 launch working
- SSO working (student auto-logged in)
- Grade passback to LMS working
- Unit tests pass (8+ tests)

**Tasks:**
- [ ] Create `LmsController.cs` API
- [ ] Implement LTI 1.3 launch endpoint
- [ ] Integrate LTI library (LtiAdvantage.IdentityServer)
- [ ] Implement SSO (JWT token from LTI launch)
- [ ] Implement grade passback (LTI AGS)
- [ ] Test with Canvas and Moodle
- [ ] Write 8+ integration tests

**Estimated Effort:** 2 days

---

#### Story 5.2: Production Hardening & Documentation

**As a** platform operator
**I want to** deploy the system to production
**So that** instructors can use it for real courses

**Acceptance Criteria:**
- Deployment guide complete
- Instructor setup guide complete
- Student onboarding guide complete
- Load testing passed (5,000 concurrent students)
- Grafana dashboards created

**Tasks:**
- [ ] Write deployment guide (Kubernetes, Docker Compose)
- [ ] Write instructor setup guide
- [ ] Write student onboarding guide
- [ ] Create Grafana dashboards (environment metrics, progress metrics)
- [ ] Run load tests (k6 or JMeter)
- [ ] Fix performance bottlenecks
- [ ] Security audit

**Estimated Effort:** 2 days

---

## Sprint Planning

### Sprint 1 (Week 1-2): Core Infrastructure

**Epic 1 Focus**

**Goals:**
- Complete domain models
- Implement course and lab management APIs
- Set up database persistence

**Deliverables:**
- Course, Lab, StudentEnvironment, Submission domain models
- CoursesController and LabsController APIs
- PostgreSQL repositories
- 60+ unit tests

**Risks:**
- Database migration issues

---

### Sprint 2 (Week 3-4): Environment Provisioning

**Epic 2 Focus**

**Goals:**
- Implement Docker container provisioning
- Integrate web-based IDE
- Implement environment lifecycle management

**Deliverables:**
- DockerEnvironmentProvisioner
- Web IDE access (VS Code Server, JupyterLab)
- Auto-suspend/resume functionality
- 37+ integration tests

**Risks:**
- Docker networking complexity
- SSL certificate management for web IDE

---

### Sprint 3 (Week 5): Submission & Grading

**Epic 3 Focus**

**Goals:**
- Implement lab submission
- Implement Docker-based autograding
- Implement grading results display

**Deliverables:**
- SubmissionsController API
- DockerAutograder implementation
- GradingController API
- MinIO integration for file storage
- 37+ integration tests

**Risks:**
- Autograder timeout issues
- Test result parsing

---

### Sprint 4 (Week 6-7): Progress Tracking & LMS Integration

**Epic 4 & 5 Focus**

**Goals:**
- Implement progress tracking
- Integrate with LMS (LTI 1.3)
- Production hardening

**Deliverables:**
- ProgressController API
- LTI 1.3 integration (Canvas, Moodle)
- Grade passback to LMS
- Deployment guides
- Grafana dashboards
- 30+ integration tests

**Risks:**
- LTI integration complexity
- LMS-specific quirks

---

## Risk Mitigation

### Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Docker networking issues | High | Medium | Use Docker Compose for local dev, test early |
| Autograding timeout | Medium | High | Implement configurable timeout, kill runaway containers |
| Resource quota enforcement | High | Medium | Test with Docker --cpus and --memory limits |
| LTI integration complexity | Medium | Medium | Use well-tested library, follow LTI spec closely |
| Load testing reveals bottlenecks | High | Low | Optimize database queries, add caching |

### Operational Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Insufficient compute resources | High | Medium | Auto-scaling, resource quotas, environment suspension |
| Storage costs (student submissions) | Medium | High | Retention policies, delete old submissions |
| Security vulnerabilities | High | Low | Security audit, penetration testing |
| Instructor resistance to new system | Medium | Medium | Comprehensive onboarding, documentation, support |

---

## Success Metrics

**Development Metrics:**
- ✅ 85%+ test coverage achieved
- ✅ All epics completed on time
- ✅ Zero critical bugs in production
- ✅ API documentation complete

**Performance Metrics:**
- ✅ 5,000+ concurrent students supported
- ✅ Environment provisioning < 60 seconds (p95)
- ✅ Lab deployment < 5 minutes for 1,000 students
- ✅ System uptime 99.9% during academic terms

**Educational Metrics:**
- ✅ Instructor setup time < 30 minutes
- ✅ Student onboarding < 5 minutes
- ✅ Student satisfaction > 80%
- ✅ Instructor satisfaction > 85%

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
**Next Review:** After Sprint 1 Completion
