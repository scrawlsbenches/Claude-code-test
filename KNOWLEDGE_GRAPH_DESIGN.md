# Knowledge Graph System Design
**Built on HotSwap Distributed Kernel**

**Version:** 1.0
**Created:** 2025-11-16
**Status:** Design Specification

---

## Design Notes & Conceptual Thinking

### Core Concept
Build a distributed, versioned knowledge graph system that leverages the HotSwap kernel's orchestration capabilities to enable zero-downtime updates to:
- Query algorithms (e.g., new graph traversal algorithms)
- Schema versions (evolving entity/relationship types)
- Storage backends (migrating between storage strategies)
- Indexing strategies (A/B testing different index structures)

### Key Insights

**Why HotSwap + Knowledge Graph?**
1. **Schema Evolution Without Downtime** - Deploy schema changes using canary strategy
2. **Query Algorithm Improvements** - Hot-swap graph traversal algorithms
3. **Distributed Graph Partitioning** - Leverage multi-node orchestration
4. **Storage Migration** - Blue-green deployment for storage backend changes
5. **Scalability** - Horizontal scaling with orchestrated partition management

**Leveraging Existing HotSwap Features:**
- **Deployment Strategies** → Schema/algorithm versioning
- **Health Monitoring** → Graph partition health checks
- **Distributed Tracing** → Query execution tracing across partitions
- **Security** → Graph access control and audit logs
- **Metrics** → Query performance, graph statistics
- **Approval Workflow** → Production schema change approval

### Architecture Philosophy

**Triple Store Foundation:**
```
(Subject, Predicate, Object) = (Entity, Relationship, Entity)
```

**Graph Model:**
- **Entities** = Nodes with types, properties, IDs
- **Relationships** = Edges with types, properties, directionality
- **Schema** = Entity types, relationship types, constraints
- **Indexes** = Fast lookup structures (by type, property, etc.)

**Distribution Strategy:**
- **Partition by entity type** - e.g., Users on partition 1, Documents on partition 2
- **Replicate critical subgraphs** - e.g., user authentication graph replicated
- **Shard large entity types** - e.g., Documents partitioned by hash(ID)

### Technical Approach

**Storage Options:**
1. **PostgreSQL with JSONB** (initial implementation)
   - Tables: entities, relationships, schema_definitions
   - Indexes: GIN indexes on JSONB properties
   - Foreign keys for referential integrity

2. **In-Memory Cache** (for hot paths)
   - Redis with graph data structures
   - LRU eviction for memory management

3. **Future: Neo4j Integration** (optional)
   - HotSwap as orchestration layer
   - Neo4j as graph storage engine

**Query Language Design:**
- **Simple Pattern Matching** - Start with basic patterns
- **Graph Traversal DSL** - Custom domain-specific language
- **Future: Cypher-like** - Industry-standard compatibility

**Hot-Swap Use Cases:**
1. Deploy new shortest-path algorithm → Canary deployment
2. Add entity type to schema → Rolling update with backward compatibility
3. Migrate from PostgreSQL to Neo4j → Blue-green deployment
4. Update indexing strategy → Direct deployment to dev, rolling to production

---

## System Overview

### Vision Statement
A production-ready, distributed knowledge graph system that enables organizations to build, query, and evolve semantic knowledge bases with zero downtime through hot-swappable components.

### Goals
1. **Zero-Downtime Evolution** - Schema, query, and storage updates without service interruption
2. **High Performance** - Sub-100ms queries for simple patterns, sub-1s for complex traversals
3. **Scalability** - Support 10M+ entities, 100M+ relationships across distributed partitions
4. **Developer-Friendly** - Simple API, comprehensive examples, extensive documentation
5. **Enterprise-Ready** - Authentication, authorization, audit logging, compliance

### Non-Goals (Phase 1)
- Full Cypher/SPARQL compatibility (start with simple query DSL)
- Real-time graph streaming (batch updates acceptable)
- Machine learning model integration (future phase)
- Multi-tenancy (single tenant initially)

---

## Epic Breakdown

## Epic 1: Core Graph Domain Model
**Priority:** 🔴 Critical
**Effort:** 3-4 days
**Dependencies:** None

Build the foundational domain models for the knowledge graph system.

### Stories

#### Story 1.1: Entity Model
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `Entity` class with ID, Type, Properties, CreatedAt, UpdatedAt
- [ ] Support strongly-typed and dynamic properties (JSONB)
- [ ] Implement validation for entity types
- [ ] Add equality and hash code implementations
- [ ] Include XML documentation

**Technical Details:**
```csharp
public class Entity
{
    public required Guid Id { get; init; }
    public required string Type { get; init; } // e.g., "Person", "Document"
    public required Dictionary<string, object> Properties { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CreatedBy { get; init; }
}
```

#### Story 1.2: Relationship Model
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `Relationship` class with ID, Type, SourceId, TargetId, Properties
- [ ] Support directionality (source → target)
- [ ] Implement validation for relationship types
- [ ] Add weight/strength property for ranking
- [ ] Include bidirectional navigation helpers

**Technical Details:**
```csharp
public class Relationship
{
    public required Guid Id { get; init; }
    public required string Type { get; init; } // e.g., "AUTHORED_BY", "RELATED_TO"
    public required Guid SourceEntityId { get; init; }
    public required Guid TargetEntityId { get; init; }
    public required Dictionary<string, object> Properties { get; init; }
    public double Weight { get; init; } = 1.0;
    public DateTimeOffset CreatedAt { get; init; }
}
```

#### Story 1.3: Graph Schema Model
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `GraphSchema` with entity types, relationship types, constraints
- [ ] Support property schemas (required, types, validation)
- [ ] Implement schema versioning
- [ ] Add schema validation for entities/relationships
- [ ] Support backward compatibility checks

**Technical Details:**
```csharp
public class GraphSchema
{
    public required string Version { get; init; }
    public required Dictionary<string, EntityTypeDefinition> EntityTypes { get; init; }
    public required Dictionary<string, RelationshipTypeDefinition> RelationshipTypes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public class EntityTypeDefinition
{
    public required string Name { get; init; }
    public required Dictionary<string, PropertyDefinition> Properties { get; init; }
    public List<string>? Indexes { get; init; }
}
```

