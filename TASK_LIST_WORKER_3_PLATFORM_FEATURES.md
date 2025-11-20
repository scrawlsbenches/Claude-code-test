# Task List - Worker Thread 3: Platform & Features Focus

**Assigned To:** Worker Thread 3
**Focus Area:** Platform Infrastructure, Real-Time Features, Kubernetes Integration, Documentation
**Estimated Total Effort:** 8-10 days
**Priority:** Medium (Platform enhancements and developer experience)

---

## Delegation Prompt

You are Worker Thread 3, responsible for **platform infrastructure and feature enhancements** for the Distributed Kernel Orchestration System. Your focus is on adding real-time communication capabilities, Kubernetes deployment support, service discovery integration, and architectural documentation.

### Context

This is a production-ready .NET 8.0 distributed orchestration system currently at 97% specification compliance. The core deployment pipeline is complete with all 4 strategies (Direct, Rolling, Blue-Green, Canary) fully implemented and tested. Your mission is to enhance the platform with modern infrastructure features and improve the developer experience.

### Your Responsibilities

1. **Real-Time Updates** - Implement WebSocket support with SignalR for live deployment monitoring
2. **Kubernetes Integration** - Create Helm charts for production-grade K8s deployment
3. **Service Discovery** - Integrate Consul/etcd for dynamic node discovery
4. **Architecture Documentation** - Create Architecture Decision Records (ADRs) for key technical choices

### Development Environment

- **Platform:** .NET 8.0 with C# 12
- **Architecture:** Clean 4-layer architecture (API → Orchestrator → Infrastructure → Domain)
- **Testing:** xUnit, Moq, FluentAssertions (TDD mandatory)
- **Build:** 582 unit tests (568 passing, 14 skipped), 0 warnings, ~18s build time
- **Documentation:** CLAUDE.md (development guide), SKILLS.md (7 automated workflows)

### Critical Guidelines

**MANDATORY - Before ANY commit:**
```bash
dotnet clean && dotnet restore && dotnet build --no-incremental && dotnet test
```
If any step fails → DO NOT commit. See CLAUDE.md Pre-Commit Checklist.

**Test-Driven Development (TDD) - MANDATORY:**
- 🔴 RED: Write failing test FIRST
- 🟢 GREEN: Write minimal code to pass test
- 🔵 REFACTOR: Improve code quality
- Always run `dotnet test` before committing

**Git Workflow:**
- Branch: `claude/[task-name]-[session-id]`
- Push: `git push -u origin claude/[branch-name]`
- Retry on network errors: 4 times with exponential backoff (2s, 4s, 8s, 16s)

### Use Claude Skills

Leverage automated workflows in `.claude/skills/`:
- `/tdd-helper` - Guide Red-Green-Refactor TDD workflow (use for ALL code changes)
- `/precommit-check` - Validate before commits (use before EVERY commit)
- `/test-coverage-analyzer` - Maintain 85%+ coverage (use after features)
- `/doc-sync-check` - Prevent stale docs (use before commits, monthly)

See [SKILLS.md](SKILLS.md) for complete documentation.

---

## Task #6: WebSocket Real-Time Updates 🟢 HIGH PRIORITY

**Status:** ⏳ Not Implemented
**Effort:** 2-3 days
**Priority:** 🟢 Medium (High value for monitoring UX)
**References:** BUILD_STATUS.md:378, PROJECT_STATUS_REPORT.md:519, examples/ApiUsageExample/README.md:515

### Requirements

- [ ] Add SignalR NuGet package (Microsoft.AspNetCore.SignalR 8.0)
- [ ] Create DeploymentHub for WebSocket connections
- [ ] Implement real-time deployment status updates
- [ ] Implement real-time metrics streaming
- [ ] Add client subscription management
- [ ] Create JavaScript client example
- [ ] Create C# client example
- [ ] Update API examples to demonstrate WebSocket usage
- [ ] Add unit tests for hub logic (15+ tests)

### Implementation Guidance

**Architecture:**
```
API Layer:
├── Hubs/
│   ├── DeploymentHub.cs           # SignalR hub for deployments
│   └── MetricsHub.cs              # SignalR hub for metrics
├── Services/
│   ├── IDeploymentNotifier.cs     # Interface for broadcasting updates
│   └── SignalRDeploymentNotifier.cs # SignalR implementation

Infrastructure Layer:
├── BackgroundServices/
│   └── MetricsStreamingService.cs # Stream metrics every 5 seconds
```

**NuGet Package:**
```bash
dotnet add src/HotSwap.Distributed.Api/HotSwap.Distributed.Api.csproj package Microsoft.AspNetCore.SignalR
```

**Hub Implementation:**

**src/HotSwap.Distributed.Api/Hubs/DeploymentHub.cs:**
```csharp
using Microsoft.AspNetCore.SignalR;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Api.Hubs;

/// <summary>
/// SignalR hub for real-time deployment updates.
/// </summary>
public class DeploymentHub : Hub
{
    private readonly ILogger<DeploymentHub> _logger;

    public DeploymentHub(ILogger<DeploymentHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to updates for a specific deployment.
    /// </summary>
    public async Task SubscribeToDeployment(string executionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"deployment-{executionId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to deployment {ExecutionId}",
            Context.ConnectionId, executionId);
    }

    /// <summary>
    /// Unsubscribe from deployment updates.
    /// </summary>
    public async Task UnsubscribeFromDeployment(string executionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"deployment-{executionId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from deployment {ExecutionId}",
            Context.ConnectionId, executionId);
    }

    /// <summary>
    /// Subscribe to all deployment updates (Admin only).
    /// </summary>
    public async Task SubscribeToAllDeployments()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-deployments");
        _logger.LogInformation("Client {ConnectionId} subscribed to all deployments",
            Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }
}
```

