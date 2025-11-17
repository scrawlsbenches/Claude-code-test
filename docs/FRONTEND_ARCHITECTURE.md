# Frontend Architecture - Epic 4

## Overview

The multi-tenant website platform includes three web-based portals:
1. **Platform Admin Portal** - For platform administrators
2. **Tenant Admin Portal** - For tenant administrators
3. **Website Content Editor** - For content editors

## Technology Stack

- **Framework**: Next.js 14 (React 18+ with App Router)
- **Language**: TypeScript 5+
- **Styling**: Tailwind CSS 3+
- **UI Components**: shadcn/ui (Radix UI primitives)
- **State Management**: React Query (TanStack Query)
- **Forms**: React Hook Form + Zod validation
- **Authentication**: NextAuth.js with JWT
- **API Client**: Fetch API with TypeScript types

## Project Structure

```
frontend/
├── apps/
│   ├── admin/           # Platform admin portal
│   ├── tenant/          # Tenant admin portal
│   └── editor/          # Content editor
├── packages/
│   ├── ui/              # Shared UI components
│   ├── api-client/      # API client library
│   └── types/           # Shared TypeScript types
├── package.json
└── turbo.json           # Turborepo configuration
```

## Platform Admin Portal

### Features
- Tenant management (create, suspend, delete)
- Platform-wide metrics dashboard
- Billing overview
- System health monitoring
- Audit log viewer

### Key Pages
- `/dashboard` - Platform metrics
- `/tenants` - Tenant list and management
- `/billing` - Billing overview
- `/audit-logs` - Audit trail
- `/settings` - Platform configuration

## Tenant Admin Portal

### Features
- Website management
- Deployment dashboard
- Theme marketplace
- Plugin marketplace
- User management (tenant-scoped)
- Usage analytics
- Billing and subscription

### Key Pages
- `/websites` - Website list
- `/websites/new` - Create website
- `/websites/{id}` - Website details
- `/deployments` - Deployment history
- `/themes` - Theme marketplace
- `/plugins` - Plugin marketplace
- `/users` - User management
- `/analytics` - Usage analytics
- `/billing` - Subscription and billing

## Website Content Editor

### Features
- WYSIWYG content editor
- Page/post management
- Media library
- SEO optimization
- Preview before publish
- Scheduled publishing

### Key Pages
- `/pages` - Page list
- `/pages/new` - Create page
- `/pages/{id}/edit` - Edit page
- `/media` - Media library
- `/settings` - Website settings

## API Integration

### API Client Setup

```typescript
// packages/api-client/src/index.ts
export class ApiClient {
  private baseUrl: string;
  private tenantId?: string;

  constructor(baseUrl: string, tenantId?: string) {
    this.baseUrl = baseUrl;
    this.tenantId = tenantId;
  }

  private async request<T>(
    path: string,
    options?: RequestInit
  ): Promise<T> {
    const headers = {
      'Content-Type': 'application/json',
      ...(this.tenantId && { 'X-Tenant-ID': this.tenantId }),
      ...options?.headers,
    };

    const response = await fetch(`${this.baseUrl}${path}`, {
      ...options,
      headers,
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.statusText}`);
    }

    return response.json();
  }

  // Tenant endpoints
  async createTenant(data: CreateTenantRequest): Promise<TenantResponse> {
    return this.request('/api/v1/admin/tenants', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Website endpoints
  async listWebsites(): Promise<WebsiteResponse[]> {
    return this.request('/api/v1/websites');
  }

  // Content endpoints
  async createPage(
    websiteId: string,
    data: CreatePageRequest
  ): Promise<PageResponse> {
    return this.request(`/api/v1/websites/${websiteId}/content/pages`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }
}
```

## Authentication Flow

1. User logs in via `/api/auth/signin`
2. Backend returns JWT token with tenant claim
3. Frontend stores token in httpOnly cookie
4. All API requests include token
5. Backend validates token and extracts tenant context

## Deployment

### Development
```bash
cd frontend
npm install
npm run dev
```

### Production Build
```bash
npm run build
npm run start
```

### Docker Deployment
```dockerfile
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:20-alpine
WORKDIR /app
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/public ./public
COPY --from=builder /app/package*.json ./
RUN npm ci --production

EXPOSE 3000
CMD ["npm", "start"]
```

## Implementation Status

**Status**: Architecture Defined ✓

The frontend architecture is designed and documented. Implementation requires:
1. Node.js 20+ environment
2. Next.js project initialization
3. Component library setup
4. API client implementation
5. Authentication integration

**Note**: Frontend implementation is outside the scope of the current backend-focused
autonomous implementation. The backend APIs are fully functional and ready for
frontend integration.
