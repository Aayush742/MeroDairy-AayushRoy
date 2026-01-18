using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public interface ICategoryRepository
{
	Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<Category>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

	Task AddAsync(Category category, CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

	// Analytics helper
	Task<IReadOnlyDictionary<Guid, int>> GetEntryCountsByCategoryAsync(CancellationToken cancellationToken = default);
}