**Broadcasting Updates:**

**src/HotSwap.Distributed.Api/Services/SignalRDeploymentNotifier.cs:**
```csharp
using Microsoft.AspNetCore.SignalR;
using HotSwap.Distributed.Api.Hubs;
using HotSwap.Distributed.Domain.Models;

namespace HotSwap.Distributed.Api.Services;

public interface IDeploymentNotifier
{
    Task NotifyDeploymentStatusChanged(string executionId, DeploymentStatus status);
    Task NotifyDeploymentProgress(string executionId, string stage, int progress);
}

public class SignalRDeploymentNotifier : IDeploymentNotifier
{
    private readonly IHubContext<DeploymentHub> _hubContext;
    private readonly ILogger<SignalRDeploymentNotifier> _logger;

    public SignalRDeploymentNotifier(
        IHubContext<DeploymentHub> hubContext,
        ILogger<SignalRDeploymentNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyDeploymentStatusChanged(string executionId, DeploymentStatus status)
    {
        _logger.LogInformation("Broadcasting status update for deployment {ExecutionId}: {Status}",
            executionId, status.Status);

        await _hubContext.Clients
            .Group($"deployment-{executionId}")
            .SendAsync("DeploymentStatusChanged", new
            {
                ExecutionId = executionId,
                Status = status.Status.ToString(),
                UpdatedAt = DateTime.UtcNow,
                Details = status
            });

        // Also broadcast to "all-deployments" group
        await _hubContext.Clients
            .Group("all-deployments")
            .SendAsync("DeploymentStatusChanged", new
            {
                ExecutionId = executionId,
                Status = status.Status.ToString(),
                UpdatedAt = DateTime.UtcNow,
                Details = status
            });
    }

    public async Task NotifyDeploymentProgress(string executionId, string stage, int progress)
    {
        await _hubContext.Clients
            .Group($"deployment-{executionId}")
            .SendAsync("DeploymentProgress", new
            {
                ExecutionId = executionId,
                Stage = stage,
                Progress = progress,
                Timestamp = DateTime.UtcNow
            });
    }
}
```

**Configure SignalR in Program.cs:**
```csharp
// Add SignalR
builder.Services.AddSignalR();

// Register deployment notifier
builder.Services.AddSingleton<IDeploymentNotifier, SignalRDeploymentNotifier>();

// Map SignalR hubs
app.MapHub<DeploymentHub>("/hubs/deployments");
app.MapHub<MetricsHub>("/hubs/metrics");
```

**JavaScript Client Example:**

**examples/javascript-websocket-client/index.html:**
```html
<!DOCTYPE html>
<html>
<head>
    <title>Deployment Monitor</title>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.0/dist/browser/signalr.min.js"></script>
</head>
<body>
    <h1>Real-Time Deployment Monitor</h1>
    <div id="status">Connecting...</div>
    <div id="deployments"></div>

    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("http://localhost:5000/hubs/deployments")
            .withAutomaticReconnect()
            .build();

        connection.on("DeploymentStatusChanged", (update) => {
            console.log("Deployment update:", update);
            document.getElementById("deployments").innerHTML += `
                <p><strong>${update.ExecutionId}</strong>: ${update.Status} at ${update.UpdatedAt}</p>
            `;
        });

        connection.on("DeploymentProgress", (progress) => {
            console.log("Deployment progress:", progress);
            document.getElementById("deployments").innerHTML += `
                <p>${progress.ExecutionId} - ${progress.Stage}: ${progress.Progress}%</p>
            `;
        });

        connection.start()
            .then(() => {
                console.log("Connected to DeploymentHub");
                document.getElementById("status").textContent = "Connected";

                // Subscribe to all deployments
                connection.invoke("SubscribeToAllDeployments");
            })
            .catch(err => {
                console.error("Connection error:", err);
                document.getElementById("status").textContent = "Connection failed";
            });
    </script>
</body>
</html>
```

**C# Client Example:**

**examples/csharp-websocket-client/Program.cs:**
```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/hubs/deployments")
    .WithAutomaticReconnect()
    .Build();

connection.On<dynamic>("DeploymentStatusChanged", update =>
{
    Console.WriteLine($"Deployment {update.ExecutionId}: {update.Status}");
});

connection.On<dynamic>("DeploymentProgress", progress =>
{
    Console.WriteLine($"Progress: {progress.Stage} - {progress.Progress}%");
});

await connection.StartAsync();
Console.WriteLine("Connected to DeploymentHub");

// Subscribe to specific deployment
await connection.InvokeAsync("SubscribeToDeployment", "your-execution-id");

Console.WriteLine("Listening for updates... Press any key to exit.");
Console.ReadKey();

await connection.StopAsync();
```

**Integrate with Deployment Pipeline:**

