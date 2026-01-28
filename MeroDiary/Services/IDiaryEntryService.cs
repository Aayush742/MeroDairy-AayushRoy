using MeroDiary.Domain.Models;

namespace MeroDiary.Services;

public interface IDiaryEntryService
{
	Task<IReadOnlyList<DiaryEntry>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<DiaryEntry> CreateAsync(string title, string content, DateTimeOffset entryDate, CancellationToken cancellationToken = default);
	Task<DiaryEntry> UpdateAsync(Guid id, string title, string content, DateTimeOffset entryDate, CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}


