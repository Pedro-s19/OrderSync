
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OrderSync.Web.Services;
using OrderSync.Web.Security;

namespace OrderSync.Web.Controllers;

public sealed class PanelController : Controller
{
    // Cliente encargado de comunicarse con la API de OrderSync.
    private readonly OrderSyncApiClient _api;

    public PanelController(OrderSyncApiClient api)
    {
        _api = api;
    }

    // Recupera el token de autenticación guardado en la sesión.
    private string? Token => HttpContext.Session.GetString("Token");

    // Recupera el rol del usuario que inició sesión.
    private string Role => HttpContext.Session.GetString("Rol") ?? "";


    // ============================================================
    // REPORTE DE VENTAS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Ventas(DateTime? desde, DateTime? hasta)
    {
        // Comprueba que el usuario haya iniciado sesión.
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        // Solo Supervisor, Gerencia y Administrador pueden entrar
        // desde el frontend. La API también debe autorizar la consulta.
        if (!RoleAccess.Allows(Role, "Supervisor", "Gerencia"))
            return Forbid();

        // Si no se indican fechas, utiliza el primer día del mes
        // y el día actual como intervalo del reporte.
        var start = (desde ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        var end = (hasta ?? DateTime.Today).Date;

        // Envía las fechas a la vista para mostrarlas en el formulario.
        ViewBag.Desde = start.ToString("yyyy-MM-dd");
        ViewBag.Hasta = end.ToString("yyyy-MM-dd");

        try
        {
            // Construye los parámetros que recibirá la API.
            var q = $"?desde={start:yyyy-MM-dd}&hasta={end:yyyy-MM-dd}";

            // Consulta las ventas agrupadas por día.
            ViewBag.Report = await _api.GetAsync<JsonElement>(
                "api/facturacion/reportes/ventas-por-dia" + q,
                Token
            );

            // Consulta los productos más vendidos.
            ViewBag.Top = await _api.GetAsync<JsonElement>(
                "api/facturacion/reportes/productos-mas-vendidos" +
                q + "&orden=cantidad&top=10",
                Token
            );
        }
        catch (ApiException ex)
        {
            // Guarda el error para mostrarlo en la vista.
            TempData["Error"] = ex.Message;
        }

        return View();
    }


    // ============================================================
    // COBRAR PEDIDOS
    // ============================================================

    // Muestra el formulario para registrar un cobro.
    [HttpGet]
    public IActionResult Cobrar()
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        // El Cajero y el Administrador del Sistema pueden registrar cobros.
        if (!RoleAccess.Allows(Role, "Cajero"))
            return Forbid();

        return View();
    }

