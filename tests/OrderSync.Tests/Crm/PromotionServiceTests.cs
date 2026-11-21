using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Crm;
using Xunit;

namespace OrderSync.Tests.Crm;

public class PromotionServiceTests
{
    private static readonly DateOnly Hoy = new(2026, 3, 10);

    private readonly Mock<IPromotionStore> _store = new();
    private readonly Mock<ICustomerStore> _customers = new();

    private PromotionService Create() => new(_store.Object, _customers.Object);

    private static PromotionDto Promo(bool activa = true) => new(2, "Cumpleaños", 10m, null, Hoy, null, activa);

    private static CustomerDto Cli(bool activo = true) => new(1, "Luis", null, null, null, 0, DateTime.UtcNow, activo);

    private void GivenPromotion(PromotionDto? promotion)
        => _store.Setup(s => s.GetAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(promotion);

    private void GivenCustomer(CustomerDto? customer)
        => _customers.Setup(c => c.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(customer);

    // ---------- CRUD ----------

    [Fact]
    public async Task Crear_LimpiaElNombre_YGuarda()
    {
        PromotionRequest? guardada = null;
        _store.Setup(s => s.CreateAsync(It.IsAny<PromotionRequest>(), It.IsAny<CancellationToken>()))
              .Callback<PromotionRequest, CancellationToken>((r, _) => guardada = r)
              .ReturnsAsync(Promo());

        await Create().CreateAsync(new PromotionRequest("  Cumpleaños ", 10m, 50, Hoy, Hoy.AddDays(30)));

        Assert.Equal("Cumpleaños", guardada!.Nombre);
        Assert.Equal(50, guardada.PuntosMinimos);
    }

    [Theory]
    [InlineData("", 10.0, 0)]
    [InlineData("Promo", 0.0, 0)]
    [InlineData("Promo", 100.5, 0)]
    [InlineData("Promo", 10.0, -1)]
    public async Task Crear_ConDatosInvalidos_LanzaValidacion(string nombre, double porcentaje, int puntosMinimos)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new PromotionRequest(nombre, (decimal)porcentaje, puntosMinimos, Hoy, null)));
    }

    [Fact]
    public async Task Crear_ConVigenciaHastaAnteriorADesde_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new PromotionRequest("Promo", 10m, null, Hoy, Hoy.AddDays(-1))));
    }

    [Fact]
    public async Task Crear_ConElCienPorCiento_EsValido()
    {
        _store.Setup(s => s.CreateAsync(It.IsAny<PromotionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Promo());

        await Create().CreateAsync(new PromotionRequest("Cortesía", 100m, null, Hoy, null));

        _store.Verify(s => s.CreateAsync(It.IsAny<PromotionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Editar_PromocionInexistente_LanzaNotFound()
    {
        GivenPromotion(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().UpdateAsync(2, new PromotionRequest("Promo", 10m, null, Hoy, null)));
    }

    [Fact]
    public async Task Editar_Valida_Actualiza()
    {
        GivenPromotion(Promo());

        await Create().UpdateAsync(2, new PromotionRequest("Nueva", 15m, null, Hoy, null));

        _store.Verify(s => s.UpdateAsync(2, It.Is<PromotionRequest>(r => r.PorcentajeDto == 15m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivarODesactivar_CambiaElEstado(bool activa)
    {
        GivenPromotion(Promo());

        await Create().SetActiveAsync(2, activa);

        _store.Verify(s => s.SetActiveAsync(2, activa, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consultar_PromocionInexistente_LanzaNotFound()
    {
        GivenPromotion(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(2));
    }

    // ---------- Asignación a clientes ----------

    [Fact]
    public async Task Asignar_Valida_GuardaLaAsignacion()
    {
        GivenCustomer(Cli());
        GivenPromotion(Promo());
        _store.Setup(s => s.IsAssignedAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _store.Setup(s => s.GetAssignedAsync(1, It.IsAny<CancellationToken>()))
              .ReturnsAsync((IReadOnlyList<PromotionDto>)[Promo()]);

        var result = await Create().AssignAsync(1, 2);

        Assert.Single(result);
        _store.Verify(s => s.AssignAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Asignar_ClienteInexistente_LanzaNotFound()
    {
        GivenCustomer(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().AssignAsync(1, 2));
    }

    [Fact]
    public async Task Asignar_ClienteDesactivado_LanzaReglaDeNegocio()
    {
        GivenCustomer(Cli(activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().AssignAsync(1, 2));
    }

    [Fact]
    public async Task Asignar_PromocionInexistente_LanzaNotFound()
    {
        GivenCustomer(Cli());
        GivenPromotion(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().AssignAsync(1, 2));
    }

    [Fact]
    public async Task Asignar_PromocionDesactivada_LanzaReglaDeNegocio()
    {
        GivenCustomer(Cli());
        GivenPromotion(Promo(activa: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().AssignAsync(1, 2));
    }

    [Fact]
    public async Task Asignar_PromocionYaAsignada_LanzaConflicto()
    {
        GivenCustomer(Cli());
        GivenPromotion(Promo());
        _store.Setup(s => s.IsAssignedAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => Create().AssignAsync(1, 2));

        _store.Verify(s => s.AssignAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Quitar_PromocionAsignada_Funciona()
    {
        GivenCustomer(Cli());
        _store.Setup(s => s.UnassignAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Create().UnassignAsync(1, 2);

        _store.Verify(s => s.UnassignAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Quitar_PromocionNoAsignada_LanzaNotFound()
    {
        GivenCustomer(Cli());
        _store.Setup(s => s.UnassignAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().UnassignAsync(1, 2));
    }

    [Fact]
    public async Task ListarDelCliente_ClienteInexistente_LanzaNotFound()
    {
        GivenCustomer(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().ListForCustomerAsync(1));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PromotionService(null!, _customers.Object));
        Assert.Throws<ArgumentNullException>(() => new PromotionService(_store.Object, null!));
    }
}
