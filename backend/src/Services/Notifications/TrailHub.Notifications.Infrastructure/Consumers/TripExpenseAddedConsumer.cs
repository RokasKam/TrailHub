using MassTransit;
using TrailHub.Contracts;
using TrailHub.Notifications.Application.Interfaces.Service;

namespace TrailHub.Notifications.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class TripExpenseAddedConsumer(ITripNotificationService notifications) : IConsumer<TripExpenseAddedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TripExpenseAddedIntegrationEvent> context) => notifications.ExpenseAddedAsync(context.Message, context.CancellationToken);
}
