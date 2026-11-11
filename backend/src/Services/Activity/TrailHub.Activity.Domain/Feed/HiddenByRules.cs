namespace TrailHub.Activity.Domain.Feed;

internal static class HiddenByRules
{
    /// <summary>The state after hiding with <paramref name="by"/>: reports don't override a moderator's decision.</summary>
    public static HiddenBy Hide(HiddenBy? current, HiddenBy by) =>
        current == HiddenBy.Moderator ? HiddenBy.Moderator : by;
}
