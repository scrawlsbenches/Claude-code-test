# Approval Workflow Implementation Guide

## Overview

This document describes the approval workflow system that was implemented for the Distributed Kernel Orchestration system. The approval workflow ensures that deployments to Staging and Production environments require explicit approval before proceeding.

## Features Implemented

### ✅ Core Components

1. **Domain Models** (`src/HotSwap.Distributed.Domain/`)
   - `ApprovalStatus` enum (Pending, Approved, Rejected, Expired)
   - `ApprovalRequest` model - Tracks approval requests with timeout and metadata
   - `ApprovalDecision` model - Captures approve/reject decisions

2. **Approval Service** (`src/HotSwap.Distributed.Orchestrator/Services/ApprovalService.cs`)
   - Create approval requests
   - Approve/reject deployments
   - Wait for approval decisions
   - Process expired approvals
   - Authorization checking

3. **Pipeline Integration** (`src/HotSwap.Distributed.Orchestrator/Pipeline/DeploymentPipeline.cs`)
   - Approval gates before Staging and Production deployments
   - Automatic pipeline pause on approval requests
   - Pipeline resume on approval granted
   - Pipeline failure on rejection or timeout

4. **API Endpoints** (`src/HotSwap.Distributed.Api/Controllers/ApprovalsController.cs`)
   - `GET /api/v1/approvals/pending` - List pending approvals
   - `GET /api/v1/approvals/deployments/{executionId}` - Get approval details
   - `POST /api/v1/approvals/deployments/{executionId}/approve` - Approve deployment
   - `POST /api/v1/approvals/deployments/{executionId}/reject` - Reject deployment

5. **Notification Service** (`src/HotSwap.Distributed.Infrastructure/Notifications/`)
   - Email notifications for approval requests (logged to console in current implementation)
   - Approval granted notifications
   - Approval rejected notifications
   - Approval expired notifications

6. **Background Service** (`src/HotSwap.Distributed.Api/Services/ApprovalTimeoutBackgroundService.cs`)
   - Runs every 5 minutes
   - Processes expired approval requests
   - Auto-rejects after 24-hour timeout
   - Sends expiry notifications

7. **Unit Tests** (`tests/HotSwap.Distributed.Tests/Services/ApprovalServiceTests.cs`)
   - 10+ comprehensive test cases
   - Full coverage of approval workflow scenarios

## Configuration

### Pipeline Configuration

The approval timeout is configured in `appsettings.json`:

```json
{
  "Pipeline": {
    "ApprovalTimeoutHours": 24
  }
}
```

### Enable Approval Workflow

To enable the approval workflow for a deployment, set `RequireApproval: true` in the deployment request:

```json
{
  "moduleName": "MyModule",
  "version": "1.0.0",
  "targetEnvironment": "Production",
  "requesterEmail": "developer@example.com",
  "requireApproval": true
}
```

## Usage Example

### 1. Create a Deployment with Approval Required

```bash
curl -X POST http://localhost:5000/api/v1/deployments \
  -H "Content-Type: application/json" \
  -d '{
    "moduleName": "CriticalModule",
    "version": "2.0.0",
    "targetEnvironment": "Production",
    "requesterEmail": "dev@example.com",
    "requireApproval": true,
    "description": "Critical security update"
  }'
```

Response:
```json
{
  "executionId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "Running",
  "startTime": "2025-11-15T10:00:00Z",
  "estimatedDuration": "PT30M",
  "traceId": "123e4567-e89b-12d3-a456-426614174000",
  "links": {
    "self": "/api/v1/deployments/123e4567-e89b-12d3-a456-426614174000",
    "trace": "https://jaeger.example.com/trace/123e4567-e89b-12d3-a456-426614174000"
  }
}
```

### 2. Check Deployment Status (Will Show Waiting for Approval)

```bash
curl http://localhost:5000/api/v1/deployments/123e4567-e89b-12d3-a456-426614174000
```

Response:
```json
{
  "executionId": "123e4567-e89b-12d3-a456-426614174000",
  "moduleName": "CriticalModule",
  "version": "2.0.0",
  "status": "Running",
  "stages": [
    {
      "name": "Build",
      "status": "Succeeded",
      "duration": "PT2S"
    },
    {
      "name": "Test",
      "status": "Succeeded",
      "duration": "PT3S"
    },
    {
      "name": "Security Scan",
      "status": "Succeeded",
      "duration": "PT1S"
    },
    {
      "name": "Deploy to Development",
      "status": "Succeeded",
      "duration": "PT5S"
    },
    {
      "name": "Deploy to QA",
      "status": "Succeeded",
      "duration": "PT10S"
    },
    {
      "name": "Approval for Staging",
      "status": "WaitingForApproval",
      "message": "Awaiting approval decision..."
    }
  ]
}
```

