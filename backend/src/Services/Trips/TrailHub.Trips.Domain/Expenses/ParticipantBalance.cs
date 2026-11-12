namespace TrailHub.Trips.Domain.Expenses;

/// <summary>Where one person stands, in the trip currency. <see cref="Net"/> above zero means others owe them.</summary>
public sealed record ParticipantBalance(string UserId, decimal Paid, decimal Share, decimal Sent, decimal Received)
{
    public decimal Net => Paid - Share + Sent - Received;
}
