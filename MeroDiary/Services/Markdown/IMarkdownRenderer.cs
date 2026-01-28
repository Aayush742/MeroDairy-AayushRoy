namespace MeroDiary.Services.Markdown;

public interface IMarkdownRenderer
{
	/// <summary>
	/// Renders Markdown to HTML for display. Raw HTML in Markdown is disabled for safety.
	/// </summary>
	string RenderToHtml(string markdown);
}