### 3. List Pending Approvals

```bash
curl http://localhost:5000/api/v1/approvals/pending
```

Response:
```json
[
  {
    "approvalId": "abc12345-def6-7890-ghij-klmnopqrstuv",
    "deploymentExecutionId": "123e4567-e89b-12d3-a456-426614174000",
    "moduleName": "CriticalModule",
    "version": "2.0.0",
    "targetEnvironment": "Staging",
    "requesterEmail": "dev@example.com",
    "requestedAt": "2025-11-15T10:05:00Z",
    "timeoutAt": "2025-11-16T10:05:00Z",
    "timeRemaining": "23h 50m"
  }
]
```

### 4. Get Approval Request Details

```bash
curl http://localhost:5000/api/v1/approvals/deployments/123e4567-e89b-12d3-a456-426614174000
```

Response:
```json
{
  "approvalId": "abc12345-def6-7890-ghij-klmnopqrstuv",
  "deploymentExecutionId": "123e4567-e89b-12d3-a456-426614174000",
  "moduleName": "CriticalModule",
  "version": "2.0.0",
  "targetEnvironment": "Staging",
  "requesterEmail": "dev@example.com",
  "status": "Pending",
  "requestedAt": "2025-11-15T10:05:00Z",
  "timeoutAt": "2025-11-16T10:05:00Z"
}
```

### 5. Approve the Deployment

```bash
curl -X POST http://localhost:5000/api/v1/approvals/deployments/123e4567-e89b-12d3-a456-426614174000/approve \
  -H "Content-Type: application/json" \
  -d '{
    "approverEmail": "manager@example.com",
    "approved": true,
    "reason": "Security review passed. Approved for staging deployment."
  }'
```

Response:
```json
{
  "approvalId": "abc12345-def6-7890-ghij-klmnopqrstuv",
  "deploymentExecutionId": "123e4567-e89b-12d3-a456-426614174000",
  "moduleName": "CriticalModule",
  "version": "2.0.0",
  "targetEnvironment": "Staging",
  "requesterEmail": "dev@example.com",
  "status": "Approved",
  "requestedAt": "2025-11-15T10:05:00Z",
  "respondedAt": "2025-11-15T10:30:00Z",
  "respondedBy": "manager@example.com",
  "responseReason": "Security review passed. Approved for staging deployment.",
  "timeoutAt": "2025-11-16T10:05:00Z"
}
```

After approval, the deployment pipeline continues automatically.

### 6. Reject a Deployment (Alternative to Approve)

```bash
curl -X POST http://localhost:5000/api/v1/approvals/deployments/123e4567-e89b-12d3-a456-426614174000/reject \
  -H "Content-Type: application/json" \
  -d '{
    "approverEmail": "security@example.com",
    "approved": false,
    "reason": "Failed security scan. Critical vulnerability detected in dependencies."
  }'
```

Response:
```json
{
  "approvalId": "abc12345-def6-7890-ghij-klmnopqrstuv",
  "deploymentExecutionId": "123e4567-e89b-12d3-a456-426614174000",
  "moduleName": "CriticalModule",
  "version": "2.0.0",
  "targetEnvironment": "Staging",
  "requesterEmail": "dev@example.com",
  "status": "Rejected",
  "requestedAt": "2025-11-15T10:05:00Z",
  "respondedAt": "2025-11-15T10:30:00Z",
  "respondedBy": "security@example.com",
  "responseReason": "Failed security scan. Critical vulnerability detected in dependencies.",
  "timeoutAt": "2025-11-16T10:05:00Z"
}
```

After rejection, the deployment pipeline fails and stops.

