using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using OrderSync.Application.Nomina;
using OrderSync.Infrastructure.Persistence;

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

        // Nómina (HU-04)
        services.AddScoped<IPayrollParametersProvider, PayrollParametersProvider>();
        services.AddScoped<IAttendanceReader, AttendanceReader>();
        services.AddScoped<IPayrollLiquidationStore, PayrollLiquidationStore>();

        // Inventario (HU-05)
        services.AddScoped<IOrderInventoryReader, OrderInventoryReader>();
        services.AddScoped<IInventoryWriter, InventoryWriter>();
        services.AddScoped<IStockAlertReader, StockAlertReader>();

        return services;
    }
}