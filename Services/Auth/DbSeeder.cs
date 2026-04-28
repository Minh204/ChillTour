using ChillTour.Data;
using ChillTour.Data.Entities;
using ChillTour.Security;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Services.Auth;

public partial class DbSeeder
{
    private readonly ChillTourDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    public DbSeeder(ChillTourDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureExternalLoginColumnsAsync(cancellationToken);
        await EnsurePasswordResetTokensTableAsync(cancellationToken);
        await EnsureTourSeatColumnsAsync(cancellationToken);
        await EnsureBookingSingleRoomColumnsAsync(cancellationToken);
        await EnsureBookingLastMinuteColumnsAsync(cancellationToken);
        await EnsureBookingPaymentLifecycleColumnsAsync(cancellationToken);
        await EnsureTourItineraryTableAsync(cancellationToken);
        await EnsureTourItineraryColumnsAsync(cancellationToken);
        await EnsureNotificationsTableAsync(cancellationToken);
        await EnsureElectronicContractsTableAsync(cancellationToken);
        await EnsurePromotionBannerColumnsAsync(cancellationToken);
        await EnsureUserPromotionsTableAsync(cancellationToken);
        await EnsureReviewMediaTableAsync(cancellationToken);
        await EnsureArticlesTableAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedDefaultUserAsync(
            email: "admin@chilltour.local",
            fullName: "System Admin",
            password: "Admin@123",
            roleCode: RoleConstants.Admin,
            cancellationToken);
        await SeedDefaultUserAsync(
            email: "director@chilltour.local",
            fullName: "System Director",
            password: "Director@123",
            roleCode: RoleConstants.Director,
            cancellationToken);
        await SeedDefaultUserAsync(
            email: "manager@chilltour.local",
            fullName: "System Manager",
            password: "Manager@123",
            roleCode: RoleConstants.Manager,
            cancellationToken);
        await SeedDefaultUserAsync(
            email: "accountant@chilltour.local",
            fullName: "System Accountant",
            password: "Accountant@123",
            roleCode: RoleConstants.Accountant,
            cancellationToken);
        await SeedDefaultUserAsync(
            email: "employee@chilltour.local",
            fullName: "System Employee",
            password: "Employee@123",
            roleCode: RoleConstants.Employee,
            cancellationToken);
        await SeedCategoriesAsync(cancellationToken);
        await SeedDestinationsAsync(cancellationToken);
        await SeedSampleToursAsync(cancellationToken);
        await SeedPromotionsAsync(cancellationToken);
        await SeedArticlesAsync(cancellationToken);
        await SeedDefaultTourSchedulesAsync(cancellationToken);
    }

