using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

namespace JASIntWeb.Services;

public static class DatabaseConfiguration
{
    public static string ResolvePostgresConnectionString(IConfiguration configuration)
    {
        var raw = configuration["DATABASE_URL"] ?? configuration["DB_URL"]
            ?? configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Configure DATABASE_URL, DB_URL, or ConnectionStrings:DefaultConnection.");

        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return new NpgsqlConnectionStringBuilder(raw).ConnectionString;

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("The PostgreSQL URL is invalid.");

        var credentials = uri.UserInfo.Split(':', 2);
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length == 2 ? Uri.UnescapeDataString(credentials[1]) : "",
            SslMode = SslMode.Require,
            Pooling = true
        };

        foreach (var (key, value) in QueryHelpers.ParseQuery(uri.Query))
        {
            var option = key.ToLowerInvariant() switch
            {
                "sslmode" => "SSL Mode",
                "channel_binding" => "Channel Binding",
                "connect_timeout" => "Timeout",
                "application_name" => "Application Name",
                _ => throw new InvalidOperationException($"Unsupported PostgreSQL URL option: {key}.")
            };
            connection[option] = key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                ? value.ToString().Replace("-", "", StringComparison.Ordinal)
                : value.ToString();
        }

        return connection.ConnectionString;
    }
}
