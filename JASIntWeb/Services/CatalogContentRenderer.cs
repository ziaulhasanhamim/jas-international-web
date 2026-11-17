using Ganss.Xss;
using Markdig;

namespace JASIntWeb.Services;

public sealed class CatalogContentRenderer
{
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().Build();
    public string Render(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "";
        if (source.Length > 100_000) throw new ArgumentException("Content exceeds 100,000 characters.");
        // Sanitize after Markdown conversion, including raw HTML that was already stored.
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "h2", "h3", "h4", "h5", "h6", "strong", "em", "b", "i", "u", "ul", "ol", "li", "blockquote", "a", "table", "thead", "tbody", "tr", "th", "td", "hr", "pre", "code", "img" }) sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[] { "href", "title", "colspan", "rowspan", "src", "alt", "width", "height" }) sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "https", "http", "mailto" }) sanitizer.AllowedSchemes.Add(scheme);
        return sanitizer.Sanitize(Markdown.ToHtml(source, pipeline));
    }
}
