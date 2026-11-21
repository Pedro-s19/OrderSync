using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Inventario;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Receta de un producto (qué ingredientes descuenta al venderse). Pertenece al módulo Inventario.</summary>
[ApiController]
[Route("api/productos/{productoId:int}/receta")]
[AutorizarModulo("INVENTARIO", PermissionLevel.Lectura)]
public sealed class RecetasController : ControllerBase
{
    private readonly RecipeService _recipes;

    public RecetasController(RecipeService recipes) => _recipes = recipes;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecipeLineDto>>> Get(int productoId, CancellationToken ct)
        => Ok(await _recipes.GetAsync(productoId, ct));

    /// <summary>Reemplaza la receta completa del producto.</summary>
    [HttpPut]
    [AutorizarModulo("INVENTARIO", PermissionLevel.Total)]
    public async Task<ActionResult<IReadOnlyList<RecipeLineDto>>> Replace(
        int productoId, [FromBody] RecipeRequest request, CancellationToken ct)
        => Ok(await _recipes.ReplaceAsync(productoId, request, ct));
}
