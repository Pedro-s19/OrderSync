using OrderSync.Application.Common;

namespace OrderSync.Infrastructure.Time;

/// <summary>Reloj con la hora de Colombia (UTC-5, sin horario de verano).</summary>
public sealed class BogotaClock : IClock
{
    private static readonly TimeZoneInfo Zone = Resolve();

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public DateTime ToUtc(DateTime local)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "America/Bogota", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("COT", TimeSpan.FromHours(-5), "Colombia", "Colombia");
    }
}
