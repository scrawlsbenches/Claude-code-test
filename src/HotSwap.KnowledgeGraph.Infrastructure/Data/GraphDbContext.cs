using System.Text.Json;
using HotSwap.KnowledgeGraph.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HotSwap.KnowledgeGraph.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the knowledge graph.
/// Maps domain models to PostgreSQL tables with JSONB support.
/// </summary>
public class GraphDbContext : DbContext
{
    /// <summary>
    /// Entities in the knowledge graph (nodes).
    /// </summary>
    public DbSet<Entity> Entities => Set<Entity>();

    /// <summary>
    /// Relationships in the knowledge graph (edges).
    /// </summary>
    public DbSet<Relationship> Relationships => Set<Relationship>();

    /// <summary>
    /// Graph schemas with versioning.
    /// </summary>
    public DbSet<GraphSchema> Schemas => Set<GraphSchema>();

    public GraphDbContext(DbContextOptions<GraphDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Check if we're using PostgreSQL
        var isPostgres = Database.IsNpgsql();

        // Value converter for Dictionary<string, object> when not using PostgreSQL
        var dictionaryConverter = new ValueConverter<Dictionary<string, object>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

        var dictionaryComparer = new ValueComparer<Dictionary<string, object>>(
            (d1, d2) => JsonSerializer.Serialize(d1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(d2, (JsonSerializerOptions?)null),
            d => d == null ? 0 : JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
            d => JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(d, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        // Value converter for Dictionary<string, EntityTypeDefinition>
        var entityTypesConverter = new ValueConverter<Dictionary<string, EntityTypeDefinition>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, EntityTypeDefinition>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, EntityTypeDefinition>());

        var entityTypesComparer = new ValueComparer<Dictionary<string, EntityTypeDefinition>>(
            (d1, d2) => JsonSerializer.Serialize(d1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(d2, (JsonSerializerOptions?)null),
            d => d == null ? 0 : JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
            d => JsonSerializer.Deserialize<Dictionary<string, EntityTypeDefinition>>(JsonSerializer.Serialize(d, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        // Value converter for Dictionary<string, RelationshipTypeDefinition>
        var relationshipTypesConverter = new ValueConverter<Dictionary<string, RelationshipTypeDefinition>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, RelationshipTypeDefinition>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, RelationshipTypeDefinition>());

        var relationshipTypesComparer = new ValueComparer<Dictionary<string, RelationshipTypeDefinition>>(
            (d1, d2) => JsonSerializer.Serialize(d1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(d2, (JsonSerializerOptions?)null),
            d => d == null ? 0 : JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
            d => JsonSerializer.Deserialize<Dictionary<string, RelationshipTypeDefinition>>(JsonSerializer.Serialize(d, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        // Entity configuration
        modelBuilder.Entity<Entity>(entity =>
        {
            entity.ToTable("entities");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasMaxLength(100)
                .IsRequired();

            var propertiesProp = entity.Property(e => e.Properties)
                .HasColumnName("properties")
                .IsRequired();

            if (isPostgres)
            {
                propertiesProp.HasColumnType("jsonb");
            }
            else
            {
                propertiesProp.HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
            }

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by")
                .HasMaxLength(255);

            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by")
                .HasMaxLength(255);

            entity.Property(e => e.Version)
                .HasColumnName("version")
                .HasDefaultValue(1)
                .IsConcurrencyToken();

            // Indexes (only for PostgreSQL)
            if (isPostgres)
            {
                entity.HasIndex(e => e.Type).HasDatabaseName("ix_entities_type");
                entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_entities_created_at");
                entity.HasIndex(e => e.Properties)
                    .HasDatabaseName("ix_entities_properties")
                    .HasMethod("gin");
            }
        });

        // Relationship configuration
        modelBuilder.Entity<Relationship>(relationship =>
        {
            relationship.ToTable("relationships");
            relationship.HasKey(r => r.Id);

            relationship.Property(r => r.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            relationship.Property(r => r.Type)
                .HasColumnName("type")
                .HasMaxLength(100)
                .IsRequired();

            relationship.Property(r => r.SourceEntityId)
                .HasColumnName("source_entity_id")
                .IsRequired();

            relationship.Property(r => r.TargetEntityId)
                .HasColumnName("target_entity_id")
                .IsRequired();

            var propertiesProp = relationship.Property(r => r.Properties)
                .HasColumnName("properties")
                .IsRequired();

            if (isPostgres)
            {
                propertiesProp.HasColumnType("jsonb");
            }
            else
            {
                propertiesProp.HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
            }

            relationship.Property(r => r.Weight)
                .HasColumnName("weight")
                .HasDefaultValue(1.0);

            relationship.Property(r => r.IsDirected)
                .HasColumnName("is_directed")
                .HasDefaultValue(true);

            relationship.Property(r => r.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            relationship.Property(r => r.CreatedBy)
                .HasColumnName("created_by")
                .HasMaxLength(255);

            // Indexes (only for PostgreSQL)
            if (isPostgres)
            {
                relationship.HasIndex(r => r.Type).HasDatabaseName("ix_relationships_type");
                relationship.HasIndex(r => r.SourceEntityId).HasDatabaseName("ix_relationships_source");
                relationship.HasIndex(r => r.TargetEntityId).HasDatabaseName("ix_relationships_target");
                relationship.HasIndex(r => new { r.SourceEntityId, r.Type }).HasDatabaseName("ix_relationships_source_type");
                relationship.HasIndex(r => new { r.TargetEntityId, r.Type }).HasDatabaseName("ix_relationships_target_type");
                relationship.HasIndex(r => r.Properties)
                    .HasDatabaseName("ix_relationships_properties")
                    .HasMethod("gin");
            }
        });

        // GraphSchema configuration
        modelBuilder.Entity<GraphSchema>(schema =>
        {
            schema.ToTable("schemas");
            schema.HasKey(s => s.Version);

            schema.Property(s => s.Version)
                .HasColumnName("version")
                .HasMaxLength(50)
                .IsRequired();

            var entityTypesProp = schema.Property(s => s.EntityTypes)
                .HasColumnName("entity_types")
                .IsRequired();

            var relationshipTypesProp = schema.Property(s => s.RelationshipTypes)
                .HasColumnName("relationship_types")
                .IsRequired();

            if (isPostgres)
            {
                entityTypesProp.HasColumnType("jsonb");
                relationshipTypesProp.HasColumnType("jsonb");
            }
            else
            {
                entityTypesProp.HasConversion(entityTypesConverter)
                    .Metadata.SetValueComparer(entityTypesComparer);
                relationshipTypesProp.HasConversion(relationshipTypesConverter)
                    .Metadata.SetValueComparer(relationshipTypesComparer);
            }

            schema.Property(s => s.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            schema.Property(s => s.CreatedBy)
                .HasColumnName("created_by")
                .HasMaxLength(255);

            // Indexes (only for PostgreSQL)
            if (isPostgres)
            {
                schema.HasIndex(s => s.CreatedAt).HasDatabaseName("ix_schemas_created_at");
            }
        });
    }
}
