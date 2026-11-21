using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OrderSync.Api.Filters;
using OrderSync.Application.Crm;
using OrderSync.Application.Facturacion;
using OrderSync.Application.Inventario;
using OrderSync.Application.Nomina;
using OrderSync.Application.Pedidos;
using OrderSync.Application.Proveedores;
using OrderSync.Application.Rrhh;
using OrderSync.Application.Seguridad;
using OrderSync.Infrastructure;
using OrderSync.Infrastructure.Persistence;
using OrderSync.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>());
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// Autenticación JWT. La clave sale de Jwt:Key (variable de entorno Jwt__Key en Azure).
var jwt = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // se conservan "sub", "role", etc. tal como van en el token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// Seguro por defecto: todo endpoint exige sesión salvo los marcados con [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Servicios de Application
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<IInventoryDeductionPlanner, InventoryDeductionPlanner>();
builder.Services.AddScoped<OrderConfirmationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ModuleAuthorizationService>();
builder.Services.AddScoped<UserAdministrationService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<StockMovementService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<PromotionService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    await DevelopmentSeeder.SeedUsersAsync(app.Services, app.Configuration);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
