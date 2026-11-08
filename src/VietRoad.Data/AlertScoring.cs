namespace VietRoad.Data;

/// <summary>
/// Quy tac tinh do tin cay (0..100) cua 1 canh bao. De don gian va de giai thich:
///   diem = diem goc theo nguon + 10 x so xac nhan - 10 x so phu nhan   (gioi han trong 0..100)
/// Duoi <see cref="MinVisible"/> thi API khong tra ve nua (nhung van giu trong database).
///
/// Vi du canh bao do nguoi dung bao: goc 30, nguoi bao tu dong tinh 1 xac nhan -> 40.
///   2 nguoi phu nhan -> 20 (van hien), 3 nguoi phu nhan -> 10 (an).
/// => 1 nguoi xau khong the tu minh an canh bao cua nguoi khac.
/// </summary>
public static class AlertScoring
{
    public const int MinVisible = 20;
    private const int PointsPerVote = 10;

    public static int BaseConfidence(string source) => source switch
    {
        AlertSources.Admin => 90,    // doi van hanh da kiem tra
        AlertSources.Partner => 70,
        AlertSources.Osm => 50,
        _ => 30,                     // nguoi dung bao: chua ai kiem chung
    };

    public static int Compute(string source, int confirmCount, int denyCount) =>
        Math.Clamp(BaseConfidence(source) + PointsPerVote * (confirmCount - denyCount), 0, 100);
}