#### Story 1.4: Graph Query Model
**Effort:** 0.5 day

**Acceptance Criteria:**
- [ ] Create `GraphQuery` request model
- [ ] Support pattern matching (entity type, relationship type filters)
- [ ] Add pagination support
- [ ] Include result projection options
- [ ] Support query options (depth limit, timeout)

**Technical Details:**
```csharp
public class GraphQuery
{
    public string? EntityType { get; init; }
    public Dictionary<string, object>? PropertyFilters { get; init; }
    public List<RelationshipPattern>? RelationshipPatterns { get; init; }
    public int MaxDepth { get; init; } = 3;
    public int PageSize { get; init; } = 100;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

public class RelationshipPattern
{
    public string? RelationshipType { get; init; }
    public Direction Direction { get; init; } // Outgoing, Incoming, Both
    public string? TargetEntityType { get; init; }
}
```

---

## Epic 2: PostgreSQL Graph Storage
**Priority:** 🔴 Critical
**Effort:** 4-5 days
**Dependencies:** Epic 1

Implement persistent storage for graph data using PostgreSQL with optimized indexing.

### Stories

#### Story 2.1: Entity Framework Models
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create EF Core entity models (EntityRecord, RelationshipRecord)
- [ ] Configure JSONB columns for properties
- [ ] Add indexes (primary keys, foreign keys, GIN on JSONB)
- [ ] Create DbContext with proper configuration
- [ ] Generate initial migration

**Technical Details:**
```csharp
public class EntityRecord
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public string PropertiesJson { get; set; } = "{}"; // JSONB column
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

// In DbContext:
modelBuilder.Entity<EntityRecord>()
    .Property(e => e.PropertiesJson)
    .HasColumnType("jsonb");

modelBuilder.Entity<EntityRecord>()
    .HasIndex(e => e.Type);

// GIN index for JSONB queries
migrationBuilder.Sql(
    "CREATE INDEX IX_Entities_PropertiesJson ON entities USING GIN (properties_json)");
```

#### Story 2.2: Graph Repository Interface
**Effort:** 0.5 day

**Acceptance Criteria:**
- [ ] Define `IGraphRepository` interface
- [ ] Include entity CRUD operations
- [ ] Include relationship CRUD operations
- [ ] Add batch operations for performance
- [ ] Include query methods

**Technical Details:**
```csharp
public interface IGraphRepository
{
    // Entity operations
    Task<Entity> CreateEntityAsync(Entity entity, CancellationToken cancellationToken = default);
    Task<Entity?> GetEntityByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entity>> GetEntitiesByTypeAsync(string type, int skip, int take, CancellationToken cancellationToken = default);
    Task<Entity> UpdateEntityAsync(Entity entity, CancellationToken cancellationToken = default);
    Task DeleteEntityAsync(Guid id, CancellationToken cancellationToken = default);

    // Relationship operations
    Task<Relationship> CreateRelationshipAsync(Relationship relationship, CancellationToken cancellationToken = default);
    Task<IEnumerable<Relationship>> GetRelationshipsAsync(Guid entityId, Direction direction, CancellationToken cancellationToken = default);
    Task DeleteRelationshipAsync(Guid id, CancellationToken cancellationToken = default);

    // Query operations
    Task<GraphQueryResult> ExecuteQueryAsync(GraphQuery query, CancellationToken cancellationToken = default);
}
```

#### Story 2.3: PostgreSQL Repository Implementation
**Effort:** 2 days

**Acceptance Criteria:**
- [ ] Implement `PostgreSqlGraphRepository`
- [ ] Use EF Core for data access
- [ ] Implement efficient JSONB queries
- [ ] Add transaction support
- [ ] Include error handling and logging
- [ ] Optimize queries with proper indexes

**Performance Requirements:**
- Single entity retrieval: <10ms
- Entity type query (100 results): <100ms
- Relationship traversal (depth 2): <200ms

#### Story 2.4: Graph Indexing Service
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `IGraphIndexService` interface
- [ ] Implement index management for common query patterns
- [ ] Support full-text search on entity properties
- [ ] Add index rebuild capability
- [ ] Include index statistics

**Technical Details:**
```csharp
public interface IGraphIndexService
{
    Task CreateIndexAsync(string entityType, string propertyName, CancellationToken cancellationToken = default);
    Task RebuildIndexesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Entity>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default);
}
```

---

## Epic 3: Graph Query Engine
**Priority:** 🔴 Critical
**Effort:** 5-6 days
**Dependencies:** Epic 2

Implement query execution engine for pattern matching and graph traversal.

### Stories

#### Story 3.1: Pattern Matching Engine
**Effort:** 2 days

**Acceptance Criteria:**
- [ ] Create `IPatternMatcher` interface
- [ ] Implement simple pattern matching (entity type + property filters)
- [ ] Support relationship pattern matching (A -[RELATION]-> B)
- [ ] Add multi-hop pattern support (A -[R1]-> B -[R2]-> C)
- [ ] Optimize with query planning

**Technical Details:**
```csharp
public interface IPatternMatcher
{
    Task<IEnumerable<Entity>> MatchPatternAsync(GraphQuery query, CancellationToken cancellationToken = default);
}

// Example query:
// Find all Documents authored by Users with role="admin"
var query = new GraphQuery
{
    EntityType = "Document",
    RelationshipPatterns = new List<RelationshipPattern>
    {
        new() {
            RelationshipType = "AUTHORED_BY",
            Direction = Direction.Incoming,
            TargetEntityType = "User",
            PropertyFilters = new() { ["role"] = "admin" }
        }
    }
};
```

#### Story 3.2: Graph Traversal Algorithms
**Effort:** 2 days

**Acceptance Criteria:**
- [ ] Implement breadth-first search (BFS)
- [ ] Implement depth-first search (DFS)
- [ ] Implement shortest path (Dijkstra)
- [ ] Add cycle detection
- [ ] Include traversal result models

