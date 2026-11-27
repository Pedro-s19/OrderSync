using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Pedidos;
using Xunit;

namespace OrderSync.Tests.Pedidos;

public class ProductServiceTests
{
    private readonly Mock<IProductStore> _store = new();

    private ProductService Create() => new(_store.Object);

    private static ProductInfo Prod(int id = 1, bool activo = true) => new(id, "Clásica", "Hamburguesas", 18000m, activo);

    private void GivenProduct(int id, ProductInfo? product)
        => _store.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

    [Fact]
    public async Task Crear_LimpiaLosTextos_YGuarda()
    {
        ProductRequest? guardado = null;
        _store.Setup(s => s.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _store.Setup(s => s.CreateAsync(It.IsAny<ProductRequest>(), It.IsAny<CancellationToken>()))
              .Callback<ProductRequest, CancellationToken>((r, _) => guardado = r)
              .ReturnsAsync(Prod());

        await Create().CreateAsync(new ProductRequest("  Clásica ", "  ", 18000m));

        Assert.Equal("Clásica", guardado!.Nombre);
        Assert.Null(guardado.Categoria);
    }

    [Fact]
    public async Task Crear_ConNombreRepetido_LanzaConflicto()
    {
        _store.Setup(s => s.NameExistsAsync("Clásica", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().CreateAsync(new ProductRequest("Clásica", null, 18000m)));
    }

    [Theory]
    [InlineData("", 100.0)]
    [InlineData("   ", 100.0)]
    [InlineData("Ok", -1.0)]
    [InlineData("Ok", 100000001.0)]
    public async Task Crear_ConDatosInvalidos_LanzaValidacion(string nombre, double precio)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new ProductRequest(nombre, null, (decimal)precio)));
    }

    [Fact]
    public async Task Crear_ConNombreOCategoriaMuyLargos_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new ProductRequest(new string('a', 101), null, 1m)));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new ProductRequest("Ok", new string('c', 51), 1m)));
    }

    [Fact]
    public async Task Editar_ProductoInexistente_LanzaNotFound()
    {
        GivenProduct(1, null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().UpdateAsync(1, new ProductRequest("Clásica", null, 1m)));
    }

    [Fact]
    public async Task Editar_ConNombreDeOtroProducto_LanzaConflicto()
    {
        GivenProduct(1, Prod());
        _store.Setup(s => s.NameExistsAsync("Doble", 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().UpdateAsync(1, new ProductRequest("Doble", null, 1m)));
    }

    [Fact]
    public async Task Editar_Valido_ActualizaYDevuelveElProducto()
    {
        GivenProduct(1, Prod());
        _store.Setup(s => s.NameExistsAsync(It.IsAny<string>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await Create().UpdateAsync(1, new ProductRequest("Clásica", "Hamburguesas", 19000m));

        Assert.Equal(1, result.ProductoId);
        _store.Verify(s => s.UpdateAsync(1, It.Is<ProductRequest>(r => r.Precio == 19000m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivarODesactivar_CambiaElEstado(bool activo)
    {
        GivenProduct(1, Prod());

        await Create().SetActiveAsync(1, activo);

        _store.Verify(s => s.SetActiveAsync(1, activo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desactivar_ProductoInexistente_LanzaNotFound()
    {
        GivenProduct(1, null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().SetActiveAsync(1, false));
    }

    [Fact]
    public async Task Consultar_ProductoInexistente_LanzaNotFound()
    {
        GivenProduct(1, null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(1));
    }

    [Fact]
    public void Constructor_ConAlmacenNulo_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ProductService(null!));
    }
}
