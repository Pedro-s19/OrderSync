using OrderSync.Application.Common;

namespace OrderSync.Tests.Support;

/// <summary>Reloj de prueba: la hora local de Colombia es UTC-5.</summary>
internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime localNow) => LocalNow = localNow;

    public DateTime LocalNow { get; set; }
    public DateTime UtcNow => LocalNow.AddHours(5);
    public DateTime ToLocal(DateTime utc) => utc.AddHours(-5);
    public DateTime ToUtc(DateTime local) => local.AddHours(5);
}
