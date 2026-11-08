namespace VietRoad.Data;

/// <summary>
/// Mot nguoi dung xac nhan "van con" (IsConfirm = true) hoac "khong con" cho 1 canh bao.
/// Moi nguoi chi co 1 phieu cho moi canh bao; bam lai thi doi phieu, khong cong them.
/// </summary>
public class AlertVoteEntity
{
    public Guid Id { get; set; }

    public int AlertId { get; set; }
    public AlertEntity Alert { get; set; } = null!;

    public Guid UserId { get; set; }

    public bool IsConfirm { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
