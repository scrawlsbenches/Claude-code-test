# Payment Gateway Rule Manager API Reference

**Version:** 1.0.0
**Base URL:** `https://payment-api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Rules API](#rules-api)
3. [Transactions API](#transactions-api)
4. [Analytics API](#analytics-api)
5. [Processors API](#processors-api)

---

## Authentication

All API endpoints require JWT authentication with appropriate fraud prevention roles.

### Roles
- **FraudAnalyst**: Deploy rules, view metrics
- **RiskManager**: Override rules, force rollback
- **Admin**: Configure processors, manage system

---

## Rules API

### Deploy Rule

Deploy a fraud detection rule with specified mode and configuration.

**Endpoint:** `POST /api/v1/rules/deploy`
**Authorization:** FraudAnalyst, RiskManager, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/rules/deploy
Authorization: Bearer {token}
Content-Type: application/json

{
  "ruleId": "velocity-check-v2.0",
  "type": "Velocity",
  "mode": "Shadow",
  "processor": "stripe",
  "configuration": {
    "maxTransactionsPerHour": 10,
    "maxTransactionsPerDay": 50,
    "maxAmountPerDay": 5000
  },
  "action": "Block",
  "riskScore": 75,
  "priority": 100
}
```

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-rule-123",
  "ruleId": "velocity-check-v2.0",
  "mode": "Shadow",
  "status": "Deploying",
  "deployedAt": "2025-11-23T10:00:00Z",
  "estimatedCompletionTime": "2025-11-23T10:00:30Z"
}
```

---

### Get Rule Metrics

Get real-time performance metrics for a deployed rule.

**Endpoint:** `GET /api/v1/rules/{ruleId}/metrics`
**Authorization:** All authenticated users

**Request:**
```http
GET /api/v1/rules/velocity-check-v2.0/metrics?mode=Shadow&timeRange=24h
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "ruleId": "velocity-check-v2.0",
  "mode": "Shadow",
  "metrics": {
    "evaluation": {
      "totalEvaluations": 125000,
      "blockDecisions": 3200,
      "allowDecisions": 121800,
      "avgLatency": 2.5,
      "p99Latency": 8.2
    },
    "falsePositives": {
      "estimatedCount": 160,
      "estimatedRate": 0.05,
      "confirmedCount": 12,
      "confirmedRate": 0.00375
    },
    "fraudDetection": {
      "estimatedFraudBlocked": 2850,
      "estimatedFraudRate": 0.0228,
      "precision": 0.95,
      "recall": 0.89
    },
    "traffic": {
      "percentage": 0.0,
      "transactionCount": 125000,
      "revenueProtected": 0.0,
      "revenueBlocked": 0.0
    }
  },
  "timestamp": "2025-11-23T10:15:00Z"
}
```

---

### Promote Rule

Promote rule from shadow mode to active or increase traffic allocation.

**Endpoint:** `POST /api/v1/rules/{ruleId}/promote`
**Authorization:** FraudAnalyst, RiskManager, Admin

**Request:**
```http
POST /api/v1/rules/velocity-check-v2.0/promote
Authorization: Bearer {token}
Content-Type: application/json

{
  "mode": "Active",
  "strategy": "Canary",
  "initialPercentage": 0.01,
  "autoProgress": true
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-rule-456",
  "ruleId": "velocity-check-v2.0",
  "previousMode": "Shadow",
  "targetMode": "Active",
  "strategy": "Canary",
  "currentStage": {
    "name": "Micro",
    "percentage": 0.01,
    "transactionsAffected": 1000
  },
  "promotedAt": "2025-11-23T10:20:00Z"
}
```

---

### Rollback Rule

Execute immediate rule rollback to previous version.

**Endpoint:** `POST /api/v1/rules/{ruleId}/rollback`
**Authorization:** RiskManager, Admin

**Request:**
```http
POST /api/v1/rules/velocity-check-v2.0/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "False positive rate exceeded threshold",
  "targetVersion": "velocity-check-v1.0"
}
```

**Response 200 OK:**
```json
{
  "rollbackId": "rollback-xyz-789",
  "ruleId": "velocity-check-v2.0",
  "executionTime": 650,
  "actions": [
    "Rule deactivated",
    "Traffic allocation set to 0%",
    "Previous version activated: velocity-check-v1.0",
    "Blocked transactions released (count: 0)"
  ],
  "rolledBackAt": "2025-11-23T10:25:00Z"
}
```

---

## Transactions API

### Evaluate Transaction

Evaluate a transaction against fraud rules (real-time).

**Endpoint:** `POST /api/v1/transactions/evaluate`
**Authorization:** Processor integration

**Request:**
```http
POST /api/v1/transactions/evaluate
Authorization: Bearer {token}
Content-Type: application/json

