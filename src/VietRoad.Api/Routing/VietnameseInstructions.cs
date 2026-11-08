namespace VietRoad.Api.Routing;

/// <summary>
/// Doi 1 buoc cua OSRM (type + modifier + ten duong) thanh cau tieng Viet.
/// OSRM chi tra ve du lieu tho, vd { type: "turn", modifier: "left", name: "Phố Huế" };
/// lop nay ghep thanh "Rẽ trái vào Phố Huế".
/// Ham thuan (khong goi mang, khong database) -> de viet unit test.
/// Danh sach type/modifier: https://project-osrm.org/docs/v5.24.0/api/#stepmaneuver-object
/// </summary>
public static class VietnameseInstructions
{
    public static string Build(string type, string? modifier, string? name, int? exit = null)
    {
        var into = string.IsNullOrWhiteSpace(name) ? "" : $" vào {name}";

        return type switch
        {
            "depart" => string.IsNullOrWhiteSpace(name) ? "Bắt đầu đi" : $"Đi theo {name}",

            "arrive" => modifier switch
            {
                "left" or "slight left" or "sharp left" => "Đã đến nơi, điểm đến ở bên trái",
                "right" or "slight right" or "sharp right" => "Đã đến nơi, điểm đến ở bên phải",
                _ => "Đã đến nơi",
            },

            "merge" => $"Nhập làn{into}",

            "on ramp" => $"Đi vào đường dẫn{Side(modifier)}{into}",
            "off ramp" => $"Rẽ ra lối{Side(modifier)}{into}",

            "fork" => modifier is "straight" or null
                ? $"Đi theo nhánh giữa{into}"
                : $"Đi theo nhánh{Side(modifier)}{into}",

            "roundabout" or "rotary" => exit is > 0
                ? $"Vào vòng xuyến, đi ra ở lối ra {Ordinal(exit.Value)}{into}"
                : $"Vào vòng xuyến{into}",

            "exit roundabout" or "exit rotary" => $"Ra khỏi vòng xuyến{into}",

            // "new name" / "continue" di thang: chi la doi ten duong.
            "new name" or "continue" when IsStraight(modifier) =>
                string.IsNullOrWhiteSpace(name) ? "Đi tiếp" : $"Đi tiếp{into}",

            // turn, end of road, roundabout turn, continue co re, notification...
            _ => $"{Turn(modifier)}{into}",
        };
    }

    /// <summary>
    /// Buoc nay co dang doc khong? Bo cac buoc "di thang, chi doi ten duong" de khong noi qua nhieu
    /// (trong pho, cu vai tram met lai doi ten duong 1 lan).
    /// </summary>
    public static bool IsWorthAnnouncing(string type, string? modifier) => type switch
    {
        "new name" or "continue" or "notification" or "use lane" => !IsStraight(modifier),
        _ => true,
    };

    private static bool IsStraight(string? modifier) => modifier is null or "straight";

    private static string Turn(string? modifier) => modifier switch
    {
        "uturn" => "Quay đầu",
        "sharp right" => "Rẽ gắt sang phải",
        "right" => "Rẽ phải",
        "slight right" => "Chếch sang phải",
        "slight left" => "Chếch sang trái",
        "left" => "Rẽ trái",
        "sharp left" => "Rẽ gắt sang trái",
        _ => "Đi thẳng",
    };

    private static string Side(string? modifier) =>
        modifier?.Contains("left") == true ? " bên trái"
        : modifier?.Contains("right") == true ? " bên phải"
        : "";

    private static string Ordinal(int n) => n switch
    {
        1 => "thứ nhất",
        2 => "thứ hai",
        3 => "thứ ba",
        4 => "thứ tư",
        _ => $"thứ {n}",
    };
}
