using MeroDiary.Domain.Models;

namespace MeroDiary.Data.Repositories;

public interface ITagRepository
{
	Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<Tag?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
	Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Tag> GetOrCreateAsync(string name, bool isPredefined = false, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<Tag>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}