{
  "transactionId": "txn_1234567890",
  "amount": 149.99,
  "currency": "USD",
  "customerId": "cus_abc123",
  "paymentMethod": {
    "type": "card",
    "last4": "4242",
    "brand": "visa"
  },
  "billingAddress": {
    "country": "US",
    "zipCode": "10001"
  },
  "deviceFingerprint": {
    "deviceId": "dev_xyz789",
    "ipAddress": "192.168.1.1",
    "userAgent": "Mozilla/5.0..."
  }
}
```

**Response 200 OK:**
```json
{
  "transactionId": "txn_1234567890",
  "decision": "Allow",
  "riskScore": 25,
  "rulesEvaluated": [
    {
      "ruleId": "velocity-check-v2.0",
      "result": "Allow",
      "riskScore": 10,
      "reason": "Within velocity limits"
    },
    {
      "ruleId": "amount-threshold-v1",
      "result": "Allow",
      "riskScore": 5,
      "reason": "Amount within normal range"
    }
  ],
  "evaluationTime": 3.2,
  "timestamp": "2025-11-23T10:30:00Z"
}
```

---

### Get Transaction History

Get fraud rule evaluation history for transactions.

**Endpoint:** `GET /api/v1/transactions`
**Authorization:** All authenticated users

**Request:**
```http
GET /api/v1/transactions?customerId=cus_abc123&startDate=2025-11-01&endDate=2025-11-23&decision=Block
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "transactions": [
    {
      "transactionId": "txn_blocked_001",
      "amount": 999.99,
      "decision": "Block",
      "riskScore": 85,
      "blockedBy": "velocity-check-v2.0",
      "reason": "Exceeded daily transaction limit",
      "isFalsePositive": false,
      "evaluatedAt": "2025-11-22T15:30:00Z"
    }
  ],
  "summary": {
    "totalTransactions": 50,
    "blocked": 2,
    "allowed": 48,
    "falsePositives": 0
  },
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalPages": 1
  }
}
```

---

## Analytics API

### Get False Positive Report

Get detailed false positive analysis.

**Endpoint:** `GET /api/v1/analytics/false-positives`
**Authorization:** All authenticated users

**Request:**
```http
GET /api/v1/analytics/false-positives?ruleId=velocity-check-v2.0&timeRange=7d
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "ruleId": "velocity-check-v2.0",
  "timeRange": "7d",
  "falsePositives": {
    "total": 45,
    "rate": 0.035,
    "byReason": {
      "customerComplaint": 12,
      "manualReview": 18,
      "historicalPattern": 15
    },
    "revenueImpact": 6750.00,
    "customerImpact": 42
  },
  "recommendations": [
    "Increase velocity limit to 12 transactions/hour",
    "Add customer whitelist for high-value repeat customers"
  ]
}
```

---

### Get Fraud Detection Stats

Get fraud detection performance statistics.

**Endpoint:** `GET /api/v1/analytics/fraud-detection`
**Authorization:** All authenticated users

**Response 200 OK:**
```json
{
  "period": "30d",
  "fraudDetection": {
    "totalFraudAttempts": 1250,
    "fraudBlocked": 1175,
    "fraudAllowed": 75,
    "blockRate": 0.94,
    "falsePositiveRate": 0.042
  },
  "byRule": [
    {
      "ruleId": "velocity-check-v2.0",
      "fraudBlocked": 650,
      "precision": 0.95,
      "recall": 0.89
    }
  ]
}
```

---

## Processors API

### List Processors

Get configured payment processors.

**Endpoint:** `GET /api/v1/processors`
**Authorization:** All authenticated users

**Response 200 OK:**
```json
{
  "processors": [
    {
      "processorId": "stripe",
      "name": "Stripe",
      "status": "Active",
      "rulesDeployed": 12,
      "transactionsProcessed": 1250000,
      "apiVersion": "2023-10-16"
    },
    {
      "processorId": "paypal",
      "name": "PayPal",
      "status": "Active",
      "rulesDeployed": 8,
      "transactionsProcessed": 350000,
      "apiVersion": "v2"
    }
  ]
}
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