**Technical Details:**
```csharp
public interface IGraphTraversal
{
    Task<IEnumerable<Entity>> BreadthFirstSearchAsync(Guid startEntityId, int maxDepth, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entity>> DepthFirstSearchAsync(Guid startEntityId, int maxDepth, CancellationToken cancellationToken = default);
    Task<PathResult?> FindShortestPathAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default);
    Task<IEnumerable<List<Entity>>> FindAllPathsAsync(Guid sourceId, Guid targetId, int maxDepth, CancellationToken cancellationToken = default);
}

public class PathResult
{
    public List<Entity> Entities { get; init; } = new();
    public List<Relationship> Relationships { get; init; } = new();
    public double TotalWeight { get; init; }
    public int Hops => Relationships.Count;
}
```

#### Story 3.3: Query Optimizer
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `IQueryOptimizer` interface
- [ ] Implement query plan generation
- [ ] Add cost-based optimization (estimate cardinality)
- [ ] Support index selection
- [ ] Include query execution statistics

**Technical Details:**
```csharp
public interface IQueryOptimizer
{
    QueryPlan OptimizeQuery(GraphQuery query);
}

public class QueryPlan
{
    public List<QueryStep> Steps { get; init; } = new();
    public double EstimatedCost { get; init; }
    public TimeSpan EstimatedDuration { get; init; }
}

public class QueryStep
{
    public string Operation { get; init; } = null!; // "IndexScan", "Filter", "Join", etc.
    public Dictionary<string, object> Parameters { get; init; } = new();
}
```

#### Story 3.4: Query Execution Service
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `IGraphQueryService` orchestrating pattern matching and traversal
- [ ] Add query caching for frequent patterns
- [ ] Implement query timeout handling
- [ ] Include distributed tracing for queries
- [ ] Add query performance metrics

**Technical Details:**
```csharp
public interface IGraphQueryService
{
    Task<GraphQueryResult> ExecuteQueryAsync(GraphQuery query, CancellationToken cancellationToken = default);
    Task<GraphQueryResult> ExecuteTraversalAsync(Guid startEntityId, TraversalQuery traversal, CancellationToken cancellationToken = default);
}

public class GraphQueryResult
{
    public List<Entity> Entities { get; init; } = new();
    public List<Relationship> Relationships { get; init; } = new();
    public int TotalCount { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public string? QueryPlan { get; init; }
}
```

---

## Epic 4: Knowledge Graph REST API
**Priority:** 🟡 High
**Effort:** 4-5 days
**Dependencies:** Epic 3

Build comprehensive REST API for graph operations.

### Stories

#### Story 4.1: Entities Controller
**Effort:** 1.5 days

**Acceptance Criteria:**
- [ ] Create `EntitiesController` with CRUD endpoints
- [ ] Support bulk entity creation
- [ ] Add pagination for list operations
- [ ] Include validation and error handling
- [ ] Add authentication and authorization (RBAC)
- [ ] Include OpenAPI documentation

**API Endpoints:**
```
POST   /api/v1/entities                      - Create entity
POST   /api/v1/entities/bulk                 - Create multiple entities
GET    /api/v1/entities/{id}                 - Get entity by ID
GET    /api/v1/entities                      - List entities (filtered)
PATCH  /api/v1/entities/{id}                 - Update entity
DELETE /api/v1/entities/{id}                 - Delete entity
GET    /api/v1/entities/{id}/relationships   - Get entity relationships
```

#### Story 4.2: Relationships Controller
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `RelationshipsController` with CRUD endpoints
- [ ] Support creating relationships between entities
- [ ] Add validation for source/target existence
- [ ] Include relationship type validation
- [ ] Add authentication and authorization

**API Endpoints:**
```
POST   /api/v1/relationships                 - Create relationship
GET    /api/v1/relationships/{id}            - Get relationship by ID
GET    /api/v1/relationships                 - List relationships (filtered)
DELETE /api/v1/relationships/{id}            - Delete relationship
```

#### Story 4.3: Queries Controller
**Effort:** 1.5 days

**Acceptance Criteria:**
- [ ] Create `QueriesController` for executing graph queries
- [ ] Support pattern matching queries
- [ ] Add graph traversal endpoints
- [ ] Include query timeout handling
- [ ] Add rate limiting for expensive queries
- [ ] Include query result caching

**API Endpoints:**
```
POST   /api/v1/queries/match                 - Execute pattern matching query
POST   /api/v1/queries/traverse              - Execute graph traversal
GET    /api/v1/queries/{id}                  - Get cached query result
POST   /api/v1/queries/shortest-path         - Find shortest path
GET    /api/v1/queries/statistics            - Get query performance stats
```

#### Story 4.4: Schema Controller
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `SchemaController` for schema management
- [ ] Support schema versioning
- [ ] Add schema validation endpoint
- [ ] Include schema migration support
- [ ] Add approval workflow for schema changes (Admin only)

**API Endpoints:**
```
GET    /api/v1/schema                        - Get current schema
GET    /api/v1/schema/versions               - List schema versions
POST   /api/v1/schema                        - Create new schema version (Admin)
POST   /api/v1/schema/validate               - Validate entity/relationship against schema
GET    /api/v1/schema/compatibility/{v1}/{v2} - Check compatibility
```

---

## Epic 5: HotSwap Integration
**Priority:** 🟡 High
**Effort:** 3-4 days
**Dependencies:** Epic 4

Integrate knowledge graph with HotSwap orchestration for zero-downtime updates.

### Stories

#### Story 5.1: Graph Module Descriptor
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create `GraphModuleDescriptor` extending `ModuleDescriptor`
- [ ] Include schema version in module metadata
- [ ] Add query algorithm version tracking
- [ ] Include backward compatibility flags
- [ ] Support module signature verification

**Technical Details:**
```csharp
public class GraphModuleDescriptor : ModuleDescriptor
{
    public required string SchemaVersion { get; init; }
    public required string QueryEngineVersion { get; init; }
    public List<string> SupportedSchemaVersions { get; init; } = new();
    public Dictionary<string, string> AlgorithmVersions { get; init; } = new();
}
```

#### Story 5.2: Schema Migration Strategy
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Implement schema migration using HotSwap deployment strategies
- [ ] Use canary deployment for schema changes in production
- [ ] Add backward compatibility validation
- [ ] Include rollback support for failed migrations
- [ ] Add migration progress tracking

