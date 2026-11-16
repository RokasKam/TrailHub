namespace TrailHub.Contracts;

/// <summary>A settle-up payment from one trip participant to another was recorded (amount in the trip currency).</summary>
public sealed record TripPaymentRecordedIntegrationEvent(
    Guid TripId,
    string TripName,
    Guid PaymentId,
    string FromId,
    string FromName,
    string ToId,
    string ToName,
    decimal Amount,
    string Currency,
    string RecordedById);
