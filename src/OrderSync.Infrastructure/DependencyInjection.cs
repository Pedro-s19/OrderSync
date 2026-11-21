using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderSync.Application.Common;
using OrderSync.Application.Crm;
using OrderSync.Application.Facturacion;
using OrderSync.Application.Inventario;
using OrderSync.Application.Nomina;
using OrderSync.Application.Pedidos;
using OrderSync.Application.Proveedores;
using OrderSync.Application.Rrhh;
using OrderSync.Application.Seguridad;
using OrderSync.Infrastructure.Persistence;
using OrderSync.Infrastructure.Security;
using OrderSync.Infrastructure.Time;

namespace OrderSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderSyncDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'OrderSyncDb' en appsettings.json.");

        services.AddDbContext<OrderSyncDbContext>(o => o.UseSqlServer(connectionString));

        services.AddScoped<ITransactionRunner, EfTransactionRunner>();
        services.AddSingleton<IClock, BogotaClock>();

        // Nómina (HU-04)
        services.AddScoped<IPayrollParametersProvider, PayrollParametersProvider>();
        services.AddScoped<IAttendanceReader, AttendanceReader>();
        services.AddScoped<IPayrollLiquidationStore, PayrollLiquidationStore>();

        // Inventario (HU-05)
        services.AddScoped<IOrderInventoryReader, OrderInventoryReader>();
        services.AddScoped<IInventoryWriter, InventoryWriter>();
        services.AddScoped<IStockAlertReader, StockAlertReader>();

        // Seguridad (HU-06)
        services.AddSingleton(JwtOptions.FromConfiguration(configuration));
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserAccountReader, UserAccountReader>();
        services.AddScoped<IPermissionReader, PermissionReader>();
        services.AddScoped<IUserAdminStore, UserAdminStore>();

        // Pedidos e Inventario
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductStore, ProductStore>();
        services.AddScoped<IInventoryRestorer, InventoryRestorer>();
        services.AddScoped<IIngredientStore, IngredientStore>();
        services.AddScoped<IRecipeStore, RecipeStore>();
        services.AddScoped<IStockMovementStore, StockMovementStore>();
        services.AddScoped<IStockAlertStore, StockAlertStore>();
        services.AddScoped<IMovementReader, MovementReader>();

        // Proveedores
        services.AddScoped<ISupplierStore, SupplierStore>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();

        // RR. HH.
        services.AddScoped<IEmployeeStore, EmployeeStore>();
        services.AddScoped<IShiftStore, ShiftStore>();
        services.AddScoped<IAttendanceStore, AttendanceStore>();
        services.AddScoped<IEmployeeLinkStore, EmployeeLinkStore>();

        // CRM
        services.AddScoped<ICustomerStore, CustomerStore>();
        services.AddScoped<IPromotionStore, PromotionStore>();

        // Facturación
        services.AddScoped<IInvoiceStore, InvoiceStore>();
        services.AddScoped<IReportReader, ReportReader>();

        return services;
    }
}
