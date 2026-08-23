using ErrorOr;

namespace LedgerLite.Application.Abstractions;

/// <summary>Handles a command (write-side use case) and returns an <see cref="ErrorOr{T}"/> result.</summary>
public interface ICommandHandler<in TCommand, TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Handles a query (read-side use case) and returns an <see cref="ErrorOr{T}"/> result.</summary>
public interface IQueryHandler<in TQuery, TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TQuery query, CancellationToken cancellationToken = default);
}
