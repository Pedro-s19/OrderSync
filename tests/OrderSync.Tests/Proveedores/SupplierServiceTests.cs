using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using OrderSync.Application.Proveedores;
using OrderSync.Tests.Support;
using Xunit;

namespace OrderSync.Tests.Proveedores;

public class SupplierServiceTests
{
    private readonly Mock<ISupplierStore> _store = new();
    private readonly Mock<IIngredientStore> _ingredients = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 9, 0, 0));

    private SupplierService Create() => new(_store.Object, _ingredients.Object, _clock);

    private static SupplierDto Sup(bool activo = true) => new(1, "Distribuidora", "Ana", "3001112233", "ana@dist.com", activo);

    private static IngredientDto Ing(bool activo = true) => new(5, "Carne", "g", 100m, 10m, activo);

    private void GivenSupplier(SupplierDto? supplier)
        => _store.Setup(s => s.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);

    private void GivenIngredient(IngredientDto? ingredient)
        => _ingredients.Setup(i => i.GetAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(ingredient);

    [Fact]
    public async Task Crear_LimpiaLosTextos_YNormalizaElCorreo()
    {
        SupplierRequest? guardado = null;
        _store.Setup(s => s.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _store.Setup(s => s.CreateAsync(It.IsAny<SupplierRequest>(), It.IsAny<CancellationToken>()))
              .Callback<SupplierRequest, CancellationToken>((r, _) => guardado = r)
              .ReturnsAsync(Sup());

        await Create().CreateAsync(new SupplierRequest("  Distribuidora ", " Ana ", "  ", "ANA@Dist.com"));

        Assert.Equal("Distribuidora", guardado!.Nombre);
        Assert.Equal("Ana", guardado.Contacto);
        Assert.Null(guardado.Telefono);
        Assert.Equal("ana@dist.com", guardado.Email);
    }

    [Fact]
    public async Task Crear_ConNombreRepetido_LanzaConflicto()
    {
        _store.Setup(s => s.NameExistsAsync("Distribuidora", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().CreateAsync(new SupplierRequest("Distribuidora", "Ana", null, null)));
    }

    [Theory]
    [InlineData("", "Ana", null)]
    [InlineData("Dist", "", null)]
    [InlineData("Dist", "Ana", "no-es-un-correo")]
    public async Task Crear_ConDatosInvalidos_LanzaValidacion(string nombre, string contacto, string? email)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new SupplierRequest(nombre, contacto, null, email)));
    }

    [Fact]
    public async Task Editar_ProveedorInexistente_LanzaNotFound()
    {
        GivenSupplier(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().UpdateAsync(1, new SupplierRequest("Dist", "Ana", null, null)));
    }

    [Fact]
    public async Task Editar_ConNombreDeOtroProveedor_LanzaConflicto()
    {
        GivenSupplier(Sup());
        _store.Setup(s => s.NameExistsAsync("Otra", 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().UpdateAsync(1, new SupplierRequest("Otra", "Ana", null, null)));
    }

    [Fact]
    public async Task Editar_Valido_Actualiza()
    {
        GivenSupplier(Sup());
        _store.Setup(s => s.NameExistsAsync(It.IsAny<string>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Create().UpdateAsync(1, new SupplierRequest("Nueva", "Ana", null, null));

        _store.Verify(s => s.UpdateAsync(1, It.Is<SupplierRequest>(r => r.Nombre == "Nueva"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivarODesactivar_CambiaElEstado(bool activo)
    {
        GivenSupplier(Sup());

        await Create().SetActiveAsync(1, activo);

        _store.Verify(s => s.SetActiveAsync(1, activo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desactivar_ProveedorInexistente_LanzaNotFound()
    {
        GivenSupplier(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().SetActiveAsync(1, false));
    }

    [Fact]
    public async Task Precio_Valido_SeGuardaConLaFechaDeHoy()
    {
        GivenSupplier(Sup());
        GivenIngredient(Ing());
        _store.Setup(s => s.GetPricesAsync(1, It.IsAny<CancellationToken>()))
              .ReturnsAsync((IReadOnlyList<SupplierPriceDto>)[]);

        await Create().SetPriceAsync(1, 5, new PriceRequest(40m));

        _store.Verify(s => s.UpsertPriceAsync(1, 5, 40m, new DateOnly(2026, 3, 10), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(100000001.0)]
    public async Task Precio_FueraDeRango_LanzaValidacion(double precio)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().SetPriceAsync(1, 5, new PriceRequest((decimal)precio)));
    }

    [Fact]
    public async Task Precio_ProveedorInexistente_LanzaNotFound()
    {
        GivenSupplier(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().SetPriceAsync(1, 5, new PriceRequest(40m)));
    }

    [Fact]
    public async Task Precio_ProveedorDesactivado_LanzaReglaDeNegocio()
    {
        GivenSupplier(Sup(activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().SetPriceAsync(1, 5, new PriceRequest(40m)));
    }

    [Fact]
    public async Task Precio_IngredienteInexistente_LanzaNotFound()
    {
        GivenSupplier(Sup());
        GivenIngredient(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().SetPriceAsync(1, 5, new PriceRequest(40m)));
    }

    [Fact]
    public async Task Precio_IngredienteDesactivado_LanzaReglaDeNegocio()
    {
        GivenSupplier(Sup());
        GivenIngredient(Ing(activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().SetPriceAsync(1, 5, new PriceRequest(40m)));
    }

    [Fact]
    public async Task QuitarPrecio_QueNoExiste_LanzaNotFound()
    {
        GivenSupplier(Sup());
        _store.Setup(s => s.RemovePriceAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().RemovePriceAsync(1, 5));
    }

    [Fact]
    public async Task QuitarPrecio_Existente_NoLanzaError()
    {
        GivenSupplier(Sup());
        _store.Setup(s => s.RemovePriceAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Create().RemovePriceAsync(1, 5);

        _store.Verify(s => s.RemovePriceAsync(1, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsultarPrecios_ProveedorInexistente_LanzaNotFound()
    {
        GivenSupplier(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetPricesAsync(1));
    }

    [Fact]
    public async Task Comparar_IngredienteInexistente_LanzaNotFound()
    {
        GivenIngredient(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().ComparePricesAsync(5));
    }

    [Fact]
    public async Task Comparar_DevuelveLosPreciosDeLosProveedoresActivos()
    {
        GivenIngredient(Ing());
        _store.Setup(s => s.ComparePricesAsync(5, It.IsAny<CancellationToken>()))
              .ReturnsAsync((IReadOnlyList<PriceComparisonDto>)[
                  new PriceComparisonDto(1, "Barata", 30m, new DateOnly(2026, 3, 1)),
                  new PriceComparisonDto(2, "Cara", 45m, new DateOnly(2026, 3, 1))]);

        var result = await Create().ComparePricesAsync(5);

        Assert.Equal(new[] { 30m, 45m }, result.Select(r => r.PrecioUnitario));
    }

    [Fact]
    public async Task ProveedorInexistente_AlConsultar_LanzaNotFound()
    {
        GivenSupplier(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(1));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SupplierService(null!, _ingredients.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new SupplierService(_store.Object, null!, _clock));
        Assert.Throws<ArgumentNullException>(() => new SupplierService(_store.Object, _ingredients.Object, null!));
    }
}
