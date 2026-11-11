using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trips.Domain.Trips;

public sealed class ChecklistItem : Entity<Guid>
{
    private ChecklistItem()
    {
    }

    internal ChecklistItem(string text, ChecklistItemKind kind, string addedBy)
    {
        Id = Guid.CreateVersion7();
        Text = text;
        Kind = kind;
        AddedBy = addedBy;
    }

    public string Text { get; private set; } = null!;

    public ChecklistItemKind Kind { get; private set; }

    public string AddedBy { get; private set; } = null!;

    /// <summary>Shared items: who is bringing it.</summary>
    public string? ClaimedBy { get; internal set; }

    /// <summary>Shared items: the claimer has packed it.</summary>
    public bool IsDone { get; internal set; }

    /// <summary>Personal items: who has packed their own.</summary>
    public List<string> DoneBy { get; private set; } = [];

    public bool IsDoneFor(string userId) => Kind == ChecklistItemKind.Personal ? DoneBy.Contains(userId) : IsDone;
}
