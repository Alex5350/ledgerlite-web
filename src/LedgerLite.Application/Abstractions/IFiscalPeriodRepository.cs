using LedgerLite.Domain.FiscalPeriods;

namespace LedgerLite.Application.Abstractions;

public interface IFiscalPeriodRepository
{
    Task<FiscalPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FiscalPeriod>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(FiscalPeriod period, CancellationToken cancellationToken = default);
}