**Migration Workflow:**
```
1. Create new schema version (v2.0)
2. Deploy to Dev (Direct strategy)
3. Deploy to QA (Rolling strategy)
4. Deploy to Staging with approval (Blue-Green strategy)
5. Deploy to Production (Canary: 10% → 30% → 50% → 100%)
6. Monitor query performance metrics
7. Rollback if degradation detected
```

#### Story 5.3: Query Algorithm Hot-Swap
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Package query algorithms as hot-swappable modules
- [ ] Support A/B testing different algorithms
- [ ] Add performance comparison metrics
- [ ] Include automatic rollback on regression
- [ ] Support gradual algorithm rollout

**Use Case:**
```
Deploy improved shortest-path algorithm:
1. Package new Dijkstra implementation as module v2.1
2. Deploy to 10% of production traffic (Canary)
3. Compare performance metrics (latency, accuracy)
4. If p95 latency improved by >20% → proceed to 100%
5. If degraded → automatic rollback to v2.0
```

#### Story 5.4: Graph Partition Management
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Implement graph partition health checks
- [ ] Add partition rebalancing support
- [ ] Include cross-partition query coordination
- [ ] Support partition failover
- [ ] Add partition metrics (entity count, query load)

---

## Epic 6: Comprehensive Testing
**Priority:** 🔴 Critical
**Effort:** 4-5 days
**Dependencies:** Epic 5

Build comprehensive test suite for knowledge graph system.

### Stories

#### Story 6.1: Unit Tests for Domain Models
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Test entity validation (15 tests)
- [ ] Test relationship validation (15 tests)
- [ ] Test schema validation (20 tests)
- [ ] Test query model validation (10 tests)
- [ ] Achieve 95%+ code coverage

#### Story 6.2: Repository Integration Tests
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Test CRUD operations with real PostgreSQL (Testcontainers)
- [ ] Test transaction handling
- [ ] Test concurrent access scenarios
- [ ] Test JSONB query performance
- [ ] Verify index effectiveness

#### Story 6.3: Query Engine Tests
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Test pattern matching accuracy (20 tests)
- [ ] Test graph traversal algorithms (15 tests)
- [ ] Test query optimization (10 tests)
- [ ] Test edge cases (cycles, disconnected graphs)
- [ ] Performance benchmarks (<100ms for simple queries)

#### Story 6.4: API Integration Tests
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Test all API endpoints end-to-end
- [ ] Test authentication and authorization
- [ ] Test error handling and validation
- [ ] Test rate limiting
- [ ] Test API documentation (Swagger)

#### Story 6.5: HotSwap Integration Tests
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Test schema migration with canary deployment
- [ ] Test query algorithm hot-swap
- [ ] Test rollback scenarios
- [ ] Test health checks during updates
- [ ] Test zero-downtime guarantees

---

## Epic 7: Performance Optimization
**Priority:** 🟢 Medium
**Effort:** 3-4 days
**Dependencies:** Epic 6

Optimize query performance and scalability.

### Stories

#### Story 7.1: Query Result Caching
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Implement Redis-based query result cache
- [ ] Add cache invalidation on entity/relationship changes
- [ ] Support TTL-based expiration
- [ ] Include cache hit/miss metrics
- [ ] Add cache warming for frequent queries

#### Story 7.2: Database Query Optimization
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Analyze slow queries with EXPLAIN ANALYZE
- [ ] Add missing indexes
- [ ] Optimize JSONB queries
- [ ] Implement connection pooling
- [ ] Add query execution plans to logs

#### Story 7.3: Batch Processing
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Implement bulk entity creation (1000+ entities)
- [ ] Add batch relationship creation
- [ ] Support batch queries
- [ ] Include progress tracking for large operations
- [ ] Add timeout handling

#### Story 7.4: Graph Partitioning Strategy
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Design entity type-based partitioning
- [ ] Implement partition routing
- [ ] Add cross-partition query support
- [ ] Include partition health monitoring
- [ ] Support dynamic repartitioning

---

## Epic 8: Documentation & Examples
**Priority:** 🟢 Medium
**Effort:** 3 days
**Dependencies:** Epic 7

Create comprehensive documentation and examples.

### Stories

#### Story 8.1: API Documentation
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Complete OpenAPI/Swagger documentation
- [ ] Add request/response examples for all endpoints
- [ ] Include authentication guide
- [ ] Document rate limits
- [ ] Add troubleshooting section

#### Story 8.2: Developer Guide
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create KNOWLEDGE_GRAPH_GUIDE.md
- [ ] Include quick start tutorial
- [ ] Add schema design best practices
- [ ] Document query patterns
- [ ] Include performance tuning guide

#### Story 8.3: Example Applications
**Effort:** 1 day

**Acceptance Criteria:**
- [ ] Create example: Document knowledge base
- [ ] Create example: Organization hierarchy
- [ ] Create example: Dependency graph
- [ ] Create example: Semantic search
- [ ] Include runnable demo scripts

---

## Technical Specifications

### Data Models

#### Entity
```csharp
namespace HotSwap.KnowledgeGraph.Domain.Models
{
    public class Entity
    {
        public required Guid Id { get; init; }
        public required string Type { get; init; }
        public required Dictionary<string, object> Properties { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? CreatedBy { get; init; }
        public string? UpdatedBy { get; set; }
        public int Version { get; set; } = 1;
    }
}
```

#### Relationship
```csharp
namespace HotSwap.KnowledgeGraph.Domain.Models
{
    public class Relationship
    {
        public required Guid Id { get; init; }
        public required string Type { get; init; }
        public required Guid SourceEntityId { get; init; }
        public required Guid TargetEntityId { get; init; }
        public required Dictionary<string, object> Properties { get; init; }
        public double Weight { get; init; } = 1.0;
        public bool IsDirected { get; init; } = true;
        public DateTimeOffset CreatedAt { get; init; }
        public string? CreatedBy { get; init; }
    }
}
```

### Database Schema

