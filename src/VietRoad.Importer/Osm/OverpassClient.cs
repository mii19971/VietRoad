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
    //  - node[man_made=surveillance][...traffic / ALPR] : camera giam sat giao thong
    //  - node[barrier=toll_booth] : tram thu phi (thuong ve moi lan xe 1 diem -> mapper gom lai)
    //  - (...)->.main            : gop tat ca vao 1 tap, dat ten .main
    //  - .main out body;         : tra ve cac doi tuong tren kem tags
    //  - .main >; out skel qt;   : tra them toa do cac node thanh vien cua relation
    //  - way[barrier=toll_booth] ... out tags center : tram thu phi ve thanh vung -> chi can diem giua
    private const string VietnamCamerasQuery = """
        [out:json][timeout:180];
        area["ISO3166-1"="VN"][admin_level=2]->.vn;
        (
          node["highway"="speed_camera"](area.vn);
          relation["type"="enforcement"](area.vn);
          node["man_made"="surveillance"]["surveillance:zone"="traffic"](area.vn);
          node["man_made"="surveillance"]["surveillance"="traffic"](area.vn);
          node["man_made"="surveillance"]["surveillance:type"="ALPR"](area.vn);
          node["barrier"="toll_booth"](area.vn);
        )->.main;
        .main out body;
        .main >;
        out skel qt;
        way["barrier"="toll_booth"](area.vn);
        out tags center;
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
