namespace TrailHub.Trips.Application.Responses;

/// <param name="Amount">In the trip currency.</param>
/// <param name="CanDelete">Whether the caller may delete it.</param>
public sealed record PaymentResponse(
    Guid Id,
    PersonResponse From,
    PersonResponse To,
    decimal Amount,
    DateOnly Date,
    string RecordedBy,
    DateTime CreatedAtUtc,
    bool CanDelete);