```sql
-- Entities table
CREATE TABLE entities (
    id UUID PRIMARY KEY,
    type VARCHAR(100) NOT NULL,
    properties JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255),
    updated_by VARCHAR(255),
    version INT NOT NULL DEFAULT 1
);

-- Indexes for entities
CREATE INDEX idx_entities_type ON entities(type);
CREATE INDEX idx_entities_created_at ON entities(created_at DESC);
CREATE INDEX idx_entities_properties ON entities USING GIN(properties);

-- Relationships table
CREATE TABLE relationships (
    id UUID PRIMARY KEY,
    type VARCHAR(100) NOT NULL,
    source_entity_id UUID NOT NULL REFERENCES entities(id) ON DELETE CASCADE,
    target_entity_id UUID NOT NULL REFERENCES entities(id) ON DELETE CASCADE,
    properties JSONB NOT NULL DEFAULT '{}',
    weight DOUBLE PRECISION NOT NULL DEFAULT 1.0,
    is_directed BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255)
);

-- Indexes for relationships
CREATE INDEX idx_relationships_type ON relationships(type);
CREATE INDEX idx_relationships_source ON relationships(source_entity_id);
CREATE INDEX idx_relationships_target ON relationships(target_entity_id);
CREATE INDEX idx_relationships_source_type ON relationships(source_entity_id, type);
CREATE INDEX idx_relationships_target_type ON relationships(target_entity_id, type);
CREATE INDEX idx_relationships_properties ON relationships USING GIN(properties);

-- Schema versions table
CREATE TABLE schema_versions (
    version VARCHAR(50) PRIMARY KEY,
    schema_json JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT false
);

-- Indexes table for tracking custom indexes
CREATE TABLE graph_indexes (
    id UUID PRIMARY KEY,
    entity_type VARCHAR(100) NOT NULL,
    property_name VARCHAR(100) NOT NULL,
    index_type VARCHAR(50) NOT NULL, -- 'btree', 'hash', 'gin', etc.
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(entity_type, property_name)
);
```

### API Endpoints Summary

#### Entities API
```
POST   /api/v1/graph/entities                - Create entity
POST   /api/v1/graph/entities/bulk           - Create multiple entities
GET    /api/v1/graph/entities/{id}           - Get entity by ID
GET    /api/v1/graph/entities                - List entities (with filters)
PATCH  /api/v1/graph/entities/{id}           - Update entity properties
PUT    /api/v1/graph/entities/{id}           - Replace entity
DELETE /api/v1/graph/entities/{id}           - Delete entity
GET    /api/v1/graph/entities/{id}/relationships - Get entity relationships
```

#### Relationships API
```
POST   /api/v1/graph/relationships           - Create relationship
POST   /api/v1/graph/relationships/bulk      - Create multiple relationships
GET    /api/v1/graph/relationships/{id}      - Get relationship by ID
GET    /api/v1/graph/relationships           - List relationships (with filters)
PATCH  /api/v1/graph/relationships/{id}      - Update relationship properties
DELETE /api/v1/graph/relationships/{id}      - Delete relationship
```

#### Queries API
```
POST   /api/v1/graph/queries/match           - Execute pattern matching query
POST   /api/v1/graph/queries/traverse        - Execute graph traversal
POST   /api/v1/graph/queries/shortest-path   - Find shortest path
POST   /api/v1/graph/queries/all-paths       - Find all paths (limited depth)
GET    /api/v1/graph/queries/{id}            - Get cached query result
GET    /api/v1/graph/queries/statistics      - Get query performance stats
POST   /api/v1/graph/queries/search          - Full-text search across entities
```

#### Schema API
```
GET    /api/v1/graph/schema                  - Get active schema
GET    /api/v1/graph/schema/versions         - List all schema versions
GET    /api/v1/graph/schema/versions/{v}     - Get specific schema version
POST   /api/v1/graph/schema                  - Create new schema version (Admin)
POST   /api/v1/graph/schema/validate         - Validate entity/relationship
GET    /api/v1/graph/schema/compatibility/{v1}/{v2} - Check compatibility
POST   /api/v1/graph/schema/activate/{v}     - Activate schema version (Admin)
```

#### Visualization API
```
GET    /api/v1/graph/visualization/{id}      - Get graph visualization data
POST   /api/v1/graph/visualization/subgraph  - Get subgraph around entity
GET    /api/v1/graph/visualization/statistics - Get graph statistics
```

### Performance Targets

| Operation | Target Latency | Notes |
|-----------|---------------|-------|
| Get entity by ID | <10ms | Single database query with index |
| List entities by type (100 results) | <100ms | With pagination and proper indexing |
| Create entity | <50ms | Single insert with JSONB |
| Pattern match (simple) | <100ms | Entity type + property filter |
| Pattern match (2-hop) | <500ms | With relationship join |
| Graph traversal (depth 3) | <1s | BFS/DFS with limit |
| Shortest path (depth ≤5) | <2s | Dijkstra with pruning |
| Bulk entity creation (1000) | <5s | Batch insert with transaction |

### Scalability Targets

| Metric | Target | Notes |
|--------|--------|-------|
| Total entities | 10M+ | With partitioning |
| Total relationships | 100M+ | With efficient indexing |
| Concurrent queries | 1000 req/s | With caching and optimization |
| Query result cache hit rate | >80% | For frequent patterns |
| Average query latency (p95) | <500ms | Across all query types |
| Database storage | <500GB | For 10M entities with JSONB |

---

## Implementation Roadmap

### Phase 1: Core Foundation (2 weeks)
**Epics:** 1, 2
**Deliverables:**
- Domain models (Entity, Relationship, Schema, Query)
- PostgreSQL storage with EF Core
- Basic CRUD operations
- Database migrations
- Unit tests (50+ tests)

**Milestone:** Can store and retrieve entities/relationships from PostgreSQL

### Phase 2: Query Engine (2 weeks)
**Epics:** 3
**Deliverables:**
- Pattern matching engine
- Graph traversal algorithms (BFS, DFS, shortest path)
- Query optimizer
- Query execution service
- Performance benchmarks

**Milestone:** Can execute complex graph queries with acceptable performance

