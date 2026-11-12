namespace TrailHub.Trips.Domain.Expenses;

public enum SplitMode
{
    /// <summary>The amount is divided evenly among the chosen people; leftover cents go one each in user-id order.</summary>
    Equal,

    /// <summary>Each person's amount is given (in the expense's own currency) and must add up to the total.</summary>
    Custom,
}