    private async Task EnsurePasswordResetTokensTableAsync(CancellationToken cancellationToken)
    {
        const string recreateBrokenTableSql = """
                                             IF OBJECT_ID('auth.PasswordResetTokens', 'U') IS NOT NULL
                                                AND (
                                                    COL_LENGTH('auth.PasswordResetTokens', 'PasswordResetTokenId') IS NULL
                                                    OR COL_LENGTH('auth.PasswordResetTokens', 'UserId') IS NULL
                                                    OR COL_LENGTH('auth.PasswordResetTokens', 'TokenHash') IS NULL
                                                    OR COL_LENGTH('auth.PasswordResetTokens', 'ExpiresAt') IS NULL
                                                    OR COL_LENGTH('auth.PasswordResetTokens', 'CreatedAt') IS NULL
                                                    OR COL_LENGTH('auth.PasswordResetTokens', 'UsedAt') IS NULL
                                                    OR COL_LENGTH('auth.PasswordResetTokens', 'RevokedAt') IS NULL
                                                    OR COL_LENGTH('auth.PasswordResetTokens', 'RequestedIp') IS NULL
                                                )
                                             BEGIN
                                                 DROP TABLE auth.PasswordResetTokens;
                                             END;
                                             """;

        const string createTableSql = """
                                      IF OBJECT_ID('auth.PasswordResetTokens', 'U') IS NULL
                                      BEGIN
                                          CREATE TABLE auth.PasswordResetTokens
                                          (
                                              PasswordResetTokenId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                              UserId BIGINT NOT NULL,
                                              TokenHash NVARCHAR(200) NOT NULL,
                                              ExpiresAt DATETIME2(0) NOT NULL,
                                              CreatedAt DATETIME2(0) NOT NULL,
                                              UsedAt DATETIME2(0) NULL,
                                              RevokedAt DATETIME2(0) NULL,
                                              RequestedIp NVARCHAR(100) NULL,
                                              CONSTRAINT FK_PasswordResetTokens_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId) ON DELETE CASCADE
                                          );
                                      END;
                                      """;

        const string createTokenIndexSql = """
                                           IF COL_LENGTH('auth.PasswordResetTokens', 'TokenHash') IS NOT NULL
                                              AND NOT EXISTS (
                                               SELECT 1
                                               FROM sys.indexes
                                               WHERE name = 'IX_PasswordResetTokens_TokenHash'
                                                 AND object_id = OBJECT_ID('auth.PasswordResetTokens')
                                           )
                                           BEGIN
                                               CREATE UNIQUE INDEX IX_PasswordResetTokens_TokenHash
                                               ON auth.PasswordResetTokens(TokenHash);
                                           END;
                                           """;

        const string createLookupIndexSql = """
                                            IF COL_LENGTH('auth.PasswordResetTokens', 'UserId') IS NOT NULL
                                               AND COL_LENGTH('auth.PasswordResetTokens', 'UsedAt') IS NOT NULL
                                               AND COL_LENGTH('auth.PasswordResetTokens', 'RevokedAt') IS NOT NULL
                                               AND NOT EXISTS (
                                                SELECT 1
                                                FROM sys.indexes
                                                WHERE name = 'IX_PasswordResetTokens_UserId_UsedAt_RevokedAt'
                                                  AND object_id = OBJECT_ID('auth.PasswordResetTokens')
                                            )
                                            BEGIN
                                                CREATE INDEX IX_PasswordResetTokens_UserId_UsedAt_RevokedAt
                                                ON auth.PasswordResetTokens(UserId, UsedAt, RevokedAt);
                                            END;
                                            """;

        await _dbContext.Database.ExecuteSqlRawAsync(recreateBrokenTableSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(createTokenIndexSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(createLookupIndexSql, cancellationToken);
    }

    private async Task EnsureElectronicContractsTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF OBJECT_ID('booking.ElectronicContracts', 'U') IS NULL
                           BEGIN
                               CREATE TABLE booking.ElectronicContracts
                               (
                                   ElectronicContractId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                   BookingId BIGINT NOT NULL,
                                   ContractCode NVARCHAR(50) NOT NULL,
                                   ContractStatus TINYINT NOT NULL CONSTRAINT DF_EContracts_Status DEFAULT (1),
                                   ContractHtml NVARCHAR(MAX) NOT NULL,
                                   DraftPdfPath NVARCHAR(500) NULL,
                                   FinalPdfPath NVARCHAR(500) NULL,
                                   CustomerSignatureDataUrl NVARCHAR(MAX) NULL,
                                   CustomerSignedAt DATETIME2(0) NULL,
                                   CustomerSignedIp NVARCHAR(100) NULL,
                                   CustomerSignedUserAgent NVARCHAR(500) NULL,
                                   CustomerOtpHash NVARCHAR(200) NULL,
                                   CustomerOtpExpiresAt DATETIME2(0) NULL,
                                   DirectorSignatureDataUrl NVARCHAR(MAX) NULL,
                                   DirectorSignedAt DATETIME2(0) NULL,
                                   DirectorSignedIp NVARCHAR(100) NULL,
                                   DirectorSignedUserAgent NVARCHAR(500) NULL,
                                   DirectorOtpHash NVARCHAR(200) NULL,
                                   DirectorOtpExpiresAt DATETIME2(0) NULL,
                                   CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_EContracts_CreatedAt DEFAULT (SYSUTCDATETIME()),
                                   UpdatedAt DATETIME2(0) NULL,
                                   CancelledAt DATETIME2(0) NULL,
                                   CancellationReason NVARCHAR(500) NULL,
                                   CONSTRAINT UQ_EContracts_ContractCode UNIQUE (ContractCode),
                                   CONSTRAINT FK_EContracts_Booking FOREIGN KEY (BookingId) REFERENCES booking.Bookings(BookingId) ON DELETE CASCADE
                               );
                           END;

                           IF NOT EXISTS (
                               SELECT 1 FROM sys.indexes
                               WHERE name = 'IX_ElectronicContracts_BookingId'
                                 AND object_id = OBJECT_ID('booking.ElectronicContracts')
                           )
                           BEGIN
                               CREATE INDEX IX_ElectronicContracts_BookingId ON booking.ElectronicContracts(BookingId);
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsurePromotionBannerColumnsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF COL_LENGTH('booking.Promotions', 'BannerImageUrl') IS NULL
                           BEGIN
                               ALTER TABLE booking.Promotions ADD BannerImageUrl NVARCHAR(500) NULL;
                           END;

                           IF COL_LENGTH('booking.Promotions', 'BannerAltText') IS NULL
                           BEGIN
                               ALTER TABLE booking.Promotions ADD BannerAltText NVARCHAR(200) NULL;
                           END;

                           IF COL_LENGTH('booking.Promotions', 'BannerLinkUrl') IS NULL
                           BEGIN
                               ALTER TABLE booking.Promotions ADD BannerLinkUrl NVARCHAR(500) NULL;
                           END;

                           IF COL_LENGTH('booking.Promotions', 'ShowOnHomeBanner') IS NULL
                           BEGIN
                               ALTER TABLE booking.Promotions
                               ADD ShowOnHomeBanner BIT NOT NULL
                                   CONSTRAINT DF_Promotions_ShowOnHomeBanner DEFAULT (0) WITH VALUES;
                           END;

                           IF COL_LENGTH('booking.Promotions', 'BannerDisplayOrder') IS NULL
                           BEGIN
                               ALTER TABLE booking.Promotions
                               ADD BannerDisplayOrder INT NOT NULL
                                   CONSTRAINT DF_Promotions_BannerDisplayOrder DEFAULT (0) WITH VALUES;
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureExternalLoginColumnsAsync(CancellationToken cancellationToken)
    {
        const string addExternalProviderSql = """
                                              IF COL_LENGTH('auth.Users', 'ExternalProvider') IS NULL
                                              BEGIN
                                                  ALTER TABLE auth.Users
                                                  ADD ExternalProvider NVARCHAR(50) NULL;
                                              END;
                                              """;

        const string addExternalProviderKeySql = """
                                                 IF COL_LENGTH('auth.Users', 'ExternalProviderKey') IS NULL
                                                 BEGIN
                                                     ALTER TABLE auth.Users
                                                     ADD ExternalProviderKey NVARCHAR(200) NULL;
                                                 END;
                                                 """;

        const string addExternalLoginIndexSql = """
                                                IF COL_LENGTH('auth.Users', 'ExternalProvider') IS NOT NULL
                                                   AND COL_LENGTH('auth.Users', 'ExternalProviderKey') IS NOT NULL
                                                   AND NOT EXISTS (
                                                       SELECT 1
                                                       FROM sys.indexes
                                                       WHERE name = 'IX_Users_ExternalProvider_ExternalProviderKey'
                                                         AND object_id = OBJECT_ID('auth.Users')
                                                   )
                                                BEGIN
                                                    CREATE UNIQUE INDEX IX_Users_ExternalProvider_ExternalProviderKey
                                                    ON auth.Users (ExternalProvider, ExternalProviderKey)
                                                    WHERE ExternalProvider IS NOT NULL AND ExternalProviderKey IS NOT NULL;
                                                END;
                                                """;

        await _dbContext.Database.ExecuteSqlRawAsync(addExternalProviderSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(addExternalProviderKeySql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(addExternalLoginIndexSql, cancellationToken);
    }

    private async Task EnsureTourSeatColumnsAsync(CancellationToken cancellationToken)
    {
        const string addTotalSeatsSql = """
                                        IF COL_LENGTH('catalog.Tours', 'TotalSeats') IS NULL
                                        BEGIN
                                            ALTER TABLE catalog.Tours
                                            ADD TotalSeats INT NOT NULL
                                                CONSTRAINT DF_Tours_TotalSeats DEFAULT (20) WITH VALUES;
                                        END;
                                        """;

        const string addRemainingSeatsSql = """
                                            IF COL_LENGTH('catalog.Tours', 'RemainingSeats') IS NULL
                                            BEGIN
                                                ALTER TABLE catalog.Tours
                                                ADD RemainingSeats INT NOT NULL
                                                    CONSTRAINT DF_Tours_RemainingSeats DEFAULT (20) WITH VALUES;
                                            END;
                                            """;

        const string normalizeSeatsSql = """
                                         UPDATE catalog.Tours
                                         SET RemainingSeats = CASE
                                                                 WHEN RemainingSeats > TotalSeats THEN TotalSeats
                                                                 WHEN RemainingSeats < 0 THEN 0
                                                                 ELSE RemainingSeats
                                                             END;
                                         """;

        await _dbContext.Database.ExecuteSqlRawAsync(addTotalSeatsSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(addRemainingSeatsSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(normalizeSeatsSql, cancellationToken);
    }

    private async Task EnsureBookingPaymentLifecycleColumnsAsync(CancellationToken cancellationToken)
    {
        const string addPaidAmountSql = """
                                        IF COL_LENGTH('booking.Bookings', 'PaidAmount') IS NULL
                                        BEGIN
                                            ALTER TABLE booking.Bookings
                                            ADD PaidAmount DECIMAL(18, 2) NOT NULL
                                                CONSTRAINT DF_Bookings_PaidAmount DEFAULT (0) WITH VALUES;
                                        END;
                                        """;

        const string addBalanceDueSql = """
                                        IF COL_LENGTH('booking.Bookings', 'BalanceDueAt') IS NULL
                                        BEGIN
                                            ALTER TABLE booking.Bookings
                                            ADD BalanceDueAt DATETIME2(0) NULL;
                                        END;
                                        """;

        const string addFullyPaidSql = """
                                       IF COL_LENGTH('booking.Bookings', 'FullyPaidAt') IS NULL
                                       BEGIN
                                           ALTER TABLE booking.Bookings
                                           ADD FullyPaidAt DATETIME2(0) NULL;
                                       END;
                                       """;

        const string addBalanceReminderSql = """
                                             IF COL_LENGTH('booking.Bookings', 'BalanceReminderSentAt') IS NULL
                                             BEGIN
                                                 ALTER TABLE booking.Bookings
                                                 ADD BalanceReminderSentAt DATETIME2(0) NULL;
                                             END;
                                             """;

        const string updateBookingStatusConstraintSql = """
                                                       IF OBJECT_ID('booking.CK_Bookings_BookingStatus', 'C') IS NOT NULL
                                                       BEGIN
                                                           ALTER TABLE booking.Bookings DROP CONSTRAINT CK_Bookings_BookingStatus;
                                                       END;

                                                       ALTER TABLE booking.Bookings WITH CHECK
                                                       ADD CONSTRAINT CK_Bookings_BookingStatus
                                                       CHECK (BookingStatus IN (0,1,2,3,4,5,6,7,8,9,10));
                                                       """;

        const string updateBookingPaymentStatusConstraintSql = """
                                                              IF OBJECT_ID('booking.CK_Bookings_PaymentStatus', 'C') IS NOT NULL
                                                              BEGIN
                                                                  ALTER TABLE booking.Bookings DROP CONSTRAINT CK_Bookings_PaymentStatus;
                                                              END;

                                                              ALTER TABLE booking.Bookings WITH CHECK
                                                              ADD CONSTRAINT CK_Bookings_PaymentStatus
                                                              CHECK (PaymentStatus IN (0,1,2,3,4));
                                                              """;

        const string updatePaymentStatusConstraintSql = """
                                                       IF OBJECT_ID('booking.CK_Payments_PaymentStatus', 'C') IS NOT NULL
                                                       BEGIN
                                                           ALTER TABLE booking.Payments DROP CONSTRAINT CK_Payments_PaymentStatus;
                                                       END;

                                                       ALTER TABLE booking.Payments WITH CHECK
                                                       ADD CONSTRAINT CK_Payments_PaymentStatus
                                                       CHECK (PaymentStatus IN (0,1,2,3,4));
                                                       """;

        const string normalizePaidAmountSql = """
                                             UPDATE p
                                             SET PaymentStatus = CASE
                                                 WHEN p.Amount >= b.TotalAmount THEN 3
                                                 ELSE 2
                                             END
                                             FROM booking.Payments p
                                             INNER JOIN booking.Bookings b ON b.BookingId = p.BookingId
                                             WHERE p.PaymentStatus = 1
                                               AND p.PaidAt IS NOT NULL;

                                             UPDATE b
                                             SET PaidAmount = ISNULL(p.PaidAmount, 0)
                                             FROM booking.Bookings b
                                             OUTER APPLY (
                                                 SELECT SUM(Amount) AS PaidAmount
                                                 FROM booking.Payments p
                                                 WHERE p.BookingId = b.BookingId
                                                   AND p.PaymentStatus IN (2, 3)
                                             ) p
                                             WHERE b.PaidAmount = 0
                                               AND b.PaymentStatus IN (1, 2, 3);

                                             UPDATE booking.Bookings
                                             SET PaymentStatus = CASE
                                                 WHEN PaidAmount >= TotalAmount AND TotalAmount > 0 THEN 3
                                                 WHEN PaidAmount > 0 THEN 2
                                                 WHEN PaymentStatus = 1 THEN 0
                                                 ELSE PaymentStatus
                                             END,
                                             BookingStatus = CASE
                                                 WHEN BookingStatus IN (4, 5, 9, 10) THEN BookingStatus
                                                 WHEN PaidAmount >= TotalAmount AND TotalAmount > 0 THEN 8
                                                 WHEN PaidAmount > 0 THEN 2
                                                 ELSE BookingStatus
                                             END
                                             WHERE PaymentStatus IN (1, 2)
                                                OR PaidAmount > 0;
                                             """;

        await _dbContext.Database.ExecuteSqlRawAsync(addPaidAmountSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(addBalanceDueSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(addFullyPaidSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(addBalanceReminderSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(updateBookingStatusConstraintSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(updateBookingPaymentStatusConstraintSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(updatePaymentStatusConstraintSql, cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(normalizePaidAmountSql, cancellationToken);
    }

    private async Task EnsureNotificationsTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'integration')
                           BEGIN
                               EXEC('CREATE SCHEMA integration');
                           END;

                           IF OBJECT_ID('integration.Notifications', 'U') IS NULL
                           BEGIN
                               EXEC('CREATE TABLE integration.Notifications (
                                   NotificationId BIGINT IDENTITY(1,1) PRIMARY KEY,
                                   UserId BIGINT NOT NULL,
                                   NotificationType TINYINT NOT NULL,
                                   Title NVARCHAR(200) NOT NULL,
                                   Message NVARCHAR(1000) NOT NULL,
                                   RelatedEntityType NVARCHAR(50) NULL,
                                   RelatedEntityId BIGINT NULL,
                                   IsRead BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
                                   SentAt DATETIME2(0) NULL,
                                   CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSDATETIME()),
                                   CONSTRAINT FK_Notifications_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId)
                               )');
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureUserPromotionsTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF OBJECT_ID('booking.UserPromotions', 'U') IS NULL
                           BEGIN
                               EXEC('CREATE TABLE booking.UserPromotions (
                                   UserPromotionId BIGINT IDENTITY(1,1) PRIMARY KEY,
                                   UserId BIGINT NOT NULL,
                                   PromotionId BIGINT NOT NULL,
                                   ClaimedAt DATETIME2(0) NOT NULL CONSTRAINT DF_UserPromotions_ClaimedAt DEFAULT (SYSDATETIME()),
                                   UsedAt DATETIME2(0) NULL,
                                   CONSTRAINT UQ_UserPromotions_UserId_PromotionId UNIQUE (UserId, PromotionId),
                                   CONSTRAINT FK_UserPromotions_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId),
                                   CONSTRAINT FK_UserPromotions_Promotion FOREIGN KEY (PromotionId) REFERENCES booking.Promotions(PromotionId)
                               )');
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureReviewMediaTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'content')
                           BEGIN
                               EXEC('CREATE SCHEMA content');
                           END;

                           IF OBJECT_ID('content.ReviewMedia', 'U') IS NULL
                           BEGIN
                               EXEC('CREATE TABLE content.ReviewMedia (
                                   ReviewMediaId BIGINT IDENTITY(1,1) PRIMARY KEY,
                                   ReviewId BIGINT NOT NULL,
                                   MediaUrl NVARCHAR(500) NOT NULL,
                                   DisplayOrder INT NOT NULL CONSTRAINT DF_ReviewMedia_DisplayOrder DEFAULT (1),
                                   CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ReviewMedia_CreatedAt DEFAULT (SYSDATETIME()),
                                   CONSTRAINT FK_ReviewMedia_Review FOREIGN KEY (ReviewId) REFERENCES content.Reviews(ReviewId)
                               )');
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureArticlesTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'content')
                           BEGIN
                               EXEC('CREATE SCHEMA content');
                           END;

                           IF OBJECT_ID('content.Articles', 'U') IS NULL
                           BEGIN
                               EXEC('CREATE TABLE content.Articles (
                                   ArticleId BIGINT IDENTITY(1,1) PRIMARY KEY,
                                   ArticleCode NVARCHAR(50) NOT NULL,
                                   Title NVARCHAR(250) NOT NULL,
                                   Slug NVARCHAR(280) NOT NULL,
                                   Summary NVARCHAR(1000) NULL,
                                   ContentHtml NVARCHAR(MAX) NULL,
                                   ThumbnailUrl NVARCHAR(500) NULL,
                                   PublishedAt DATETIME2(0) NULL,
                                   Status TINYINT NOT NULL CONSTRAINT DF_Articles_Status DEFAULT (0),
                                   AuthorUserId BIGINT NULL,
                                   CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Articles_CreatedAt DEFAULT (SYSDATETIME()),
                                   UpdatedAt DATETIME2(0) NULL,
                                   CONSTRAINT UQ_Articles_ArticleCode UNIQUE (ArticleCode),
                                   CONSTRAINT UQ_Articles_Slug UNIQUE (Slug),
                                   CONSTRAINT FK_Articles_Author FOREIGN KEY (AuthorUserId) REFERENCES auth.Users(UserId)
                               )');
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureBookingSingleRoomColumnsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF COL_LENGTH('booking.Bookings', 'SingleRoomCount') IS NULL
                           BEGIN
                               ALTER TABLE booking.Bookings
                               ADD SingleRoomCount INT NOT NULL
                                   CONSTRAINT DF_Bookings_SingleRoomCount DEFAULT (0) WITH VALUES;
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureBookingLastMinuteColumnsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF COL_LENGTH('booking.Bookings', 'LastMinuteDiscountAmount') IS NULL
                           BEGIN
                               ALTER TABLE booking.Bookings
                               ADD LastMinuteDiscountAmount DECIMAL(18,2) NOT NULL
                                   CONSTRAINT DF_Bookings_LastMinuteDiscountAmount DEFAULT (0) WITH VALUES;
                           END;

                           IF COL_LENGTH('booking.Bookings', 'IsLastMinuteDeal') IS NULL
                           BEGIN
                               ALTER TABLE booking.Bookings
                               ADD IsLastMinuteDeal BIT NOT NULL
                                   CONSTRAINT DF_Bookings_IsLastMinuteDeal DEFAULT (0) WITH VALUES;
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureTourItineraryTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF OBJECT_ID('catalog.TourItineraryDays', 'U') IS NULL
                           BEGIN
                               EXEC('CREATE TABLE catalog.TourItineraryDays (
                                   ItineraryDayId BIGINT IDENTITY(1,1) PRIMARY KEY,
                                   TourId BIGINT NOT NULL,
                                   DayNumber INT NOT NULL,
                                   Title NVARCHAR(200) NOT NULL,
                                   Summary NVARCHAR(500) NULL,
                                   Description NVARCHAR(MAX) NULL,
                                   OvernightStay NVARCHAR(200) NULL,
                                   BreakfastIncluded BIT NOT NULL CONSTRAINT DF_TourItineraryDays_Breakfast DEFAULT (0),
                                   LunchIncluded BIT NOT NULL CONSTRAINT DF_TourItineraryDays_Lunch DEFAULT (0),
                                   DinnerIncluded BIT NOT NULL CONSTRAINT DF_TourItineraryDays_Dinner DEFAULT (0),
                                   HotelName NVARCHAR(200) NULL,
                                   TransportationName NVARCHAR(200) NULL,
                                   HotelId BIGINT NULL,
                                   TransportationId BIGINT NULL,
                                   CONSTRAINT UQ_TourItineraryDays_TourId_DayNumber UNIQUE (TourId, DayNumber),
                                   CONSTRAINT FK_TourItineraryDays_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
                                   CONSTRAINT FK_TourItineraryDays_Hotel FOREIGN KEY (HotelId) REFERENCES catalog.Hotels(HotelId),
                                   CONSTRAINT FK_TourItineraryDays_Transportation FOREIGN KEY (TransportationId) REFERENCES catalog.Transportations(TransportationId)
                               )');
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureTourItineraryColumnsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           IF COL_LENGTH('catalog.TourItineraryDays', 'OvernightStay') IS NULL
                           BEGIN
                               ALTER TABLE catalog.TourItineraryDays
                               ADD OvernightStay NVARCHAR(200) NULL;
                           END;

                           IF COL_LENGTH('catalog.TourItineraryDays', 'HotelName') IS NULL
                           BEGIN
                               ALTER TABLE catalog.TourItineraryDays
                               ADD HotelName NVARCHAR(200) NULL;
                           END;

                           IF COL_LENGTH('catalog.TourItineraryDays', 'TransportationName') IS NULL
                           BEGIN
                               ALTER TABLE catalog.TourItineraryDays
                               ADD TransportationName NVARCHAR(200) NULL;
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in RoleConstants.All)
        {
            var exists = await _dbContext.Roles.AnyAsync(x => x.RoleCode == roleName, cancellationToken);
            if (exists)
            {
                continue;
            }

            _dbContext.Roles.Add(new Role
            {
                RoleCode = roleName,
                RoleName = roleName,
                Description = $"System role: {roleName}",
                IsSystemRole = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDefaultUserAsync(
        string email,
        string fullName,
        string password,
        string roleCode,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();

        var existingUser = await _dbContext.Users
            .Include(x => x.UserRoles)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is null)
        {
            var passwordHash = _passwordHasher.HashPassword(password, out var salt);
            existingUser = new User
            {
                Email = email,
                NormalizedEmail = normalizedEmail,
                FullName = fullName,
                PasswordHash = passwordHash,
                PasswordSalt = salt,
                Status = 1,
                EmailVerified = true,
                PhoneVerified = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(existingUser);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var managerRole = await _dbContext.Roles.SingleAsync(x => x.RoleCode.ToUpper() == roleCode.ToUpper(), cancellationToken);
        var hasManagerRole = await _dbContext.UserRoles
            .AnyAsync(x => x.UserId == existingUser.UserId && x.RoleId == managerRole.RoleId, cancellationToken);

        if (!hasManagerRole)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = existingUser.UserId,
                RoleId = managerRole.RoleId,
                AssignedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        var categoryNames = new[]
        {
            "Tour nghỉ dưỡng",
            "Tour biển đảo",
            "Tour khám phá",
            "Tour gia đình",
            "Tour quốc tế"
        };

        var categories = categoryNames.Select((name, index) => new Category
        {
            CategoryCode = $"CAT{index + 1:D4}",
            CategoryName = name,
            Slug = BuildSlug(name),
            DisplayOrder = index + 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.Categories.AddRange(categories);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDestinationsAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Destinations.AnyAsync(cancellationToken))
        {
            return;
        }

        var destinations = new[]
        {
            new Destination { DestinationCode = "DES0001", DestinationName = "Hà Nội", DestinationType = 1, CountryCode = "VN", ProvinceName = "Hà Nội", Summary = "Thủ đô nghìn năm văn hiến", IsFeatured = true, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0002", DestinationName = "Hạ Long", DestinationType = 2, CountryCode = "VN", ProvinceName = "Quảng Ninh", Summary = "Di sản thiên nhiên thế giới", IsFeatured = true, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0003", DestinationName = "Đà Nẵng", DestinationType = 1, CountryCode = "VN", ProvinceName = "Đà Nẵng", Summary = "Thành phố biển năng động", IsFeatured = true, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0004", DestinationName = "Hội An", DestinationType = 2, CountryCode = "VN", ProvinceName = "Quảng Nam", Summary = "Phố cổ yên bình", IsFeatured = true, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0005", DestinationName = "Nha Trang", DestinationType = 1, CountryCode = "VN", ProvinceName = "Khánh Hòa", Summary = "Biển xanh cát trắng", IsFeatured = true, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0006", DestinationName = "Đà Lạt", DestinationType = 1, CountryCode = "VN", ProvinceName = "Lâm Đồng", Summary = "Thành phố ngàn hoa", IsFeatured = true, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0007", DestinationName = "Phú Quốc", DestinationType = 2, CountryCode = "VN", ProvinceName = "Kiên Giang", Summary = "Đảo ngọc nghỉ dưỡng", IsFeatured = true, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0008", DestinationName = "TP. Hồ Chí Minh", DestinationType = 1, CountryCode = "VN", ProvinceName = "Hồ Chí Minh", Summary = "Trung tâm kinh tế sôi động", IsFeatured = false, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0009", DestinationName = "Bangkok", DestinationType = 1, CountryCode = "TH", ProvinceName = "Bangkok", Summary = "Thủ đô sôi động của Thái Lan", IsFeatured = false, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Destination { DestinationCode = "DES0010", DestinationName = "Singapore", DestinationType = 1, CountryCode = "SG", ProvinceName = "Singapore", Summary = "Đảo quốc hiện đại", IsFeatured = false, IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        foreach (var destination in destinations)
        {
            destination.Slug = BuildSlug(destination.DestinationName);
        }

        _dbContext.Destinations.AddRange(destinations);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDefaultTourSchedulesAsync(CancellationToken cancellationToken)
    {
        var toursWithoutSchedule = await _dbContext.Tours
            .Include(x => x.Schedules)
            .Where(x => !x.Schedules.Any())
            .ToListAsync(cancellationToken);

        if (toursWithoutSchedule.Count == 0)
        {
            return;
        }

        var offset = 0;
        foreach (var tour in toursWithoutSchedule)
        {
            var departureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7 + offset));
            var returnDate = departureDate.AddDays(Math.Max(tour.DurationDays - 1, 0));
            var totalSeats = tour.TotalSeats > 0 ? tour.TotalSeats : 20;
            var availableSeats = tour.RemainingSeats > 0 ? tour.RemainingSeats : totalSeats;

            _dbContext.TourSchedules.Add(new TourSchedule
            {
                TourId = tour.TourId,
                ScheduleCode = $"SCH{tour.TourId:D6}01",
                DepartureDate = departureDate,
                ReturnDate = returnDate,
                TotalSeats = totalSeats,
                AvailableSeats = availableSeats,
                ReservedSeats = Math.Max(totalSeats - availableSeats, 0),
                AdultPrice = tour.BasePrice,
                ChildPrice = tour.ChildPrice,
                InfantPrice = 0m,
                SingleSupplement = tour.SingleSupplement,
                Status = 1,
                CreatedAt = DateTime.UtcNow
            });

            offset++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPromotionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var promotions = new[]
        {
            new Promotion
            {
                PromotionCode = "FIRST15",
                PromotionName = "Ưu đãi khách hàng mới",
                PromotionType = 1,
                Description = "Giảm 15% cho lần đặt tour đầu tiên, tối đa 1.500.000 đ.",
                DiscountPercent = 15,
                MaxDiscountAmount = 1_500_000m,
                MinOrderValue = 0m,
                MaxUsageCount = 10000,
                MaxUsagePerUser = 1,
                StartAt = now.AddDays(-30),
                EndAt = now.AddYears(1),
                IsAutoApply = false,
                IsActive = true,
                CreatedAt = now
            },
            new Promotion
            {
                PromotionCode = "CHILL10",
                PromotionName = "Giảm 10% cho đơn hè",
                PromotionType = 1,
                Description = "Áp dụng cho đơn từ 5 triệu trở lên, giảm 10% tối đa 1.000.000 đ.",
                DiscountPercent = 10,
                MaxDiscountAmount = 1_000_000m,
                MinOrderValue = 5_000_000m,
                MaxUsageCount = 500,
                MaxUsagePerUser = 2,
                StartAt = now.AddDays(-7),
                EndAt = now.AddMonths(2),
                IsAutoApply = false,
                IsActive = true,
                BannerAltText = "ChillTour deal hot du lịch thả ga giảm đến 30%",
                CreatedAt = now
            },
            new Promotion
            {
                PromotionCode = "CHILL30",
                PromotionName = "Deal hot du lịch thả ga",
                PromotionType = 1,
                Description = "Giảm đến 30% cho các tour đang mở bán, áp dụng theo chương trình banner trang chủ.",
                DiscountPercent = 30,
                MaxDiscountAmount = 3_000_000m,
                MinOrderValue = 0m,
                MaxUsageCount = 1000,
                MaxUsagePerUser = 1,
                StartAt = now.AddDays(-7),
                EndAt = now.AddYears(1),
                IsAutoApply = false,
                IsActive = true,
                BannerImageUrl = "/uploads/banners/chilltour-deal-hot-sample.svg",
                BannerAltText = "ChillTour deal hot du lịch thả ga giảm đến 30%",
                BannerLinkUrl = "/Promotions",
                ShowOnHomeBanner = true,
                BannerDisplayOrder = 1,
                CreatedAt = now
            },
            new Promotion
            {
                PromotionCode = "FAMILY500",
                PromotionName = "Gia đình tiết kiệm",
                PromotionType = 2,
                Description = "Giảm trực tiếp 500.000 đ cho đơn từ 8 triệu.",
                DiscountAmount = 500_000m,
                MinOrderValue = 8_000_000m,
                MaxUsageCount = 300,
                MaxUsagePerUser = 1,
                StartAt = now.AddDays(-7),
                EndAt = now.AddMonths(1),
                IsAutoApply = false,
                IsActive = true,
                CreatedAt = now
            },
            new Promotion
            {
                PromotionCode = "DEAL2TR",
                PromotionName = "Ưu đãi tour cao cấp",
                PromotionType = 2,
                Description = "Giảm 2.000.000 đ cho đơn từ 20 triệu trở lên.",
                DiscountAmount = 2_000_000m,
                MinOrderValue = 20_000_000m,
                MaxUsageCount = 100,
                MaxUsagePerUser = 1,
                StartAt = now.AddDays(-7),
                EndAt = now.AddMonths(1),
                IsAutoApply = false,
                IsActive = true,
                CreatedAt = now
            }
        };

        foreach (var promotion in promotions)
        {
            if (promotion.PromotionCode is "FAMILY500" or "DEAL2TR")
            {
                continue;
            }

            var exists = await _dbContext.Promotions.AnyAsync(x => x.PromotionCode == promotion.PromotionCode, cancellationToken);
            if (!exists)
            {
                _dbContext.Promotions.Add(promotion);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var sampleBanner = await _dbContext.Promotions.SingleOrDefaultAsync(x => x.PromotionCode == "CHILL30", cancellationToken);
        if (sampleBanner is not null && string.IsNullOrWhiteSpace(sampleBanner.BannerImageUrl))
        {
            sampleBanner.BannerImageUrl = "/uploads/banners/chilltour-deal-hot-sample.svg";
            sampleBanner.BannerAltText = "ChillTour deal hot du lịch thả ga giảm đến 30%";
            sampleBanner.BannerLinkUrl = "/Promotions";
            sampleBanner.ShowOnHomeBanner = true;
            sampleBanner.BannerDisplayOrder = 1;
            sampleBanner.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildSlug(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = slug.Replace("đ", "d");
        var normalized = slug.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder();

        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }
}
