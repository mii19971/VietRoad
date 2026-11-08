using System.Globalization;
using System.Text.Json.Serialization;

namespace VietRoad.Api.Routing;

/// <summary>Tim dia diem theo ten / dia chi.</summary>
public interface IPlaceSearch
{
    /// <param name="nearLat">Vi tri hien tai (neu co): uu tien ket qua gan day.</param>
    Task<IReadOnlyList<Place>> SearchAsync(string query, double? nearLat, double? nearLng, CancellationToken ct);

    /// <summary>Tra nguoc: toa do -> dia chi ("Phố Đinh Tiên Hoàng, Phường Hoàn Kiếm"). null = khong biet.</summary>
    Task<Place?> ReverseAsync(double lat, double lng, CancellationToken ct);
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

        var items = await CallAsync<List<NominatimItem>>(url, ct);
        return (items ?? []).Select(ToPlace).OfType<Place>().ToList();
    }

    public async Task<Place?> ReverseAsync(double lat, double lng, CancellationToken ct)
    {
        // zoom=17: chinh xac toi muc con duong (khong can so nha).
        var url = string.Format(CultureInfo.InvariantCulture,
            "reverse?format=jsonv2&zoom=17&accept-language=vi&lat={0}&lon={1}", lat, lng);
        var item = await CallAsync<NominatimItem>(url, ct);
        return item is null ? null : ToReversePlace(item);
    }

    /// <summary>Goi Nominatim: xep hang (toi da 1 request/giay), doi loi mang thanh RoutingUnavailableException.</summary>
    private async Task<T?> CallAsync<T>(string url, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var wait = _lastCall.AddSeconds(1) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _lastCall = DateTimeOffset.UtcNow;

            return await http.GetFromJsonAsync<T>(url, ct);
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
    }

    /// <summary>
    /// Ket qua tra nguoc -> ten ngan de hien "Từ: ...": 2 phan dau cua dia chi, bo so nha / ma buu dien.
    /// "15, Phố Đinh Tiên Hoàng, Khu phố cổ, Phường Hoàn Kiếm, Hà Nội, 11024, Việt Nam"
    ///   -> "Phố Đinh Tiên Hoàng, Khu phố cổ" (chi ten duong thi kho biet o khu nao).
    /// </summary>
    public static Place? ToReversePlace(NominatimItem i)
    {
        var place = ToPlace(i);
        if (place is null) return null;

        var parts = place.Address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.All(c => char.IsDigit(c) || c is '/' or '-' or ' '))
            .Take(2);
        var shortName = string.Join(", ", parts);
        return place with { Name = shortName.Length > 0 ? shortName : place.Name };
    }

    /// <summary>Doi 1 ket qua Nominatim sang Place. Ten rong -> lay phan dau cua dia chi.</summary>
    public static Place? ToPlace(NominatimItem i)
    {
        if (i.Lat is null || i.Lon is null ||
            !double.TryParse(i.Lat, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(i.Lon, CultureInfo.InvariantCulture, out var lng))
            return null;

        var address = i.DisplayName ?? "";
        var name = string.IsNullOrWhiteSpace(i.Name) ? address.Split(',')[0].Trim() : i.Name;
        return new Place(name, address, lat, lng);
    }
}

// Nominatim tra lat/lon dang chuoi. Tra nguoc khong ra gi -> {"error": "..."}: lat/lon = null.
public record NominatimItem(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("lat")] string? Lat,
    [property: JsonPropertyName("lon")] string? Lon);
