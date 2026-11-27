
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderSync.Web.Services;

public sealed class OrderSyncApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public OrderSyncApiClient(HttpClient http)
    {
        _http = http;
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string? token)
    {
        var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    public async Task<T?> GetAsync<T>(string path, string? token)
    {
        using var request = CreateRequest(HttpMethod.Get, path, token);
        using var response = await _http.SendAsync(request);

        await EnsureSuccess(response);

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        string? token)
    {
        using var request = CreateRequest(HttpMethod.Post, path, token);

        request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await _http.SendAsync(request);

        await EnsureSuccess(response);

        var json = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<TResponse>(json, JsonOptions);
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        string? token)
    {
        using var request = CreateRequest(HttpMethod.Put, path, token);

        request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await _http.SendAsync(request);

        await EnsureSuccess(response);

        var json = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<TResponse>(json, JsonOptions);
    }

    public async Task PostNoContentAsync(string path, string? token)
    {
        using var request = CreateRequest(HttpMethod.Post, path, token);
        using var response = await _http.SendAsync(request);

        await EnsureSuccess(response);
    }

    public async Task<LoginResultDto?> LoginAsync(LoginDto login)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/auth/login")
        {
            Content = JsonContent.Create(login, options: JsonOptions)
        };

        using var response = await _http.SendAsync(request);

        // Mensaje genérico para credenciales incorrectas o cuenta inactiva.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ApiException(
                "Correo o contraseña incorrectos, o cuenta inactiva.",
                401);
        }

        await EnsureSuccess(response);

        return await response.Content.ReadFromJsonAsync<LoginResultDto>(
            JsonOptions);
    }

    // Recuperación de contraseña: solicitar código por correo.
    public async Task RequestPasswordRecoveryAsync(string email)
    {
        await PostAsync<RequestRecoveryCodeDto, ApiMessageDto>(
            "api/PasswordRecovery/request",
            new RequestRecoveryCodeDto(email),
            null);
    }

    // Recuperación de contraseña: establecer una nueva contraseña.
    public async Task ResetPasswordAsync(
        string email,
        string code,
        string newPassword)
    {
        await PostAsync<ResetPasswordDto, ApiMessageDto>(
            "api/PasswordRecovery/reset",
            new ResetPasswordDto(email, code, newPassword),
            null);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();

        string message = "No fue posible completar la operación.";

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String)
            {
                message = detail.GetString() ?? message;
            }
            else if (root.TryGetProperty("title", out var title) &&
                     title.ValueKind == JsonValueKind.String)
            {
                message = title.GetString() ?? message;
            }
            else if (root.TryGetProperty("message", out var msg) &&
                     msg.ValueKind == JsonValueKind.String)
            {
                message = msg.GetString() ?? message;
            }
        }
        catch (JsonException)
        {
            // No mostramos información técnica del servidor.
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ApiException(
                "Tu sesión no es válida o ha expirado. Inicia sesión nuevamente.",
                401);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new ApiException(
                "No tienes permisos para realizar esta operación.",
                403);
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new ApiException(
                "El servidor tuvo un problema. Inténtalo nuevamente más tarde.",
                (int)response.StatusCode);
        }

        throw new ApiException(message, (int)response.StatusCode);
    }
}

public sealed class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed record LoginDto(string Email, string Password, string Rol);

public sealed record LoginResultDto(
    string Token,
    DateTime ExpiraUtc,
    int UsuarioId,
    string Nombre,
    string Rol);

// DTO para recuperación de contraseña.
public sealed record RequestRecoveryCodeDto(string Email);

public sealed record ResetPasswordDto(
    string Email,
    string Code,
    string NewPassword);

public sealed record ApiMessageDto(string Message);

public sealed record ProductDto(
    int ProductoId,
    string Nombre,
    string? Categoria,
    decimal Precio,
    bool Activo);

public sealed record OrderLineDto(int ProductoId, int Cantidad);

public sealed record OrderRequestDto(
    string Canal,
    string? Mesa,
    string? DireccionEntrega,
    string? TelefonoContacto,
    int? ClienteId,
    IReadOnlyList<OrderLineDto> Lineas);

public sealed record OrderSummaryDto(
    int PedidoId,
    string Canal,
    string Estado,
    string? Mesa,
    decimal Total,
    DateTime FechaCreacion,
    int Items);

public sealed record OrderLineDetailDto(
    int DetalleId,
    int ProductoId,
    string Producto,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);

public sealed record OrderDetailDto(
    int PedidoId,
    int? ClienteId,
    int? UsuarioId,
    string Canal,
    string Estado,
    string? Mesa,
    string? DireccionEntrega,
    string? TelefonoContacto,
    decimal Total,
    DateTime FechaCreacion,
    DateTime? FechaEnvioCocina,
    DateTime? FechaListo,
    IReadOnlyList<OrderLineDetailDto> Lineas);

public sealed record StockAlertDto(
    int AlertaId,
    int IngredienteId,
    string Ingrediente,
    string UnidadMedida,
    decimal StockAlMomento,
    decimal StockMinimo,
    string Estado,
    DateTime Fecha,
    DateTime? FechaAtencion);
