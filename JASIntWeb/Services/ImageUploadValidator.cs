using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using ImageMagick;

namespace JASIntWeb.Services;

public static class ImageUploadValidator
{
    public static async Task<string> UploadAsync(IStorageService storage, Stream input, string name, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024) throw new InvalidDataException("Invalid image size.");
        if (Path.GetExtension(name).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
            var svg = XDocument.Load(reader);
            string[] elements = ["svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "defs", "linearGradient", "radialGradient", "stop", "clipPath", "title", "desc", "use"];
            if (svg.Root?.Name.LocalName != "svg" || svg.Root.DescendantsAndSelf().Any(e => !elements.Contains(e.Name.LocalName)))
                throw new InvalidDataException("Unsupported SVG content.");
            foreach (var attribute in svg.Root.DescendantsAndSelf().Attributes())
            {
                var value = attribute.Value.Trim();
                if (attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    || attribute.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase)
                    || (attribute.Name.LocalName == "href" && !value.StartsWith('#'))
                    || (Regex.Matches(value, @"url\(([^)]*)\)", RegexOptions.IgnoreCase).Any(match => !match.Groups[1].Value.Trim(' ', '\'', '"').StartsWith('#'))))
                    throw new InvalidDataException("Unsupported SVG content.");
            }
            bytes = System.Text.Encoding.UTF8.GetBytes(svg.ToString());
            contentType = "image/svg+xml";
        }
        else
        {
            var info = new MagickImageInfo(bytes);
            if (info.Width > 12000 || info.Height > 12000 || (long)info.Width * info.Height > 40_000_000)
                throw new InvalidDataException("Image dimensions are too large.");
            using var decoded = new MagickImage(bytes);
            contentType = decoded.Format switch
            {
                MagickFormat.Jpeg => "image/jpeg", MagickFormat.Png => "image/png", MagickFormat.WebP => "image/webp",
                MagickFormat.Avif => "image/avif", MagickFormat.Gif => "image/gif", _ => throw new InvalidDataException("Unsupported image format.")
            };
            var expected = contentType switch { "image/jpeg" => new[] { ".jpg", ".jpeg" }, "image/png" => [".png"], "image/webp" => [".webp"], "image/avif" => [".avif"], _ => [".gif"] };
            if (!expected.Contains(Path.GetExtension(name).ToLowerInvariant())) throw new InvalidDataException("The file does not match its extension.");
        }
        using var validated = new MemoryStream(bytes);
        return await storage.UploadFileAsync(validated, Path.GetFileName(name), contentType, ct);
    }
}
