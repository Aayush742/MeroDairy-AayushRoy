#if IOS || MACCATALYST
using System.Text;
using Foundation;
using MeroDiary.Data.Repositories;
using MeroDiary.Domain.Models;
using MeroDiary.Services.Markdown;
using WebKit;

namespace MeroDiary.Services.Export;

/// <summary>
/// Apple-platform PDF export using WKWebView (HTML -> PDF).
/// This avoids QuestPDF native backend issues on iOS/MacCatalyst.
/// </summary>
public sealed class JournalPdfExportWebKitService : IJournalPdfExportService
{
	private readonly IJournalEntryRepository _entries;
	private readonly ICategoryRepository _categories;
	private readonly IMoodRepository _moods;
	private readonly IJournalEntryMoodRepository _entryMoods;
	private readonly ITagRepository _tags;
	private readonly IJournalEntryTagRepository _entryTags;
	private readonly IMarkdownRenderer _markdown;

	public JournalPdfExportWebKitService(
		IJournalEntryRepository entries,
		ICategoryRepository categories,
		IMoodRepository moods,
		IJournalEntryMoodRepository entryMoods,
		ITagRepository tags,
		IJournalEntryTagRepository entryTags,
		IMarkdownRenderer markdown)
	{
		_entries = entries;
		_categories = categories;
		_moods = moods;
		_entryMoods = entryMoods;
		_tags = tags;
		_entryTags = entryTags;
		_markdown = markdown;
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

		var html = BuildHtml(startInclusive, endInclusive, entries, categoryMap, moodMap, moodSelections, tagIdsByEntry, tagMap);

		var exportDir = Path.Combine(FileSystem.AppDataDirectory, "Exports");
		Directory.CreateDirectory(exportDir);
		var fileName = $"Journal_{startInclusive:yyyyMMdd}_{endInclusive:yyyyMMdd}.pdf";
		var filePath = Path.Combine(exportDir, fileName);

		var pdfBytes = await GeneratePdfFromHtmlAsync(html, cancellationToken).ConfigureAwait(false);
		await File.WriteAllBytesAsync(filePath, pdfBytes, cancellationToken).ConfigureAwait(false);

		return new PdfExportResult
		{
			Success = true,
			EntryCount = entries.Count,
			FilePath = filePath,
			Message = null,
		};
	}

