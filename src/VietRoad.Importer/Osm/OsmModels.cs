namespace VietRoad.Importer.Osm;

// Cau truc JSON ma Overpass API tra ve (chi lay cac truong can dung).
// Vi du 1 phan tu:
//   { "type": "node", "id": 123, "lat": 21.03, "lon": 105.85,
//     "tags": { "highway": "speed_camera", "maxspeed": "50" } }

public record OsmResponse(List<OsmElement> Elements);

public record OsmElement(
    string Type,                          // "node" | "way" | "relation"
    long Id,
    double? Lat,                          // chi node moi co toa do
    double? Lon,
    Dictionary<string, string>? Tags,     // cac cap key=value mo ta doi tuong
    List<OsmMember>? Members);            // chi relation moi co thanh vien

/// <summary>Thanh vien cua 1 relation, vd camera (role "device") cua 1 vung kiem soat toc do.</summary>
public record OsmMember(string Type, long Ref, string Role);

/// <summary>Ket qua sau khi chuyen tu OSM sang dang cua VietRoad, chua ghi vao database.</summary>
public record ImportedAlert(
    string ExternalId,
    string Type,
    string Name,
    double Lat,
    double Lng,
    int? SpeedLimit,
    double? Direction);