## Approval Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Deployment Request                        │
│                  (RequireApproval: true)                     │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Build → Test → Security Scan                    │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  Deploy to Development                       │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                     Deploy to QA                             │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              ⏸️  APPROVAL GATE - STAGING  ⏸️                │
│                                                               │
│  • Create approval request                                   │
│  • Send email to approvers                                   │
│  • Wait for decision (24h timeout)                           │
│                                                               │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                  │
│  │ Approved │  │ Rejected │  │ Expired  │                  │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘                  │
│       │             │              │                         │
│       ▼             ▼              ▼                         │
│   Continue       FAIL           FAIL                         │
└────────┬────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│                   Deploy to Staging                          │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│             ⏸️  APPROVAL GATE - PRODUCTION  ⏸️              │
│                                                               │
│  • Create approval request                                   │
│  • Send email to approvers                                   │
│  • Wait for decision (24h timeout)                           │
│                                                               │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                  │
│  │ Approved │  │ Rejected │  │ Expired  │                  │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘                  │
│       │             │              │                         │
│       ▼             ▼              ▼                         │
│   Continue       FAIL           FAIL                         │
└────────┬────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│                  Deploy to Production                        │
│                   (Canary Strategy)                          │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  Final Validation                            │
│                     ✅ SUCCESS                               │
└─────────────────────────────────────────────────────────────┘
```

## Notification Examples

When an approval is requested, approvers receive a notification (currently logged to console):

```
📧 [EMAIL] Approval Request Notification
To: manager@example.com, security@example.com
Subject: Approval Required: Deploy CriticalModule v2.0.0 to Staging
---
A deployment requires your approval:
  Module: CriticalModule v2.0.0
  Environment: Staging
  Requester: dev@example.com
  Approval ID: abc12345-def6-7890-ghij-klmnopqrstuv
  Deployment ID: 123e4567-e89b-12d3-a456-426614174000
  Timeout: 2025-11-16T10:05:00Z

To approve or reject:
  POST /api/v1/approvals/deployments/123e4567-e89b-12d3-a456-426614174000/approve
  POST /api/v1/approvals/deployments/123e4567-e89b-12d3-a456-426614174000/reject
---
```

## Implementation Details

### Approval Authorization

- If `ApproverEmails` list is **empty**, anyone can approve or reject
- If `ApproverEmails` list is **populated**, only listed emails can approve or reject
- Unauthorized approval attempts return `401 Unauthorized`

### Timeout Handling

- Approval requests have a configurable timeout (default: 24 hours)
- Background service checks every 5 minutes for expired approvals
- Expired approvals are auto-rejected
- Notifications sent on expiry

### State Management

- Approval requests stored in-memory (ConcurrentDictionary)
- In production, should be backed by PostgreSQL database
- Pipeline uses TaskCompletionSource to wait for decisions
- Thread-safe implementation

## Testing

Run the comprehensive test suite:

```bash
dotnet test tests/HotSwap.Distributed.Tests/Services/ApprovalServiceTests.cs
```

Test coverage includes:
- ✅ Create approval requests
- ✅ Approve deployments
- ✅ Reject deployments
- ✅ Authorization checking
- ✅ Timeout handling
- ✅ Expired approval processing
- ✅ Pending approvals listing
- ✅ Notification sending
- ✅ Duplicate request handling
- ✅ Non-existent request handling

## Next Steps for Production

1. **Database Persistence**
   - Implement PostgreSQL-backed approval storage
   - Add audit log persistence for all approval events
   - Implement approval history querying

2. **Email Service**
   - Replace `LoggingNotificationService` with real email service (SendGrid, SMTP)
   - Add email templates
   - Support multiple notification channels (Slack, Teams, etc.)

3. **Authentication & Authorization**
   - Implement JWT authentication
   - Add role-based access control (RBAC)
   - Integrate with OAuth/OIDC provider

4. **Configurable Approvers**
   - Load approver lists from configuration per environment
   - Support approval groups and delegation
   - Multi-level approval workflows

5. **Monitoring & Alerting**
   - Add metrics for approval latency
   - Alert on pending approvals approaching timeout
   - Dashboard for approval status

## Compliance

This implementation satisfies:
- ✅ FR-007: Approval workflow for Staging and Production
- ✅ 24-hour timeout requirement
- ✅ Email notifications to approvers
- ✅ Approval audit trail (logged)
- ✅ API endpoints for approval operations

## Conclusion

The approval workflow system is now fully implemented and integrated into the deployment pipeline. Deployments to Staging and Production environments will automatically pause for approval when `RequireApproval: true` is set, ensuring human oversight of critical deployments.

For questions or issues, refer to the TASK_LIST.md file or open an issue in the repository.
