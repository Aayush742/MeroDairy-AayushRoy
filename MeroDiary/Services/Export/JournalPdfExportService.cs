using MeroDiary.Data.Repositories;
using MeroDiary.Domain.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace MeroDiary.Services.Export;

public sealed class JournalPdfExportService : IJournalPdfExportService
{
	private readonly IJournalEntryRepository _entries;
	private readonly ICategoryRepository _categories;
	private readonly IMoodRepository _moods;
	private readonly IJournalEntryMoodRepository _entryMoods;
	private readonly ITagRepository _tags;
	private readonly IJournalEntryTagRepository _entryTags;

	public JournalPdfExportService(
		IJournalEntryRepository entries,
		ICategoryRepository categories,
		IMoodRepository moods,
		IJournalEntryMoodRepository entryMoods,
		ITagRepository tags,
		IJournalEntryTagRepository entryTags)
	{
		_entries = entries;
		_categories = categories;
		_moods = moods;
		_entryMoods = entryMoods;
		_tags = tags;
		_entryTags = entryTags;
	}

	public async Task<PdfExportResult> ExportAsync(DateOnly startInclusive, DateOnly endInclusive, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (startInclusive > endInclusive)
			(startInclusive, endInclusive) = (endInclusive, startInclusive);

		var entries = await _entries.GetEntriesInRangeAsync(startInclusive, endInclusive, cancellationToken).ConfigureAwait(false);
		if (entries.Count == 0)
		{
			return new PdfExportResult
			{
				Success = false,
				EntryCount = 0,
				FilePath = null,
				Message = "No entries found in the selected date range.",
			};
		}

		var entryIds = entries.Select(e => e.Id).ToList();

		var categoryIds = entries.Select(e => e.CategoryId).Distinct().ToList();
		var categories = await _categories.GetByIdsAsync(categoryIds, cancellationToken).ConfigureAwait(false);
		var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);

		var moodMap = (await _moods.GetAllAsync(cancellationToken).ConfigureAwait(false))
			.ToDictionary(m => m.Id, m => $"{m.Category}: {m.Name}");

		var moodSelections = await _entryMoods.GetSelectionsByEntryAsync(entryIds, cancellationToken).ConfigureAwait(false);
		var tagIdsByEntry = await _entryTags.GetTagIdsByEntryAsync(entryIds, cancellationToken).ConfigureAwait(false);
		var allTagIds = tagIdsByEntry.Values.SelectMany(x => x).Distinct().ToList();
		var tags = await _tags.GetByIdsAsync(allTagIds, cancellationToken).ConfigureAwait(false);
		var tagMap = tags.ToDictionary(t => t.Id, t => t.Name);

		var exportDir = Path.Combine(FileSystem.AppDataDirectory, "Exports");
		Directory.CreateDirectory(exportDir);

		var fileName = $"Journal_{startInclusive:yyyyMMdd}_{endInclusive:yyyyMMdd}.pdf";
		var filePath = Path.Combine(exportDir, fileName);

		QuestPDF.Settings.License = LicenseType.Community;

		var document = Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(QuestPDF.Helpers.PageSizes.A4);
				page.Margin(30);
				page.DefaultTextStyle(x => x.FontSize(11));

				page.Header().Column(h =>
				{
					h.Item().Text(t =>
					{
						t.DefaultTextStyle(s => s.FontSize(18).SemiBold());
						t.Span("Journal Export");
					});
					h.Item().Text(t =>
					{
						t.DefaultTextStyle(s => s.FontColor(QuestPDF.Helpers.Colors.Grey.Darken1));
						t.Span($"{startInclusive:yyyy-MM-dd} → {endInclusive:yyyy-MM-dd}");
					});
					h.Item().LineHorizontal(1);
				});

				page.Content().Column(col =>
				{
					col.Spacing(16);

					foreach (var e in entries)
					{
						col.Item().Element(entry =>
						{
							entry.Border(1);
							entry.BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
							entry.Padding(10);
							entry.Column(ec =>
							{
								ec.Spacing(8);

								ec.Item().Row(r =>
								{
									r.RelativeItem().Text(t =>
									{
										t.DefaultTextStyle(s => s.SemiBold());
										t.Span($"{e.EntryDate:yyyy-MM-dd} — {e.Title}");
									});
									var catName = categoryMap.TryGetValue(e.CategoryId, out var cn) ? cn : "Unknown";
									r.ConstantItem(180).AlignRight().Text(t =>
									{
										t.DefaultTextStyle(s => s.FontColor(QuestPDF.Helpers.Colors.Grey.Darken1));
										t.Span(catName);
									});
								});

								// Moods + tags
								ec.Item().Text(t =>
								{
									t.DefaultTextStyle(s => s.FontColor(QuestPDF.Helpers.Colors.Grey.Darken1));
									if (moodSelections.TryGetValue(e.Id, out var sel))
									{
										if (moodMap.TryGetValue(sel.PrimaryMoodId, out var primary))
											t.Span($"Mood: {primary}");
										else
											t.Span("Mood: Unknown");

										var secondary = sel.SecondaryMoodIds
											.Where(id => id != Guid.Empty)
											.Select(id => moodMap.TryGetValue(id, out var mn) ? mn : "Unknown")
											.ToList();

										if (secondary.Count > 0)
											t.Span($" (Secondary: {string.Join(", ", secondary)})");
									}
									else
									{
										t.Span("Mood: Unknown");
									}
								});

								var tagNames = tagIdsByEntry.TryGetValue(e.Id, out var ids)
									? ids.Select(id => tagMap.TryGetValue(id, out var n) ? n : "Unknown").OrderBy(x => x).ToList()
									: new List<string>();

								if (tagNames.Count > 0)
								{
									ec.Item().Text(t =>
									{
										t.DefaultTextStyle(s => s.FontColor(QuestPDF.Helpers.Colors.Grey.Darken1));
										t.Span($"Tags: {string.Join(", ", tagNames)}");
									});
								}

								ec.Item().LineHorizontal(1);

								// Markdown content
								ec.Item().Element(c => MarkdownToPdfRenderer.Render(c, e.Content));
							});
						});
					}
				});

				page.Footer().AlignCenter().Text(x =>
				{
					x.DefaultTextStyle(s => s.FontSize(9));
					x.Span("Generated ");
					x.Span(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm")).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
				});
			});
		});

		document.GeneratePdf(filePath);

		return new PdfExportResult
		{
			Success = true,
			EntryCount = entries.Count,
			FilePath = filePath,
			Message = null,
		};
	}
}


