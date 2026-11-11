namespace TrailHub.BuildingBlocks.Application;

public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    public PageRequest(int page = 1, int pageSize = 20)
    {
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}
