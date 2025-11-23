# Deployment & Operations Guide

**Version:** 1.0.0
**Last Updated:** 2025-11-23

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Kubernetes Deployment](#kubernetes-deployment)
5. [Model Storage Setup](#model-storage-setup)
6. [Configuration](#configuration)
7. [Monitoring Setup](#monitoring-setup)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Infrastructure

**Mandatory:**
- **.NET 8.0 Runtime** - Application runtime
- **MinIO or S3** - Model artifact storage
- **PostgreSQL 15+** - Metadata storage
- **Redis 7+** - Distributed locks, caching

**Optional:**
- **Jaeger** - Distributed tracing
- **Prometheus** - Metrics collection
- **Grafana** - Metrics visualization

---

## Local Development Setup

### Quick Start

**Step 1: Start Infrastructure**

```bash
docker-compose -f docker-compose.edge-ai.yml up -d
```

**docker-compose.edge-ai.yml:**

```yaml
version: '3.8'

services:
  minio:
    image: minio/minio:latest
    ports:
      - "9000:9000"
      - "9001:9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    command: server /data --console-address ":9001"
    volumes:
      - minio-data:/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  postgres:
    image: postgres:15-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_DB: edge_ai
      POSTGRES_USER: edge_ai_user
      POSTGRES_PASSWORD: dev_password

volumes:
  minio-data:
```

**Step 2: Configure Application**

```bash
cat > appsettings.Development.json <<EOF
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=edge_ai;Username=edge_ai_user;Password=dev_password",
    "Redis": "localhost:6379"
  },
  "ModelStorage": {
    "Provider": "MinIO",
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "BucketName": "ai-models",
    "UseSSL": false
  },
  "Distribution": {
    "MaxConcurrentDistributions": 10,
    "DefaultStrategy": "Canary",
    "DeviceHeartbeatTimeout": "PT5M"
  }
}
