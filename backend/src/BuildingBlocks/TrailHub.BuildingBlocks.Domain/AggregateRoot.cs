namespace TrailHub.BuildingBlocks.Domain;

/// <summary>Marks an entity as the consistency boundary that repositories load and save.</summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull;
