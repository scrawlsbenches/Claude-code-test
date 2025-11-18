# Multi-Tenant Website System - Implementation Plan

**Project Name:** HotSwap Multi-Tenant Website Platform (HMWP)
**Based On:** HotSwap Distributed Kernel Orchestration System
**Version:** 1.0
**Date:** 2025-11-17
**Status:** Planning Phase

---

## Executive Summary

The **HotSwap Multi-Tenant Website Platform** is an enterprise-grade SaaS solution that enables organizations to host, manage, and deploy multiple websites across isolated tenant environments with zero downtime. Built upon the proven HotSwap Distributed Kernel Orchestration System, this platform extends hot-swappable module deployment capabilities to website management, allowing tenants to deploy themes, plugins, content updates, and API changes instantly without service interruption.

### Vision

Create a scalable, secure, and cost-effective platform where:
- **SaaS Providers** can offer white-label website hosting to hundreds of customers
- **Enterprise Organizations** can manage multiple brand websites from a single platform
- **Digital Agencies** can deploy and manage client websites with streamlined workflows
- **E-commerce Platforms** can scale tenant stores independently with zero-downtime updates

### Core Value Propositions

1. **Zero-Downtime Deployments** - Hot-swap website components without service interruption
2. **Complete Tenant Isolation** - Data, deployments, and resources are fully isolated per tenant
3. **Elastic Scaling** - Scale individual tenants independently based on traffic and resource needs
4. **Advanced Deployment Strategies** - Canary, blue-green, rolling deployments at the tenant level
5. **Enterprise Security** - RBAC, audit logging, encryption, and compliance ready
6. **Cost Optimization** - Shared infrastructure with tenant-specific resource quotas

---

## System Architecture Overview

### Conceptual Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Tenant Management Layer                      │
│  - Tenant Provisioning  - Billing  - Resource Quotas           │
│  - Domain Management    - Branding - Subscription Tiers         │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│              Multi-Tenant Orchestration Layer                   │
│  - Tenant-Aware Deployment Pipeline                            │
│  - Tenant Isolation Engine                                      │
│  - Resource Allocation & Quotas                                 │
│  - Tenant-Specific Approval Workflows                           │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│           HotSwap Kernel Orchestration Layer                    │
│  - Deployment Strategies (Direct, Rolling, Blue-Green, Canary) │
│  - Pipeline Execution (Build → Test → Deploy → Validate)       │
│  - Health Monitoring & Rollback                                 │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                 Website Runtime Layer                           │
│  - Tenant-Specific Website Instances                           │
│  - Content Rendering Engine                                     │
│  - Plugin/Theme System                                          │
│  - API Gateway (Tenant-Scoped)                                  │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                  Infrastructure Layer                           │
│  - Kubernetes Clusters (Multi-Tenant)                          │
│  - PostgreSQL (Tenant-Isolated Schemas)                        │
│  - Redis (Tenant-Namespaced)                                    │
│  - Object Storage (S3-Compatible, Tenant-Isolated)             │
└─────────────────────────────────────────────────────────────────┘
```

### Multi-Tenant Deployment Model

**Shared Infrastructure Model (Cost-Effective):**
```
┌─────────────────────────────────────────────────────────┐
│                 Kubernetes Cluster                      │
│                                                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ Tenant A     │  │ Tenant B     │  │ Tenant C     │ │
│  │ - Namespace  │  │ - Namespace  │  │ - Namespace  │ │
│  │ - Quota: 2GB │  │ - Quota: 4GB │  │ - Quota: 1GB │ │
│  │ - 3 Pods     │  │ - 6 Pods     │  │ - 2 Pods     │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│                                                         │
│  Shared: Control Plane, Networking, Monitoring         │
└─────────────────────────────────────────────────────────┘
```

**Dedicated Cluster Model (Enterprise):**
```
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ Tenant D        │  │ Tenant E        │  │ Tenant F        │
│ (Enterprise)    │  │ (Enterprise)    │  │ (Enterprise)    │
│                 │  │                 │  │                 │
│ Dedicated K8s   │  │ Dedicated K8s   │  │ Dedicated K8s   │
│ Full Isolation  │  │ Full Isolation  │  │ Full Isolation  │
│ Custom Config   │  │ Custom Config   │  │ Custom Config   │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

### Website Module System

**Module Types:**
1. **Themes** - Visual appearance, layouts, styles
2. **Plugins** - Functionality extensions (e-commerce, forms, analytics)
3. **Content Modules** - Pages, posts, media libraries
4. **API Modules** - Custom endpoints, integrations, webhooks
5. **Configuration Modules** - Settings, feature flags, environment variables

**Module Deployment Flow:**
```
Developer/Admin → Upload Module → Validation → Security Scan →
  Staging Deploy (Tenant-Specific) → Smoke Tests → Approval →
  Production Deploy (Canary/Blue-Green) → Monitoring → Rollback if needed
```

---

## Implementation Epics

### Epic 1: Tenant Management Foundation
**Priority:** 🔴 Critical
**Estimated Effort:** 15-20 days
**Dependencies:** None

**Objective:** Build the core tenant management system that handles tenant lifecycle, resource allocation, and tenant-specific configurations.

**User Stories:**

#### Story 1.1: Tenant Provisioning
**As a** platform administrator
**I want to** create new tenant accounts with isolated resources
**So that** each customer has their own secure environment

**Acceptance Criteria:**
- [ ] Admin can create tenant via API/UI
- [ ] Tenant receives unique tenant ID (GUID)
- [ ] System creates tenant-specific:
  - Kubernetes namespace
  - Database schema (PostgreSQL)
  - Redis namespace
  - Storage bucket
  - Default admin user
- [ ] Tenant provisioning completes in < 60 seconds
- [ ] Rollback on provisioning failure
- [ ] Audit log entry created

**Technical Requirements:**
```csharp
// Domain Model
public class Tenant
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public string? CustomDomain { get; set; }
    public TenantStatus Status { get; set; }
    public SubscriptionTier Tier { get; set; }
    public ResourceQuota ResourceQuota { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}

public class ResourceQuota
{
    public int MaxWebsites { get; set; }
    public long StorageQuotaGB { get; set; }
    public long BandwidthQuotaGB { get; set; }
    public int MaxConcurrentDeployments { get; set; }
    public int MaxCustomDomains { get; set; }
}

public enum TenantStatus
{
    Provisioning,
    Active,
    Suspended,
    Deprovisioning,
    Deleted
}

public enum SubscriptionTier
{
    Free,
    Starter,
    Professional,
    Enterprise,
    Custom
}
```

