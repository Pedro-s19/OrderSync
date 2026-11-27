using OrderSync.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".OrderSync.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddHttpClient<OrderSyncApiClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["OrderSyncApi:BaseUrl"] ?? "http://localhost:5092/");
    client.Timeout = TimeSpan.FromSeconds(20);
});

var app = builder.Build();

// La página de error controlada se utiliza también en desarrollo.
// Los detalles técnicos quedan en el registro del servidor, no en el navegador.
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
