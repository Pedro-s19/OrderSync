using OrderSync.Application.Crm;
using Xunit;

namespace OrderSync.Tests.Crm;

public class PromotionPolicyTests
{
    private static readonly DateOnly Hoy = new(2026, 3, 10);

    private static PromotionDto Promo(
        int id = 1, decimal pct = 10m, int? minimo = null, DateOnly? desde = null, DateOnly? hasta = null, bool activa = true)
        => new(id, $"Promo {id}", pct, minimo, desde ?? Hoy.AddDays(-10), hasta, activa);

    [Fact]
    public void SinPromociones_NoAplicaNinguna()
    {
        Assert.Null(PromotionPolicy.SelectBest([], 100, Hoy));
    }

    [Fact]
    public void PromocionVigenteSinCondicion_Aplica()
    {
        var result = PromotionPolicy.SelectBest([Promo()], 0, Hoy);

        Assert.Equal(1, result!.PromocionId);
    }

    [Fact]
    public void PromocionDesactivada_NoAplica()
    {
        Assert.Null(PromotionPolicy.SelectBest([Promo(activa: false)], 100, Hoy));
    }

    [Fact]
    public void PromocionQueAunNoEmpieza_NoAplica()
    {
        Assert.Null(PromotionPolicy.SelectBest([Promo(desde: Hoy.AddDays(1))], 100, Hoy));
    }

    [Fact]
    public void PromocionVencida_NoAplica()
    {
        Assert.Null(PromotionPolicy.SelectBest([Promo(hasta: Hoy.AddDays(-1))], 100, Hoy));
    }

    [Theory]
    [InlineData(0)]   // el último día de vigencia todavía aplica
    [InlineData(5)]
    public void PromocionConVigenciaHastaHoyOMasTarde_Aplica(int diasHastaElFinal)
    {
        Assert.NotNull(PromotionPolicy.SelectBest([Promo(hasta: Hoy.AddDays(diasHastaElFinal))], 0, Hoy));
    }

    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    [InlineData(500, true)]
    public void PuntosMinimos_SeCumplenDesdeElUmbral(int puntosDelCliente, bool aplica)
    {
        var result = PromotionPolicy.SelectBest([Promo(minimo: 50)], puntosDelCliente, Hoy);

        Assert.Equal(aplica, result is not null);
    }

    [Fact]
    public void ConVariasAplicables_GanaElMayorDescuento()
    {
        var result = PromotionPolicy.SelectBest([Promo(1, 10m), Promo(2, 25m), Promo(3, 15m)], 0, Hoy);

        Assert.Equal(2, result!.PromocionId);
    }

    [Fact]
    public void ConDescuentoIgual_GanaLaDeMenorId()
    {
        var result = PromotionPolicy.SelectBest([Promo(5, 10m), Promo(3, 10m)], 0, Hoy);

        Assert.Equal(3, result!.PromocionId);
    }

    [Fact]
    public void LaMejorNoAplicable_SeIgnora()
    {
        var result = PromotionPolicy.SelectBest([Promo(1, 10m), Promo(2, 50m, minimo: 1000)], 10, Hoy);

        Assert.Equal(1, result!.PromocionId);
    }

    [Fact]
    public void ListaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => PromotionPolicy.SelectBest(null!, 0, Hoy));
    }
}
