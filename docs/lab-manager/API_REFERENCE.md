# Lab Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Courses API](#courses-api)
3. [Labs API](#labs-api)
4. [Environments API](#environments-api)
5. [Submissions API](#submissions-api)
6. [Grading API](#grading-api)
7. [Progress API](#progress-api)
8. [LMS Integration API](#lms-integration-api)
9. [Error Responses](#error-responses)
10. [Rate Limiting](#rate-limiting)

---

## Authentication

All API endpoints (except `/health`) require JWT authentication.

### Get JWT Token

```http
POST /api/v1/authentication/login
Content-Type: application/json

{
  "username": "instructor@example.com",
  "password": "Instructor123!"
}

Response 200 OK:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-11-24T12:00:00Z",
  "user": {
    "username": "instructor@example.com",
    "role": "Instructor"
  }
}
```

### Use Token in Requests

```http
GET /api/v1/courses
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Courses API

### Create Course

Create a new course.

**Endpoint:** `POST /api/v1/courses`
**Authorization:** Instructor, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/courses
Authorization: Bearer {token}
Content-Type: application/json

{
  "courseName": "CS101",
  "title": "Introduction to Programming",
  "term": "Fall 2025",
  "instructor": "instructor@example.com",
  "description": "Learn the fundamentals of programming using C#"
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `courseName` | string | Yes | Unique course ID (e.g., "CS101") |
| `title` | string | Yes | Course title |
| `term` | string | Yes | Academic term (e.g., "Fall 2025") |
| `instructor` | string | Yes | Instructor email/username |
| `description` | string | No | Course description |

**Response 201 Created:**
```json
{
  "courseName": "CS101",
  "title": "Introduction to Programming",
  "term": "Fall 2025",
  "instructor": "instructor@example.com",
  "description": "Learn the fundamentals of programming using C#",
  "status": "Active",
  "enrollmentCount": 0,
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### List Courses

Get all courses (filtered by status, instructor, term).

**Endpoint:** `GET /api/v1/courses`
**Authorization:** Instructor, TA, Admin, Student
**Rate Limit:** 60 req/min per user

**Request:**
```http
GET /api/v1/courses?status=Active&term=Fall%202025
Authorization: Bearer {token}
```

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `status` | string | No | Filter by status (Active, Archived) |
| `instructor` | string | No | Filter by instructor |
| `term` | string | No | Filter by term |

**Response 200 OK:**
```json
{
  "courses": [
    {
      "courseName": "CS101",
      "title": "Introduction to Programming",
      "term": "Fall 2025",
      "instructor": "instructor@example.com",
      "status": "Active",
      "enrollmentCount": 150
    }
  ],
  "total": 1
}
```

---

### Get Course

Get course details.

**Endpoint:** `GET /api/v1/courses/{courseName}`
**Authorization:** Instructor, TA, Admin, Student (enrolled)

**Response 200 OK:**
```json
{
  "courseName": "CS101",
  "title": "Introduction to Programming",
  "term": "Fall 2025",
  "instructor": "instructor@example.com",
  "description": "Learn the fundamentals of programming using C#",
  "status": "Active",
  "enrollmentCount": 150,
  "createdAt": "2025-11-23T12:00:00Z",
  "updatedAt": "2025-11-23T12:00:00Z"
}
```

---

### Update Course

Update course configuration.

**Endpoint:** `PUT /api/v1/courses/{courseName}`
**Authorization:** Instructor (course owner), Admin

**Request:**
```http
PUT /api/v1/courses/CS101
Authorization: Bearer {token}
Content-Type: application/json

{
  "title": "Introduction to Programming with C#",
  "description": "Updated description"
}
```

**Response 200 OK:**
```json
{
  "courseName": "CS101",
  "title": "Introduction to Programming with C#",
  "description": "Updated description",
  "updatedAt": "2025-11-23T13:00:00Z"
}
```

---

### Archive Course

Archive a course (makes it read-only).

**Endpoint:** `POST /api/v1/courses/{courseName}/archive`
**Authorization:** Instructor (course owner), Admin

**Response 200 OK:**
```json
{
  "courseName": "CS101",
  "status": "Archived",
  "archivedAt": "2025-11-23T14:00:00Z"
}
```

---

## Labs API

### Create Lab

Create a new lab exercise.

**Endpoint:** `POST /api/v1/labs`
**Authorization:** Instructor, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/labs
Authorization: Bearer {token}
Content-Type: application/json

{
  "courseName": "CS101",
  "labNumber": 1,
  "title": "Hello World",
  "description": "Introduction to C# programming",
  "instructions": "# Lab 1: Hello World\n\nWrite a C# program...",
  "type": "Coding",
  "resourceTemplate": "dotnet-basic",
  "starterCodeUrl": "https://github.com/course/lab1-starter",
  "dueDate": "2025-11-30T23:59:59Z",
  "latePolicy": "Penalty",
  "latePenaltyPercent": 10,
  "maxSubmissionAttempts": 3,
  "autogradingEnabled": true,
  "autograderConfig": {
    "type": "Docker",
    "dockerImage": "gradescope/autograder-dotnet",
    "testScript": "/autograder/run_tests.sh",
    "timeoutSeconds": 300
  },
  "totalPoints": 100
}
```

**Response 201 Created:**
```json
{
  "labId": "lab-cs101-1",
  "courseName": "CS101",
  "labNumber": 1,
  "title": "Hello World",
  "status": "Draft",
  "version": "1.0",
  "createdAt": "2025-11-23T12:00:00Z"
}
```

---

### Publish Lab

Publish a lab (make it available to students).

**Endpoint:** `POST /api/v1/labs/{labId}/publish`
**Authorization:** Instructor, Admin

**Response 200 OK:**
```json
{
  "labId": "lab-cs101-1",
  "status": "Published",
  "publishedAt": "2025-11-23T14:00:00Z"
}
```

---

### Deploy Lab to Cohort

Deploy a lab to a specific cohort or all students.

**Endpoint:** `POST /api/v1/deployments`
**Authorization:** Instructor, Admin
**Rate Limit:** 5 req/min per user

**Request:**
```http
POST /api/v1/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "labId": "lab-cs101-1",
  "cohortName": "section-a",
  "strategy": "Progressive",
  "schedule": "2025-11-25T09:00:00Z",
  "notifyStudents": true
}
```

**Request Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `labId` | string | Yes | Lab to deploy |
| `cohortName` | string | No | Target cohort (null = all students) |
| `strategy` | string | Yes | Deployment strategy (Direct, Cohort, Progressive, Scheduled) |
| `schedule` | datetime | No | Schedule time (for Scheduled strategy) |
| `notifyStudents` | boolean | No | Send email notification (default: true) |

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-123",
  "labId": "lab-cs101-1",
  "cohortName": "section-a",
  "strategy": "Progressive",
  "status": "InProgress",
  "startedAt": "2025-11-23T14:00:00Z"
}
```

---

### Get Deployment Status

Get deployment status.

**Endpoint:** `GET /api/v1/deployments/{deploymentId}`
**Authorization:** Instructor, Admin

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-123",
  "labId": "lab-cs101-1",
  "strategy": "Progressive",
  "status": "Completed",
  "environmentsProvisioned": 150,
  "failures": 2,
  "startedAt": "2025-11-23T14:00:00Z",
  "completedAt": "2025-11-23T14:05:00Z",
  "duration": "00:05:00"
}
```

---

## Environments API

### Provision Environment

Provision a student environment (usually done automatically on deployment).

**Endpoint:** `POST /api/v1/environments`
**Authorization:** Student, Instructor, Admin

**Request:**
```http
POST /api/v1/environments
Authorization: Bearer {token}
Content-Type: application/json

{
  "studentId": "student@example.com",
  "labId": "lab-cs101-1"
}
```

**Response 202 Accepted:**
```json
{
  "environmentId": "env-abc123",
  "studentId": "student@example.com",
  "labId": "lab-cs101-1",
  "status": "Provisioning",
  "provisionedAt": "2025-11-23T14:00:00Z"
}
```

---

### Get Environment

Get environment details.

**Endpoint:** `GET /api/v1/environments/{environmentId}`
**Authorization:** Student (owner), Instructor, TA, Admin

**Response 200 OK:**
```json
{
  "environmentId": "env-abc123",
  "studentId": "student@example.com",
  "labId": "lab-cs101-1",
  "courseName": "CS101",
  "status": "Active",
  "accessUrl": "https://env-abc123.labs.example.com/?token=xyz",
  "quota": {
    "cpuCores": 2.0,
    "memoryGb": 4.0,
    "storageGb": 10.0
  },
  "usage": {
    "cpuCores": 0.5,
    "memoryGb": 1.2,
    "storageGb": 2.3
  },
  "activeTime": "02:30:00",
  "accessCount": 5,
  "lastAccessedAt": "2025-11-23T14:30:00Z"
}
```

---

### Get Environment Access URL

Get web access URL for environment.

**Endpoint:** `GET /api/v1/environments/{environmentId}/access`
**Authorization:** Student (owner), Instructor, TA, Admin

**Response 200 OK:**
```json
{
  "accessUrl": "https://env-abc123.labs.example.com/?token=xyz",
  "expiresAt": "2025-11-23T18:00:00Z"
}
```

---

### Suspend Environment

Suspend an environment (pause to save resources).

**Endpoint:** `POST /api/v1/environments/{environmentId}/suspend`
**Authorization:** Student (owner), Instructor, Admin

**Response 200 OK:**
```json
{
  "environmentId": "env-abc123",
  "status": "Suspended",
  "suspendedAt": "2025-11-23T15:00:00Z"
}
```

---

### Resume Environment

Resume a suspended environment.

**Endpoint:** `POST /api/v1/environments/{environmentId}/resume`
**Authorization:** Student (owner), Instructor, Admin

**Response 200 OK:**
```json
{
  "environmentId": "env-abc123",
  "status": "Active",
  "resumedAt": "2025-11-23T16:00:00Z"
}
```

---

## Submissions API

### Submit Lab

Submit lab work for grading.

**Endpoint:** `POST /api/v1/submissions`
**Authorization:** Student
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/submissions
Authorization: Bearer {token}
Content-Type: multipart/form-data

studentId: student@example.com
labId: lab-cs101-1
notes: This is my submission
files: [file1.cs, file2.cs]
```

**Response 201 Created:**
```json
{
  "submissionId": "sub-xyz789",
  "studentId": "student@example.com",
  "labId": "lab-cs101-1",
  "attemptNumber": 1,
  "submittedAt": "2025-11-23T16:00:00Z",
  "isLate": false,
  "status": "Pending",
  "receiptId": "RECEIPT-sub-xyz789-20251123160000"
}
```

---

### Get Submission

Get submission details.

**Endpoint:** `GET /api/v1/submissions/{submissionId}`
**Authorization:** Student (owner), Instructor, TA, Admin

**Response 200 OK:**
```json
{
  "submissionId": "sub-xyz789",
  "studentId": "student@example.com",
  "labId": "lab-cs101-1",
  "attemptNumber": 1,
  "submittedAt": "2025-11-23T16:00:00Z",
  "isLate": false,
  "daysLate": 0,
  "status": "Graded",
  "files": [
    {
      "filePath": "Program.cs",
      "sizeBytes": 1024,
      "storageUrl": "s3://submissions/sub-xyz789/Program.cs"
    }
  ]
}
```

---

### Get Student Submissions

Get all submissions for a student in a specific lab.

**Endpoint:** `GET /api/v1/submissions/student/{studentId}/lab/{labId}`
**Authorization:** Student (owner), Instructor, TA, Admin

**Response 200 OK:**
```json
{
  "submissions": [
    {
      "submissionId": "sub-xyz789",
      "attemptNumber": 1,
      "submittedAt": "2025-11-23T16:00:00Z",
      "status": "Graded",
      "score": 85
    }
  ],
  "total": 1
}
```

---

## Grading API

### Get Grading Results

Get grading results for a submission.

**Endpoint:** `GET /api/v1/grading/{submissionId}/results`
**Authorization:** Student (owner), Instructor, TA, Admin

**Response 200 OK:**
```json
{
  "submissionId": "sub-xyz789",
  "score": 85,
  "totalPoints": 100,
  "percentage": 85.0,
  "status": "Completed",
  "feedback": "# Grading Results\n\nGood work! ...",
  "testResults": [
    {
      "testName": "Test_HelloWorld",
      "passed": true,
      "points": 50,
      "totalPoints": 50
    },
    {
      "testName": "Test_Advanced",
      "passed": false,
      "points": 35,
      "totalPoints": 50,
      "errorMessage": "Expected output: '42', got: '40'"
    }
  ],
  "gradingStartedAt": "2025-11-23T16:01:00Z",
  "gradingCompletedAt": "2025-11-23T16:03:00Z",
  "gradingDuration": "00:02:00"
}
```

---

### Override Grade

Manually override autograding result (instructor only).

**Endpoint:** `PUT /api/v1/grading/{submissionId}/override`
**Authorization:** Instructor, Admin

**Request:**
```http
PUT /api/v1/grading/sub-xyz789/override
Authorization: Bearer {token}
Content-Type: application/json

{
  "score": 90,
  "reason": "Extra credit for creative solution"
}
```

**Response 200 OK:**
```json
{
  "submissionId": "sub-xyz789",
  "score": 90,
  "manualOverride": true,
  "overrideReason": "Extra credit for creative solution",
  "overrideBy": "instructor@example.com"
}
```

---

## Progress API

### Get Course Progress

Get progress analytics for a course.

**Endpoint:** `GET /api/v1/progress/course/{courseName}`
**Authorization:** Instructor, TA, Admin

**Response 200 OK:**
```json
{
  "courseName": "CS101",
  "totalStudents": 150,
  "labsPublished": 5,
  "averageCompletionRate": 78.5,
  "averageScore": 82.3,
  "strugglingStudents": 12,
  "labs": [
    {
      "labId": "lab-cs101-1",
      "title": "Hello World",
      "studentsStarted": 145,
      "studentsSubmitted": 140,
      "studentsGraded": 135,
      "averageScore": 85.2,
      "completionRate": 96.7
    }
  ]
}
```

---

### Get Student Progress

Get progress for a specific student.

**Endpoint:** `GET /api/v1/progress/student/{studentId}`
**Authorization:** Student (self), Instructor, TA, Admin

**Response 200 OK:**
```json
{
  "studentId": "student@example.com",
  "courseName": "CS101",
  "labs": [
    {
      "labId": "lab-cs101-1",
      "title": "Hello World",
      "labStarted": true,
      "labSubmitted": true,
      "labGraded": true,
      "activeTime": "02:30:00",
      "accessCount": 5,
      "submissionAttempts": 1,
      "score": 85,
      "completionPercent": 100
    }
  ],
  "overallCompletionRate": 80.0,
  "averageScore": 82.5
}
```

---

### Get Struggling Students

Get list of struggling students (low progress, high error rate).

**Endpoint:** `GET /api/v1/progress/struggling`
**Authorization:** Instructor, TA, Admin

**Response 200 OK:**
```json
{
  "strugglingStudents": [
    {
      "studentId": "student2@example.com",
      "labId": "lab-cs101-2",
      "labStarted": true,
      "labSubmitted": false,
      "activeTime": "00:15:00",
      "accessCount": 12,
      "completionPercent": 20,
      "daysSinceStart": 8,
      "reason": "Low completion after 8 days"
    }
  ],
  "total": 12
}
```

---

## LMS Integration API

### LTI 1.3 Launch

LTI 1.3 launch endpoint (called by LMS).

**Endpoint:** `POST /api/v1/lms/lti/launch`
**Authorization:** LTI 1.3 JWT (from LMS)

**Request:**
```http
POST /api/v1/lms/lti/launch
Content-Type: application/x-www-form-urlencoded

id_token=eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response 302 Redirect:**
```
Location: https://labs.example.com/courses/CS101?token=xyz
```

---

### Grade Passback

Pass grade back to LMS gradebook.

**Endpoint:** `POST /api/v1/lms/grades/passback`
**Authorization:** System (internal)

**Request:**
```http
POST /api/v1/lms/grades/passback
Content-Type: application/json

{
  "submissionId": "sub-xyz789",
  "studentId": "student@example.com",
  "labId": "lab-cs101-1",
  "score": 85,
  "totalPoints": 100
}
```

**Response 200 OK:**
```json
{
  "success": true,
  "lmsGradeId": "12345",
  "passedBackAt": "2025-11-23T16:05:00Z"
}
```

---

## Error Responses

### Standard Error Format

All errors follow this format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid course name format",
    "details": [
      "CourseName must contain only alphanumeric characters, underscores, and dashes"
    ],
    "timestamp": "2025-11-23T12:00:00Z",
    "traceId": "abc-123"
  }
}
```

### Common Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `VALIDATION_ERROR` | 400 | Request validation failed |
| `UNAUTHORIZED` | 401 | Authentication required |
| `FORBIDDEN` | 403 | Insufficient permissions |
| `NOT_FOUND` | 404 | Resource not found |
| `CONFLICT` | 409 | Resource already exists |
| `RATE_LIMIT_EXCEEDED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Server error |

---

## Rate Limiting

Rate limits are enforced per user per endpoint.

### Limits

| Endpoint Group | Limit |
|----------------|-------|
| Courses API | 60 req/min |
| Labs API | 30 req/min |
| Environments API | 120 req/min |
| Submissions API | 10 req/min |
| Progress API | 60 req/min |

### Rate Limit Headers

```http
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 45
X-RateLimit-Reset: 2025-11-23T12:01:00Z
```

### Rate Limit Exceeded Response

```json
{
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Rate limit exceeded. Try again in 30 seconds.",
    "retryAfter": 30
  }
}
```

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
