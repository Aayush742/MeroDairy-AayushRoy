using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public interface IMoodRepository
{
	Task<IReadOnlyList<Mood>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}


