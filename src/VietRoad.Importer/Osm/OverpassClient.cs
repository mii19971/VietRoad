using System.Net.Http.Json;
using System.Text.Json;

namespace VietRoad.Importer.Osm;

/// <summary>Goi Overpass API (dich vu truy van du lieu OpenStreetMap).</summary>
public class OverpassClient(HttpClient http)
{
    public const string DefaultEndpoint = "https://overpass-api.de/api/interpreter";

    // Ngon ngu Overpass QL:
    //  - area[...]->.vn          : lay vung bien gioi Viet Nam, dat ten la .vn
    //  - node[highway=speed_camera](area.vn) : cac diem camera toc do trong VN
    //  - relation[type=enforcement](area.vn) : cac "vung kiem soat" (toc do, den do...)
    //  - out body;               : tra ve cac doi tuong tren kem tags
    //  - >; out skel qt;         : tra them toa do cac node thanh vien cua relation
    private const string VietnamCamerasQuery = """
        [out:json][timeout:120];
        area["ISO3166-1"="VN"][admin_level=2]->.vn;
        (
          node["highway"="speed_camera"](area.vn);
          relation["type"="enforcement"](area.vn);
        );
        out body;
        >;
        out skel qt;
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OsmResponse> FetchVietnamCamerasAsync(string endpoint, CancellationToken ct = default)
    {
        // Overpass nhan truy van qua form field "data".
        using var content = new FormUrlEncodedContent([new("data", VietnamCamerasQuery)]);
        using var response = await http.PostAsync(endpoint, content, ct);
        response.EnsureSuccessStatusCode(); // loi HTTP (4xx/5xx) -> nem exception

        return await response.Content.ReadFromJsonAsync<OsmResponse>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Overpass tra ve du lieu rong.");
    }
}