### Phase 3: REST API (1.5 weeks)
**Epics:** 4
**Deliverables:**
- Entities, Relationships, Queries, Schema controllers
- OpenAPI documentation
- Authentication and authorization
- Rate limiting
- API integration tests

**Milestone:** Full REST API available for external integration

### Phase 4: HotSwap Integration (1 week)
**Epics:** 5
**Deliverables:**
- Graph module descriptor
- Schema migration via HotSwap
- Query algorithm hot-swap
- Partition management
- Integration with existing HotSwap deployment strategies

**Milestone:** Can deploy schema changes and algorithm updates with zero downtime

### Phase 5: Optimization & Polish (1 week)
**Epics:** 6, 7, 8
**Deliverables:**
- Comprehensive test suite (100+ tests)
- Query result caching
- Database optimization
- Batch processing
- Complete documentation
- Example applications

**Milestone:** Production-ready knowledge graph system

---

## Technology Stack

### Core Dependencies (Reuse from HotSwap)
- **.NET 8.0** - Runtime and framework
- **ASP.NET Core 8.0** - REST API
- **Entity Framework Core 8.0** - ORM for PostgreSQL
- **OpenTelemetry 1.9.0** - Distributed tracing
- **Serilog.AspNetCore 8.0.0** - Structured logging

### New Dependencies
- **Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0** - PostgreSQL provider
- **Npgsql.Json.NET 8.0.0** - JSONB support
- **StackExchange.Redis 2.7.10** - Already available (query caching)
- **Microsoft.Extensions.Caching.Memory 8.0.0** - In-memory caching

### Testing Dependencies (Existing)
- **xUnit 2.6.2** - Testing framework
- **Moq 4.20.70** - Mocking
- **FluentAssertions 6.12.0** - Assertions
- **Testcontainers 3.5.0** (NEW) - Integration tests with Docker

### Development Tools
- **EF Core CLI Tools** - Migrations
- **pgAdmin** - PostgreSQL management
- **GraphQL Playground** (future) - Query testing

---

## Project Structure

```
Claude-code-test/
├── src/
│   ├── HotSwap.KnowledgeGraph.Domain/          # NEW
│   │   ├── Models/
│   │   │   ├── Entity.cs
│   │   │   ├── Relationship.cs
│   │   │   ├── GraphSchema.cs
│   │   │   ├── GraphQuery.cs
│   │   │   └── QueryResult.cs
│   │   ├── Enums/
│   │   │   ├── Direction.cs
│   │   │   └── IndexType.cs
│   │   └── Validation/
│   │       ├── EntityValidator.cs
│   │       └── SchemaValidator.cs
│   │
│   ├── HotSwap.KnowledgeGraph.Infrastructure/  # NEW
│   │   ├── Data/
│   │   │   ├── Entities/
│   │   │   │   ├── EntityRecord.cs
│   │   │   │   ├── RelationshipRecord.cs
│   │   │   │   └── SchemaVersionRecord.cs
│   │   │   ├── GraphDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Repositories/
│   │   │   ├── IGraphRepository.cs
│   │   │   └── PostgreSqlGraphRepository.cs
│   │   ├── Caching/
│   │   │   ├── IQueryCacheService.cs
│   │   │   └── RedisQueryCacheService.cs
│   │   └── Indexing/
│   │       ├── IGraphIndexService.cs
│   │       └── PostgreSqlIndexService.cs
│   │
│   ├── HotSwap.KnowledgeGraph.QueryEngine/     # NEW
│   │   ├── PatternMatching/
│   │   │   ├── IPatternMatcher.cs
│   │   │   └── PatternMatcher.cs
│   │   ├── Traversal/
│   │   │   ├── IGraphTraversal.cs
│   │   │   ├── BreadthFirstSearch.cs
│   │   │   ├── DepthFirstSearch.cs
│   │   │   └── ShortestPath.cs
│   │   ├── Optimization/
│   │   │   ├── IQueryOptimizer.cs
│   │   │   └── CostBasedOptimizer.cs
│   │   └── Services/
│   │       ├── IGraphQueryService.cs
│   │       └── GraphQueryService.cs
│   │
│   ├── HotSwap.KnowledgeGraph.Api/             # NEW
│   │   ├── Controllers/
│   │   │   ├── EntitiesController.cs
│   │   │   ├── RelationshipsController.cs
│   │   │   ├── QueriesController.cs
│   │   │   ├── SchemaController.cs
│   │   │   └── VisualizationController.cs
│   │   ├── Models/
│   │   │   ├── CreateEntityRequest.cs
│   │   │   ├── CreateRelationshipRequest.cs
│   │   │   ├── QueryRequest.cs
│   │   │   └── ApiResponses.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── HotSwap.KnowledgeGraph.Integration/     # NEW (HotSwap bridge)
│       ├── GraphModuleDescriptor.cs
│       ├── SchemaMigrationStrategy.cs
│       ├── QueryAlgorithmModule.cs
│       └── GraphPartitionManager.cs
│
├── tests/
│   ├── HotSwap.KnowledgeGraph.Tests/           # NEW
│   │   ├── Domain/
│   │   │   ├── EntityTests.cs
│   │   │   ├── RelationshipTests.cs
│   │   │   └── SchemaTests.cs
│   │   ├── Infrastructure/
│   │   │   ├── GraphRepositoryTests.cs
│   │   │   └── IndexServiceTests.cs
│   │   ├── QueryEngine/
│   │   │   ├── PatternMatchingTests.cs
│   │   │   ├── TraversalTests.cs
│   │   │   └── OptimizerTests.cs
│   │   └── Integration/
│   │       ├── ApiIntegrationTests.cs
│   │       └── HotSwapIntegrationTests.cs
│   │
├── examples/
│   └── KnowledgeGraphExamples/                 # NEW
│       ├── DocumentKnowledgeBase/
│       ├── OrganizationHierarchy/
│       └── DependencyGraph/
│
├── docs/
│   ├── KNOWLEDGE_GRAPH_GUIDE.md                # NEW
│   ├── KNOWLEDGE_GRAPH_API.md                  # NEW
│   ├── SCHEMA_DESIGN_BEST_PRACTICES.md         # NEW
│   └── QUERY_PATTERNS.md                       # NEW
│
└── KNOWLEDGE_GRAPH_DESIGN.md                   # This file
```

