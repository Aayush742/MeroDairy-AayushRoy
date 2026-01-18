namespace MeroDiary.Services.Export;

/// <summary>
/// Fallback export service for platforms where no PDF backend is available.
/// </summary>
public sealed class JournalPdfExportNotSupportedService : IJournalPdfExportService
{
	public Task<PdfExportResult> ExportAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new PdfExportResult
		{
			Success = false,
			EntryCount = 0,
			FilePath = null,
			Message = "PDF export is not supported on this platform.",
		});
	}
}


