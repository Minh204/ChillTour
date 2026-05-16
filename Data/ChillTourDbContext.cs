using ChillTour.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Data;

public class ChillTourDbContext : DbContext
{
    public ChillTourDbContext(DbContextOptions<ChillTourDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourMedia> TourMedia => Set<TourMedia>();
    public DbSet<TourSchedule> TourSchedules => Set<TourSchedule>();
    public DbSet<TourItineraryDay> TourItineraryDays => Set<TourItineraryDay>();
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<TransportationType> TransportationTypes => Set<TransportationType>();
    public DbSet<Transportation> Transportations => Set<Transportation>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<UserPromotion> UserPromotions => Set<UserPromotion>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ElectronicContract> ElectronicContracts => Set<ElectronicContract>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewMedia> ReviewMedia => Set<ReviewMedia>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles", "auth");
            entity.HasKey(x => x.RoleId);
            entity.HasIndex(x => x.RoleCode).IsUnique();
            entity.Property(x => x.RoleCode).HasMaxLength(50);
            entity.Property(x => x.RoleName).HasMaxLength(100);
            entity.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", "auth");
            entity.HasKey(x => x.UserId);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
            entity.HasIndex(x => new { x.ExternalProvider, x.ExternalProviderKey }).IsUnique().HasFilter("[ExternalProvider] IS NOT NULL AND [ExternalProviderKey] IS NOT NULL");
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(255);
            entity.Property(x => x.ExternalProvider).HasMaxLength(50);
            entity.Property(x => x.ExternalProviderKey).HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.PasswordSalt).HasMaxLength(200);
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.AvatarUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens", "auth");
            entity.HasKey(x => x.PasswordResetTokenId);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.UsedAt, x.RevokedAt });
            entity.Property(x => x.TokenHash).HasMaxLength(200);
            entity.Property(x => x.RequestedIp).HasMaxLength(100);
            entity.HasOne(x => x.User)
                .WithMany(x => x.PasswordResetTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles", "auth");
            entity.HasKey(x => x.UserRoleId);
            entity.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories", "catalog");
            entity.HasKey(x => x.CategoryId);
            entity.HasIndex(x => x.CategoryCode).IsUnique();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.CategoryCode).HasMaxLength(50);
            entity.Property(x => x.CategoryName).HasMaxLength(150);
            entity.Property(x => x.Slug).HasMaxLength(180);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasOne(x => x.ParentCategory).WithMany(x => x.Children).HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Destination>(entity =>
        {
            entity.ToTable("Destinations", "catalog");
            entity.HasKey(x => x.DestinationId);
            entity.HasIndex(x => x.DestinationCode).IsUnique();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.DestinationCode).HasMaxLength(50);
            entity.Property(x => x.DestinationName).HasMaxLength(150);
            entity.Property(x => x.Slug).HasMaxLength(180);
            entity.Property(x => x.CountryCode).HasMaxLength(10);
            entity.Property(x => x.ProvinceName).HasMaxLength(100);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.ThumbnailUrl).HasMaxLength(500);
            entity.Property(x => x.Latitude).HasPrecision(10, 7);
            entity.Property(x => x.Longitude).HasPrecision(10, 7);
            entity.HasOne(x => x.ParentDestination).WithMany(x => x.Children).HasForeignKey(x => x.ParentDestinationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tour>(entity =>
        {
            entity.ToTable("Tours", "catalog");
            entity.HasKey(x => x.TourId);
            entity.HasIndex(x => x.TourCode).IsUnique();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.TourCode).HasMaxLength(50);
            entity.Property(x => x.TourName).HasMaxLength(250);
            entity.Property(x => x.Slug).HasMaxLength(280);
            entity.Property(x => x.MainImageUrl).HasMaxLength(500);
            entity.Property(x => x.ShortDescription).HasMaxLength(1000);
            entity.Property(x => x.CurrencyCode).HasMaxLength(10);
            entity.Property(x => x.DeparturePoint).HasMaxLength(200);
            entity.Property(x => x.ReturnPoint).HasMaxLength(200);
            entity.Property(x => x.SeoTitle).HasMaxLength(255);
            entity.Property(x => x.SeoDescription).HasMaxLength(500);
            entity.Property(x => x.BasePrice).HasPrecision(18, 2);
            entity.Property(x => x.ChildPrice).HasPrecision(18, 2);
            entity.Property(x => x.SingleSupplement).HasPrecision(18, 2);
            entity.HasOne(x => x.Category).WithMany(x => x.Tours).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.StartDestination).WithMany().HasForeignKey(x => x.StartDestinationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.EndDestination).WithMany().HasForeignKey(x => x.EndDestinationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TourMedia>(entity =>
        {
            entity.ToTable("TourMedia", "catalog");
            entity.HasKey(x => x.TourMediaId);
            entity.Property(x => x.MediaUrl).HasMaxLength(500);
            entity.Property(x => x.Caption).HasMaxLength(255);
            entity.HasOne(x => x.Tour).WithMany(x => x.MediaItems).HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TourSchedule>(entity =>
        {
            entity.ToTable("TourSchedules", "catalog");
            entity.HasKey(x => x.TourScheduleId);
            entity.HasIndex(x => x.ScheduleCode).IsUnique();
            entity.Property(x => x.ScheduleCode).HasMaxLength(50);
            entity.Property(x => x.AdultPrice).HasPrecision(18, 2);
            entity.Property(x => x.ChildPrice).HasPrecision(18, 2);
            entity.Property(x => x.InfantPrice).HasPrecision(18, 2);
            entity.Property(x => x.SingleSupplement).HasPrecision(18, 2);
            entity.HasOne(x => x.Tour).WithMany(x => x.Schedules).HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TourItineraryDay>(entity =>
        {
            entity.ToTable("TourItineraryDays", "catalog");
            entity.HasKey(x => x.ItineraryDayId);
            entity.HasIndex(x => new { x.TourId, x.DayNumber }).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.OvernightStay).HasMaxLength(200);
            entity.Property(x => x.HotelName).HasMaxLength(200);
            entity.Property(x => x.TransportationName).HasMaxLength(200);
            entity.HasOne(x => x.Tour).WithMany(x => x.ItineraryDays).HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Transportation).WithMany().HasForeignKey(x => x.TransportationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Hotel>(entity =>
        {
            entity.ToTable("Hotels", "catalog");
            entity.HasKey(x => x.HotelId);
            entity.HasIndex(x => x.HotelCode).IsUnique();
            entity.Property(x => x.HotelCode).HasMaxLength(50);
            entity.Property(x => x.HotelName).HasMaxLength(200);
            entity.Property(x => x.StarRating).HasPrecision(2, 1);
            entity.Property(x => x.AddressLine).HasMaxLength(300);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20);
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.HasOne(x => x.Destination).WithMany(x => x.Hotels).HasForeignKey(x => x.DestinationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TransportationType>(entity =>
        {
            entity.ToTable("TransportationTypes", "catalog");
            entity.HasKey(x => x.TransportationTypeId);
            entity.HasIndex(x => x.TypeCode).IsUnique();
            entity.Property(x => x.TypeCode).HasMaxLength(50);
            entity.Property(x => x.TypeName).HasMaxLength(100);
        });

        modelBuilder.Entity<Transportation>(entity =>
        {
            entity.ToTable("Transportations", "catalog");
            entity.HasKey(x => x.TransportationId);
            entity.HasIndex(x => x.TransportationCode).IsUnique();
            entity.Property(x => x.TransportationCode).HasMaxLength(50);
            entity.Property(x => x.ProviderName).HasMaxLength(150);
            entity.Property(x => x.VehicleName).HasMaxLength(150);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasOne(x => x.TransportationType).WithMany(x => x.Transportations).HasForeignKey(x => x.TransportationTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.ToTable("Promotions", "booking");
            entity.HasKey(x => x.PromotionId);
            entity.HasIndex(x => x.PromotionCode).IsUnique();
            entity.Property(x => x.PromotionCode).HasMaxLength(50);
            entity.Property(x => x.PromotionName).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.MinOrderValue).HasPrecision(18, 2);
            entity.Property(x => x.BannerImageUrl).HasMaxLength(500);
            entity.Property(x => x.BannerAltText).HasMaxLength(200);
            entity.Property(x => x.BannerLinkUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<UserPromotion>(entity =>
        {
            entity.ToTable("UserPromotions", "booking");
            entity.HasKey(x => x.UserPromotionId);
            entity.HasIndex(x => new { x.UserId, x.PromotionId }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.UserPromotions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Promotion).WithMany(x => x.UserPromotions).HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Bookings", "booking");
            entity.HasKey(x => x.BookingId);
            entity.HasIndex(x => x.BookingCode).IsUnique();
            entity.Property(x => x.BookingCode).HasMaxLength(50);
            entity.Property(x => x.ContactName).HasMaxLength(150);
            entity.Property(x => x.ContactEmail).HasMaxLength(255);
            entity.Property(x => x.ContactPhone).HasMaxLength(20);
            entity.Property(x => x.CurrencyCode).HasMaxLength(10);
            entity.Property(x => x.SpecialRequests).HasMaxLength(1000);
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.Property(x => x.BaseAmount).HasPrecision(18, 2);
            entity.Property(x => x.LastMinuteDiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.ServiceFee).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.User).WithMany(x => x.Bookings).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tour).WithMany(x => x.Bookings).HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TourSchedule).WithMany(x => x.Bookings).HasForeignKey(x => x.TourScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Promotion).WithMany(x => x.Bookings).HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookingStatusHistory>(entity =>
        {
            entity.ToTable("BookingStatusesHistory", "booking");
            entity.HasKey(x => x.BookingStatusHistoryId);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Booking).WithMany(x => x.StatusHistory).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ChangedByUser).WithMany(x => x.BookingStatusChanges).HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments", "booking");
            entity.HasKey(x => x.PaymentId);
            entity.HasIndex(x => x.PaymentCode).IsUnique();
            entity.Property(x => x.PaymentCode).HasMaxLength(50);
            entity.Property(x => x.PaymentGateway).HasMaxLength(100);
            entity.Property(x => x.TransactionReference).HasMaxLength(150);
            entity.Property(x => x.CurrencyCode).HasMaxLength(10);
            entity.Property(x => x.FailureReason).HasMaxLength(500);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.Booking).WithMany(x => x.Payments).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ElectronicContract>(entity =>
        {
            entity.ToTable("ElectronicContracts", "booking");
            entity.HasKey(x => x.ElectronicContractId);
            entity.HasIndex(x => x.ContractCode).IsUnique();
            entity.HasIndex(x => x.BookingId);
            entity.Property(x => x.ContractCode).HasMaxLength(50);
            entity.Property(x => x.DraftPdfPath).HasMaxLength(500);
            entity.Property(x => x.FinalPdfPath).HasMaxLength(500);
            entity.Property(x => x.CustomerSignedIp).HasMaxLength(100);
            entity.Property(x => x.CustomerSignedUserAgent).HasMaxLength(500);
            entity.Property(x => x.CustomerOtpHash).HasMaxLength(200);
            entity.Property(x => x.DirectorSignedIp).HasMaxLength(100);
            entity.Property(x => x.DirectorSignedUserAgent).HasMaxLength(500);
            entity.Property(x => x.DirectorOtpHash).HasMaxLength(200);
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.HasOne(x => x.Booking).WithMany(x => x.ElectronicContracts).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications", "integration");
            entity.HasKey(x => x.NotificationId);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.RelatedEntityType).HasMaxLength(50);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("Reviews", "content");
            entity.HasKey(x => x.ReviewId);
            entity.Property(x => x.Rating).HasPrecision(2, 1);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Comment).HasMaxLength(2000);
            entity.HasOne(x => x.Tour).WithMany(x => x.Reviews).HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany(x => x.Reviews).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReviewMedia>(entity =>
        {
            entity.ToTable("ReviewMedia", "content");
            entity.HasKey(x => x.ReviewMediaId);
            entity.Property(x => x.MediaUrl).HasMaxLength(500);
            entity.HasOne(x => x.Review).WithMany(x => x.MediaItems).HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.ToTable("Wishlists", "content");
            entity.HasKey(x => x.WishlistId);
            entity.HasIndex(x => new { x.UserId, x.TourId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.User).WithMany(x => x.Wishlists).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tour).WithMany(x => x.Wishlists).HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Article>(entity =>
        {
            entity.ToTable("Articles", "content");
            entity.HasKey(x => x.ArticleId);
            entity.HasIndex(x => x.ArticleCode).IsUnique();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.ArticleCode).HasMaxLength(50);
            entity.Property(x => x.Title).HasMaxLength(250);
            entity.Property(x => x.Slug).HasMaxLength(280);
            entity.Property(x => x.Summary).HasMaxLength(1000);
            entity.Property(x => x.ThumbnailUrl).HasMaxLength(500);
            entity.HasOne(x => x.AuthorUser).WithMany(x => x.Articles).HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