	private string BuildHtml(
		DateOnly startInclusive,
		DateOnly endInclusive,
		IReadOnlyList<JournalEntry> entries,
		IReadOnlyDictionary<Guid, string> categoryMap,
		IReadOnlyDictionary<Guid, string> moodMap,
		IReadOnlyDictionary<Guid, MoodSelection> moodSelections,
		IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> tagIdsByEntry,
		IReadOnlyDictionary<Guid, string> tagMap)
	{
		static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

		var sb = new StringBuilder();
		sb.AppendLine("<!doctype html>");
		sb.AppendLine("<html><head><meta charset=\"utf-8\" />");
		sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
		sb.AppendLine("<style>");
		sb.AppendLine(@"
			body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial; font-size: 12px; color: #111; }
			h1 { font-size: 20px; margin: 0 0 6px 0; }
			.sub { color: #666; margin-bottom: 14px; }
			.entry { border: 1px solid #e5e5e5; border-radius: 8px; padding: 12px; margin: 0 0 16px 0; page-break-inside: avoid; }
			.entry-title { display: flex; justify-content: space-between; gap: 12px; align-items: baseline; margin-bottom: 6px; }
			.entry-title .left { font-weight: 600; font-size: 14px; }
			.entry-title .right { color: #666; }
			.meta { color: #666; margin: 2px 0; }
			hr { border: none; border-top: 1px solid #eee; margin: 10px 0; }
			/* Markdown-ish defaults */
			p { margin: 6px 0; }
			ul, ol { margin: 6px 0 6px 18px; }
			code { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', monospace; background: #f5f5f5; padding: 1px 3px; border-radius: 3px; }
			a { color: #0a66c2; text-decoration: underline; }
			/* Page breaks between entries */
			.entry { break-inside: avoid; }
			.entry + .entry { page-break-before: auto; }
		");
		sb.AppendLine("</style>");
		sb.AppendLine("</head><body>");
		sb.AppendLine($"<h1>Journal Export</h1>");
		sb.AppendLine($"<div class=\"sub\">{startInclusive:yyyy-MM-dd} \u2192 {endInclusive:yyyy-MM-dd}</div>");

		foreach (var e in entries)
		{
			var catName = categoryMap.TryGetValue(e.CategoryId, out var cn) ? cn : "Unknown";

			var moodText = "Mood: Unknown";
			if (moodSelections.TryGetValue(e.Id, out var sel))
			{
				var primary = moodMap.TryGetValue(sel.PrimaryMoodId, out var p) ? p : "Unknown";
				var secondary = sel.SecondaryMoodIds
					.Where(id => id != Guid.Empty)
					.Select(id => moodMap.TryGetValue(id, out var m) ? m : "Unknown")
					.ToList();

				moodText = secondary.Count > 0
					? $"Mood: {primary} (Secondary: {string.Join(", ", secondary)})"
					: $"Mood: {primary}";
			}

			var tagNames = tagIdsByEntry.TryGetValue(e.Id, out var ids)
				? ids.Select(id => tagMap.TryGetValue(id, out var n) ? n : "Unknown").OrderBy(x => x).ToList()
				: new List<string>();

			sb.AppendLine("<div class=\"entry\">");
			sb.AppendLine("<div class=\"entry-title\">");
			sb.AppendLine($"<div class=\"left\">{e.EntryDate:yyyy-MM-dd} — {Esc(e.Title)}</div>");
			sb.AppendLine($"<div class=\"right\">{Esc(catName)}</div>");
			sb.AppendLine("</div>");
			sb.AppendLine($"<div class=\"meta\">{Esc(moodText)}</div>");
			if (tagNames.Count > 0)
				sb.AppendLine($"<div class=\"meta\">Tags: {Esc(string.Join(", ", tagNames))}</div>");
			sb.AppendLine("<hr />");

			var contentHtml = _markdown.RenderToHtml(e.Content ?? string.Empty);
			sb.AppendLine($"<div class=\"content\">{contentHtml}</div>");

			sb.AppendLine("</div>");
		}

		sb.AppendLine($"<div class=\"sub\">Generated {DateTimeOffset.Now:yyyy-MM-dd HH:mm}</div>");
		sb.AppendLine("</body></html>");
		return sb.ToString();
	}

	private static Task<byte[]> GeneratePdfFromHtmlAsync(string html, CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

		_ = MainThread.InvokeOnMainThreadAsync(() =>
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();

				var webView = new WKWebView(CoreGraphics.CGRect.Empty, new WKWebViewConfiguration());
				var navTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				webView.NavigationDelegate = new NavDelegate(navTcs);

				// Provide a base URL to avoid nullability issues and give WebKit a stable origin.
				webView.LoadHtmlString(html, baseUrl: new NSUrl("file:///"));

				_ = Task.Run(async () =>
				{
					try
					{
						using var reg = cancellationToken.Register(() => navTcs.TrySetCanceled(cancellationToken));
						await navTcs.Task.ConfigureAwait(false);

						var pdfTcs = new TaskCompletionSource<NSData>(TaskCreationOptions.RunContinuationsAsynchronously);
						var config = new WKPdfConfiguration();

						webView.CreatePdf(config, (data, error) =>
						{
							if (error is not null)
								pdfTcs.TrySetException(new NSErrorException(error));
							else if (data is null)
								pdfTcs.TrySetException(new InvalidOperationException("Failed to generate PDF data."));
							else
								pdfTcs.TrySetResult(data);
						});

						using var reg2 = cancellationToken.Register(() => pdfTcs.TrySetCanceled(cancellationToken));
						var pdfData = await pdfTcs.Task.ConfigureAwait(false);
						tcs.TrySetResult(pdfData.ToArray());
					}
					catch (Exception ex)
					{
						tcs.TrySetException(ex);
					}
				});
			}
			catch (Exception ex)
			{
				tcs.TrySetException(ex);
			}
		});

		return tcs.Task;
	}

	private sealed class NavDelegate : WKNavigationDelegate
	{
		private readonly TaskCompletionSource<bool> _tcs;

		public NavDelegate(TaskCompletionSource<bool> tcs) => _tcs = tcs;

		public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
			=> _tcs.TrySetResult(true);

		public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
			=> _tcs.TrySetException(new NSErrorException(error));

		public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
			=> _tcs.TrySetException(new NSErrorException(error));
	}
}
#endif


