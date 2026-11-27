using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OrderSync.Web.Security;
using OrderSync.Web.Services;

namespace OrderSync.Web.Controllers;

public sealed record GestionField(
    string Name,
    string Label,
    string Type = "text",
    bool Required = false,
    string? Step = null
);

public sealed class GestionController : Controller
{
    private readonly OrderSyncApiClient _api;

    public GestionController(OrderSyncApiClient api)
    {
        _api = api;
    }

    private string? Token => HttpContext.Session.GetString("Token");
    private string Role => HttpContext.Session.GetString("Rol") ?? string.Empty;

    private sealed record Module(
        string Title,
        string Endpoint,
        string Id,
        string[] Roles,
        GestionField[] Fields,
        string? Deactivate = null,
        string? Activate = null,
        bool CreateOnly = false
    );

    private static readonly Dictionary<string, Module> Modules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Clientes"] = new(
            "Clientes",
            "api/clientes",
            "clienteId",
            ["Cajero", "Supervisor", "Gerencia", "Administrador del Sistema"],
            [
                new("nombre", "Nombre", "text", true),
                new("telefono", "Teléfono"),
                new("email", "Correo", "email"),
                new("direccion", "Dirección")
            ],
            "desactivar",
            "activar"
        ),
        ["Promociones"] = new(
            "Promociones",
            "api/promociones",
            "promocionId",
            ["Gerencia", "Administrador del Sistema"],
            [
                new("nombre", "Nombre", "text", true),
                new("porcentajeDto", "Descuento (%)", "number", true, "0.01"),
                new("puntosMinimos", "Puntos mínimos", "number"),
                new("vigenteDesde", "Vigente desde", "date", true),
                new("vigenteHasta", "Vigente hasta", "date")
            ],
            "desactivar",
            "activar"
        ),
        ["Ingredientes"] = new(
            "Ingredientes",
            "api/inventario/ingredientes",
            "ingredienteId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [
                new("nombre", "Nombre", "text", true),
                new("unidadMedida", "Unidad de medida", "text", true),
                new("stockMinimo", "Stock mínimo", "number", true, "0.01"),
                new("stockInicial", "Stock inicial", "number", true, "0.01")
            ],
            "desactivar",
            "activar"
        ),
        ["Empleados"] = new(
            "Empleados",
            "api/empleados",
            "empleadoId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [
                new("documento", "Documento", "text", true),
                new("nombre", "Nombre", "text", true),
                new("areaCargo", "Área / cargo", "text", true),
                new("tipoContrato", "Tipo de contrato"),
                new("tarifaHora", "Tarifa por hora", "number", true, "0.01"),
                new("salarioMensual", "Salario mensual", "number", false, "0.01"),
                new("fechaIngreso", "Fecha de ingreso", "date")
            ],
            "desactivar",
            "activar"
        ),
        ["Proveedores"] = new(
            "Proveedores",
            "api/proveedores",
            "proveedorId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [
                new("nombre", "Nombre", "text", true),
                new("contacto", "Contacto", "text", true),
                new("telefono", "Teléfono"),
                new("email", "Correo", "email")
            ],
            "desactivar",
            "activar"
        ),
        ["Turnos"] = new(
            "Turnos",
            "api/turnos",
            "turnoId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [
                new("empleadoId", "ID empleado", "number", true),
                new("inicio", "Inicio", "datetime-local", true),
                new("fin", "Fin", "datetime-local", true)
            ]
        ),
        ["Productos"] = new(
            "Productos",
            "api/productos",
            "productoId",
            ["Cajero", "Supervisor", "Gerencia", "Administrador del Sistema"],
            [
                new("nombre", "Nombre", "text", true),
                new("categoria", "Categoría"),
                new("precio", "Precio", "number", true, "0.01")
            ],
            "desactivar",
            "activar"
        ),
        ["Usuarios"] = new(
            "Usuarios",
            "api/usuarios",
            "usuarioId",
            ["Administrador del Sistema"],
            [
                new("nombre", "Nombre", "text", true),
                new("email", "Correo", "email", true),
                new("password", "Contraseña", "password", true),
                new("rolId", "ID del rol", "number", true)
            ],
            CreateOnly: true
        ),
        ["Facturas"] = new(
            "Facturas",
            "api/facturacion/facturas",
            "facturaId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [],
            CreateOnly: true
        ),
        ["Compras"] = new(
            "Órdenes de compra",
            "api/ordenes-compra",
            "ordenCompraId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [],
            CreateOnly: true
        ),
        ["Asistencia"] = new(
            "Asistencia",
            "api/asistencia",
            "asistenciaId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [],
            CreateOnly: true
        ),
        ["Movimientos"] = new(
            "Movimientos de inventario",
            "api/inventario/movimientos",
            "movimientoId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [],
            CreateOnly: true
        ),
        ["Alertas"] = new(
            "Alertas de inventario",
            "api/inventario/alertas?estado=todas",
            "alertaId",
            ["Supervisor", "Gerencia", "Administrador del Sistema"],
            [],
            CreateOnly: true
        )
    };

    private bool CanWrite(string modulo) =>
        RoleAccess.IsAdministrator(Role) || (modulo switch
        {
            "Clientes" => Role is "Cajero" or "Gerencia",
            "Promociones" => Role == "Gerencia",
            "Ingredientes" => false,
            "Empleados" => Role == "Supervisor",
            "Proveedores" => false,
            "Turnos" => Role == "Supervisor",
            "Productos" => Role == "Cajero",
            "Usuarios" => RoleAccess.IsAdministrator(Role),
            _ => false
        });

    private Module? Resolve(string modulo)
    {
        if (!Modules.TryGetValue(modulo, out var config))
            return null;

        if (!RoleAccess.Allows(Role, config.Roles))
            return null;

        return config;
    }

    public async Task<IActionResult> Index(string modulo)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        var config = Resolve(modulo);
        if (config is null)
            return Forbid();

        try
        {
            var path = config.Endpoint;
            if (config.Deactivate is not null)
                path += "?incluirInactivos=true";

            var data = await _api.GetAsync<JsonElement>(path, Token);

            ViewBag.Module = config.Title;
            ViewBag.ModuleKey = modulo;
            ViewBag.IdField = config.Id;

            if (modulo == "Usuarios")
            {
                ViewBag.Roles = await _api.GetAsync<JsonElement>("api/roles", Token);
            }

            ViewBag.CanEdit = !config.CreateOnly && CanWrite(modulo);
            ViewBag.CanCreate = modulo switch
            {
                "Usuarios" => RoleAccess.IsAdministrator(Role),
                "Clientes" or "Promociones" or "Ingredientes" or "Empleados"
                    or "Proveedores" or "Turnos" or "Productos" => CanWrite(modulo),
                _ => false
            };
            ViewBag.CanDeactivate = (bool)ViewBag.CanEdit && config.Deactivate is not null;

            return View(data);
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return View(JsonDocument.Parse("[]").RootElement.Clone());
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "No fue posible conectar con la API.";
            return View(JsonDocument.Parse("[]").RootElement.Clone());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Nuevo(string modulo)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        var config = Resolve(modulo);
        if (config is null)
            return Forbid();

        if (config.CreateOnly && modulo != "Usuarios")
            return Forbid();

        if (modulo == "Usuarios")
        {
            ViewBag.Roles = await _api.GetAsync<JsonElement>("api/roles", Token);
        }

        return FormView(modulo, config, null);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(string modulo, int id)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        var config = Resolve(modulo);
        if (config is null)
            return Forbid();

        if (config.CreateOnly || !CanWrite(modulo))
            return Forbid();

        try
        {
            var item = await _api.GetAsync<JsonElement>($"{config.Endpoint}/{id}", Token);
            if (item.ValueKind == JsonValueKind.Undefined)
                return NotFound();

            return FormView(modulo, config, item);
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index), new { modulo });
        }
    }

    private IActionResult FormView(string modulo, Module config, JsonElement? item)
    {
        ViewBag.Module = config.Title;
        ViewBag.ModuleKey = modulo;
        ViewBag.ItemId = item.HasValue && item.Value.TryGetProperty(config.Id, out var id)
            ? id.ToString()
            : null;

        var fields = config.Fields
            .Where(field => !(modulo == "Ingredientes"
                && item.HasValue
                && field.Name == "stockInicial"))
            .ToArray();

        ViewBag.Fields = fields;

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            string? value = null;

            if (item.HasValue
                && item.Value.TryGetProperty(field.Name, out var property)
                && property.ValueKind != JsonValueKind.Null)
            {
                value = property.ToString();

                if (field.Type == "date" && value.Length >= 10)
                    value = value[..10];

                if (field.Type == "datetime-local"
                    && DateTime.TryParse(value, out var dateTime))
                {
                    value = dateTime.ToString("yyyy-MM-ddTHH:mm");
                }
            }

            values[field.Name] = value;
        }

        ViewBag.Values = values;
        return View("Form");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(
        string modulo,
        int? id,
        Dictionary<string, string?> values)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        var config = Resolve(modulo);
        if (config is null || !CanWrite(modulo))
            return Forbid();

        if (config.CreateOnly && modulo != "Usuarios")
            return Forbid();

        if (config.CreateOnly && id.HasValue)
            return Forbid();

        var fields = config.Fields
            .Where(field => !(modulo == "Ingredientes"
                && id.HasValue
                && field.Name == "stockInicial"))
            .ToArray();

        var payload = new Dictionary<string, object?>();

        foreach (var field in fields)
        {
            values.TryGetValue(field.Name, out var raw);

            if (string.IsNullOrWhiteSpace(raw))
            {
                payload[field.Name] = null;
                continue;
            }

            if (field.Type == "number")
            {
                var parsed = decimal.TryParse(
                    raw,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var number
                );

                if (!parsed)
                {
                    parsed = decimal.TryParse(
                        raw,
                        NumberStyles.Any,
                        CultureInfo.GetCultureInfo("es-CO"),
                        out number
                    );
                }

                if (!parsed)
                {
                    ModelState.AddModelError(
                        field.Name,
                        $"El campo {field.Label} debe ser numérico."
                    );
                }

                payload[field.Name] = number;
            }
            else
            {
                payload[field.Name] = raw;
            }
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Revisa los valores ingresados.";
            return FormView(modulo, config, null);
        }

        try
        {
            var json = JsonSerializer.SerializeToElement(payload);

            if (id.HasValue)
            {
                await _api.PutAsync<JsonElement, JsonElement>(
                    $"{config.Endpoint}/{id.Value}",
                    json,
                    Token
                );
            }
            else
            {
                await _api.PostAsync<JsonElement, JsonElement>(
                    config.Endpoint,
                    json,
                    Token
                );
            }

            TempData["Ok"] = $"{config.Title}: cambios guardados correctamente.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index), new { modulo });
        }

        return RedirectToAction(nameof(Index), new { modulo });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
        string modulo,
        int id,
        bool activar = false)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        var config = Resolve(modulo);
        if (config is null || config.Deactivate is null || !CanWrite(modulo))
            return Forbid();

        try
        {
            var action = activar ? config.Activate : config.Deactivate;
            await _api.PostNoContentAsync($"{config.Endpoint}/{id}/{action}", Token);
            TempData["Ok"] = "Estado actualizado.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { modulo });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarRol(int id, int rolId)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.IsAdministrator(Role))
            return Forbid();

        try
        {
            var body = JsonSerializer.SerializeToElement(new { rolId });
            await _api.PutAsync<JsonElement, JsonElement>(
                $"api/usuarios/{id}/rol",
                body,
                Token
            );
            TempData["Ok"] = "Rol actualizado.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { modulo = "Usuarios" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarTurno(int id)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor"))
            return Forbid();

        try
        {
            await _api.PostNoContentAsync($"api/turnos/{id}/cancelar", Token);
            TempData["Ok"] = "Turno cancelado.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { modulo = "Turnos" });
    }
}
