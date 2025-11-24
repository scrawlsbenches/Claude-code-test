# Research Cluster API Reference

**Version:** 1.0.0
**Base URL:** `https://api.example.com/api/v1`
**Authentication:** JWT Bearer Token

---

## Projects API

### Create Project
```http
POST /api/v1/projects
{
  "projectId": "genomics-2025",
  "name": "Cancer Genomics Analysis",
  "owner": "researcher@university.edu",
  "computeBudget": 50000.00,
  "currency": "USD"
}
```

### Get Project
```http
GET /api/v1/projects/{projectId}
```

---

## Workflows API

### Create Workflow
```http
POST /api/v1/workflows
{
  "projectId": "genomics-2025",
  "workflowId": "variant-calling-v2",
  "name": "Variant Calling Pipeline",
  "type": "Pipeline",
  "runtime": "slurm",
  "requirements": {
    "nodes": 10,
    "cpuPerNode": 32,
    "memoryGbPerNode": 128,
    "walltime": "48:00:00"
  },
  "definition": "..."
}
```

### Validate Workflow
```http
POST /api/v1/workflows/{workflowId}/validate
```

---

## Deployments API

### Deploy Workflow
```http
POST /api/v1/deployments
{
  "workflowId": "variant-calling-v2",
  "targetEnvironment": "production",
  "strategy": "Progressive",
  "nodePercentages": [10, 30, 50, 100]
}
```

### Get Deployment Status
```http
GET /api/v1/deployments/{deploymentId}
```

---

## Jobs API

### Submit Job
```http
POST /api/v1/jobs
{
  "workflowId": "variant-calling-v2",
  "environmentId": "prod"
}
```

### Get Job
```http
GET /api/v1/jobs/{jobId}
```

### Get Job Logs
```http
GET /api/v1/jobs/{jobId}/logs
```

### Get Job Metrics
```http
GET /api/v1/jobs/{jobId}/metrics
```

### Cancel Job
```http
POST /api/v1/jobs/{jobId}/cancel
```

---

## Resources API

### Get Cluster Nodes
```http
GET /api/v1/resources/nodes
```

### Get Node Metrics
```http
GET /api/v1/resources/nodes/{nodeId}/metrics
```

### Get Resource Utilization
```http
GET /api/v1/resources/utilization
```

---

## Costs API

### Get Project Costs
```http
GET /api/v1/costs/project/{projectId}
```

### Get Workflow Costs
```http
GET /api/v1/costs/workflow/{workflowId}
```

### Generate Cost Report
```http
POST /api/v1/costs/report
{
  "projectId": "genomics-2025",
  "startDate": "2025-01-01",
  "endDate": "2025-11-23",
  "format": "csv"
}
```

### Get Cost Forecast
```http
GET /api/v1/costs/forecast?projectId={projectId}&months=3
```

---

## Optimization API

### Get Workflow Optimization Recommendations
```http
GET /api/v1/optimize/workflow/{workflowId}
```

### Apply Optimization
```http
POST /api/v1/optimize/{recommendationId}/apply
```

---

**Document Version:** 1.0.0
**Last Updated:** 2025-11-23
