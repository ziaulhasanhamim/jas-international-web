namespace JASIntWeb.Services;
public static class BangladeshPhone
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("880")) digits = digits[2..];
        return System.Text.RegularExpressions.Regex.IsMatch(digits, "^01[3-9][0-9]{8}$") ? digits : null;
    }
}
