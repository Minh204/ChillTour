namespace ChillTour.Data.Entities;

public class User
{
    public long UserId { get; set; }
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public string? ExternalProvider { get; set; }
    public string? ExternalProviderKey { get; set; }
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = null!;
    public string? PasswordSalt { get; set; }
    public string FullName { get; set; } = null!;
    public byte? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public byte Status { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<UserPromotion> UserPromotions { get; set; } = new List<UserPromotion>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Article> Articles { get; set; } = new List<Article>();
    public ICollection<BookingStatusHistory> BookingStatusChanges { get; set; } = new List<BookingStatusHistory>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
