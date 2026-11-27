using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using Xunit;

namespace OrderSync.Tests.InventarioCrud;

public class IngredientServiceTests
{
    private readonly Mock<IIngredientStore> _store = new();

    private IngredientService Create() => new(_store.Object);

    private static IngredientDto Ing(int id = 1) => new(id, "Pan", "und", 100m, 20m, true);

    private void GivenIngredient(int id, IngredientDto? ingredient)
        => _store.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(ingredient);

    [Fact]
    public async Task Crear_LimpiaLosTextos_YGuarda()
    {
        CreateIngredientRequest? guardado = null;
        _store.Setup(s => s.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _store.Setup(s => s.CreateAsync(It.IsAny<CreateIngredientRequest>(), It.IsAny<CancellationToken>()))
              .Callback<CreateIngredientRequest, CancellationToken>((r, _) => guardado = r)
              .ReturnsAsync(Ing());

        await Create().CreateAsync(new CreateIngredientRequest("  Pan ", " und ", 20m, 100m));

        Assert.Equal("Pan", guardado!.Nombre);
        Assert.Equal("und", guardado.UnidadMedida);
        Assert.Equal(100m, guardado.StockInicial);
    }

    [Theory]
    [InlineData("", "und", 0, 0)]
    [InlineData("Pan", "", 0, 0)]
    [InlineData("Pan", "unidad-larga", 0, 0)]
    [InlineData("Pan", "und", -1, 0)]
    [InlineData("Pan", "und", 0, -1)]
    public async Task Crear_ConDatosInvalidos_LanzaValidacion(string nombre, string unidad, int minimo, int inicial)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new CreateIngredientRequest(nombre, unidad, minimo, inicial)));
    }

    [Fact]
    public async Task Crear_ConNombreRepetido_LanzaConflicto()
    {
        _store.Setup(s => s.NameExistsAsync("Pan", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().CreateAsync(new CreateIngredientRequest("Pan", "und", 0m, 0m)));
    }

    [Fact]
    public async Task Editar_IngredienteInexistente_LanzaNotFound()
    {
        GivenIngredient(1, null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().UpdateAsync(1, new UpdateIngredientRequest("Pan", "und", 5m)));
    }

    [Fact]
    public async Task Editar_ConNombreDeOtroIngrediente_LanzaConflicto()
    {
        GivenIngredient(1, Ing());
        _store.Setup(s => s.NameExistsAsync("Queso", 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().UpdateAsync(1, new UpdateIngredientRequest("Queso", "und", 5m)));
    }

    [Fact]
    public async Task Editar_ConMinimoNegativo_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().UpdateAsync(1, new UpdateIngredientRequest("Pan", "und", -5m)));
    }

    [Fact]
    public async Task Editar_Valido_Actualiza()
    {
        GivenIngredient(1, Ing());
        _store.Setup(s => s.NameExistsAsync(It.IsAny<string>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Create().UpdateAsync(1, new UpdateIngredientRequest("Pan brioche", "und", 30m));

        _store.Verify(s => s.UpdateAsync(1,
            It.Is<UpdateIngredientRequest>(r => r.Nombre == "Pan brioche" && r.StockMinimo == 30m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivarODesactivar_CambiaElEstado(bool activo)
    {
        GivenIngredient(1, Ing());

        await Create().SetActiveAsync(1, activo);

        _store.Verify(s => s.SetActiveAsync(1, activo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desactivar_IngredienteInexistente_LanzaNotFound()
    {
        GivenIngredient(1, null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().SetActiveAsync(1, false));
    }

    [Fact]
    public async Task Consultar_IngredienteInexistente_LanzaNotFound()
    {
        GivenIngredient(1, null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(1));
    }

    [Fact]
    public void Constructor_ConAlmacenNulo_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new IngredientService(null!));
    }
}