#### Story 1.2: Tenant Isolation Engine
**As a** platform architect
**I want to** ensure complete isolation between tenants
**So that** one tenant cannot access another tenant's data or resources

**Acceptance Criteria:**
- [ ] Database: Schema-based isolation (tenant_123.*, tenant_456.*)
- [ ] Kubernetes: Namespace isolation with NetworkPolicy
- [ ] Redis: Key prefix isolation (tenant:123:*, tenant:456:*)
- [ ] Storage: Bucket prefix isolation (s3://bucket/tenant-123/*)
- [ ] API: Tenant context middleware validates all requests
- [ ] Cross-tenant access attempts are logged and blocked

**Technical Requirements:**
```csharp
// Tenant Context Middleware
public class TenantContextMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Extract tenant from subdomain, header, or JWT claim
        var tenantId = ExtractTenantId(context);

        if (tenantId == null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(
                new { error = "Tenant context required" });
            return;
        }

        // Validate tenant exists and is active
        var tenant = await _tenantService.GetTenantAsync(tenantId);

        if (tenant == null || tenant.Status != TenantStatus.Active)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(
                new { error = "Tenant not found or inactive" });
            return;
        }

        // Set tenant context for downstream services
        context.Items["TenantId"] = tenantId;
        context.Items["Tenant"] = tenant;

        await _next(context);
    }
}
```

#### Story 1.3: Subscription & Billing Integration
**As a** SaaS provider
**I want to** manage tenant subscriptions and billing
**So that** I can monetize the platform

**Acceptance Criteria:**
- [ ] Support multiple subscription tiers (Free, Starter, Pro, Enterprise)
- [ ] Track usage metrics (storage, bandwidth, deployments)
- [ ] Enforce resource quotas based on subscription tier
- [ ] Integrate with Stripe for billing (webhook handling)
- [ ] Auto-suspend tenants on payment failure (after grace period)
- [ ] Usage-based billing calculation (monthly)

**Technical Requirements:**
```csharp
public class SubscriptionService
{
    Task<Subscription> CreateSubscriptionAsync(Guid tenantId, SubscriptionTier tier);
    Task<Subscription> UpgradeSubscriptionAsync(Guid tenantId, SubscriptionTier newTier);
    Task<Subscription> DowngradeSubscriptionAsync(Guid tenantId, SubscriptionTier newTier);
    Task SuspendForNonPaymentAsync(Guid tenantId);
    Task<UsageReport> GetUsageReportAsync(Guid tenantId, DateTime start, DateTime end);
    Task<bool> CheckQuotaAsync(Guid tenantId, ResourceType type, long amount);
}

public class UsageReport
{
    public Guid TenantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public long StorageUsedGB { get; set; }
    public long BandwidthUsedGB { get; set; }
    public int DeploymentsCount { get; set; }
    public decimal TotalCost { get; set; }
    public Dictionary<string, decimal> LineItems { get; set; }
}
```

---

### Epic 2: Multi-Tenant Website Runtime
**Priority:** 🔴 Critical
**Estimated Effort:** 20-25 days
**Dependencies:** Epic 1

**Objective:** Build the website hosting runtime that renders tenant websites, manages content, and handles traffic routing.

**User Stories:**

#### Story 2.1: Tenant Website Provisioning
**As a** tenant administrator
**I want to** create multiple websites within my tenant account
**So that** I can host different brands/projects

**Acceptance Criteria:**
- [ ] Tenant can create website via API
- [ ] Website gets unique subdomain (site1.tenant.platform.com)
- [ ] Support custom domain mapping (www.customdomain.com → site1.tenant.platform.com)
- [ ] SSL certificate auto-provisioned (Let's Encrypt)
- [ ] Website has default theme and empty content
- [ ] Website status tracked (Provisioning, Active, Suspended)

**Technical Requirements:**
```csharp
public class Website
{
    public Guid WebsiteId { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string Subdomain { get; set; }
    public List<string> CustomDomains { get; set; } = new();
    public WebsiteStatus Status { get; set; }
    public Guid CurrentThemeId { get; set; }
    public List<Guid> InstalledPluginIds { get; set; } = new();
    public Dictionary<string, string> Configuration { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public enum WebsiteStatus
{
    Provisioning,
    Active,
    Suspended,
    Maintenance,
    Deleted
}
```

#### Story 2.2: Content Management System
**As a** website administrator
**I want to** manage pages, posts, and media
**So that** I can control my website content

**Acceptance Criteria:**
- [ ] CRUD operations for pages, posts, media
- [ ] Rich text editor integration
- [ ] Media library (images, videos, documents)
- [ ] SEO metadata (title, description, keywords)
- [ ] URL slug management
- [ ] Publish/draft/scheduled publishing
- [ ] Version history and rollback

**Technical Requirements:**
```csharp
public class Page
{
    public Guid PageId { get; set; }
    public Guid WebsiteId { get; set; }
    public Guid TenantId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Content { get; set; }
    public PageStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public SeoMetadata Seo { get; set; }
    public int Version { get; set; }
}

public class MediaAsset
{
    public Guid MediaId { get; set; }
    public Guid WebsiteId { get; set; }
    public Guid TenantId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string StorageUrl { get; set; }
    public string? AltText { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}
```

#### Story 2.3: Theme System
**As a** website administrator
**I want to** change my website's theme
**So that** I can customize the appearance

**Acceptance Criteria:**
- [ ] Theme marketplace (built-in + custom)
- [ ] Theme preview before activation
- [ ] Hot-swap theme deployment (zero downtime)
- [ ] Theme customization (colors, fonts, logos)
- [ ] Responsive design support
- [ ] Theme version management

**Technical Requirements:**
```csharp
public class Theme
{
    public Guid ThemeId { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string Author { get; set; }
    public bool IsPublic { get; set; }
    public byte[] ThemePackage { get; set; }
    public string? PreviewImageUrl { get; set; }
    public ThemeManifest Manifest { get; set; }
}

public class ThemeManifest
{
    public string Name { get; set; }
    public string Version { get; set; }
    public List<string> Templates { get; set; }
    public List<string> Stylesheets { get; set; }
    public List<string> Scripts { get; set; }
    public Dictionary<string, ThemeCustomization> CustomizationOptions { get; set; }
}
```

#### Story 2.4: Plugin System
**As a** website administrator
**I want to** install plugins to extend functionality
**So that** I can add features without custom development

**Acceptance Criteria:**
- [ ] Plugin marketplace
- [ ] Plugin installation/activation/deactivation
- [ ] Hot-swap plugin deployment
- [ ] Plugin dependency management
- [ ] Plugin settings UI
- [ ] Plugin hooks/events system

**Technical Requirements:**
```csharp
public class Plugin
{
    public Guid PluginId { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public PluginCategory Category { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public byte[] PluginPackage { get; set; }
    public PluginManifest Manifest { get; set; }
}

public enum PluginCategory
{
    Ecommerce,
    Forms,
    Analytics,
    SEO,
    Security,
    Performance,
    Integration,
    Custom
}

public class PluginManifest
{
    public string Name { get; set; }
    public string Version { get; set; }
    public List<string> RequiredPermissions { get; set; }
    public Dictionary<string, object> DefaultSettings { get; set; }
    public List<HookRegistration> Hooks { get; set; }
    public List<ApiEndpoint> ApiEndpoints { get; set; }
}
```

---

### Epic 3: Tenant-Aware Deployment Pipeline
**Priority:** 🔴 Critical
**Estimated Effort:** 15-20 days
**Dependencies:** Epic 1, Epic 2

**Objective:** Extend the HotSwap deployment pipeline to support multi-tenant deployments with tenant-specific approval workflows and isolation.

**User Stories:**

#### Story 3.1: Tenant-Scoped Deployments
**As a** tenant administrator
**I want to** deploy website updates to my environments
**So that** I can update my sites without affecting other tenants

**Acceptance Criteria:**
- [ ] Deployment requests include tenant context
- [ ] Deployments isolated to tenant namespace
- [ ] Tenant-specific deployment history
- [ ] Tenant cannot deploy to other tenants
- [ ] Deployment quotas enforced (max concurrent deployments)
- [ ] Deployment metrics tracked per tenant

**Technical Requirements:**
```csharp
public class TenantDeploymentRequest : DeploymentRequest
{
    public Guid TenantId { get; set; }
    public Guid WebsiteId { get; set; }
    public WebsiteModuleType ModuleType { get; set; }
    public DeploymentScope Scope { get; set; }
}

public enum WebsiteModuleType
{
    Theme,
    Plugin,
    Content,
    Configuration,
    Api
}

public enum DeploymentScope
{
    SingleWebsite,
    AllTenantWebsites,
    SpecificWebsites
}
```

#### Story 3.2: Tenant-Specific Approval Workflows
**As a** tenant administrator
**I want to** configure approval workflows for my deployments
**So that** I can control deployment governance

**Acceptance Criteria:**
- [ ] Tenant can configure approval requirements per environment
- [ ] Support multiple approvers per tenant
- [ ] Approval notifications sent to tenant-specific approvers
- [ ] Approval timeout configurable per tenant
- [ ] Approval audit trail scoped to tenant
- [ ] Auto-approval option for low-risk deployments

**Technical Requirements:**
```csharp
public class TenantApprovalConfiguration
{
    public Guid TenantId { get; set; }
    public bool RequireApprovalForStaging { get; set; }
    public bool RequireApprovalForProduction { get; set; }
    public List<string> ApproverEmails { get; set; } = new();
    public TimeSpan ApprovalTimeout { get; set; }
    public bool AutoApproveMinorUpdates { get; set; }
}
```

#### Story 3.3: Website Hot-Swap Deployment
**As a** website administrator
**I want to** deploy themes/plugins without downtime
**So that** my website remains available during updates

**Acceptance Criteria:**
- [ ] Theme swaps happen instantly (< 100ms)
- [ ] Plugin activation doesn't interrupt active requests
- [ ] Content updates are atomic
- [ ] Rollback within 5 seconds if failure detected
- [ ] Active user sessions preserved during deployment
- [ ] WebSocket connections maintained

**Technical Requirements:**
```csharp
public class WebsiteDeploymentStrategy : IDeploymentStrategy
{
    // Implements zero-downtime deployment for website modules
    Task<DeploymentResult> DeployThemeAsync(
        Guid websiteId,
        Theme theme,
        CancellationToken cancellationToken);

    Task<DeploymentResult> DeployPluginAsync(
        Guid websiteId,
        Plugin plugin,
        CancellationToken cancellationToken);

    Task<DeploymentResult> DeployContentAsync(
        Guid websiteId,
        List<Page> pages,
        CancellationToken cancellationToken);
}
```

---

### Epic 4: Tenant Portal & Management UI
**Priority:** 🟡 High
**Estimated Effort:** 20-25 days
**Dependencies:** Epic 1, Epic 2, Epic 3

**Objective:** Build web-based admin portals for platform administrators and tenant users.

**User Stories:**

#### Story 4.1: Platform Admin Portal
**As a** platform administrator
**I want to** manage all tenants from a central dashboard
**So that** I can monitor platform health and tenant activity

**Features:**
- [ ] Tenant list with search/filter
- [ ] Tenant creation/suspension/deletion
- [ ] Platform-wide metrics dashboard
- [ ] Billing overview (all tenants)
- [ ] System health monitoring
- [ ] Audit log viewer (all tenants)
- [ ] Support ticket management

#### Story 4.2: Tenant Admin Portal
**As a** tenant administrator
**I want to** manage my websites and deployments
**So that** I can control my tenant resources

**Features:**
- [ ] Website list and creation
- [ ] Deployment dashboard per website
- [ ] Theme marketplace and installation
- [ ] Plugin marketplace and installation
- [ ] User management (tenant-scoped)
- [ ] Usage metrics and billing
- [ ] API key management

#### Story 4.3: Website Content Editor
**As a** content editor
**I want to** edit website content via rich UI
**So that** I can manage content easily

**Features:**
- [ ] WYSIWYG content editor
- [ ] Page/post management
- [ ] Media library with upload
- [ ] SEO optimization tools
- [ ] Preview before publish
- [ ] Scheduled publishing

---

### Epic 5: Monitoring, Observability & Analytics
**Priority:** 🟢 Medium
**Estimated Effort:** 10-15 days
**Dependencies:** Epic 1, Epic 2

**Objective:** Provide comprehensive monitoring and analytics for both platform and tenants.

**User Stories:**

#### Story 5.1: Tenant Usage Analytics
**As a** tenant administrator
**I want to** view usage analytics for my websites
**So that** I can understand traffic and resource consumption

**Metrics:**
- [ ] Website traffic (page views, unique visitors)
- [ ] Storage usage (media, database)
- [ ] Bandwidth usage
- [ ] Deployment frequency and success rate
- [ ] Response time percentiles (p50, p95, p99)
- [ ] Error rate and 5xx responses

#### Story 5.2: Platform-Wide Monitoring
**As a** platform administrator
**I want to** monitor platform health and performance
**So that** I can ensure SLA compliance

**Metrics:**
- [ ] Total tenants (active, suspended, provisioning)
- [ ] Total websites hosted
- [ ] Cluster resource utilization (CPU, memory, disk)
- [ ] Deployment pipeline throughput
- [ ] API latency and error rates
- [ ] Database performance metrics

#### Story 5.3: Cost Attribution & Chargeback
**As a** platform administrator
**I want to** track costs per tenant
**So that** I can optimize infrastructure and pricing

**Features:**
- [ ] Resource cost calculation (compute, storage, bandwidth)
- [ ] Cost per tenant breakdown
- [ ] Cost trends and forecasting
- [ ] Budget alerts and recommendations

---

### Epic 6: Advanced Features & Optimizations
**Priority:** 🟢 Medium
**Estimated Effort:** 15-20 days
**Dependencies:** Epic 1-5

**User Stories:**

#### Story 6.1: CDN Integration
**As a** platform administrator
**I want to** integrate CDN for static assets
**So that** tenant websites load faster globally

**Features:**
- [ ] CloudFront/Cloudflare integration
- [ ] Automatic asset caching
- [ ] Cache invalidation on deployment
- [ ] Geographic distribution
- [ ] DDoS protection

#### Story 6.2: Database Replication & Backup
**As a** platform administrator
**I want to** replicate tenant databases and automate backups
**So that** data is protected and recoverable

**Features:**
- [ ] PostgreSQL replication (async)
- [ ] Automated daily backups
- [ ] Point-in-time recovery (PITR)
- [ ] Backup retention policies (30 days)
- [ ] Tenant self-service restore (within limits)

#### Story 6.3: API Gateway & Rate Limiting
**As a** tenant
**I want to** expose custom APIs for my website
**So that** third parties can integrate with my site

**Features:**
- [ ] Tenant-scoped API gateway
- [ ] API key management
- [ ] Rate limiting per API key
- [ ] API usage analytics
- [ ] Webhook support

---

## Technical Specifications

### Technology Stack

**Backend (.NET 8):**
- ASP.NET Core 8.0 (Web API)
- Entity Framework Core 8.0 (PostgreSQL)
- Dapper (high-performance queries)
- MassTransit (RabbitMQ for async messaging)
- Hangfire (background jobs)

**Frontend (React/Next.js):**
- Next.js 14 (SSR/SSG)
- TypeScript
- Tailwind CSS
- ShadCN UI components
- React Query (data fetching)

**Infrastructure:**
- Kubernetes (container orchestration)
- PostgreSQL 16 (multi-tenant schemas)
- Redis 7 (caching, rate limiting)
- RabbitMQ (message broker)
- MinIO/S3 (object storage)
- Nginx Ingress (routing)

**Observability:**
- OpenTelemetry (distributed tracing)
- Prometheus (metrics)
- Grafana (dashboards)
- Loki (log aggregation)
- Jaeger (trace visualization)

**CI/CD:**
- GitHub Actions
- ArgoCD (GitOps)
- Helm (Kubernetes deployments)

### Database Schema Design

**Multi-Tenant Schema Isolation:**

```sql
-- Schema per tenant approach
CREATE SCHEMA tenant_a1b2c3d4;
CREATE SCHEMA tenant_e5f6g7h8;

-- Shared platform schema
CREATE SCHEMA platform;

-- Platform tables
CREATE TABLE platform.tenants (
    tenant_id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    subdomain VARCHAR(255) UNIQUE NOT NULL,
    status VARCHAR(50) NOT NULL,
    subscription_tier VARCHAR(50) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    metadata JSONB
);

CREATE TABLE platform.subscriptions (
    subscription_id UUID PRIMARY KEY,
    tenant_id UUID REFERENCES platform.tenants(tenant_id),
    tier VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL,
    billing_cycle VARCHAR(50) NOT NULL,
    amount_cents INTEGER NOT NULL,
    current_period_start TIMESTAMP NOT NULL,
    current_period_end TIMESTAMP NOT NULL
);

CREATE TABLE platform.resource_quotas (
    tenant_id UUID PRIMARY KEY REFERENCES platform.tenants(tenant_id),
    max_websites INTEGER NOT NULL,
    storage_quota_gb INTEGER NOT NULL,
    bandwidth_quota_gb INTEGER NOT NULL,
    max_concurrent_deployments INTEGER NOT NULL
);

-- Tenant-specific tables (in tenant schema)
-- Example for tenant_a1b2c3d4:
CREATE TABLE tenant_a1b2c3d4.websites (
    website_id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    subdomain VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL,
    current_theme_id UUID,
    created_at TIMESTAMP NOT NULL
);

CREATE TABLE tenant_a1b2c3d4.pages (
    page_id UUID PRIMARY KEY,
    website_id UUID REFERENCES tenant_a1b2c3d4.websites(website_id),
    title VARCHAR(500) NOT NULL,
    slug VARCHAR(500) NOT NULL,
    content TEXT NOT NULL,
    status VARCHAR(50) NOT NULL,
    published_at TIMESTAMP,
    version INTEGER NOT NULL,
    created_at TIMESTAMP NOT NULL
);

CREATE TABLE tenant_a1b2c3d4.media_assets (
    media_id UUID PRIMARY KEY,
    website_id UUID REFERENCES tenant_a1b2c3d4.websites(website_id),
    file_name VARCHAR(500) NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    size_bytes BIGINT NOT NULL,
    storage_url TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL
);
```

### API Endpoints

**Platform Admin API:**
```
POST   /api/v1/admin/tenants                     # Create tenant
GET    /api/v1/admin/tenants                     # List all tenants
GET    /api/v1/admin/tenants/{tenantId}          # Get tenant details
PUT    /api/v1/admin/tenants/{tenantId}          # Update tenant
DELETE /api/v1/admin/tenants/{tenantId}          # Delete tenant
POST   /api/v1/admin/tenants/{tenantId}/suspend  # Suspend tenant
POST   /api/v1/admin/tenants/{tenantId}/activate # Activate tenant
GET    /api/v1/admin/metrics/platform            # Platform metrics
GET    /api/v1/admin/billing/overview            # Billing overview
```

**Tenant Management API:**
```
GET    /api/v1/tenant/profile                    # Get tenant profile
PUT    /api/v1/tenant/profile                    # Update tenant profile
GET    /api/v1/tenant/subscription               # Get subscription
PUT    /api/v1/tenant/subscription               # Upgrade/downgrade
GET    /api/v1/tenant/usage                      # Usage metrics
GET    /api/v1/tenant/billing/invoices           # Billing history
```

**Website Management API:**
```
POST   /api/v1/websites                          # Create website
GET    /api/v1/websites                          # List tenant websites
GET    /api/v1/websites/{websiteId}              # Get website
PUT    /api/v1/websites/{websiteId}              # Update website
DELETE /api/v1/websites/{websiteId}              # Delete website
POST   /api/v1/websites/{websiteId}/domains      # Add custom domain
```

**Content Management API:**
```
POST   /api/v1/websites/{websiteId}/pages        # Create page
GET    /api/v1/websites/{websiteId}/pages        # List pages
GET    /api/v1/websites/{websiteId}/pages/{id}   # Get page
PUT    /api/v1/websites/{websiteId}/pages/{id}   # Update page
DELETE /api/v1/websites/{websiteId}/pages/{id}   # Delete page
POST   /api/v1/websites/{websiteId}/media        # Upload media
GET    /api/v1/websites/{websiteId}/media        # List media
```

**Deployment API (Extended HotSwap):**
```
POST   /api/v1/deployments/theme                 # Deploy theme
POST   /api/v1/deployments/plugin                # Deploy plugin
POST   /api/v1/deployments/content               # Deploy content
GET    /api/v1/deployments                       # List deployments
GET    /api/v1/deployments/{id}                  # Get deployment status
POST   /api/v1/deployments/{id}/rollback         # Rollback deployment
```

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)
**Objective:** Build core multi-tenancy infrastructure

- [ ] Week 1: Tenant Management Domain Models
- [ ] Week 2: Tenant Provisioning Service
- [ ] Week 3: Tenant Isolation Middleware & Database Schema
- [ ] Week 4: Subscription & Billing Integration (Stripe)

**Deliverables:**
- Tenant CRUD API
- Automated tenant provisioning
- Database schema isolation
- Basic subscription management

### Phase 2: Website Runtime (Weeks 5-8)
**Objective:** Build website hosting and content management

- [ ] Week 5: Website Provisioning & Domain Management
- [ ] Week 6: Content Management System (Pages, Media)
- [ ] Week 7: Theme System & Marketplace
- [ ] Week 8: Plugin System & Marketplace

**Deliverables:**
- Website CRUD API
- Content editor API
- Theme deployment
- Plugin deployment

### Phase 3: Deployment Pipeline (Weeks 9-11)
**Objective:** Extend HotSwap for multi-tenant deployments

- [ ] Week 9: Tenant-Scoped Deployment Pipeline
- [ ] Week 10: Tenant-Specific Approval Workflows
- [ ] Week 11: Zero-Downtime Website Module Deployment

**Deliverables:**
- Tenant deployment API
- Approval workflow per tenant
- Hot-swap theme/plugin deployment

### Phase 4: Admin Portals (Weeks 12-15)
**Objective:** Build management UIs

- [ ] Week 12: Platform Admin Portal (React/Next.js)
- [ ] Week 13: Tenant Admin Portal
- [ ] Week 14: Website Content Editor UI
- [ ] Week 15: Deployment Dashboard

**Deliverables:**
- Admin web portals
- Content editor UI
- Deployment monitoring UI

### Phase 5: Monitoring & Analytics (Weeks 16-17)
**Objective:** Add observability and analytics

- [ ] Week 16: Tenant Usage Analytics
- [ ] Week 17: Platform Monitoring Dashboards

**Deliverables:**
- Grafana dashboards
- Usage reports API
- Cost attribution

### Phase 6: Advanced Features (Weeks 18-20)
**Objective:** Production-ready enhancements

- [ ] Week 18: CDN Integration & Performance Optimization
- [ ] Week 19: Database Replication & Backup
- [ ] Week 20: Security Hardening & Load Testing

**Deliverables:**
- CDN integration
- Automated backups
- Load test results (1000+ tenants)

---

## Task Breakdown

### Epic 1: Tenant Management Foundation (15-20 days)

#### Task 1.1: Design Tenant Domain Models
- **Effort:** 1 day
- **Assignee:** Backend Lead
- [ ] Define Tenant entity with all properties
- [ ] Define ResourceQuota value object
- [ ] Define SubscriptionTier enum
- [ ] Define TenantStatus enum
- [ ] Create validation rules
- [ ] Write unit tests for domain models

#### Task 1.2: Implement Tenant Repository
- **Effort:** 2 days
- **Assignee:** Backend Developer
- [ ] Create ITenantRepository interface
- [ ] Implement PostgreSQL repository
- [ ] Add schema isolation logic
- [ ] Implement CRUD operations
- [ ] Add tenant search/filtering
- [ ] Write repository tests

#### Task 1.3: Build Tenant Provisioning Service
- **Effort:** 3 days
- **Assignee:** Backend Developer
- [ ] Create ITenantProvisioningService interface
- [ ] Implement tenant creation workflow:
  - Create tenant record
  - Create PostgreSQL schema
  - Create Kubernetes namespace
  - Create Redis namespace
  - Create S3 bucket
  - Create default admin user
- [ ] Add rollback on failure
- [ ] Add provisioning audit logging
- [ ] Write integration tests

#### Task 1.4: Implement Tenant Context Middleware
- **Effort:** 2 days
- **Assignee:** Backend Developer
- [ ] Extract tenant from request (subdomain/header/JWT)
- [ ] Validate tenant exists and active
- [ ] Set HttpContext tenant items
- [ ] Block cross-tenant access attempts
- [ ] Log tenant context for all requests
- [ ] Write middleware tests

#### Task 1.5: Create Tenant Management API
- **Effort:** 2 days
- **Assignee:** Backend Developer
- [ ] Implement TenantsController (Admin API)
- [ ] POST /api/v1/admin/tenants (create)
- [ ] GET /api/v1/admin/tenants (list)
- [ ] GET /api/v1/admin/tenants/{id} (get)
- [ ] PUT /api/v1/admin/tenants/{id} (update)
- [ ] DELETE /api/v1/admin/tenants/{id} (delete)
- [ ] Add Swagger documentation
- [ ] Write API tests

#### Task 1.6: Integrate Stripe Billing
- **Effort:** 3 days
- **Assignee:** Backend Developer
- [ ] Add Stripe.NET package
- [ ] Implement subscription creation
- [ ] Implement webhook handlers (payment succeeded/failed)
- [ ] Implement usage metering
- [ ] Add billing invoice generation
- [ ] Auto-suspend on payment failure
- [ ] Write billing tests

#### Task 1.7: Implement Resource Quota Enforcement
- **Effort:** 2 days
- **Assignee:** Backend Developer
- [ ] Create IQuotaService interface
- [ ] Implement quota checks (storage, bandwidth, websites)
- [ ] Reject operations exceeding quota (HTTP 429)
- [ ] Add quota usage tracking
- [ ] Add quota alerts (90% threshold)
- [ ] Write quota tests

### Epic 2: Website Runtime (20-25 days)

#### Task 2.1: Design Website Domain Models
- **Effort:** 1 day
- **Assignee:** Backend Lead
- [ ] Define Website entity
- [ ] Define Page, Post, MediaAsset entities
- [ ] Define Theme, Plugin entities
- [ ] Define SeoMetadata value object
- [ ] Create validation rules
- [ ] Write unit tests

#### Task 2.2: Implement Website Repository
- **Effort:** 2 days
- **Assignee:** Backend Developer
- [ ] Create IWebsiteRepository interface
- [ ] Implement tenant-scoped queries
- [ ] Add website CRUD operations
- [ ] Add custom domain management
- [ ] Write repository tests

#### Task 2.3: Build Website Provisioning Service
- **Effort:** 3 days
- **Assignee:** Backend Developer
- [ ] Create IWebsiteProvisioningService
- [ ] Implement website creation workflow
- [ ] Generate subdomain (unique check)
- [ ] Provision SSL certificate (Let's Encrypt)
- [ ] Create default theme and empty content
- [ ] Add Nginx Ingress configuration
- [ ] Write integration tests

#### Task 2.4: Implement Content Management Service
- **Effort:** 4 days
- **Assignee:** Backend Developer
- [ ] Create IContentService interface
- [ ] Implement page CRUD operations
- [ ] Implement media upload/storage (S3)
- [ ] Add content versioning
- [ ] Add scheduled publishing
- [ ] Implement SEO metadata management
- [ ] Write content service tests

#### Task 2.5: Build Theme System
- **Effort:** 5 days
- **Assignee:** Backend + Frontend Developer
- [ ] Define theme package format (.zip with manifest)
- [ ] Create IThemeService interface
- [ ] Implement theme upload/validation
- [ ] Build theme marketplace
- [ ] Implement theme hot-swap deployment
- [ ] Create default themes (3-5 themes)
- [ ] Add theme preview functionality
- [ ] Write theme deployment tests

#### Task 2.6: Build Plugin System
- **Effort:** 5 days
- **Assignee:** Backend + Frontend Developer
- [ ] Define plugin package format
- [ ] Create IPluginService interface
- [ ] Implement plugin upload/validation
- [ ] Build plugin marketplace
- [ ] Implement plugin activation/deactivation
- [ ] Create sample plugins (contact form, analytics)
- [ ] Add plugin settings management
- [ ] Write plugin tests

### Epic 3: Tenant-Aware Deployment Pipeline (15-20 days)

#### Task 3.1: Extend Deployment Models for Multi-Tenancy
- **Effort:** 1 day
- **Assignee:** Backend Lead
- [ ] Create TenantDeploymentRequest model
- [ ] Add WebsiteModuleType enum
- [ ] Add DeploymentScope enum
- [ ] Update DeploymentResult for website modules
- [ ] Write model tests

#### Task 3.2: Implement Tenant-Scoped Deployment Service
- **Effort:** 3 days
- **Assignee:** Backend Developer
- [ ] Create ITenantDeploymentService
- [ ] Add tenant validation to deployment requests
- [ ] Enforce deployment quotas
- [ ] Implement tenant deployment history
- [ ] Add deployment metrics per tenant
- [ ] Write deployment service tests

#### Task 3.3: Build Website Module Deployment Strategies
- **Effort:** 5 days
- **Assignee:** Backend Developer
- [ ] Create WebsiteThemeDeploymentStrategy
- [ ] Create WebsitePluginDeploymentStrategy
- [ ] Create WebsiteContentDeploymentStrategy
- [ ] Implement zero-downtime swap logic
- [ ] Add pre-deployment validation
- [ ] Add post-deployment smoke tests
- [ ] Write strategy tests

#### Task 3.4: Implement Tenant-Specific Approval Workflows
- **Effort:** 3 days
- **Assignee:** Backend Developer
- [ ] Extend ApprovalService for tenant context
- [ ] Add TenantApprovalConfiguration model
- [ ] Allow tenants to configure approval rules
- [ ] Send notifications to tenant approvers
- [ ] Add tenant approval audit logging
- [ ] Write approval workflow tests

#### Task 3.5: Create Deployment API for Websites
- **Effort:** 3 days
- **Assignee:** Backend Developer
- [ ] Implement WebsiteDeploymentsController
- [ ] POST /api/v1/deployments/theme
- [ ] POST /api/v1/deployments/plugin
- [ ] POST /api/v1/deployments/content
- [ ] GET /api/v1/deployments (tenant-scoped)
- [ ] Add Swagger documentation
- [ ] Write API tests

### Epic 4: Admin Portals (20-25 days)

#### Task 4.1: Set Up Frontend Project
- **Effort:** 2 days
- **Assignee:** Frontend Lead
- [ ] Initialize Next.js 14 project
- [ ] Configure TypeScript, Tailwind CSS
- [ ] Set up ShadCN UI components
- [ ] Configure React Query
- [ ] Set up authentication (JWT)
- [ ] Configure routing

#### Task 4.2: Build Platform Admin Portal
- **Effort:** 8 days
- **Assignee:** Frontend Developer
- [ ] Create tenant list page with search/filter
- [ ] Create tenant creation form
- [ ] Create tenant details page
- [ ] Build platform metrics dashboard
- [ ] Build billing overview page
- [ ] Create audit log viewer
- [ ] Add responsive design
- [ ] Write frontend tests

#### Task 4.3: Build Tenant Admin Portal
- **Effort:** 6 days
- **Assignee:** Frontend Developer
- [ ] Create website list page
- [ ] Create website creation form
- [ ] Build deployment dashboard
- [ ] Create theme marketplace UI
- [ ] Create plugin marketplace UI
- [ ] Build user management UI
- [ ] Add usage metrics page
- [ ] Write frontend tests

#### Task 4.4: Build Website Content Editor
- **Effort:** 6 days
- **Assignee:** Frontend Developer
- [ ] Integrate WYSIWYG editor (TipTap/Lexical)
- [ ] Create page/post editor
- [ ] Build media library with upload
- [ ] Add SEO metadata editor
- [ ] Implement preview functionality
- [ ] Add scheduling UI
- [ ] Write editor tests

### Epic 5: Monitoring & Analytics (10-15 days)

#### Task 5.1: Implement Usage Tracking Service
- **Effort:** 3 days
- **Assignee:** Backend Developer
- [ ] Create IUsageTrackingService
- [ ] Track storage usage per tenant
- [ ] Track bandwidth usage per tenant
- [ ] Track deployment counts
- [ ] Aggregate daily/monthly usage
- [ ] Write usage tracking tests

#### Task 5.2: Build Analytics API
- **Effort:** 2 days
- **Assignee:** Backend Developer
- [ ] Implement AnalyticsController
- [ ] GET /api/v1/tenant/analytics/traffic
- [ ] GET /api/v1/tenant/analytics/usage
- [ ] GET /api/v1/tenant/analytics/deployments
- [ ] Add time-range filtering
- [ ] Write analytics API tests

#### Task 5.3: Create Grafana Dashboards
- **Effort:** 3 days
- **Assignee:** DevOps Engineer
- [ ] Create platform-wide dashboard
- [ ] Create per-tenant dashboard template
- [ ] Add resource utilization panels
- [ ] Add deployment metrics panels
- [ ] Add cost attribution panels
- [ ] Configure alerting rules

#### Task 5.4: Implement Cost Attribution
- **Effort:** 2 days
- **Assignee:** Backend Developer
- [ ] Calculate compute costs per tenant
- [ ] Calculate storage costs per tenant
- [ ] Calculate bandwidth costs per tenant
- [ ] Generate cost reports
- [ ] Write cost attribution tests

### Epic 6: Advanced Features (15-20 days)

#### Task 6.1: Integrate CDN (CloudFront)
- **Effort:** 3 days
- **Assignee:** DevOps Engineer
- [ ] Configure CloudFront distribution
- [ ] Set up origin (S3 + API)
- [ ] Configure cache behaviors
- [ ] Implement cache invalidation on deployment
- [ ] Add CDN URL rewriting
- [ ] Test cache hit rates

#### Task 6.2: Implement Database Replication
- **Effort:** 4 days
- **Assignee:** DevOps Engineer + DBA
- [ ] Set up PostgreSQL replication (async)
- [ ] Configure read replicas
- [ ] Implement read/write splitting in app
- [ ] Test replication lag
- [ ] Document failover procedures

#### Task 6.3: Automate Database Backups
- **Effort:** 2 days
- **Assignee:** DevOps Engineer
- [ ] Configure automated daily backups (pg_dump)
- [ ] Store backups in S3 with encryption
- [ ] Implement 30-day retention policy
- [ ] Add point-in-time recovery (WAL archiving)
- [ ] Test restore procedures

#### Task 6.4: Build Tenant API Gateway
- **Effort:** 4 days
- **Assignee:** Backend Developer
- [ ] Implement API Gateway service
- [ ] Add tenant-scoped API key management
- [ ] Implement rate limiting per API key
- [ ] Add API usage analytics
- [ ] Support webhook registration
- [ ] Write API gateway tests

#### Task 6.5: Security Hardening
- **Effort:** 3 days
- **Assignee:** Security Engineer + Backend Lead
- [ ] Conduct OWASP Top 10 review
- [ ] Implement CSP headers
- [ ] Add SQL injection prevention checks
- [ ] Enable WAF (Web Application Firewall)
- [ ] Implement secrets rotation
- [ ] Run penetration testing

---

## Success Metrics

### Platform Metrics
- **Tenant Provisioning Time:** < 60 seconds
- **Website Provisioning Time:** < 30 seconds
- **Deployment Success Rate:** > 99%
- **API Uptime:** > 99.9%
- **API Latency (p95):** < 300ms
- **Zero-Downtime Deployments:** 100%

### Business Metrics
- **Time to First Website:** < 5 minutes (from signup)
- **Tenant Churn Rate:** < 5% monthly
- **Average Revenue Per Tenant (ARPT):** Track and grow
- **Support Tickets per Tenant:** < 1 per month
- **Deployment Frequency:** Track per tenant

### Technical Metrics
- **Database Query Performance:** p95 < 50ms
- **Storage Cost per Tenant:** Track and optimize
- **Compute Cost per Tenant:** Track and optimize
- **CDN Cache Hit Rate:** > 80%
- **Error Rate:** < 0.1%

---

## Risk Assessment

### High Risks

**Risk 1: Database Schema Scalability**
- **Impact:** High
- **Probability:** Medium
- **Mitigation:**
  - Limit to 1000 schemas per database
  - Implement database sharding for > 1000 tenants
  - Monitor schema count and plan migration

**Risk 2: Tenant Resource Abuse**
- **Impact:** High
- **Probability:** Medium
- **Mitigation:**
  - Strict quota enforcement
  - Rate limiting per tenant
  - Automated suspension on abuse detection
  - Kubernetes resource limits

**Risk 3: Zero-Downtime Deployment Failures**
- **Impact:** Medium
- **Probability:** Low
- **Mitigation:**
  - Comprehensive pre-deployment validation
  - Instant rollback on failure detection
  - Canary deployments for high-risk changes
  - Extensive testing in staging

### Medium Risks

**Risk 4: CDN Cache Invalidation Delays**
- **Impact:** Medium
- **Probability:** Medium
- **Mitigation:**
  - Implement versioned asset URLs
  - Short TTLs for dynamic content
  - Manual invalidation option

**Risk 5: Multi-Tenant Authentication Complexity**
- **Impact:** Medium
- **Probability:** Low
- **Mitigation:**
  - Clear tenant context in all JWT tokens
  - Middleware validation on every request
  - Regular security audits

---

## Appendix: Sample Code Snippets

### Tenant Context Service

```csharp
public class TenantContextService : ITenantContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantRepository _tenantRepository;

    public async Task<Tenant?> GetCurrentTenantAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Check if already resolved
        if (httpContext.Items.TryGetValue("Tenant", out var tenant))
            return tenant as Tenant;

        // Extract tenant ID from various sources
        var tenantId = ExtractTenantId(httpContext);
        if (tenantId == null)
            return null;

        // Load tenant from database
        var resolvedTenant = await _tenantRepository.GetByIdAsync(tenantId.Value);

        // Cache in HttpContext
        httpContext.Items["TenantId"] = tenantId;
        httpContext.Items["Tenant"] = resolvedTenant;

        return resolvedTenant;
    }

    private Guid? ExtractTenantId(HttpContext context)
    {
        // Option 1: From subdomain (preferred for web access)
        var host = context.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length >= 3)
        {
            var subdomain = parts[0];
            return _tenantRepository.GetBySubdomainAsync(subdomain).Result?.TenantId;
        }

        // Option 2: From X-Tenant-ID header (for API access)
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerValue))
        {
            if (Guid.TryParse(headerValue, out var tenantId))
                return tenantId;
        }

        // Option 3: From JWT claim
        var user = context.User;
        var tenantClaim = user.FindFirst("tenant_id");
        if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var claimTenantId))
            return claimTenantId;

        return null;
    }
}
```

### Website Hot-Swap Deployment

```csharp
public class WebsiteThemeDeploymentStrategy : IDeploymentStrategy
{
    private readonly ILogger<WebsiteThemeDeploymentStrategy> _logger;
    private readonly IWebsiteRepository _websiteRepository;
    private readonly IThemeRepository _themeRepository;
    private readonly ICacheService _cacheService;

    public async Task<DeploymentResult> DeployAsync(
        TenantDeploymentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting hot-swap theme deployment for website {WebsiteId}",
            request.WebsiteId);

        try
        {
            // 1. Validate theme package
            var theme = await _themeRepository.GetByIdAsync(request.Module.ThemeId);
            if (theme == null)
                return DeploymentResult.Failure("Theme not found");

            // 2. Pre-deployment validation
            var validationResult = await ValidateThemeAsync(theme);
            if (!validationResult.IsValid)
                return DeploymentResult.Failure(validationResult.Errors);

            // 3. Prepare new theme (extract assets to CDN)
            await PrepareThemeAssetsAsync(theme, request.WebsiteId);

            // 4. Atomic theme swap (database transaction)
            await using var transaction = await _websiteRepository.BeginTransactionAsync();
            try
            {
                var website = await _websiteRepository.GetByIdAsync(request.WebsiteId);
                var previousThemeId = website.CurrentThemeId;

                website.CurrentThemeId = theme.ThemeId;
                website.ThemeVersion = theme.Version;
                await _websiteRepository.UpdateAsync(website);

                await transaction.CommitAsync(cancellationToken);

                // 5. Invalidate cache (theme templates, CSS, JS)
                await _cacheService.InvalidateThemeCache(request.WebsiteId);

                // 6. Verify deployment (smoke test)
                var smokeTestResult = await SmokeTestWebsiteAsync(request.WebsiteId);
                if (!smokeTestResult.Success)
                {
                    // Rollback to previous theme
                    await RollbackThemeAsync(request.WebsiteId, previousThemeId);
                    return DeploymentResult.Failure("Smoke test failed, rolled back");
                }

                _logger.LogInformation("Theme deployment succeeded for website {WebsiteId}",
                    request.WebsiteId);

                return DeploymentResult.Success(
                    $"Theme {theme.Name} v{theme.Version} deployed");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Theme deployment failed for website {WebsiteId}",
                request.WebsiteId);
            return DeploymentResult.Failure(ex.Message);
        }
    }
}
```

---

## Conclusion

The **HotSwap Multi-Tenant Website Platform** represents a comprehensive, enterprise-grade solution for hosting and managing thousands of websites with zero-downtime deployments. By leveraging the proven HotSwap kernel architecture and extending it with multi-tenancy capabilities, this platform offers:

- **Scalability:** Support for 1000+ tenants on shared infrastructure
- **Reliability:** 99.9%+ uptime with zero-downtime deployments
- **Security:** Complete tenant isolation and enterprise-grade security
- **Economics:** Cost-effective shared infrastructure with usage-based billing
- **Developer Experience:** Modern APIs, SDKs, and admin portals

**Total Estimated Effort:** 95-125 days (3-4 months with 2-3 developers)

**Recommended Team:**
- 1 Backend Lead (.NET/C#)
- 2 Backend Developers
- 1 Frontend Developer (React/Next.js)
- 1 DevOps Engineer
- 1 QA Engineer (part-time)

**Next Steps:**
1. Review and approve this implementation plan
2. Assemble development team
3. Set up development environment
4. Begin Phase 1 (Foundation) implementation
5. Establish weekly sprint reviews

---

**Document Version:** 1.0
**Last Updated:** 2025-11-17
**Author:** AI Architecture Team
**Status:** Ready for Review