    // Recibe los datos del formulario y envía el cobro a la API.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cobrar(
        int pedidoId,
        decimal? propina,
        string metodo,
        decimal monto)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Cajero"))
            return Forbid();

        // Comprueba que el pedido y el monto sean válidos.
        if (pedidoId <= 0 || monto <= 0)
        {
            TempData["Error"] = "Indica un pedido y un monto válido.";
            return View();
        }

        // Construye el cuerpo JSON que espera la API.
        var body = JsonSerializer.SerializeToElement(new
        {
            pedidoId,
            propina,
            pagos = new[]
            {
                new { metodo, monto }
            }
        });

        try
        {
            // Envía el cobro a la API.
            await _api.PostAsync<JsonElement, JsonElement>(
                "api/facturacion/cobros",
                body,
                Token
            );

            TempData["Ok"] = "Cobro registrado y factura generada correctamente.";

            return RedirectToAction(nameof(Cobrar));
        }
        catch (ApiException ex)
        {
            // Muestra el error si la API rechaza el cobro.
            TempData["Error"] = ex.Message;
            return View();
        }
    }


    // ============================================================
    // MOVIMIENTOS DE INVENTARIO
    // ============================================================

    // Muestra el formulario para registrar mermas o ajustes.
    [HttpGet]
    public IActionResult Movimiento()
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor"))
            return Forbid();

        return View();
    }

    // Procesa un movimiento de inventario.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Movimiento(
        string tipo,
        int ingredienteId,
        decimal cantidad,
        decimal stockReal,
        string motivo)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor"))
            return Forbid();

        // Verifica que se haya seleccionado un ingrediente
        // y que se haya escrito el motivo del movimiento.
        if (ingredienteId <= 0 || string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Error"] = "Completa el ingrediente y el motivo.";
            return View();
        }

        try
        {
            JsonElement body;
            string endpoint;

            // Si el movimiento es una merma, se registra la cantidad perdida.
            if (tipo == "MERMA")
            {
                if (cantidad <= 0)
                    throw new ApiException(
                        "La cantidad debe ser mayor que cero.",
                        400
                    );

                body = JsonSerializer.SerializeToElement(new
                {
                    ingredienteId,
                    cantidad,
                    motivo
                });

                endpoint = "api/inventario/mermas";
            }
            else
            {
                // En un ajuste se registra el stock real encontrado.
                if (stockReal < 0)
                    throw new ApiException(
                        "El stock real no puede ser negativo.",
                        400
                    );

                body = JsonSerializer.SerializeToElement(new
                {
                    ingredienteId,
                    stockReal,
                    motivo
                });

                endpoint = "api/inventario/ajustes";
            }

            // Envía el movimiento correspondiente a la API.
            await _api.PostAsync<JsonElement, JsonElement>(
                endpoint,
                body,
                Token
            );

            TempData["Ok"] = "Movimiento de inventario registrado.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return View();
    }


    // ============================================================
    // NÓMINA
    // ============================================================

    // Muestra el módulo de nómina y consulta los empleados.
    [HttpGet]
    public async Task<IActionResult> Nomina()
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        // El Supervisor y el Administrador del Sistema pueden acceder a nómina.
        // La API vuelve a validar el permiso antes de ejecutar la operación.
        if (!RoleAccess.Allows(Role, "Supervisor"))
            return Forbid();

        try
        {
            // Consulta los empleados para llenar el formulario de nómina.
            ViewBag.Empleados = await _api.GetAsync<JsonElement>(
                "api/empleados",
                Token
            );
        }
        catch (ApiException ex)
        {
            // Si falla la consulta, informa el error en la vista.
            TempData["Error"] = ex.Message;
        }

        return View();
    }

    // Recibe el año, el mes y el empleado opcional
    // para ejecutar el cálculo de nómina.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nomina(
        int anio,
        int mes,
        int? empleadoId)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        // El Supervisor y el Administrador del Sistema pueden ejecutar el cálculo.
        if (!RoleAccess.Allows(Role, "Supervisor"))
            return Forbid();

        try
        {
            // Construye el JSON con los datos del cálculo.
            var body = JsonSerializer.SerializeToElement(new
            {
                anio,
                mes,
                empleadoId
            });

            // Envía la solicitud al endpoint de nómina de la API.
            ViewBag.Resultado = await _api.PostAsync<JsonElement, JsonElement>(
                "api/nomina/calculate",
                body,
                Token
            );

            TempData["Ok"] = "Cálculo de nómina ejecutado.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        try
        {
            // Vuelve a consultar los empleados para conservar
            // las opciones del formulario después del cálculo.
            ViewBag.Empleados = await _api.GetAsync<JsonElement>(
                "api/empleados",
                Token
            );
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return View();
    }


    // ============================================================
    // CONSULTAR MI ASISTENCIA
    // ============================================================

    // Permite al Cajero y al personal de Cocina consultar
    // su propio historial de asistencia.
    [HttpGet]
    public async Task<IActionResult> MiAsistencia()
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        // Solo estos roles tienen acceso a su asistencia.
        if (Role is not ("Cajero" or "Cocina"))
            return Forbid();

        try
        {
            // Consulta los registros de asistencia del usuario actual.
            return View(
                await _api.GetAsync<List<JsonElement>>(
                    "api/asistencia/mia",
                    Token
                ) ?? []
            );
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;

            // Devuelve una lista vacía si ocurre un error.
            return View(new List<JsonElement>());
        }
    }


    // ============================================================
    // MARCAR ENTRADA O SALIDA
    // ============================================================

    // Registra la entrada o salida del empleado autenticado.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarAsistencia(string accion)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        // Solo Cajero y Cocina pueden marcar su asistencia.
        if (Role is not ("Cajero" or "Cocina"))
            return Forbid();

        try
        {
            // Selecciona el endpoint según la acción recibida.
            string endpoint = accion == "entrada"
                ? "api/asistencia/mia/entrada"
                : "api/asistencia/mia/salida";

            // Envía la solicitud a la API.
            await _api.PostNoContentAsync(endpoint, Token);

            TempData["Ok"] = "Asistencia registrada.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        // Regresa a la pantalla de asistencia.
        return RedirectToAction(nameof(MiAsistencia));
    }


    // ============================================================
    // PROGRAMACIÓN DE TURNOS
    // ============================================================

    // Supervisor y Administrador gestionan turnos; Gerencia consulta.
    [HttpGet]
    public async Task<IActionResult> Turnos(DateTime? desde, DateTime? hasta)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor", "Gerencia"))
            return Forbid();

        var fechaDesde = (desde ?? DateTime.Today).Date;
        var fechaHasta = (hasta ?? DateTime.Today.AddDays(30)).Date;

        if (fechaHasta < fechaDesde)
        {
            TempData["Error"] = "La fecha final no puede ser anterior a la fecha inicial.";
            fechaHasta = fechaDesde.AddDays(30);
        }

        ViewBag.Desde = fechaDesde.ToString("yyyy-MM-dd");
        ViewBag.Hasta = fechaHasta.ToString("yyyy-MM-dd");
        ViewBag.PuedeGestionar = RoleAccess.Allows(Role, "Supervisor");

        try
        {
            var query = $"?desde={fechaDesde:yyyy-MM-dd}&hasta={fechaHasta:yyyy-MM-dd}&pagina=1&tamano=100";
            var turnos = await _api.GetAsync<List<JsonElement>>("api/turnos" + query, Token) ?? [];
            ViewBag.Empleados = await _api.GetAsync<List<JsonElement>>("api/empleados", Token) ?? [];
            return View(turnos);
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            ViewBag.Empleados = new List<JsonElement>();
            return View(new List<JsonElement>());
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ProgramarTurno(int empleadoId, DateTime inicio, DateTime fin)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor"))
            return Forbid();

        if (empleadoId <= 0 || fin <= inicio)
        {
            TempData["Error"] = "Selecciona un empleado y un intervalo de fechas válido.";
            return RedirectToAction(nameof(Turnos));
        }

        try
        {
            await _api.PostAsync<object, JsonElement>(
                "api/turnos",
                new { EmpleadoId = empleadoId, Inicio = inicio, Fin = fin },
                Token);
            TempData["Ok"] = "El turno fue programado correctamente.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Turnos), new { desde = inicio.Date, hasta = inicio.Date.AddDays(30) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarTurno(int turnoId, DateTime inicio, DateTime fin)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor"))
            return Forbid();

        if (turnoId <= 0 || fin <= inicio)
        {
            TempData["Error"] = "El intervalo del turno no es válido.";
            return RedirectToAction(nameof(Turnos));
        }

        try
        {
            await _api.PutAsync<object, JsonElement>(
                $"api/turnos/{turnoId}",
                new { Inicio = inicio, Fin = fin },
                Token);
            TempData["Ok"] = "El horario del turno fue actualizado.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Turnos), new { desde = inicio.Date, hasta = inicio.Date.AddDays(30) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarTurno(int turnoId)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor"))
            return Forbid();

        if (turnoId <= 0)
            return BadRequest();

        try
        {
            await _api.PostNoContentAsync($"api/turnos/{turnoId}/cancelar", Token);
            TempData["Ok"] = "El turno fue cancelado.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Turnos));
    }

}
