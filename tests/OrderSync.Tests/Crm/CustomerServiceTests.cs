using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Crm;
using OrderSync.Tests.Inventario;
using Xunit;

namespace OrderSync.Tests.Crm;

public class CustomerServiceTests
{
    private readonly Mock<ICustomerStore> _store = new();
    private readonly PassThroughTransactionRunner _tx = new();

    private CustomerService Create() => new(_store.Object, _tx);

    private static CustomerDto Cli(int puntos = 100, bool activo = true)
        => new(1, "Luis", "3001234567", "luis@correo.com", "Calle 1", puntos, DateTime.UtcNow, activo);

    private void GivenCustomer(CustomerDto? customer)
    {
        _store.Setup(s => s.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _store.Setup(s => s.LockAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
    }

    // ---------- Crear y editar ----------

    [Fact]
    public async Task Crear_LimpiaLosTextos_YNormalizaElCorreo()
    {
        CustomerRequest? guardado = null;
        _store.Setup(s => s.CreateAsync(It.IsAny<CustomerRequest>(), It.IsAny<CancellationToken>()))
              .Callback<CustomerRequest, CancellationToken>((r, _) => guardado = r)
              .ReturnsAsync(Cli());

        await Create().CreateAsync(new CustomerRequest("  Luis ", " 300 ", "LUIS@Correo.com", "  "));

        Assert.Equal("Luis", guardado!.Nombre);
        Assert.Equal("300", guardado.Telefono);
        Assert.Equal("luis@correo.com", guardado.Email);
        Assert.Null(guardado.Direccion);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Luis", "no-es-correo")]
    public async Task Crear_ConDatosInvalidos_LanzaValidacion(string nombre, string? email)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new CustomerRequest(nombre, null, email, null)));
    }

    [Fact]
    public async Task Crear_ConTelefonoODireccionMuyLargos_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new CustomerRequest("Luis", new string('1', 21), null, null)));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new CustomerRequest("Luis", null, null, new string('d', 201))));
    }

    [Fact]
    public async Task Editar_ClienteInexistente_LanzaNotFound()
    {
        GivenCustomer(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().UpdateAsync(1, new CustomerRequest("Luis", null, null, null)));
    }

    [Fact]
    public async Task Editar_Valido_Actualiza()
    {
        GivenCustomer(Cli());

        await Create().UpdateAsync(1, new CustomerRequest("Luis Pérez", null, null, null));

        _store.Verify(s => s.UpdateAsync(1, It.Is<CustomerRequest>(r => r.Nombre == "Luis Pérez"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivarODesactivar_CambiaElEstado(bool activo)
    {
        GivenCustomer(Cli());

        await Create().SetActiveAsync(1, activo);

        _store.Verify(s => s.SetActiveAsync(1, activo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desactivar_ClienteInexistente_LanzaNotFound()
    {
        GivenCustomer(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().SetActiveAsync(1, false));
    }

    // ---------- Puntos ----------

    [Fact]
    public async Task Puntos_Sumar_RegistraElMovimientoSinPedido()
    {
        GivenCustomer(Cli(100));

        await Create().AdjustPointsAsync(1, new PointsRequest(50));

        _store.Verify(s => s.AddPointsAsync(1, null, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Puntos_RestarDentroDelSaldo_Funciona()
    {
        GivenCustomer(Cli(100));

        await Create().AdjustPointsAsync(1, new PointsRequest(-100));

        _store.Verify(s => s.AddPointsAsync(1, null, -100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Puntos_RestarMasQueElSaldo_LanzaReglaDeNegocio()
    {
        GivenCustomer(Cli(100));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().AdjustPointsAsync(1, new PointsRequest(-101)));

        _store.Verify(s => s.AddPointsAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Puntos_Cero_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().AdjustPointsAsync(1, new PointsRequest(0)));
    }

    [Fact]
    public async Task Puntos_ClienteInexistente_LanzaNotFound()
    {
        GivenCustomer(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().AdjustPointsAsync(1, new PointsRequest(10)));
    }

    [Fact]
    public async Task Puntos_ClienteDesactivado_LanzaReglaDeNegocio()
    {
        GivenCustomer(Cli(activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().AdjustPointsAsync(1, new PointsRequest(10)));
    }

    // ---------- Historial y consulta ----------

    [Fact]
    public async Task Historial_ReuneElPerfilLosPedidosYLosPuntos()
    {
        GivenCustomer(Cli());
        _store.Setup(s => s.GetOrdersAsync(1, 20, It.IsAny<CancellationToken>()))
              .ReturnsAsync((IReadOnlyList<CustomerOrderDto>)[new CustomerOrderDto(5, "MOSTRADOR", "ENTREGADO", 36000m, DateTime.UtcNow)]);
        _store.Setup(s => s.GetPointsAsync(1, 20, It.IsAny<CancellationToken>()))
              .ReturnsAsync((IReadOnlyList<PointsMovementDto>)[new PointsMovementDto(1, 5, 36, DateTime.UtcNow)]);

        var result = await Create().GetHistoryAsync(1);

        Assert.Equal("Luis", result.Cliente.Nombre);
        Assert.Equal(5, Assert.Single(result.Pedidos).PedidoId);
        Assert.Equal(36, Assert.Single(result.Puntos).Puntos);
    }

    [Fact]
    public async Task Historial_ClienteInexistente_LanzaNotFound()
    {
        GivenCustomer(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetHistoryAsync(1));
    }

    [Fact]
    public async Task Listar_LimpiaElTextoDeBusqueda()
    {
        _store.Setup(s => s.ListAsync(It.IsAny<CustomerFilter>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((IReadOnlyList<CustomerDto>)[]);

        await Create().ListAsync(new CustomerFilter("  luis "));
        await Create().ListAsync(new CustomerFilter("   "));

        _store.Verify(s => s.ListAsync(It.Is<CustomerFilter>(f => f.Texto == "luis"), It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.ListAsync(It.Is<CustomerFilter>(f => f.Texto == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_ConPaginacionInvalida_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListAsync(new CustomerFilter(null, false, 0, 50)));
    }

    [Fact]
    public async Task Consultar_ClienteInexistente_LanzaNotFound()
    {
        GivenCustomer(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(1));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new CustomerService(null!, _tx));
        Assert.Throws<ArgumentNullException>(() => new CustomerService(_store.Object, null!));
    }
}
