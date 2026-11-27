using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using OrderSync.Application.Pedidos;
using Xunit;

namespace OrderSync.Tests.InventarioCrud;

public class RecipeServiceTests
{
    private readonly Mock<IProductStore> _products = new();
    private readonly Mock<IIngredientStore> _ingredients = new();
    private readonly Mock<IRecipeStore> _recipes = new();

    private RecipeService Create() => new(_products.Object, _ingredients.Object, _recipes.Object);

    private void GivenProduct(bool exists = true)
        => _products.Setup(p => p.GetAsync(1, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(exists ? new ProductInfo(1, "Clásica", "Hamburguesas", 18000m, true) : null);

    private void GivenIngredients(params IngredientDto[] ingredients)
        => _ingredients.Setup(i => i.GetManyAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync((IReadOnlyList<IngredientDto>)ingredients);

    private static IngredientDto Ing(int id, bool activo = true) => new(id, $"Ing{id}", "und", 10m, 1m, activo);

    [Fact]
    public async Task Consultar_ProductoInexistente_LanzaNotFound()
    {
        GivenProduct(false);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(1));
    }

    [Fact]
    public async Task Consultar_DevuelveLaRecetaDelProducto()
    {
        GivenProduct();
        _recipes.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<RecipeLineDto>)[new RecipeLineDto(10, "Pan", "und", 1m)]);

        var result = await Create().GetAsync(1);

        Assert.Equal("Pan", Assert.Single(result).Ingrediente);
    }

    [Fact]
    public async Task Reemplazar_ConIngredientesValidos_GuardaLaReceta()
    {
        GivenProduct();
        GivenIngredients(Ing(10), Ing(11));
        _recipes.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<RecipeLineDto>)[]);

        await Create().ReplaceAsync(1, new RecipeRequest([new RecipeLineInput(10, 1m), new RecipeLineInput(11, 30m)]));

        _recipes.Verify(r => r.ReplaceAsync(1, It.Is<IReadOnlyList<RecipeLineInput>>(l => l.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reemplazar_ConListaVacia_DejaElProductoSinReceta()
    {
        GivenProduct();
        GivenIngredients();
        _recipes.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<RecipeLineDto>)[]);

        var result = await Create().ReplaceAsync(1, new RecipeRequest([]));

        Assert.Empty(result);
        _recipes.Verify(r => r.ReplaceAsync(1, It.Is<IReadOnlyList<RecipeLineInput>>(l => l.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Reemplazar_ConCantidadNoPositiva_LanzaValidacion(int cantidad)
    {
        GivenProduct();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ReplaceAsync(1, new RecipeRequest([new RecipeLineInput(10, cantidad)])));
    }

    [Fact]
    public async Task Reemplazar_ConIngredienteRepetido_LanzaValidacion()
    {
        GivenProduct();

        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ReplaceAsync(1,
            new RecipeRequest([new RecipeLineInput(10, 1m), new RecipeLineInput(10, 2m)])));
    }

    [Fact]
    public async Task Reemplazar_ConIngredienteInexistente_LanzaNotFound()
    {
        GivenProduct();
        GivenIngredients(Ing(10));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().ReplaceAsync(1,
            new RecipeRequest([new RecipeLineInput(10, 1m), new RecipeLineInput(99, 1m)])));

        _recipes.Verify(r => r.ReplaceAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RecipeLineInput>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reemplazar_ConIngredienteDesactivado_LanzaReglaDeNegocio()
    {
        GivenProduct();
        GivenIngredients(Ing(10, activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create().ReplaceAsync(1, new RecipeRequest([new RecipeLineInput(10, 1m)])));
    }

    [Fact]
    public async Task Reemplazar_ProductoInexistente_LanzaNotFound()
    {
        GivenProduct(false);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().ReplaceAsync(1, new RecipeRequest([])));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RecipeService(null!, _ingredients.Object, _recipes.Object));
        Assert.Throws<ArgumentNullException>(() => new RecipeService(_products.Object, null!, _recipes.Object));
        Assert.Throws<ArgumentNullException>(() => new RecipeService(_products.Object, _ingredients.Object, null!));
    }
}
