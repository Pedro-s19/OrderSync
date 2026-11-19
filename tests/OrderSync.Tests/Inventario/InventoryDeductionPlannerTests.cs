using OrderSync.Application.Inventario;
using Xunit;

namespace OrderSync.Tests.Inventario;

public class InventoryDeductionPlannerTests
{
    private readonly InventoryDeductionPlanner _planner = new();

    private static IngredientStock Ing(int id, decimal stock, decimal min, string? nombre = null)
        => new(id, nombre ?? $"Ing{id}", "und", stock, min);

    private static OrderInventorySnapshot Snapshot(
        OrderLine[] lineas, RecipeItem[] receta, IngredientStock[] ingredientes)
        => new(1, "BORRADOR", lineas, receta, ingredientes);

    [Fact]
    public void ProductoConRecetaDeTresIngredientes_DescuentaLosTres()
    {
        var plan = _planner.Plan(Snapshot(
            [new OrderLine(1, 1)],
            [new RecipeItem(1, 10, 1m), new RecipeItem(1, 11, 1m), new RecipeItem(1, 12, 30m)],
            [Ing(10, 100m, 20m), Ing(11, 80m, 15m), Ing(12, 3000m, 500m)]));

        Assert.Equal(3, plan.Descuentos.Count);
        Assert.Equal(new[] { 1m, 1m, 30m }, plan.Descuentos.Select(d => d.Cantidad));
        Assert.Equal(new[] { 99m, 79m, 2970m }, plan.Descuentos.Select(d => d.StockResultante));
        Assert.Empty(plan.Alertas);
    }

    [Fact]
    public void MultiplicaLaReceta_PorLaCantidadPedida()
    {
        var plan = _planner.Plan(Snapshot(
            [new OrderLine(1, 3)],
            [new RecipeItem(1, 10, 2m)],
            [Ing(10, 100m, 5m)]));

        var d = Assert.Single(plan.Descuentos);
        Assert.Equal(6m, d.Cantidad);
        Assert.Equal(94m, d.StockResultante);
    }

    [Fact]
    public void ElMismoIngredienteEnProductosDistintos_SeSumaEnUnSoloDescuento()
    {
        var plan = _planner.Plan(Snapshot(
            [new OrderLine(1, 2), new OrderLine(2, 1)],
            [new RecipeItem(1, 10, 1m), new RecipeItem(2, 10, 3m)],
            [Ing(10, 100m, 5m)]));

        var d = Assert.Single(plan.Descuentos);
        Assert.Equal(5m, d.Cantidad);
    }

    [Fact]
    public void StockQueQuedaSobreElMinimo_NoGeneraAlerta()
    {
        // Mínimo 10; el pedido lo deja en 11.
        var plan = _planner.Plan(Snapshot(
            [new OrderLine(1, 1)],
            [new RecipeItem(1, 10, 1m)],
            [Ing(10, 12m, 10m)]));

        Assert.Equal(11m, Assert.Single(plan.Descuentos).StockResultante);
        Assert.Empty(plan.Alertas);
    }

    [Fact]
    public void StockQueQuedaIgualAlMinimo_NoGeneraAlerta()
    {
        var plan = _planner.Plan(Snapshot(
            [new OrderLine(1, 1)],
            [new RecipeItem(1, 10, 1m)],
            [Ing(10, 11m, 10m)]));

        Assert.Equal(10m, Assert.Single(plan.Descuentos).StockResultante);
        Assert.Empty(plan.Alertas);
    }

    [Fact]
    public void StockQueQuedaBajoElMinimo_GeneraAlertaConIngredienteYCantidad()
    {
        // Mínimo 10; el pedido lo deja en 9.
        var plan = _planner.Plan(Snapshot(
            [new OrderLine(1, 1)],
            [new RecipeItem(1, 10, 1m)],
            [Ing(10, 10m, 10m, "Pan")]));

        var alerta = Assert.Single(plan.Alertas);
        Assert.Equal(10, alerta.IngredienteId);
        Assert.Equal("Pan", alerta.Nombre);
        Assert.Equal(9m, alerta.StockAlMomento);
        Assert.Equal(10m, alerta.StockMinimo);
    }

    [Fact]
    public void StockQueQuedaEnCeroConMinimoPositivo_DescuentaYGeneraAlerta()
    {
        var plan = _planner.Plan(Snapshot(
            [new OrderLine(1, 2)],
            [new RecipeItem(1, 10, 1m)],
            [Ing(10, 2m, 5m)]));

        Assert.Equal(0m, Assert.Single(plan.Descuentos).StockResultante);
        Assert.Equal(0m, Assert.Single(plan.Alertas).StockAlMomento);
    }

    [Fact]
    public void StockInsuficiente_LanzaExcepcionConElFaltante()
    {
        var ex = Assert.Throws<InsufficientStockException>(() => _planner.Plan(Snapshot(
            [new OrderLine(1, 2)],
            [new RecipeItem(1, 10, 1m)],
            [Ing(10, 1m, 0m, "Carne")])));

        var faltante = Assert.Single(ex.Faltantes);
        Assert.Equal("Carne", faltante.Nombre);
        Assert.Equal(2m, faltante.Requerido);
        Assert.Equal(1m, faltante.Disponible);
        Assert.Equal(1, ex.PedidoId);
    }

    [Fact]
    public void VariosFaltantes_SeListanTodos()
    {
        var ex = Assert.Throws<InsufficientStockException>(() => _planner.Plan(Snapshot(
            [new OrderLine(1, 5)],
            [new RecipeItem(1, 10, 1m), new RecipeItem(1, 11, 1m), new RecipeItem(1, 12, 1m)],
            [Ing(10, 1m, 0m), Ing(11, 100m, 0m), Ing(12, 2m, 0m)])));

        Assert.Equal(new[] { 10, 12 }, ex.Faltantes.Select(f => f.IngredienteId));
    }

    [Fact]
    public void PedidoSinLineas_LanzaEmptyOrder()
    {
        Assert.Throws<EmptyOrderException>(() => _planner.Plan(Snapshot([], [], [])));
    }

    [Fact]
    public void ProductoSinReceta_NoDescuentaNiAlerta()
    {
        var plan = _planner.Plan(Snapshot([new OrderLine(9, 1)], [], []));

        Assert.Empty(plan.Descuentos);
        Assert.Empty(plan.Alertas);
    }

    [Fact]
    public void IngredienteDeLaRecetaInexistente_LanzaInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => _planner.Plan(Snapshot(
            [new OrderLine(1, 1)],
            [new RecipeItem(1, 99, 1m)],
            [])));
    }

    [Fact]
    public void SnapshotNulo_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => _planner.Plan(null!));
    }

    [Fact]
    public void ElDescuentoReportaStockAnteriorYResultante()
    {
        var plan = _planner.Plan(Snapshot(
            [new OrderLine(1, 4)],
            [new RecipeItem(1, 10, 1m)],
            [Ing(10, 50m, 5m)]));

        var d = Assert.Single(plan.Descuentos);
        Assert.Equal(50m, d.StockAnterior);
        Assert.Equal(46m, d.StockResultante);
    }
}