---

## Risk Assessment & Mitigation

### Technical Risks

#### 1. Query Performance at Scale
**Risk:** Complex graph queries may exceed latency targets with large datasets
**Likelihood:** Medium
**Impact:** High
**Mitigation:**
- Implement aggressive query result caching (Redis)
- Add query depth limits (max depth = 5)
- Use query timeout enforcement (30s max)
- Optimize database indexes continuously
- Consider read replicas for heavy query load

#### 2. Schema Migration Complexity
**Risk:** Backward-incompatible schema changes may cause issues during canary deployment
**Likelihood:** Medium
**Impact:** Medium
**Mitigation:**
- Enforce backward compatibility checks
- Use dual-write strategy during migration
- Implement schema versioning
- Require approval workflow for schema changes
- Comprehensive migration testing in QA

#### 3. JSONB Query Performance
**Risk:** PostgreSQL JSONB queries may not scale well
**Likelihood:** Low
**Impact:** Medium
**Mitigation:**
- Use GIN indexes on JSONB columns
- Benchmark with realistic data volumes (10M+ entities)
- Consider denormalizing hot properties to columns
- Implement query optimizer with cost-based decisions
- Monitor slow query log

#### 4. Cross-Partition Query Complexity
**Risk:** Queries spanning multiple partitions may be slow
**Likelihood:** Medium
**Impact:** Medium
**Mitigation:**
- Design partition strategy to minimize cross-partition queries
- Implement partition-aware query routing
- Use caching for frequently accessed cross-partition data
- Consider entity co-location for related data

### Project Risks

#### 1. Scope Creep
**Risk:** Feature requests may delay core functionality
**Likelihood:** High
**Impact:** Medium
**Mitigation:**
- Strict adherence to epic/story breakdown
- Phase-based delivery (MVP in Phase 1-3)
- Defer advanced features to Phase 6+ (GraphQL, ML, etc.)
- Regular scope reviews

#### 2. Integration Complexity with HotSwap
**Risk:** Deep integration with HotSwap may introduce unexpected issues
**Likelihood:** Medium
**Impact:** High
**Mitigation:**
- Start with simple module deployment (Phase 4)
- Extensive integration testing
- Leverage existing HotSwap deployment strategies
- Collaborate with HotSwap core team (review CLAUDE.md)

---

## Success Criteria

### MVP (Minimum Viable Product) - End of Phase 3
- [ ] Can create, read, update, delete entities and relationships
- [ ] Can execute pattern matching queries with filters
- [ ] Can perform graph traversal (BFS, DFS, shortest path)
- [ ] REST API with 15+ endpoints fully documented
- [ ] 80+ unit tests with 85%+ coverage
- [ ] PostgreSQL storage with proper indexing
- [ ] Sub-100ms query latency for simple queries
- [ ] Complete API documentation with examples

### Production-Ready - End of Phase 5
- [ ] All MVP criteria met
- [ ] HotSwap integration for schema migration and algorithm updates
- [ ] 100+ comprehensive tests (unit + integration)
- [ ] Query result caching with >80% hit rate
- [ ] Performance benchmarks documented
- [ ] Comprehensive developer guide
- [ ] 3+ example applications
- [ ] Zero-downtime schema migration demonstrated
- [ ] 10M+ entities supported with acceptable performance

### Enterprise-Grade - Future Phases
- [ ] Multi-tenancy support
- [ ] GraphQL API layer
- [ ] Real-time graph streaming (SignalR/WebSocket)
- [ ] ML-based query optimization
- [ ] Graph visualization UI
- [ ] Advanced analytics (PageRank, community detection)

---

## Appendix A: Example Use Cases

### Use Case 1: Document Knowledge Base

**Scenario:** Build a knowledge base of documents with relationships

**Entities:**
- Document (title, content, category, tags)
- Author (name, email, department)
- Tag (name, category)
- Department (name, location)

**Relationships:**
- AUTHORED_BY (Document → Author)
- TAGGED_WITH (Document → Tag)
- BELONGS_TO (Author → Department)
- RELATED_TO (Document → Document, weight)
- CITES (Document → Document)

**Sample Queries:**
```
1. Find all documents authored by "Alice" in "Engineering" department
2. Find documents related to "API Design" with depth ≤ 2
3. Find shortest path from Document A to Document B via citations
4. Find all documents tagged with "security" and authored in last 30 days
```

### Use Case 2: Organization Hierarchy

**Scenario:** Model organizational structure with reporting relationships

**Entities:**
- Employee (name, email, title, hireDate)
- Department (name, budget)
- Project (name, status, deadline)
- Skill (name, category)

**Relationships:**
- REPORTS_TO (Employee → Employee)
- WORKS_IN (Employee → Department)
- MANAGES (Employee → Department)
- ASSIGNED_TO (Employee → Project)
- HAS_SKILL (Employee → Skill)

**Sample Queries:**
```
1. Find all direct reports for Manager X
2. Find entire reporting chain from Employee Y to CEO
3. Find employees with "Python" skill in "Engineering" department
4. Find all projects assigned to employees in Department Z
```

### Use Case 3: Software Dependency Graph

**Scenario:** Track dependencies between software packages

**Entities:**
- Package (name, version, language)
- Vulnerability (cveId, severity, description)
- License (name, type, permissive)
- Repository (url, owner)

**Relationships:**
- DEPENDS_ON (Package → Package, version)
- HAS_VULNERABILITY (Package → Vulnerability)
- LICENSED_UNDER (Package → License)
- HOSTED_AT (Package → Repository)
- TRANSITIVE_DEP (Package → Package, depth)

**Sample Queries:**
```
1. Find all packages that depend on "log4j" (direct and transitive)
2. Find packages with high-severity vulnerabilities
3. Find shortest dependency path from Package A to Package B
4. Find all GPL-licensed dependencies in the graph
```

---

## Appendix B: Sample API Requests

