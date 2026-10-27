namespace TrailHub.BuildingBlocks.Domain;

/// <summary>Base type for objects with identity. Equality is by type and id.</summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected init; } = default!;

    public bool Equals(Entity<TId>? other) =>
        other is not null && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}

/// <summary>Marks an entity as the consistency boundary that repositories load and save.</summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull;
