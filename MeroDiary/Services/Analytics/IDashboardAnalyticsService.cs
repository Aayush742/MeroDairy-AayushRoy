using MeroDiary.Domain.Models.Analytics;

namespace MeroDiary.Services.Analytics;

public interface IDashboardAnalyticsService
{
	Task<DashboardAnalyticsReport> GetReportAsync(DateOnly? rangeStart = null, DateOnly? rangeEnd = null, int topTags = 10, CancellationToken cancellationToken = default);
}