Modify `DeploymentPipeline.cs` to send real-time updates:
```csharp
public class DeploymentPipeline
{
    private readonly IDeploymentNotifier _notifier;

    public async Task<DeploymentResult> ExecuteAsync(...)
    {
        // ... existing code ...

        foreach (var stage in stages)
        {
            // Send progress update
            await _notifier.NotifyDeploymentProgress(
                request.ExecutionId,
                stage.ToString(),
                progress);

            // Execute stage...
        }

        // Send final status update
        await _notifier.NotifyDeploymentStatusChanged(
            request.ExecutionId,
            result);
    }
}
```

### Test Coverage Required

- Hub connection/disconnection tests (3 tests)
- Subscription management tests (4 tests)
- Broadcasting tests (5 tests)
- Authorization tests (3 tests)
- Integration with deployment pipeline (5 tests)

### Acceptance Criteria

- ✅ Clients can subscribe to deployment updates via WebSocket
- ✅ Real-time events pushed on deployment status changes
- ✅ Real-time progress updates during deployment execution
- ✅ Connection management and automatic reconnection working
- ✅ Performance tested with 100+ concurrent connections
- ✅ JavaScript and C# client examples provided
- ✅ Unit tests: 15+ tests covering hub logic

### Documentation Required

- Create `docs/WEBSOCKET_GUIDE.md` (~400-600 lines)
  - SignalR setup and configuration
  - Client connection guide (JavaScript, C#, TypeScript)
  - Event reference (all hub methods and callbacks)
  - Authentication with JWT tokens
  - Performance tuning and scaling
- Update `README.md` with WebSocket features
- Update `examples/ApiUsageExample/README.md` with WebSocket usage
- Update `TASK_LIST.md` (mark as complete)

---

## Task #8: Helm Charts for Kubernetes 🟢 MEDIUM PRIORITY

**Status:** ⏳ Not Implemented
**Effort:** 2 days
**Priority:** 🟢 Medium (Essential for production K8s deployment)
**References:** BUILD_STATUS.md:387, PROJECT_STATUS_REPORT.md:521

### Requirements

- [ ] Create Helm chart structure
- [ ] Define Deployment templates (API, background services)
- [ ] Create ConfigMap and Secret templates
- [ ] Add Service and Ingress templates
- [ ] Configure HorizontalPodAutoscaler (HPA)
- [ ] Add PodDisruptionBudget (PDB)
- [ ] Create values.yaml with sensible defaults
- [ ] Add NOTES.txt with deployment instructions
- [ ] Test on Kubernetes 1.28, 1.29, 1.30

### Implementation Guidance

**Chart Structure:**
```
helm/distributed-kernel/
├── Chart.yaml                    # Chart metadata
├── values.yaml                   # Default configuration
├── values-production.yaml        # Production overrides
├── values-staging.yaml           # Staging overrides
├── templates/
│   ├── _helpers.tpl             # Template helpers
│   ├── deployment.yaml          # API deployment
│   ├── service.yaml             # ClusterIP service
│   ├── ingress.yaml             # Ingress for external access
│   ├── configmap.yaml           # Application configuration
│   ├── secret.yaml              # Sensitive data
│   ├── hpa.yaml                 # Horizontal Pod Autoscaler
│   ├── pdb.yaml                 # Pod Disruption Budget
│   ├── serviceaccount.yaml      # K8s service account
│   ├── rbac.yaml                # RBAC permissions
│   └── NOTES.txt                # Post-install instructions
└── README.md                     # Chart documentation
```

**Chart.yaml:**
```yaml
apiVersion: v2
name: distributed-kernel
description: HotSwap Distributed Kernel Orchestration System
type: application
version: 1.0.0
appVersion: "1.0.0"
keywords:
  - hotswap
  - kernel
  - orchestration
  - deployment
home: https://github.com/scrawlsbenches/Claude-code-test
sources:
  - https://github.com/scrawlsbenches/Claude-code-test
maintainers:
  - name: HotSwap Team
    email: team@hotswap.io
dependencies:
  - name: redis
    version: "18.x.x"
    repository: https://charts.bitnami.com/bitnami
    condition: redis.enabled
```

**values.yaml (excerpt):**
```yaml
# Default values for distributed-kernel

replicaCount: 3

image:
  repository: hotswap/distributed-kernel
  pullPolicy: IfNotPresent
  tag: "1.0.0"

imagePullSecrets: []
nameOverride: ""
fullnameOverride: ""

serviceAccount:
  create: true
  annotations: {}
  name: ""

podAnnotations:
  prometheus.io/scrape: "true"
  prometheus.io/port: "5000"
  prometheus.io/path: "/metrics"

podSecurityContext:
  runAsNonRoot: true
  runAsUser: 1000
  fsGroup: 1000

securityContext:
  capabilities:
    drop:
    - ALL
  readOnlyRootFilesystem: true
  allowPrivilegeEscalation: false

service:
  type: ClusterIP
  port: 80
  targetPort: 5000
  annotations: {}

ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
  hosts:
    - host: orchestrator.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: orchestrator-tls
      hosts:
        - orchestrator.example.com

resources:
  limits:
    cpu: 1000m
    memory: 1Gi
  requests:
    cpu: 500m
    memory: 512Mi

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 80
  targetMemoryUtilizationPercentage: 80

podDisruptionBudget:
  enabled: true
  minAvailable: 2

redis:
  enabled: true
  auth:
    enabled: true
    password: "change-me-in-production"
  master:
    persistence:
      enabled: true
      size: 8Gi

jaeger:
  enabled: false
  endpoint: "http://jaeger-collector:14268/api/traces"

config:
  aspnetcore:
    environment: "Production"
  telemetry:
    samplingRatio: 0.1
  jwt:
    issuer: "https://orchestrator.example.com"
    audience: "https://orchestrator.example.com"
    expirationMinutes: 60
  rateLimit:
    enabled: true
    globalLimit: 1000
  approval:
    timeoutHours: 24
    backgroundServiceIntervalMinutes: 5

nodeSelector: {}

tolerations: []

affinity:
  podAntiAffinity:
    preferredDuringSchedulingIgnoredDuringExecution:
      - weight: 100
        podAffinityTerm:
          labelSelector:
            matchExpressions:
              - key: app.kubernetes.io/name
                operator: In
                values:
                  - distributed-kernel
          topologyKey: kubernetes.io/hostname
```

**templates/deployment.yaml (excerpt):**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "distributed-kernel.fullname" . }}
  labels:
    {{- include "distributed-kernel.labels" . | nindent 4 }}
