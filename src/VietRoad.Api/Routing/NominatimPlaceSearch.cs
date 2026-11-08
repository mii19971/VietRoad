using System.Globalization;
using System.Text.Json.Serialization;

namespace VietRoad.Api.Routing;

/// <summary>Tim dia diem theo ten / dia chi.</summary>
public interface IPlaceSearch
{
    /// <param name="nearLat">Vi tri hien tai (neu co): uu tien ket qua gan day.</param>
    Task<IReadOnlyList<Place>> SearchAsync(string query, double? nearLat, double? nearLng, CancellationToken ct);
}

/// <summary>
/// Goi Nominatim (may tim kiem dia chi cua OpenStreetMap).
/// May chu cong cong cho phep toi da 1 request/giay -> xep hang cac request (static: dung chung cho moi instance).
/// </summary>
public class NominatimPlaceSearch(HttpClient http, ILogger<NominatimPlaceSearch> logger) : IPlaceSearch
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastCall = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<Place>> SearchAsync(
        string query, double? nearLat, double? nearLng, CancellationToken ct)
    {
        var url = $"search?format=jsonv2&countrycodes=vn&limit=8&accept-language=vi&q={Uri.EscapeDataString(query)}";
        if (nearLat is { } lat && nearLng is { } lng)
        {
            // viewbox = khung ~50 km quanh xe; bounded=0: chi UU TIEN, van tim noi khac neu can.
            url += string.Format(CultureInfo.InvariantCulture,
                "&viewbox={0},{1},{2},{3}&bounded=0", lng - 0.5, lat + 0.5, lng + 0.5, lat - 0.5);
        }

        List<NominatimItem>? items;
        await Gate.WaitAsync(ct);
        try
        {
            var wait = _lastCall.AddSeconds(1) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _lastCall = DateTimeOffset.UtcNow;

            items = await http.GetFromJsonAsync<List<NominatimItem>>(url, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Khong goi duoc Nominatim");
            throw new RoutingUnavailableException("Khong ket noi duoc dich vu tim dia diem", ex);
        }
        finally
        {
            Gate.Release();
        }

        return (items ?? []).Select(ToPlace).OfType<Place>().ToList();
    }

    /// <summary>Doi 1 ket qua Nominatim sang Place. Ten rong -> lay phan dau cua dia chi.</summary>
    public static Place? ToPlace(NominatimItem i)
    {
        if (!double.TryParse(i.Lat, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(i.Lon, CultureInfo.InvariantCulture, out var lng))
            return null;

        var address = i.DisplayName ?? "";
        var name = string.IsNullOrWhiteSpace(i.Name) ? address.Split(',')[0].Trim() : i.Name;
        return new Place(name, address, lat, lng);
    }
}

// Nominatim tra lat/lon dang chuoi.
public record NominatimItem(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("lat")] string Lat,
    [property: JsonPropertyName("lon")] string Lon);
