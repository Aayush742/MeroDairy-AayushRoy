using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace MeroDiary.Services.Export;

/// <summary>
/// Minimal Markdown renderer for PDF (headings, bold/italic, lists, links, paragraphs).
/// </summary>
public static class MarkdownToPdfRenderer
{
	private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
		.UseAdvancedExtensions()
		.DisableHtml()
		.Build();

	public static void Render(QuestPDF.Infrastructure.IContainer container, string markdown)
	{
		markdown ??= string.Empty;
		var doc = Markdig.Markdown.Parse(markdown, Pipeline);

		container.Column(col =>
		{
			col.Spacing(6);
			foreach (var block in doc)
			{
				switch (block)
				{
					case HeadingBlock h:
						col.Item().Text(text =>
						{
							text.DefaultTextStyle(s => s.FontSize(h.Level switch { 1 => 18, 2 => 16, 3 => 14, _ => 12 }).SemiBold());
							RenderInlines(text, h.Inline);
						});
						break;

					case ParagraphBlock p:
						col.Item().Text(text =>
						{
							text.DefaultTextStyle(s => s.FontSize(11));
							RenderInlines(text, p.Inline);
						});
						break;

					case ListBlock list:
						RenderList(col, list);
						break;

					case ThematicBreakBlock:
						col.Item().LineHorizontal(1);
						break;

					default:
						// Fallback: ignore unsupported blocks.
						break;
				}
			}
		});
	}

	private static void RenderList(ColumnDescriptor col, ListBlock list)
	{
		var index = 1;
		foreach (var item in list)
		{
			if (item is not ListItemBlock li)
				continue;

			foreach (var sub in li)
			{
				if (sub is ParagraphBlock p)
				{
					var prefix = list.IsOrdered ? $"{index}." : "•";
					col.Item().Row(row =>
					{
						row.ConstantItem(18).Text(t =>
						{
							t.DefaultTextStyle(s => s.FontSize(11));
							t.Span(prefix);
						});
						row.RelativeItem().Text(t =>
						{
							t.DefaultTextStyle(s => s.FontSize(11));
							RenderInlines(t, p.Inline);
						});
					});
				}
			}

			index++;
		}
	}

	private static void RenderInlines(TextDescriptor text, ContainerInline? inline)
	{
		if (inline is null)
			return;

		foreach (var child in inline)
		{
			RenderInline(text, child);
		}
	}

	private static void RenderInline(TextDescriptor text, Inline inline)
	{
		switch (inline)
		{
			case LiteralInline lit:
				text.Span(lit.Content.ToString());
				break;

			case LineBreakInline:
				text.Span("\n");
				break;

			case EmphasisInline emph:
				RenderEmphasis(text, emph);
				break;

			case LinkInline link:
				RenderLink(text, link);
				break;

			default:
				// Try children
				if (inline is ContainerInline c)
				{
					foreach (var child in c)
						RenderInline(text, child);
				}
				break;
		}
	}

	private static void RenderEmphasis(TextDescriptor text, EmphasisInline emph)
	{
		// Markdig uses DelimiterCount to distinguish strong/emphasis
		var isBold = emph.DelimiterChar == '*' && emph.DelimiterCount >= 2
		             || emph.DelimiterChar == '_' && emph.DelimiterCount >= 2;
		var isItalic = !isBold;

		// QuestPDF doesn't support nested styling via a stack, so we approximate by styling each literal span.
		foreach (var child in emph)
		{
			if (child is LiteralInline lit)
			{
				var span = text.Span(lit.Content.ToString());
				if (isBold) span.SemiBold();
				if (isItalic) span.Italic();
			}
			else
			{
				RenderInline(text, child);
			}
		}
	}

	private static void RenderLink(TextDescriptor text, LinkInline link)
	{
		// Show link text, then url in parentheses for readability in PDF.
		var linkText = ExtractPlainText(link);
		if (string.IsNullOrWhiteSpace(linkText))
			linkText = link.Url ?? "link";

		text.Span(linkText).FontColor(QuestPDF.Helpers.Colors.Blue.Darken2).Underline();

		if (!string.IsNullOrWhiteSpace(link.Url) && !string.Equals(linkText, link.Url, StringComparison.Ordinal))
		{
			text.Span($" ({link.Url})").FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
		}
	}

	private static string ExtractPlainText(ContainerInline inline)
	{
		var parts = new List<string>();
		foreach (var child in inline)
		{
			if (child is LiteralInline lit)
				parts.Add(lit.Content.ToString());
			else if (child is ContainerInline c)
				parts.Add(ExtractPlainText(c));
		}
		return string.Concat(parts);
	}
}


