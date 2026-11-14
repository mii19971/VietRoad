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

    // Diem nguy hiem tren duong, CHI lay diem nam tren duong o to di (bo duong di bo, duong mon...):
    //  - node.lc(w.roadsLc) : cac node trong tap .lc ma cung la diem cua 1 duong trong .roadsLc
    //  - Duong ngang duong sat: moi loai duong xe chay (ca duong noi bo "service").
    //  - Go giam toc (bump, hump...): bo duong "service" (bai do xe, cay xang... go rat day, doc gay phien).
    //  - Vach son giam toc (rumble_strip): chi tren duong chinh, noi no danh dau nut giao / khuc cua nguy hiem.
    //  - Cuoi cung tra ca cac duong do (id node + toa do) de tinh HUONG con duong tai moi diem.
    private const string VietnamHazardsQuery = """
        [out:json][timeout:240];
        area["ISO3166-1"="VN"][admin_level=2]->.vn;
        node["railway"="level_crossing"](area.vn)->.lc;
        way(bn.lc)["highway"~"^(motorway|trunk|primary|secondary|tertiary|unclassified|residential|living_street|service|road)(_link)?$"]->.roadsLc;
        node.lc(w.roadsLc);
        out body;
        node["traffic_calming"~"^(bump|hump|table|cushion|mini_bumps|yes)$"](area.vn)->.b;
        way(bn.b)["highway"~"^(motorway|trunk|primary|secondary|tertiary|unclassified|residential|living_street|road)(_link)?$"]->.roadsB;
        node.b(w.roadsB);
        out body;
        node["traffic_calming"="rumble_strip"](area.vn)->.rs;
        way(bn.rs)["highway"~"^(motorway|trunk|primary|secondary|tertiary)(_link)?$"]->.roadsRs;
        node.rs(w.roadsRs);
        out body;
        (.roadsLc; .roadsB; .roadsRs;);
        out body geom;
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<OsmResponse> FetchVietnamCamerasAsync(string endpoint, CancellationToken ct = default) =>
        QueryAsync(endpoint, VietnamCamerasQuery, ct);

    /// <summary>Duong ngang duong sat, go / vach giam toc tren duong o to (truy van rieng cho nhe).</summary>
    public Task<OsmResponse> FetchVietnamHazardsAsync(string endpoint, CancellationToken ct = default) =>
        QueryAsync(endpoint, VietnamHazardsQuery, ct);

    // Truong hoc: node, way (vung) va relation. "out center" -> way/relation tra ve 1 diem giua,
    // node tra ve toa do cua no -> tat ca thanh 1 diem de bao "khu vuc truong hoc".
    private const string VietnamSchoolsQuery = """
        [out:json][timeout:180];
        area["ISO3166-1"="VN"][admin_level=2]->.vn;
        nwr["amenity"="school"](area.vn);
        out center;
        """;

    /// <summary>Truong hoc tren ca nuoc (de bao "khu vuc truong hoc, chu y tre em").</summary>
    public Task<OsmResponse> FetchVietnamSchoolsAsync(string endpoint, CancellationToken ct = default) =>
        QueryAsync(endpoint, VietnamSchoolsQuery, ct);

    // Benh vien: node / way / relation -> 1 diem giua. Chi lay amenity=hospital (co cap cuu),
    // KHONG lay "clinic" / "doctors" (phong kham nho, khong co xe cap cuu ra vao nhieu).
    private const string VietnamHospitalsQuery = """
        [out:json][timeout:180];
        area["ISO3166-1"="VN"][admin_level=2]->.vn;
        nwr["amenity"="hospital"](area.vn);
        out center;
        """;

    /// <summary>Benh vien tren ca nuoc (de bao "khu vuc benh vien, chu y xe cap cuu").</summary>
    public Task<OsmResponse> FetchVietnamHospitalsAsync(string endpoint, CancellationToken ct = default) =>
        QueryAsync(endpoint, VietnamHospitalsQuery, ct);

    /// <summary>
    /// Cac loai duong lay gioi han toc do. Tai TUNG LOAI 1 lan: hoi tat ca cung luc qua nang,
    /// may chu Overpass cong cong tra 504.
    /// </summary>
    public static readonly string[] SpeedLimitHighwayGroups =
        ["motorway", "trunk", "primary", "secondary", "tertiary", "motorway_link|trunk_link|primary_link"];

    /// <summary>Cac duong (way) loai highwayRegex co the maxspeed, kem toa do tung diem ("out geom").</summary>
    public Task<OsmResponse> FetchSpeedLimitWaysAsync(string endpoint, string highwayRegex, CancellationToken ct = default) =>
        QueryAsync(endpoint, $$"""
            [out:json][timeout:300];
            area["ISO3166-1"="VN"][admin_level=2]->.vn;
            way["highway"~"^({{highwayRegex}})$"]["maxspeed"](area.vn);
            out tags geom;
            """, ct);

    /// <summary>
    /// Gui truy van; may chu ban (429/502/503/504) hoac het gio cho (Overpass dang qua tai) ->
    /// doi roi thu lai, toi da 4 lan. ct la huy that su cua nguoi dung -> khong thu lai.
    /// </summary>
    private async Task<OsmResponse> QueryAsync(string endpoint, string query, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await QueryOnceAsync(endpoint, query, ct);
            }
            catch (HttpRequestException ex) when (attempt < 4 && (int?)ex.StatusCode is 429 or >= 502 and <= 504)
            {
                await Task.Delay(TimeSpan.FromSeconds(30 * attempt), ct);
            }
            // Het gio cho (timeout cua HttpClient) nem TaskCanceledException ma ct CHUA bi huy.
            catch (TaskCanceledException) when (attempt < 4 && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30 * attempt), ct);
            }
        }
    }

    private async Task<OsmResponse> QueryOnceAsync(string endpoint, string query, CancellationToken ct)
    {
        // Overpass nhan truy van qua form field "data".
        using var content = new FormUrlEncodedContent([new("data", query)]);
        using var response = await http.PostAsync(endpoint, content, ct);
        response.EnsureSuccessStatusCode(); // loi HTTP (4xx/5xx) -> nem exception

        return await response.Content.ReadFromJsonAsync<OsmResponse>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Overpass tra ve du lieu rong.");
    }
}
