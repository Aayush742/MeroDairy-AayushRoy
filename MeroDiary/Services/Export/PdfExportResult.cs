namespace MeroDiary.Services.Export;

public sealed class PdfExportResult
{
	public required bool Success { get; init; }
	public required int EntryCount { get; init; }
	public required string? FilePath { get; init; }
	public required string? Message { get; init; }
}


