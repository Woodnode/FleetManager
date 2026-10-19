using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoNexusMobile.Core.Api;

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

/// <summary>
/// Client de l'API AutoNexus (préfixe /api/v1). Chaque méthode correspond à un appel du front web
/// (client/src/api), plus le point d'accès « mes interventions » propre au mobile. Les erreurs
/// remontent en <see cref="ApiException"/> avec le message à afficher.
/// </summary>
public sealed class FleetApi
{
    private readonly HttpClient _http;

    public FleetApi(HttpClient http) => _http = http;

    // ── Tableau de bord ───────────────────────────────────────────────────────

    public Task<DashboardSummaryDto> GetDashboardAsync(CancellationToken ct = default) =>
        GetAsync<DashboardSummaryDto>("dashboard/summary", ct);

    // ── Véhicules ─────────────────────────────────────────────────────────────

    public Task<PagedResult<VehicleDto>> GetVehiclesAsync(
        int page = 1, int pageSize = 20, string? search = null, VehicleStatus? status = null, CancellationToken ct = default) =>
        GetAsync<PagedResult<VehicleDto>>(Query("vehicles",
            ("page", page.ToString(CultureInfo.InvariantCulture)),
            ("pageSize", pageSize.ToString(CultureInfo.InvariantCulture)),
            ("search", string.IsNullOrWhiteSpace(search) ? null : search.Trim()),
            ("status", status?.ToString())), ct);

    public Task<VehicleDto> GetVehicleAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<VehicleDto>($"vehicles/{id}", ct);

    /// <summary>Véhicule dont le NIV est exactement <paramref name="vin"/>, ou null (recherche puis filtre exact).</summary>
    public async Task<VehicleDto?> FindVehicleByVinAsync(string vin, CancellationToken ct = default)
    {
        var page = await GetVehiclesAsync(1, 20, vin, null, ct);
        return page.Items.FirstOrDefault(v => string.Equals(v.Vin, vin, StringComparison.OrdinalIgnoreCase));
    }

    public Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken ct = default) =>
        SendAsync<VehicleDto>(HttpMethod.Post, "vehicles", request, ct);

    public Task<VehicleDto> UpdateVehicleAsync(Guid id, UpdateVehicleRequest request, CancellationToken ct = default) =>
        SendAsync<VehicleDto>(HttpMethod.Put, $"vehicles/{id}", request, ct);

    public Task<VehicleDto> ChangeVehicleStatusAsync(Guid id, VehicleStatus status, CancellationToken ct = default) =>
        SendAsync<VehicleDto>(HttpMethod.Patch, $"vehicles/{id}/status", new ChangeVehicleStatusRequest(status), ct);

    public Task DeleteVehicleAsync(Guid id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"vehicles/{id}", null, ct);

    // ── Archives ──────────────────────────────────────────────────────────────

    public Task<PagedResult<ArchivedVehicleDto>> GetArchivedVehiclesAsync(
        int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default) =>
        GetAsync<PagedResult<ArchivedVehicleDto>>(Query("vehicles/archived",
            ("page", page.ToString(CultureInfo.InvariantCulture)),
            ("pageSize", pageSize.ToString(CultureInfo.InvariantCulture)),
            ("search", string.IsNullOrWhiteSpace(search) ? null : search.Trim())), ct);

    public Task<ArchivedVehicleHistoryDto> GetArchivedHistoryAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<ArchivedVehicleHistoryDto>($"vehicles/archived/{id}", ct);

    public Task<VehicleDto> RestoreVehicleAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<VehicleDto>(HttpMethod.Post, $"vehicles/archived/{id}/restore", null, ct);

    // ── Interventions ─────────────────────────────────────────────────────────

    public Task<PagedResult<InterventionDto>> GetInterventionsAsync(
        int page = 1, int pageSize = 20, InterventionStatus? status = null, InterventionType? type = null, CancellationToken ct = default) =>
        GetAsync<PagedResult<InterventionDto>>(Query("interventions",
            ("page", page.ToString(CultureInfo.InvariantCulture)),
            ("pageSize", pageSize.ToString(CultureInfo.InvariantCulture)),
            ("status", status?.ToString()),
            ("type", type?.ToString())), ct);

    public Task<InterventionDto> GetInterventionAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<InterventionDto>($"interventions/{id}", ct);

    public Task<IReadOnlyList<InterventionDto>> GetVehicleInterventionsAsync(Guid vehicleId, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<InterventionDto>>($"interventions/vehicle/{vehicleId}", ct);

    /// <summary>Journée du technicien connecté (bornes en heure murale, comme les dates stockées).</summary>
    public Task<IReadOnlyList<InterventionDto>> GetMyInterventionsAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<InterventionDto>>(Query("interventions/mine",
            ("from", WallClock(from)),
            ("to", WallClock(to))), ct);

    public Task<InterventionDto> CreateInterventionAsync(CreateInterventionRequest request, CancellationToken ct = default) =>
        SendAsync<InterventionDto>(HttpMethod.Post, "interventions", request, ct);

    public Task<InterventionDto> ChangeInterventionStatusAsync(
        Guid id, InterventionStatus status, string? comment, int? mileage = null, CancellationToken ct = default) =>
        SendAsync<InterventionDto>(HttpMethod.Patch, $"interventions/{id}/status",
            new ChangeInterventionStatusRequest(status, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), mileage), ct);

    // ── Enseignes et techniciens ──────────────────────────────────────────────

    public Task<IReadOnlyList<StoreDto>> GetStoresAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<StoreDto>>("stores", ct);

    public Task<StoreDto> CreateStoreAsync(StoreRequest request, CancellationToken ct = default) =>
        SendAsync<StoreDto>(HttpMethod.Post, "stores", request, ct);

    public Task<StoreDto> UpdateStoreAsync(Guid id, StoreRequest request, CancellationToken ct = default) =>
        SendAsync<StoreDto>(HttpMethod.Put, $"stores/{id}", request, ct);

    public Task DeleteStoreAsync(Guid id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"stores/{id}", null, ct);

    public Task<IReadOnlyList<TechnicianDto>> GetTechniciansAsync(Guid storeId, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<TechnicianDto>>($"users/technicians/{storeId}", ct);

    // ── Transport ─────────────────────────────────────────────────────────────

    internal static string WallClock(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    internal static string Query(string path, params (string Key, string? Value)[] parameters)
    {
        var pairs = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}")
            .ToList();
        return pairs.Count == 0 ? path : $"{path}?{string.Join('&', pairs)}";
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await SendRawAsync(new HttpRequestMessage(HttpMethod.Get, path), ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await SendRawAsync(Build(method, path, body), ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await SendRawAsync(Build(method, path, body), ct);
    }

    private static HttpRequestMessage Build(HttpMethod method, string path, object? body) =>
        new(method, path) { Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: ApiJson.Options) };

    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            using (request)
                response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new ApiException(0, "Serveur injoignable. Vérifiez votre connexion et réessayez.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(HttpStatusCode.RequestTimeout, "Le serveur met trop de temps à répondre. Réessayez.");
        }

        if (response.IsSuccessStatusCode)
            return response;

        var content = await response.Content.ReadAsStringAsync(ct);
        response.Dispose();
        throw ApiException.FromResponse(response.StatusCode, content);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, ct)
        ?? throw new ApiException(response.StatusCode, "Réponse du serveur vide.");
}
