namespace ZamanTakasi.Infrastructure.Persistence;

/// <summary>
/// Bağlantı cümlesini sağlayıcıdan bağımsız çözer.
/// Railway/Heroku/Neon gibi platformlar <c>DATABASE_URL</c>'i URI formatında verir
/// (postgresql://user:pass@host:port/db). Npgsql ise key-value bekler. Bu sınıf,
/// URI verilmişse onu Npgsql key-value formatına çevirir; zaten key-value ise olduğu gibi döner.
/// Böylece host değiştirmek kod değil yalnızca bir env değişkeni güncellemek olur.
/// </summary>
public static class NpgsqlConnectionResolver
{
    public static string? Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var value = raw.Trim();
        var isUri = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                 || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

        return isUri ? FromUri(value) : value;
    }

    private static string FromUri(string uriString)
    {
        var uri = new Uri(uriString);

        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var port = uri.Port > 0 ? uri.Port : 5432;

        // SSL: yönetilen Postgres servisleri (Railway/Neon vb.) TLS ister. Npgsql 8+'da SslMode.Require
        // şifreler ama sertifika zincirini doğrulamaz (self-signed'a uygun). Lokal için Prefer yeterli.
        var local = uri.Host is "localhost" or "127.0.0.1";

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = local ? Npgsql.SslMode.Prefer : Npgsql.SslMode.Require
        };

        return builder.ConnectionString;
    }
}
