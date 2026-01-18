using Markdig;

namespace MeroDiary.Services.Markdown;

public sealed class MarkdigMarkdownRenderer : IMarkdownRenderer
{
	private readonly MarkdownPipeline _pipeline;

	public MarkdigMarkdownRenderer()
	{
		// Supports headings, emphasis (bold/italic), lists, and links (plus many extras).
		// DisableHtml ensures raw HTML in Markdown is not rendered, preventing script injection.
		_pipeline = new MarkdownPipelineBuilder()
			.UseAdvancedExtensions()
			.DisableHtml()
			.Build();
	}

	public string RenderToHtml(string markdown)
	{
		markdown ??= string.Empty;
		return Markdig.Markdown.ToHtml(markdown, _pipeline);
	}
}


