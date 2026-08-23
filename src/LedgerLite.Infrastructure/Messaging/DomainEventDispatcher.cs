using LedgerLite.Application.Abstractions;
using LedgerLite.Domain.Common;
using LedgerLite.Domain.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace LedgerLite.Infrastructure.Messaging;

/// <summary>
/// Logs every domain event and forwards budget threshold events into a bounded channel
/// consumed by <see cref="BudgetAlertWorker"/>. When the channel is full the event is
/// dropped (DropWrite) with a warning — alerts must never block the request path.
/// </summary>
internal sealed class ChannelDomainEventDispatcher(
    ILogger<ChannelDomainEventDispatcher> logger,
    ChannelWriter<BudgetThresholdExceededDomainEvent> channelWriter) : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            logger.LogInformation(
                "Domain event {EventType} {EventId} occurred at {OccurredOnUtc:O}",
                domainEvent.GetType().Name,
                domainEvent.EventId,
                domainEvent.OccurredOnUtc);

            if (domainEvent is BudgetThresholdExceededDomainEvent budgetEvent)
            {
                if (!channelWriter.TryWrite(budgetEvent))
                {
                    logger.LogWarning(
                        "Budget alert channel is full; dropped event {EventId} for category '{Category}'",
                        budgetEvent.EventId,
                        budgetEvent.Category);
                }
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Background consumer of budget threshold events; logs a warning per alert.</summary>
internal sealed class BudgetAlertWorker(
    ChannelReader<BudgetThresholdExceededDomainEvent> channelReader,
    ILogger<BudgetAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Budget alert worker started");

        try
        {
            await foreach (var alert in channelReader.ReadAllAsync(stoppingToken))
            {
                logger.LogWarning(
                    "Budget alert: category '{Category}' in period {FiscalPeriodId} crossed {Threshold} " +
                    "of its limit — spent {SpentAmount} {SpentCurrency} of {LimitAmount} {LimitCurrency} (budget {BudgetId})",
                    alert.Category,
                    alert.FiscalPeriodId,
                    alert.Threshold,
                    alert.Spent.Amount,
                    alert.Spent.Currency,
                    alert.Limit.Amount,
                    alert.Limit.Currency,
                    alert.BudgetId);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
    }
}
