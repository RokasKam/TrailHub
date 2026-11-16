using TrailHub.BuildingBlocks.Application;

namespace TrailHub.UnitTests;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.FromResult(0);
    }
}