### Create Entity
```bash
POST /api/v1/graph/entities
Content-Type: application/json
Authorization: Bearer {jwt_token}

{
  "type": "Document",
  "properties": {
    "title": "API Design Best Practices",
    "content": "Comprehensive guide to REST API design...",
    "category": "Engineering",
    "tags": ["api", "rest", "design"],
    "publishedAt": "2025-11-16T00:00:00Z"
  }
}

Response: 201 Created
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "Document",
  "properties": { ... },
  "createdAt": "2025-11-16T10:30:00Z",
  "updatedAt": "2025-11-16T10:30:00Z",
  "createdBy": "user@example.com",
  "version": 1
}
```

### Create Relationship
```bash
POST /api/v1/graph/relationships
Content-Type: application/json
Authorization: Bearer {jwt_token}

{
  "type": "AUTHORED_BY",
  "sourceEntityId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetEntityId": "7bc91c28-1234-5678-b3fc-9d8e7f66afa6",
  "properties": {
    "role": "primary",
    "contributionPercent": 100
  },
  "weight": 1.0
}

Response: 201 Created
{
  "id": "9ab12c34-5678-9012-b3fc-3d4e5f67afa6",
  "type": "AUTHORED_BY",
  "sourceEntityId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetEntityId": "7bc91c28-1234-5678-b3fc-9d8e7f66afa6",
  "properties": { ... },
  "weight": 1.0,
  "createdAt": "2025-11-16T10:35:00Z"
}
```

### Execute Pattern Matching Query
```bash
POST /api/v1/graph/queries/match
Content-Type: application/json
Authorization: Bearer {jwt_token}

{
  "entityType": "Document",
  "propertyFilters": {
    "category": "Engineering",
    "tags": { "$contains": "api" }
  },
  "relationshipPatterns": [
    {
      "relationshipType": "AUTHORED_BY",
      "direction": "Outgoing",
      "targetEntityType": "Author",
      "propertyFilters": {
        "department": "Engineering"
      }
    }
  ],
  "maxDepth": 2,
  "pageSize": 50
}

Response: 200 OK
{
  "entities": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "type": "Document",
      "properties": { ... }
    },
    ...
  ],
  "relationships": [
    {
      "id": "9ab12c34-5678-9012-b3fc-3d4e5f67afa6",
      "type": "AUTHORED_BY",
      ...
    }
  ],
  "totalCount": 12,
  "executionTime": "00:00:00.085",
  "queryPlan": "IndexScan(entities.type='Document') -> Filter(category='Engineering') -> Join(relationships.AUTHORED_BY)"
}
```

### Find Shortest Path
```bash
POST /api/v1/graph/queries/shortest-path
Content-Type: application/json
Authorization: Bearer {jwt_token}

{
  "sourceEntityId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetEntityId": "8cd34e56-9012-3456-c4de-5e6f7a89bcd1",
  "maxDepth": 5,
  "relationshipTypes": ["CITES", "RELATED_TO"]
}

Response: 200 OK
{
  "path": {
    "entities": [
      { "id": "3fa85f64-...", "type": "Document", ... },
      { "id": "7ab12c34-...", "type": "Document", ... },
      { "id": "8cd34e56-...", "type": "Document", ... }
    ],
    "relationships": [
      { "id": "...", "type": "CITES", ... },
      { "id": "...", "type": "RELATED_TO", ... }
    ],
    "totalWeight": 2.5,
    "hops": 2
  },
  "executionTime": "00:00:00.342"
}
```

---

## Appendix C: Testing Strategy

### Unit Testing (Target: 100+ tests, 90%+ coverage)

**Domain Models (20 tests):**
- Entity validation (required fields, type validation)
- Relationship validation (source/target IDs, directionality)
- Schema validation (entity types, relationship types, constraints)
- Query model validation (filters, patterns, pagination)

**Repository Layer (25 tests):**
- CRUD operations for entities
- CRUD operations for relationships
- Query execution
- Transaction handling
- Concurrent access scenarios
- Error handling

**Query Engine (30 tests):**
- Pattern matching accuracy
- Graph traversal algorithms (BFS, DFS, shortest path)
- Query optimization logic
- Cache invalidation
- Edge cases (cycles, disconnected graphs, large depths)

**API Layer (25 tests):**
- Request validation
- Authentication and authorization
- Error responses (400, 401, 403, 404, 500)
- Rate limiting
- Pagination

### Integration Testing (Target: 30+ tests)

**Database Integration (10 tests):**
- Real PostgreSQL with Testcontainers
- Migration execution
- JSONB query performance
- Index effectiveness
- Connection pooling

**API Integration (15 tests):**
- End-to-end workflows (create entity → create relationship → query)
- Multi-user scenarios
- Concurrent requests
- Large payload handling
- API versioning

**HotSwap Integration (5 tests):**
- Schema migration via canary deployment
- Query algorithm hot-swap
- Rollback scenarios
- Zero-downtime validation

### Performance Testing

**Load Tests (k6 scripts):**
- Sustained load: 100 req/s for 10 minutes
- Spike test: 0 → 500 req/s
- Soak test: 50 req/s for 1 hour
- Query latency percentiles (p50, p95, p99)

**Benchmark Tests:**
- Entity creation: 1000 entities in <5s
- Pattern match (simple): <100ms
- Pattern match (2-hop): <500ms
- Graph traversal (depth 3): <1s
- Shortest path (depth 5): <2s

### End-to-End Testing

**Smoke Tests (6 tests):**
- Health check endpoint
- Create and retrieve entity
- Create and retrieve relationship
- Execute simple query
- Schema validation
- Authentication flow

---

## Changelog

### 2025-11-16 (Initial Design)
- Created comprehensive knowledge graph system design
- Defined 8 epics with detailed stories
- Specified data models, API endpoints, database schema
- Outlined 5-phase implementation roadmap (7.5 weeks)
- Defined performance and scalability targets
- Included 3 example use cases
- Added risk assessment and mitigation strategies
- Specified testing strategy with 130+ planned tests

---

**Status:** Ready for Review and Implementation Planning
**Next Steps:**
1. Review design with stakeholders
2. Refine epic priorities based on business needs
3. Create JIRA/GitHub issues for all stories
4. Begin Phase 1 implementation (Epic 1 & 2)
