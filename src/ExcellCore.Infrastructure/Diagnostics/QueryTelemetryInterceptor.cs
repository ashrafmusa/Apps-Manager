using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Infrastructure.Diagnostics;

internal sealed class QueryTelemetryInterceptor : DbCommandInterceptor
{
    private static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(500);
    private static readonly AsyncLocal<bool> SuppressLogging = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueryTelemetryInterceptor> _logger;

    public QueryTelemetryInterceptor(IServiceProvider serviceProvider, ILogger<QueryTelemetryInterceptor> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        _ = TryLogAsync(command, eventData, CancellationToken.None);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await TryLogAsync(command, eventData, cancellationToken).ConfigureAwait(false);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken).ConfigureAwait(false);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader? result)
    {
        _ = TryLogAsync(command, eventData, CancellationToken.None);
        return base.ReaderExecuted(command, eventData, result ?? throw new InvalidOperationException("Command result cannot be null."));
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader? result, CancellationToken cancellationToken = default)
    {
        await TryLogAsync(command, eventData, cancellationToken).ConfigureAwait(false);
        return await base.ReaderExecutedAsync(command, eventData, result ?? throw new InvalidOperationException("Command result cannot be null."), cancellationToken).ConfigureAwait(false);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        _ = TryLogAsync(command, eventData, CancellationToken.None);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        await TryLogAsync(command, eventData, cancellationToken).ConfigureAwait(false);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken).ConfigureAwait(false);
    }
    private async Task TryLogAsync(DbCommand command, CommandExecutedEventData eventData, CancellationToken cancellationToken)
    {
        if (SuppressLogging.Value)
        {
            return;
        }

        if (eventData.Duration < Threshold)
        {
            return;
        }

        try
        {
            SuppressLogging.Value = true;
            var contextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<ExcellCoreContext>>();
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var telemetry = new TelemetryEvent
            {
                EventType = "Query",
                CommandText = Truncate(command.CommandText, 1024),
                DurationMilliseconds = eventData.Duration.TotalMilliseconds,
                OccurredOnUtc = DateTime.UtcNow,
                Audit = new AuditTrail
                {
                    CreatedBy = "telemetry",
                    SourceModule = "Infrastructure"
                }
            };

            dbContext.TelemetryEvents.Add(telemetry);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to persist telemetry event for slow query.");
        }
        finally
        {
            SuppressLogging.Value = false;
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength);
    }
}
