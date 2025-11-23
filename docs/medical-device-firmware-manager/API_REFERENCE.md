# Medical Device Firmware Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Devices API](#devices-api)
3. [Firmware API](#firmware-api)
4. [Deployments API](#deployments-api)
5. [Approvals API](#approvals-api)
6. [Compliance API](#compliance-api)
7. [Hospitals API](#hospitals-api)

---

## Authentication

All endpoints require JWT authentication.

```http
POST /api/v1/authentication/login
{
  "username": "clinicaleng@hospital.com",
  "password": "SecurePass123!"
}

Response 200 OK:
{
  "token": "eyJhbGc...",
  "expiresAt": "2025-11-24T12:00:00Z",
  "user": {
    "username": "clinicaleng@hospital.com",
    "role": "ClinicalEngineer"
  }
}
```

---

## Devices API

### Register Device

```http
POST /api/v1/devices
Authorization: Bearer {token}

{
  "udi": "(01)00643169007222(21)SN12345",
  "modelNumber": "CardiacMonitor-X200",
  "serialNumber": "SN-2025-001",
  "manufacturer": "MedTech Corp",
  "fdaClass": "ClassII",
  "hospitalId": "HOSP-001",
  "currentFirmwareVersion": "2.1.0",
  "location": "ICU Room 3"
}

Response 201 Created:
{
  "deviceId": "DEV-12345",
  "status": "Active",
  "registeredAt": "2025-11-23T10:00:00Z"
}
```

### List Devices

```http
GET /api/v1/devices?hospitalId=HOSP-001&status=Active
Authorization: Bearer {token}

Response 200 OK:
{
  "devices": [
    {
      "deviceId": "DEV-12345",
      "modelNumber": "CardiacMonitor-X200",
      "currentFirmwareVersion": "2.1.0",
      "status": "Active",
      "lastHeartbeat": "2025-11-23T10:05:00Z"
    }
  ],
  "total": 142
}
```

### Get Device Health

```http
GET /api/v1/devices/DEV-12345/health
Authorization: Bearer {token}

Response 200 OK:
{
  "deviceId": "DEV-12345",
  "status": "Healthy",
  "uptime": 259200,
  "errorCountLastHour": 0,
  "cpuUsage": 45.2,
  "memoryUsage": 62.8,
  "isConnected": true,
  "lastErrorAt": null
}
```

---

## Firmware API

### Upload Firmware

```http
POST /api/v1/firmware
Authorization: Bearer {token}
Content-Type: multipart/form-data

{
  "version": "2.2.0",
  "deviceModel": "CardiacMonitor-X200",
  "binaryFile": <file>,
  "releaseNotes": "Critical security patch for cardiac monitoring algorithm",
  "isCriticalPatch": true,
  "fdaSubmissionPath": "/docs/fda-submission-2.2.0.pdf"
}

Response 201 Created:
{
  "firmwareId": "FW-abc123",
  "version": "2.2.0",
  "sha256Checksum": "a1b2c3d4...",
  "approvalStatus": "PendingReview",
  "uploadedAt": "2025-11-23T11:00:00Z"
}
```

### List Firmware

```http
GET /api/v1/firmware?deviceModel=CardiacMonitor-X200&approvalStatus=Approved
Authorization: Bearer {token}

Response 200 OK:
{
  "firmware": [
    {
      "firmwareId": "FW-abc123",
      "version": "2.2.0",
      "approvalStatus": "Approved",
      "approvedAt": "2025-11-23T14:00:00Z"
    }
  ]
}
```

---

## Deployments API

### Create Deployment

```http
POST /api/v1/deployments
Authorization: Bearer {token}

{
  "firmwareId": "FW-abc123",
  "strategy": "Progressive",
  "targetDevices": ["DEV-12345", "DEV-12346"],
  "phases": [
    {
      "name": "Pilot",
      "hospitalCohort": "pilot",
      "percentage": 10,
      "maxErrorRatePercent": 1.0,
      "monitoringDurationHours": 72
    },
    {
      "name": "Regional",
      "hospitalCohort": "regional",
      "percentage": 50,
      "maxErrorRatePercent": 0.5,
      "monitoringDurationHours": 48
    },
    {
      "name": "Full",
      "hospitalCohort": "all",
      "percentage": 100,
      "maxErrorRatePercent": 0.1,
      "monitoringDurationHours": 24
    }
  ]
}

Response 201 Created:
{
  "deploymentId": "DEPLOY-xyz789",
  "status": "Pending",
  "initiatedAt": "2025-11-23T15:00:00Z",
  "currentPhase": "Pilot"
}
```

### Rollback Deployment

```http
POST /api/v1/deployments/DEPLOY-xyz789/rollback
Authorization: Bearer {token}

{
  "reason": "High error rate detected in pilot phase",
  "targetDevices": ["DEV-12345"]
}

Response 200 OK:
{
  "deploymentId": "DEPLOY-xyz789",
  "status": "RolledBack",
  "rolledBackAt": "2025-11-23T15:30:00Z"
}
```

---

## Approvals API

### Request Approval

```http
POST /api/v1/approvals/request
Authorization: Bearer {token}

{
  "firmwareId": "FW-abc123",
  "level": "Clinical",
  "assignedReviewer": "clinicaleng@hospital.com",
  "requiredDocuments": [
    "Safety Analysis",
    "Risk Assessment",
    "Clinical Validation Report"
  ]
}

Response 201 Created:
{
  "approvalId": "APPR-001",
  "status": "PendingReview",
  "expiresAt": "2025-11-25T15:00:00Z"
}
```

### Approve with Electronic Signature

```http
POST /api/v1/approvals/APPR-001/approve
Authorization: Bearer {token}

{
  "reviewerComments": "Safety analysis reviewed and approved",
  "password": "SecurePassword123!",
  "otpCode": "123456",
  "signatureMeaning": "I approve this firmware for deployment",
  "signatureIntent": "Deploy firmware v2.2.0 to CardiacMonitor-X200 devices"
}

Response 200 OK:
{
  "approvalId": "APPR-001",
  "status": "Approved",
  "approvedAt": "2025-11-23T16:00:00Z",
  "signatureId": "SIG-abc123"
}
```

---

## Compliance API

### Get Audit Logs

```http
GET /api/v1/audit-logs?entityType=Firmware&startDate=2025-11-01&endDate=2025-11-23
Authorization: Bearer {token}

Response 200 OK:
{
  "auditLogs": [
    {
      "auditId": 12345,
      "timestamp": "2025-11-23T11:00:00Z",
      "userId": "clinicaleng@hospital.com",
      "action": "FirmwareUploaded",
      "entityType": "Firmware",
      "entityId": "FW-abc123",
      "ipAddress": "192.168.1.100",
      "tamperDetectionHash": "x1y2z3..."
    }
  ],
  "total": 1523
}
```

### Generate Compliance Report

```http
POST /api/v1/reports/generate
Authorization: Bearer {token}

{
  "reportType": "DeploymentSummary",
  "startDate": "2025-11-01",
  "endDate": "2025-11-23",
  "format": "PDF",
  "includeSignatures": true
}

Response 200 OK:
{
  "reportId": "RPT-001",
  "status": "Generating",
  "estimatedCompletionAt": "2025-11-23T17:05:00Z"
}
```

---

**Last Updated:** 2025-11-23