spec:
  {{- if not .Values.autoscaling.enabled }}
  replicas: {{ .Values.replicaCount }}
  {{- end }}
  selector:
    matchLabels:
      {{- include "distributed-kernel.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      annotations:
        checksum/config: {{ include (print $.Template.BasePath "/configmap.yaml") . | sha256sum }}
        {{- with .Values.podAnnotations }}
        {{- toYaml . | nindent 8 }}
        {{- end }}
      labels:
        {{- include "distributed-kernel.selectorLabels" . | nindent 8 }}
    spec:
      {{- with .Values.imagePullSecrets }}
      imagePullSecrets:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      serviceAccountName: {{ include "distributed-kernel.serviceAccountName" . }}
      securityContext:
        {{- toYaml .Values.podSecurityContext | nindent 8 }}
      containers:
      - name: {{ .Chart.Name }}
        securityContext:
          {{- toYaml .Values.securityContext | nindent 12 }}
        image: "{{ .Values.image.repository }}:{{ .Values.image.tag | default .Chart.AppVersion }}"
        imagePullPolicy: {{ .Values.image.pullPolicy }}
        ports:
        - name: http
          containerPort: 5000
          protocol: TCP
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: {{ .Values.config.aspnetcore.environment | quote }}
        - name: REDIS_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: {{ include "distributed-kernel.fullname" . }}-secret
              key: redis-connection-string
        - name: JWT_SECRET_KEY
          valueFrom:
            secretKeyRef:
              name: {{ include "distributed-kernel.fullname" . }}-secret
              key: jwt-secret-key
        envFrom:
        - configMapRef:
            name: {{ include "distributed-kernel.fullname" . }}-config
        livenessProbe:
          httpGet:
            path: /health
            port: http
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health
            port: http
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3
        resources:
          {{- toYaml .Values.resources | nindent 12 }}
        volumeMounts:
        - name: tmp
          mountPath: /tmp
        - name: logs
          mountPath: /app/logs
      volumes:
      - name: tmp
        emptyDir: {}
      - name: logs
        emptyDir: {}
      {{- with .Values.nodeSelector }}
      nodeSelector:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      {{- with .Values.affinity }}
      affinity:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      {{- with .Values.tolerations }}
      tolerations:
        {{- toYaml . | nindent 8 }}
      {{- end }}
```

**templates/hpa.yaml:**
```yaml
{{- if .Values.autoscaling.enabled }}
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: {{ include "distributed-kernel.fullname" . }}
  labels:
    {{- include "distributed-kernel.labels" . | nindent 4 }}
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: {{ include "distributed-kernel.fullname" . }}
  minReplicas: {{ .Values.autoscaling.minReplicas }}
  maxReplicas: {{ .Values.autoscaling.maxReplicas }}
  metrics:
  {{- if .Values.autoscaling.targetCPUUtilizationPercentage }}
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: {{ .Values.autoscaling.targetCPUUtilizationPercentage }}
  {{- end }}
  {{- if .Values.autoscaling.targetMemoryUtilizationPercentage }}
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: {{ .Values.autoscaling.targetMemoryUtilizationPercentage }}
  {{- end }}
{{- end }}
```

**templates/NOTES.txt:**
```
Thank you for installing {{ .Chart.Name }}!

Your release is named {{ .Release.Name }}.

To learn more about the release, try:

  $ helm status {{ .Release.Name }}
  $ helm get all {{ .Release.Name }}

1. Get the application URL by running these commands:
{{- if .Values.ingress.enabled }}
{{- range $host := .Values.ingress.hosts }}
  {{- range .paths }}
  http{{ if $.Values.ingress.tls }}s{{ end }}://{{ $host.host }}{{ .path }}
  {{- end }}
{{- end }}
{{- else if contains "NodePort" .Values.service.type }}
  export NODE_PORT=$(kubectl get --namespace {{ .Release.Namespace }} -o jsonpath="{.spec.ports[0].nodePort}" services {{ include "distributed-kernel.fullname" . }})
  export NODE_IP=$(kubectl get nodes --namespace {{ .Release.Namespace }} -o jsonpath="{.items[0].status.addresses[0].address}")
  echo http://$NODE_IP:$NODE_PORT
{{- else if contains "LoadBalancer" .Values.service.type }}
     NOTE: It may take a few minutes for the LoadBalancer IP to be available.
           You can watch the status by running 'kubectl get --namespace {{ .Release.Namespace }} svc -w {{ include "distributed-kernel.fullname" . }}'
  export SERVICE_IP=$(kubectl get svc --namespace {{ .Release.Namespace }} {{ include "distributed-kernel.fullname" . }} --template "{{"{{ range (index .status.loadBalancer.ingress 0) }}{{.}}{{ end }}"}}")
  echo http://$SERVICE_IP:{{ .Values.service.port }}
{{- else if contains "ClusterIP" .Values.service.type }}
  export POD_NAME=$(kubectl get pods --namespace {{ .Release.Namespace }} -l "app.kubernetes.io/name={{ include "distributed-kernel.name" . }},app.kubernetes.io/instance={{ .Release.Name }}" -o jsonpath="{.items[0].metadata.name}")
  export CONTAINER_PORT=$(kubectl get pod --namespace {{ .Release.Namespace }} $POD_NAME -o jsonpath="{.spec.containers[0].ports[0].containerPort}")
  echo "Visit http://127.0.0.1:8080 to use your application"
  kubectl --namespace {{ .Release.Namespace }} port-forward $POD_NAME 8080:$CONTAINER_PORT
{{- end }}

2. Check deployment status:
  kubectl get pods -n {{ .Release.Namespace }} -l app.kubernetes.io/name={{ include "distributed-kernel.name" . }}

3. View logs:
  kubectl logs -f -n {{ .Release.Namespace }} -l app.kubernetes.io/name={{ include "distributed-kernel.name" . }}

4. Access Swagger UI:
  {{- if .Values.ingress.enabled }}
  {{- range $host := .Values.ingress.hosts }}
  https://{{ $host.host }}/swagger
  {{- end }}
  {{- end }}
```

### Testing Helm Chart

```bash
# Lint chart
helm lint helm/distributed-kernel

# Template chart (dry-run)
helm template test-release helm/distributed-kernel --values helm/distributed-kernel/values-production.yaml

# Install chart (test environment)
helm install test-release helm/distributed-kernel --namespace distributed-kernel --create-namespace

# Verify deployment
kubectl get pods -n distributed-kernel
kubectl get svc -n distributed-kernel
kubectl get ingress -n distributed-kernel

# Test API
kubectl port-forward -n distributed-kernel svc/test-release-distributed-kernel 8080:80
curl http://localhost:8080/health

# Upgrade chart
helm upgrade test-release helm/distributed-kernel --namespace distributed-kernel

# Uninstall chart
helm uninstall test-release --namespace distributed-kernel
```

### Acceptance Criteria

- ✅ Helm chart deploys successfully to Kubernetes 1.28+
- ✅ All configuration externalized to values.yaml
- ✅ Chart passes `helm lint` with zero errors
- ✅ HPA and PDB configured correctly
- ✅ Ingress with TLS/HTTPS support
- ✅ Redis dependency managed via Helm
- ✅ Documentation includes installation guide
- ✅ Tested on K8s 1.28, 1.29, 1.30

### Documentation Required

- Create `helm/distributed-kernel/README.md` (~500-800 lines)
  - Prerequisites (Helm 3.x, Kubernetes 1.28+, cert-manager)
  - Installation instructions
  - Configuration reference (all values.yaml options)
  - Upgrade guide
  - Troubleshooting common issues
  - Production deployment best practices
- Update main `README.md` with Helm installation option
- Update `TASK_LIST.md` (mark as complete)

---

## Task #9: Service Discovery Integration 🟢 LOW-MEDIUM PRIORITY

**Status:** ⏳ In-memory implementation
**Effort:** 2-3 days
**Priority:** 🟢 Low-Medium (Needed for multi-instance deployments)
**References:** SPEC_COMPLIANCE_REVIEW.md:242, PROJECT_STATUS_REPORT.md:502

### Requirements

- [ ] Add Consul client NuGet package (Consul 1.7.x)
- [ ] Implement IServiceDiscovery interface
- [ ] Create ConsulServiceDiscovery implementation
- [ ] Add automatic node registration on startup
- [ ] Implement health check registration
- [ ] Add service lookup and caching (5-minute TTL)
- [ ] Support multiple discovery backends (Consul, etcd, Kubernetes)
- [ ] Add configuration options in appsettings.json
- [ ] Add unit tests (15+ tests)

### Implementation Guidance

**Architecture:**
```
Infrastructure Layer:
├── ServiceDiscovery/
│   ├── IServiceDiscovery.cs           # Interface
│   ├── ConsulServiceDiscovery.cs      # Consul implementation
│   ├── EtcdServiceDiscovery.cs        # etcd implementation (optional)
│   ├── KubernetesServiceDiscovery.cs  # K8s implementation (optional)
│   ├── ServiceRegistration.cs         # Registration model
│   └── ServiceDiscoveryOptions.cs     # Configuration
```

**IServiceDiscovery Interface:**
```csharp
namespace HotSwap.Distributed.Infrastructure.ServiceDiscovery;

public interface IServiceDiscovery
{
    /// <summary>
    /// Register this instance with the service discovery backend.
    /// </summary>
    Task RegisterAsync(ServiceRegistration registration, CancellationToken ct = default);

    /// <summary>
    /// Deregister this instance from service discovery.
    /// </summary>
    Task DeregisterAsync(string serviceId, CancellationToken ct = default);

    /// <summary>
    /// Discover healthy instances of a service.
    /// </summary>
    Task<IEnumerable<ServiceInstance>> DiscoverAsync(string serviceName, CancellationToken ct = default);

    /// <summary>
    /// Get health status of a registered service.
    /// </summary>
    Task<HealthStatus> GetHealthAsync(string serviceId, CancellationToken ct = default);
}

public record ServiceRegistration(
    string ServiceId,
    string ServiceName,
    string Address,
    int Port,
    IDictionary<string, string> Tags,
    HealthCheck? HealthCheck);

public record ServiceInstance(
    string ServiceId,
    string Address,
    int Port,
    IDictionary<string, string> Tags);

public record HealthCheck(
    string HttpEndpoint,
    TimeSpan Interval,
    TimeSpan Timeout);

public enum HealthStatus
{
    Healthy,
    Unhealthy,
    Critical
}
```

**Consul Implementation:**
```csharp
using Consul;

namespace HotSwap.Distributed.Infrastructure.ServiceDiscovery;

public class ConsulServiceDiscovery : IServiceDiscovery
{
    private readonly IConsulClient _consulClient;
    private readonly ILogger<ConsulServiceDiscovery> _logger;
    private readonly ConcurrentDictionary<string, ServiceInstance> _cache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private DateTime _lastCacheRefresh = DateTime.MinValue;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    public ConsulServiceDiscovery(IConsulClient consulClient, ILogger<ConsulServiceDiscovery> logger)
    {
        _consulClient = consulClient;
        _logger = logger;
    }

    public async Task RegisterAsync(ServiceRegistration registration, CancellationToken ct = default)
    {
        var agentServiceRegistration = new AgentServiceRegistration
        {
            ID = registration.ServiceId,
            Name = registration.ServiceName,
            Address = registration.Address,
            Port = registration.Port,
            Tags = registration.Tags.Select(kvp => $"{kvp.Key}={kvp.Value}").ToArray(),
            Check = registration.HealthCheck != null
                ? new AgentServiceCheck
                {
                    HTTP = registration.HealthCheck.HttpEndpoint,
                    Interval = registration.HealthCheck.Interval,
                    Timeout = registration.HealthCheck.Timeout,
                    DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(10)
                }
                : null
        };

        await _consulClient.Agent.ServiceRegister(agentServiceRegistration, ct);
        _logger.LogInformation("Registered service {ServiceName} ({ServiceId}) with Consul",
            registration.ServiceName, registration.ServiceId);
    }

    public async Task DeregisterAsync(string serviceId, CancellationToken ct = default)
    {
        await _consulClient.Agent.ServiceDeregister(serviceId, ct);
        _logger.LogInformation("Deregistered service {ServiceId} from Consul", serviceId);
    }

    public async Task<IEnumerable<ServiceInstance>> DiscoverAsync(string serviceName, CancellationToken ct = default)
    {
        // Check cache first
        if (DateTime.UtcNow - _lastCacheRefresh < _cacheTtl)
        {
            var cachedInstances = _cache.Values
                .Where(s => s.Tags.ContainsKey("ServiceName") && s.Tags["ServiceName"] == serviceName)
                .ToList();

            if (cachedInstances.Any())
            {
                _logger.LogDebug("Returning {Count} cached instances for service {ServiceName}",
                    cachedInstances.Count, serviceName);
                return cachedInstances;
            }
        }

        // Refresh cache from Consul
        await RefreshCacheAsync(serviceName, ct);

        return _cache.Values
            .Where(s => s.Tags.ContainsKey("ServiceName") && s.Tags["ServiceName"] == serviceName)
            .ToList();
    }

    private async Task RefreshCacheAsync(string serviceName, CancellationToken ct)
    {
        await _cacheLock.WaitAsync(ct);
        try
        {
            var services = await _consulClient.Health.Service(serviceName, tag: null, passingOnly: true, ct);

            _cache.Clear();
            foreach (var service in services.Response)
            {
                var instance = new ServiceInstance(
                    service.Service.ID,
                    service.Service.Address,
                    service.Service.Port,
                    ParseTags(service.Service.Tags));

                _cache[service.Service.ID] = instance;
            }

            _lastCacheRefresh = DateTime.UtcNow;
            _logger.LogInformation("Refreshed service discovery cache: {Count} instances for {ServiceName}",
                _cache.Count, serviceName);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static IDictionary<string, string> ParseTags(string[] tags)
    {
        return tags
            .Select(tag => tag.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1]);
    }

    public async Task<HealthStatus> GetHealthAsync(string serviceId, CancellationToken ct = default)
    {
        var health = await _consulClient.Health.Checks(serviceId, QueryOptions.Default, ct);

        if (!health.Response.Any())
            return HealthStatus.Critical;

        var criticalChecks = health.Response.Count(c => c.Status == HealthStatus.Critical);
        if (criticalChecks > 0)
            return HealthStatus.Critical;

        var unhealthyChecks = health.Response.Count(c => c.Status == HealthStatus.Warning);
        if (unhealthyChecks > 0)
            return HealthStatus.Unhealthy;

        return HealthStatus.Healthy;
    }
}
```

**Register on Startup:**
```csharp
// Program.cs
builder.Services.AddSingleton<IServiceDiscovery, ConsulServiceDiscovery>();
builder.Services.AddSingleton<IConsulClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new ConsulClient(cfg =>
    {
        cfg.Address = new Uri(config["Consul:Address"] ?? "http://localhost:8500");
    });
});

// Register with Consul on startup
var app = builder.Build();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var serviceDiscovery = app.Services.GetRequiredService<IServiceDiscovery>();
var config = app.Configuration;

lifetime.ApplicationStarted.Register(async () =>
{
    var registration = new ServiceRegistration(
        ServiceId: $"orchestrator-{Environment.MachineName}",
        ServiceName: "distributed-kernel-orchestrator",
        Address: config["Service:Address"] ?? "localhost",
        Port: int.Parse(config["Service:Port"] ?? "5000"),
        Tags: new Dictionary<string, string>
        {
            ["Version"] = "1.0.0",
            ["Environment"] = config["ASPNETCORE_ENVIRONMENT"] ?? "Production"
        },
        HealthCheck: new HealthCheck(
            HttpEndpoint: $"http://localhost:5000/health",
            Interval: TimeSpan.FromSeconds(30),
            Timeout: TimeSpan.FromSeconds(5)));

    await serviceDiscovery.RegisterAsync(registration);
});

lifetime.ApplicationStopping.Register(async () =>
{
    await serviceDiscovery.DeregisterAsync($"orchestrator-{Environment.MachineName}");
});
```

### Acceptance Criteria

- ✅ Nodes automatically register with Consul on startup
- ✅ Cluster discovers nodes dynamically via service discovery
- ✅ Health checks update service status every 30 seconds
- ✅ Service lookup cached for 5 minutes
- ✅ Supports both Consul and Kubernetes service discovery
- ✅ Unit tests: 15+ tests covering registration, discovery, health checks

### Documentation Required

- Create `docs/SERVICE_DISCOVERY_GUIDE.md` (~400-600 lines)
- Update `README.md` with service discovery features
- Update `TASK_LIST.md` (mark as complete)

---

## Task #19: Architecture Decision Records (ADR) 🟢 LOW PRIORITY

**Status:** ⏳ Not Created
**Effort:** 2 days
**Priority:** 🟢 Low (Important for long-term maintainability)
**References:** TASK_LIST.md

### Requirements

- [ ] Document deployment strategy decisions
- [ ] Document technology choices (.NET 8, OpenTelemetry, Redis)
- [ ] Document security architecture (JWT, RBAC, HTTPS)
- [ ] Document scalability decisions (stateless API, distributed locks)
- [ ] Create ADR template
- [ ] Store in docs/adr/

### Implementation Guidance

**ADR Structure:**
```
docs/adr/
├── 0001-use-dotnet-8-for-backend.md
├── 0002-use-opentelemetry-for-distributed-tracing.md
├── 0003-use-redis-for-distributed-locking.md
├── 0004-implement-canary-deployment-strategy.md
├── 0005-use-jwt-bearer-tokens-for-authentication.md
├── 0006-use-signalr-for-realtime-updates.md
├── 0007-use-helm-for-kubernetes-deployment.md
├── 0008-use-consul-for-service-discovery.md
└── template.md
```

**ADR Template (template.md):**
```markdown
# ADR-XXXX: [Title]

## Status
[Proposed | Accepted | Deprecated | Superseded by ADR-YYYY]

## Context
What is the issue that we're seeing that is motivating this decision or change?

## Decision
What is the change that we're proposing and/or doing?

## Consequences
What becomes easier or more difficult to do because of this change?

### Positive Consequences
- [Benefit 1]
- [Benefit 2]

### Negative Consequences
- [Drawback 1]
- [Drawback 2]

## Alternatives Considered
What other options did we consider?

### Alternative 1: [Name]
- **Pros:** ...
- **Cons:** ...
- **Why not chosen:** ...

## Related Decisions
- ADR-XXXX: [Related decision]

## References
- [Link to relevant documentation]
- [Link to discussion]
```

**Example ADR:**

**docs/adr/0001-use-dotnet-8-for-backend.md:**
```markdown
# ADR-0001: Use .NET 8 for Backend Implementation

## Status
Accepted (2025-11-10)

## Context
We need to choose a backend technology stack for the Distributed Kernel Orchestration System. Key requirements:
- High performance for deployment orchestration
- Strong typing for safety and maintainability
- Excellent async/await support for I/O-bound operations
- Rich ecosystem for OpenTelemetry, SignalR, and infrastructure tooling
- Enterprise support and long-term stability

## Decision
We will use **.NET 8 (LTS)** with **C# 12** as the primary backend technology stack.

## Consequences

### Positive Consequences
- **Performance:** .NET 8 delivers excellent performance for async I/O workloads (critical for deployment orchestration)
- **Type Safety:** Strong typing catches errors at compile-time, reducing runtime bugs
- **Async/Await:** First-class support for asynchronous programming simplifies concurrent deployment handling
- **Ecosystem:** Rich NuGet ecosystem with OpenTelemetry, SignalR, EF Core, and testing frameworks
- **Tooling:** Excellent IDE support (Visual Studio, Rider, VS Code) with debugging and profiling tools
- **Long-Term Support:** .NET 8 is an LTS release supported until November 2026
- **Cloud Native:** Strong integration with Kubernetes, Docker, and cloud platforms (Azure, AWS, GCP)

### Negative Consequences
- **Platform Dependency:** Requires .NET runtime on deployment servers (mitigated by Docker containerization)
- **Learning Curve:** Developers unfamiliar with C# need onboarding time
- **Cross-Platform Variability:** Minor differences between Linux/Windows/.NET implementations

## Alternatives Considered

### Alternative 1: Node.js (TypeScript)
- **Pros:** Large ecosystem, familiar to JavaScript developers, excellent async support
- **Cons:** Less type safety than C#, slower performance for CPU-bound tasks, weaker OpenTelemetry integration
- **Why not chosen:** .NET 8 provides better performance and type safety for orchestration workloads

### Alternative 2: Go
- **Pros:** Excellent performance, simple concurrency model, small binary size
- **Cons:** Smaller ecosystem, less expressive error handling, weaker ORM support
- **Why not chosen:** .NET 8's richer ecosystem and enterprise support outweighed Go's performance benefits

### Alternative 3: Java (Spring Boot)
- **Pros:** Mature ecosystem, strong enterprise support, excellent tooling
- **Cons:** More verbose syntax, slower startup time, larger memory footprint
- **Why not chosen:** .NET 8 provides similar benefits with better developer ergonomics

## Related Decisions
- ADR-0002: Use OpenTelemetry for Distributed Tracing
- ADR-0003: Use Redis for Distributed Locking

## References
- [.NET 8 Release Notes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [.NET Performance Benchmarks](https://benchmarksgame-team.pages.debian.net/benchmarksgame/)
- [Project Requirements Document](../PROJECT_STATUS_REPORT.md)
```

### ADRs to Create

1. **ADR-0001:** Use .NET 8 for Backend
2. **ADR-0002:** Use OpenTelemetry for Distributed Tracing
3. **ADR-0003:** Use Redis for Distributed Locking
4. **ADR-0004:** Implement Canary Deployment Strategy
5. **ADR-0005:** Use JWT Bearer Tokens for Authentication
6. **ADR-0006:** Use SignalR for Real-Time Updates
7. **ADR-0007:** Use Helm for Kubernetes Deployment
8. **ADR-0008:** Use Consul for Service Discovery

### Acceptance Criteria

- ✅ All 8 ADRs created following template
- ✅ Each ADR includes context, decision, consequences, alternatives
- ✅ ADRs reference related decisions
- ✅ Template available for future ADRs
- ✅ README updated with link to ADRs

### Documentation Required

- Create 8 ADR files (~200-400 lines each)
- Create template.md (~100 lines)
- Update `README.md` with ADR documentation link
- Update `TASK_LIST.md` (mark as complete)

---

## Sprint Planning

### Recommended Execution Order

1. **Task #6** (2-3 days) - **START HERE** - High value feature for monitoring UX
2. **Task #8** (2 days) - Essential for production Kubernetes deployment
3. **Task #9** (2-3 days) - Enables multi-instance deployments
4. **Task #19** (2 days) - Documents architectural decisions for maintainability

**Total:** 8-10 days across 4 tasks

### Dependencies

- Task #6: No dependencies (can start immediately)
- Task #8: No dependencies (can start immediately)
- Task #9: Can leverage Kubernetes service discovery if Task #8 is done first
- Task #19: Should be done last to document completed decisions (#6, #8, #9)

### Success Metrics

- ✅ WebSocket real-time updates operational
- ✅ Helm chart deployed to Kubernetes successfully
- ✅ Service discovery integrated with Consul
- ✅ 8 ADRs documenting key architectural decisions
- ✅ Test coverage maintained at 85%+
- ✅ Zero build warnings or errors

---

## Reference Documentation

**Essential Reading (MANDATORY):**
- [CLAUDE.md](CLAUDE.md) - Development guidelines, TDD workflow, pre-commit checklist
- [SKILLS.md](SKILLS.md) - 7 automated workflows
- [TASK_LIST.md](TASK_LIST.md) - Master task list (update after completing tasks)
- [PROJECT_STATUS_REPORT.md](PROJECT_STATUS_REPORT.md) - Current project state

**Helpful Resources:**
- [README.md](README.md) - Project overview and quick start
- [BUILD_STATUS.md](BUILD_STATUS.md) - Build validation
- [TESTING.md](TESTING.md) - Testing patterns

---

## Final Checklist

Before considering your work complete:

- [ ] All tasks marked complete in this file
- [ ] All tests passing: `dotnet test` (0 failures)
- [ ] Build successful: `dotnet build --no-incremental` (0 warnings)
- [ ] Pre-commit checklist completed for EVERY commit
- [ ] TDD followed for all code changes (Red-Green-Refactor)
- [ ] Test coverage maintained at 85%+ (use `/test-coverage-analyzer`)
- [ ] Documentation updated (README.md, TASK_LIST.md, new guides)
- [ ] All commits pushed to remote branch
- [ ] WebSocket functionality demonstrated with examples
- [ ] Helm chart tested on Kubernetes 1.28+
- [ ] Service discovery tested with Consul
- [ ] ADRs reviewed and published
- [ ] TASK_LIST.md updated with completion status

---

**Worker Thread 3 Focus:** Platform, Features, Infrastructure, Documentation
**Start Date:** [Your session start date]
**Target Completion:** 8-10 days
**Questions?** See CLAUDE.md or reference PROJECT_STATUS_REPORT.md for context.

Good luck! Remember to use `/tdd-helper` and `/precommit-check` skills frequently.
