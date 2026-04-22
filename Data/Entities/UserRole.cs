namespace ChillTour.Data.Entities;

public class UserRole
{
    public long UserRoleId { get; set; }
    public long UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedAt { get; set; }
    public long? AssignedByUserId { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
