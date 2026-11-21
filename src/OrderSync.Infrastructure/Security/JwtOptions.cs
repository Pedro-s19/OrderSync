using Microsoft.Extensions.Configuration;

namespace OrderSync.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Key { get; init; } = "";
    public string Issuer { get; init; } = "OrderSync";
    public string Audience { get; init; } = "OrderSync.Api";
    public int ExpirationMinutes { get; init; } = 60;

    /// <summary>Lee la sección "Jwt" y falla al arrancar si la clave es débil o no existe.</summary>
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new JwtOptions
        {
            Key = configuration["Jwt:Key"] ?? "",
            Issuer = configuration["Jwt:Issuer"] ?? "OrderSync",
            Audience = configuration["Jwt:Audience"] ?? "OrderSync.Api",
            ExpirationMinutes = int.TryParse(configuration["Jwt:ExpirationMinutes"], out var minutes) ? minutes : 60
        };

        if (string.IsNullOrWhiteSpace(options.Key) || options.Key.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Key debe tener al menos 32 caracteres. Defínala en appsettings.Development.json " +
                "o, en Azure, en la variable de entorno Jwt__Key.");

        return options;
    }
}
