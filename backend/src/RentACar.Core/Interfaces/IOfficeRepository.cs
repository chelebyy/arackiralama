using RentACar.Core.Entities;

namespace RentACar.Core.Interfaces;

public interface IOfficeRepository
{
    Task<IReadOnlyList<Office>> ListAsync(CancellationToken cancellationToken = default);
    Task<Office?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Office office, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
