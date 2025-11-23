# Trading Algorithm Deployment API Reference

**Version:** 1.0.0
**Base URL:** `https://trading-api.example.com/api/v1`
**Authentication:** JWT Bearer Token
**Content-Type:** `application/json`

---

## Table of Contents

1. [Authentication](#authentication)
2. [Algorithms API](#algorithms-api)
3. [Risk API](#risk-api)
4. [Positions API](#positions-api)
5. [Trades API](#trades-api)

---

## Authentication

All API endpoints require JWT authentication with appropriate trading roles.

### Roles
- **Trader**: Deploy algorithms, view metrics
- **RiskManager**: Override limits, force rollback
- **Admin**: Configure risk limits, approve deployments

---

## Algorithms API

### Deploy Algorithm

Deploy a trading algorithm with specified strategy and environment.

**Endpoint:** `POST /api/v1/algorithms/deploy`
**Authorization:** Trader, RiskManager, Admin
**Rate Limit:** 10 req/min per user

**Request:**
```http
POST /api/v1/algorithms/deploy
Authorization: Bearer {token}
Content-Type: application/json

{
  "algorithmId": "momentum-v2.1.0",
  "environment": "PaperTrading",
  "strategy": "PaperTrading",
  "configuration": {
    "simulatedCapital": 1000000,
    "marketDataSource": "LiveMarketData",
    "exchanges": ["NASDAQ", "NYSE"],
    "symbols": ["AAPL", "MSFT", "GOOGL"]
  },
  "riskLimits": {
    "maxDailyLoss": 0.02,
    "maxDrawdown": 0.05,
    "maxPositionSize": 100000,
    "maxTradeVelocity": 100
  }
}
```

**Response 202 Accepted:**
```json
{
  "deploymentId": "deploy-abc-123",
  "algorithmId": "momentum-v2.1.0",
  "environment": "PaperTrading",
  "status": "Deploying",
  "deployedAt": "2025-11-23T10:00:00Z",
  "estimatedCompletionTime": "2025-11-23T10:00:30Z"
}
```

---

### Get Algorithm Metrics

Get real-time performance metrics for a deployed algorithm.

**Endpoint:** `GET /api/v1/algorithms/{algorithmId}/metrics`
**Authorization:** Trader, RiskManager, Admin

**Request:**
```http
GET /api/v1/algorithms/momentum-v2.1.0/metrics?environment=Production&timeRange=1h
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "algorithmId": "momentum-v2.1.0",
  "environment": "Production",
  "metrics": {
    "pnl": {
      "daily": 12500.50,
      "cumulative": 45200.75,
      "unrealized": 3200.00
    },
    "risk": {
      "currentDrawdown": 0.023,
      "maxDrawdown": 0.034,
      "sharpeRatio": 1.85,
      "volatility": 0.15
    },
    "positions": {
      "totalValue": 250000.00,
      "longValue": 180000.00,
      "shortValue": 70000.00,
      "count": 8
    },
    "trades": {
      "totalToday": 45,
      "winRate": 0.62,
      "avgWin": 550.00,
      "avgLoss": -320.00
    },
    "capital": {
      "allocated": 500000.00,
      "allocationPercent": 0.05,
      "stage": "Small"
    }
  },
  "timestamp": "2025-11-23T10:15:00Z"
}
```

---

### Promote Algorithm

Promote algorithm to next capital allocation stage or environment.

**Endpoint:** `POST /api/v1/algorithms/{algorithmId}/promote`
**Authorization:** Trader, RiskManager, Admin

**Request:**
```http
POST /api/v1/algorithms/momentum-v2.1.0/promote
Authorization: Bearer {token}
Content-Type: application/json

{
  "targetEnvironment": "Production",
  "strategy": "Canary",
  "initialAllocation": 0.01,
  "autoProgress": true
}
```

**Response 200 OK:**
```json
{
  "deploymentId": "deploy-def-456",
  "algorithmId": "momentum-v2.1.0",
  "previousEnvironment": "PaperTrading",
  "targetEnvironment": "Production",
  "strategy": "Canary",
  "currentStage": {
    "name": "Micro",
    "allocation": 0.01,
    "capitalAllocated": 100000.00
  },
  "promotedAt": "2025-11-23T10:20:00Z"
}
```

---

### Rollback Algorithm

Execute immediate algorithm rollback.

**Endpoint:** `POST /api/v1/algorithms/{algorithmId}/rollback`
**Authorization:** RiskManager, Admin

**Request:**
```http
POST /api/v1/algorithms/momentum-v2.1.0/rollback
Authorization: Bearer {token}
Content-Type: application/json

{
  "reason": "Maximum drawdown exceeded",
  "flattenPositions": false,
  "targetVersion": "momentum-v2.0.0"
}
```

**Response 200 OK:**
```json
{
  "rollbackId": "rollback-xyz-789",
  "algorithmId": "momentum-v2.1.0",
  "executionTime": 850,
  "actions": [
    "Algorithm halted",
    "All open orders canceled (count: 5)",
    "Capital allocation set to 0%",
    "Previous version activated: momentum-v2.0.0"
  ],
  "rolledBackAt": "2025-11-23T10:25:00Z"
}
```

---

## Risk API

### Get Risk Limits

Get configured risk limits for an algorithm.

**Endpoint:** `GET /api/v1/risk/limits/{algorithmId}`
**Authorization:** All authenticated users

**Response 200 OK:**
```json
{
  "algorithmId": "momentum-v2.1.0",
  "limits": {
    "maxDailyLoss": {
      "percent": 0.02,
      "absolute": 50000.00,
      "current": 12500.50,
      "remaining": 37499.50
    },
    "maxDrawdown": {
      "threshold": 0.05,
      "current": 0.023,
      "peak": 458200.75,
      "currentValue": 447670.25
    },
    "maxPositionSize": {
      "perSymbol": 100000.00,
      "portfolio": 500000.00,
      "largest": {
        "symbol": "AAPL",
        "value": 85000.00
      }
    },
    "maxTradeVelocity": {
      "perMinute": 100,
      "current": 12
    }
  },
  "violations": [],
  "timestamp": "2025-11-23T10:30:00Z"
}
```

---

### Update Risk Limits

Update risk limits for an algorithm (requires approval).

**Endpoint:** `PUT /api/v1/risk/limits/{algorithmId}`
**Authorization:** RiskManager, Admin

**Request:**
```http
PUT /api/v1/risk/limits/momentum-v2.1.0
Authorization: Bearer {token}
Content-Type: application/json

{
  "maxDailyLoss": 0.03,
  "maxDrawdown": 0.07,
  "reason": "Increasing limits for proven algorithm"
}
```

**Response 202 Accepted:**
```json
{
  "approvalId": "approval-123",
  "status": "PendingApproval",
  "requiresApproval": true,
  "approver": "risk-manager@example.com"
}
```

---

## Positions API

### Get Current Positions

Get all current positions for an algorithm.

**Endpoint:** `GET /api/v1/algorithms/{algorithmId}/positions`
**Authorization:** All authenticated users

**Response 200 OK:**
```json
{
  "algorithmId": "momentum-v2.1.0",
  "positions": [
    {
      "symbol": "AAPL",
      "quantity": 500,
      "side": "Long",
      "avgPrice": 170.25,
      "currentPrice": 172.50,
      "marketValue": 86250.00,
      "unrealizedPnL": 1125.00,
      "exchange": "NASDAQ"
    },
    {
      "symbol": "MSFT",
      "quantity": -200,
      "side": "Short",
      "avgPrice": 380.50,
      "currentPrice": 378.25,
      "marketValue": 75650.00,
      "unrealizedPnL": 450.00,
      "exchange": "NASDAQ"
    }
  ],
  "summary": {
    "totalPositions": 8,
    "longValue": 180000.00,
    "shortValue": 70000.00,
    "netValue": 110000.00,
    "totalUnrealizedPnL": 3200.00
  },
  "timestamp": "2025-11-23T10:35:00Z"
}
```

---

## Trades API

### Get Trade History

Get trade execution history for an algorithm.

**Endpoint:** `GET /api/v1/algorithms/{algorithmId}/trades`
**Authorization:** All authenticated users

**Request:**
```http
GET /api/v1/algorithms/momentum-v2.1.0/trades?startDate=2025-11-23&endDate=2025-11-23&limit=50
Authorization: Bearer {token}
```

**Response 200 OK:**
```json
{
  "algorithmId": "momentum-v2.1.0",
  "trades": [
    {
      "tradeId": "trade-001",
      "symbol": "AAPL",
      "side": "Buy",
      "quantity": 100,
      "price": 170.25,
      "value": 17025.00,
      "commission": 1.00,
      "exchange": "NASDAQ",
      "executedAt": "2025-11-23T09:30:05Z",
      "orderType": "Limit",
      "attribution": {
        "algorithmVersion": "momentum-v2.1.0",
        "deploymentId": "deploy-abc-123",
        "signalType": "Momentum"
      }
    }
  ],
  "summary": {
    "totalTrades": 45,
    "totalValue": 1250000.00,
    "totalCommissions": 45.00,
    "winningTrades": 28,
    "losingTrades": 17,
    "winRate": 0.62
  },
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalPages": 1
  }
}
```

---

**Last Updated:** 2025-11-23
**Version:** 1.0.0
