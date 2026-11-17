using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace JASIntWeb.Client.Services;

public static partial class EditorSlug
{
    public static string FromName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var text = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        text = text.Replace("&", "and").Replace("+", "-plus-").Replace("@", "-at-").Replace("%", "-percent-");
        text = Invalid().Replace(text, "");
        return Separators().Replace(text, "-").Trim('-');
    }
    [GeneratedRegex(@"[^a-z0-9\s-]")] private static partial Regex Invalid();
    [GeneratedRegex(@"[\s-]+")] private static partial Regex Separators();
}
