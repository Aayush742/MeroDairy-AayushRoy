using MeroDiary.Services.Export;

namespace MeroDiary.Services.Export;

public interface IJournalPdfExportService
{
	Task<PdfExportResult> ExportAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default);
}


