using OrderSync.Application.Inventario;
using OrderSync.Application.Nomina;
using OrderSync.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// Servicios de Application
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<IInventoryDeductionPlanner, InventoryDeductionPlanner>();
builder.Services.AddScoped<OrderConfirmationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();