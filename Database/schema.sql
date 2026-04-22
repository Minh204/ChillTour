
/*
    ChillTour SQL Server database schema
    Target: Microsoft SQL Server 2019+
*/

SET NOCOUNT ON;
GO

IF DB_ID(N'ChillTourDb') IS NULL
BEGIN
    CREATE DATABASE ChillTourDb;
END
GO

USE ChillTourDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'auth') EXEC('CREATE SCHEMA auth');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'catalog') EXEC('CREATE SCHEMA catalog');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'booking') EXEC('CREATE SCHEMA booking');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'content') EXEC('CREATE SCHEMA content');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'integration') EXEC('CREATE SCHEMA integration');
GO

CREATE TABLE auth.Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleCode NVARCHAR(50) NOT NULL,
    RoleName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    IsSystemRole BIT NOT NULL CONSTRAINT DF_Roles_IsSystemRole DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_Roles_RoleCode UNIQUE (RoleCode)
);
GO

CREATE TABLE auth.Users (
    UserId BIGINT IDENTITY(1,1) PRIMARY KEY,
    Email NVARCHAR(255) NOT NULL,
    NormalizedEmail NVARCHAR(255) NOT NULL,
    PhoneNumber NVARCHAR(20) NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    PasswordSalt NVARCHAR(200) NULL,
    FullName NVARCHAR(150) NOT NULL,
    Gender TINYINT NULL,
    DateOfBirth DATE NULL,
    AvatarUrl NVARCHAR(500) NULL,
    Status TINYINT NOT NULL CONSTRAINT DF_Users_Status DEFAULT (1),
    EmailVerified BIT NOT NULL CONSTRAINT DF_Users_EmailVerified DEFAULT (0),
    PhoneVerified BIT NOT NULL CONSTRAINT DF_Users_PhoneVerified DEFAULT (0),
    LastLoginAt DATETIME2(0) NULL,
    FailedLoginCount INT NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0),
    LockoutEndAt DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT UQ_Users_NormalizedEmail UNIQUE (NormalizedEmail),
    CONSTRAINT CK_Users_Gender CHECK (Gender IS NULL OR Gender IN (1,2,3)),
    CONSTRAINT CK_Users_Status CHECK (Status IN (0,1,2,3))
);
GO

CREATE TABLE auth.UserRoles (
    UserRoleId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    RoleId INT NOT NULL,
    AssignedAt DATETIME2(0) NOT NULL CONSTRAINT DF_UserRoles_AssignedAt DEFAULT (SYSDATETIME()),
    AssignedByUserId BIGINT NULL,
    CONSTRAINT UQ_UserRoles_UserId_RoleId UNIQUE (UserId, RoleId),
    CONSTRAINT FK_UserRoles_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId),
    CONSTRAINT FK_UserRoles_Role FOREIGN KEY (RoleId) REFERENCES auth.Roles(RoleId),
    CONSTRAINT FK_UserRoles_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES auth.Users(UserId)
);
GO

CREATE TABLE auth.UserAddresses (
    AddressId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    AddressType TINYINT NOT NULL,
    ContactName NVARCHAR(150) NOT NULL,
    ContactPhone NVARCHAR(20) NOT NULL,
    CountryCode NVARCHAR(10) NOT NULL,
    ProvinceName NVARCHAR(100) NOT NULL,
    DistrictName NVARCHAR(100) NULL,
    WardName NVARCHAR(100) NULL,
    AddressLine NVARCHAR(300) NOT NULL,
    PostalCode NVARCHAR(20) NULL,
    IsDefault BIT NOT NULL CONSTRAINT DF_UserAddresses_IsDefault DEFAULT (0),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_UserAddresses_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT FK_UserAddresses_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId),
    CONSTRAINT CK_UserAddresses_AddressType CHECK (AddressType IN (1,2,3))
);
GO

CREATE TABLE auth.UserSessions (
    SessionId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY CONSTRAINT DF_UserSessions_SessionId DEFAULT (NEWSEQUENTIALID()),
    UserId BIGINT NOT NULL,
    RefreshToken NVARCHAR(300) NOT NULL,
    DeviceName NVARCHAR(150) NULL,
    IpAddress NVARCHAR(64) NULL,
    UserAgent NVARCHAR(500) NULL,
    ExpiresAt DATETIME2(0) NOT NULL,
    RevokedAt DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_UserSessions_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_UserSessions_RefreshToken UNIQUE (RefreshToken),
    CONSTRAINT FK_UserSessions_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId)
);
GO

CREATE TABLE auth.EmailVerificationTokens (
    VerificationTokenId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    Token NVARCHAR(200) NOT NULL,
    ExpiresAt DATETIME2(0) NOT NULL,
    VerifiedAt DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_EmailVerificationTokens_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_EmailVerificationTokens_Token UNIQUE (Token),
    CONSTRAINT FK_EmailVerificationTokens_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId)
);
GO

CREATE TABLE auth.PasswordResetTokens (
    ResetTokenId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    Token NVARCHAR(200) NOT NULL,
    ExpiresAt DATETIME2(0) NOT NULL,
    UsedAt DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_PasswordResetTokens_Token UNIQUE (Token),
    CONSTRAINT FK_PasswordResetTokens_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId)
);
GO

CREATE TABLE catalog.Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    ParentCategoryId INT NULL,
    CategoryCode NVARCHAR(50) NOT NULL,
    CategoryName NVARCHAR(150) NOT NULL,
    Slug NVARCHAR(180) NOT NULL,
    Description NVARCHAR(500) NULL,
    DisplayOrder INT NOT NULL CONSTRAINT DF_Categories_DisplayOrder DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT UQ_Categories_CategoryCode UNIQUE (CategoryCode),
    CONSTRAINT UQ_Categories_Slug UNIQUE (Slug),
    CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentCategoryId) REFERENCES catalog.Categories(CategoryId)
);
GO

CREATE TABLE catalog.Destinations (
    DestinationId INT IDENTITY(1,1) PRIMARY KEY,
    ParentDestinationId INT NULL,
    DestinationCode NVARCHAR(50) NOT NULL,
    DestinationType TINYINT NOT NULL,
    DestinationName NVARCHAR(150) NOT NULL,
    Slug NVARCHAR(180) NOT NULL,
    CountryCode NVARCHAR(10) NOT NULL,
    ProvinceName NVARCHAR(100) NULL,
    Latitude DECIMAL(10,7) NULL,
    Longitude DECIMAL(10,7) NULL,
    Summary NVARCHAR(500) NULL,
    Description NVARCHAR(MAX) NULL,
    ThumbnailUrl NVARCHAR(500) NULL,
    IsFeatured BIT NOT NULL CONSTRAINT DF_Destinations_IsFeatured DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_Destinations_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Destinations_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT UQ_Destinations_DestinationCode UNIQUE (DestinationCode),
    CONSTRAINT UQ_Destinations_Slug UNIQUE (Slug),
    CONSTRAINT FK_Destinations_Parent FOREIGN KEY (ParentDestinationId) REFERENCES catalog.Destinations(DestinationId),
    CONSTRAINT CK_Destinations_DestinationType CHECK (DestinationType IN (1,2,3,4))
);
GO
CREATE TABLE catalog.Tags (
    TagId INT IDENTITY(1,1) PRIMARY KEY,
    TagName NVARCHAR(100) NOT NULL,
    Slug NVARCHAR(120) NOT NULL,
    TagType TINYINT NOT NULL CONSTRAINT DF_Tags_TagType DEFAULT (1),
    IsActive BIT NOT NULL CONSTRAINT DF_Tags_IsActive DEFAULT (1),
    CONSTRAINT UQ_Tags_TagName UNIQUE (TagName),
    CONSTRAINT UQ_Tags_Slug UNIQUE (Slug)
);
GO

CREATE TABLE catalog.Tours (
    TourId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TourCode NVARCHAR(50) NOT NULL,
    TourName NVARCHAR(250) NOT NULL,
    Slug NVARCHAR(280) NOT NULL,
    CategoryId INT NOT NULL,
    StartDestinationId INT NOT NULL,
    EndDestinationId INT NOT NULL,
    MainImageUrl NVARCHAR(500) NULL,
    ShortDescription NVARCHAR(1000) NULL,
    Description NVARCHAR(MAX) NULL,
    DurationDays INT NOT NULL,
    DurationNights INT NOT NULL,
    MinGroupSize INT NOT NULL CONSTRAINT DF_Tours_MinGroupSize DEFAULT (1),
    MaxGroupSize INT NULL,
    MinAge INT NULL,
    MaxAge INT NULL,
    BasePrice DECIMAL(18,2) NOT NULL,
    ChildPrice DECIMAL(18,2) NULL,
    SingleSupplement DECIMAL(18,2) NULL,
    CurrencyCode NVARCHAR(10) NOT NULL CONSTRAINT DF_Tours_CurrencyCode DEFAULT (N'VND'),
    DeparturePoint NVARCHAR(200) NULL,
    ReturnPoint NVARCHAR(200) NULL,
    PickupIncluded BIT NOT NULL CONSTRAINT DF_Tours_PickupIncluded DEFAULT (0),
    IsFeatured BIT NOT NULL CONSTRAINT DF_Tours_IsFeatured DEFAULT (0),
    IsPublished BIT NOT NULL CONSTRAINT DF_Tours_IsPublished DEFAULT (0),
    ApprovalStatus TINYINT NOT NULL CONSTRAINT DF_Tours_ApprovalStatus DEFAULT (0),
    SeoTitle NVARCHAR(255) NULL,
    SeoDescription NVARCHAR(500) NULL,
    CreatedByUserId BIGINT NULL,
    UpdatedByUserId BIGINT NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Tours_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT UQ_Tours_TourCode UNIQUE (TourCode),
    CONSTRAINT UQ_Tours_Slug UNIQUE (Slug),
    CONSTRAINT FK_Tours_Category FOREIGN KEY (CategoryId) REFERENCES catalog.Categories(CategoryId),
    CONSTRAINT FK_Tours_StartDestination FOREIGN KEY (StartDestinationId) REFERENCES catalog.Destinations(DestinationId),
    CONSTRAINT FK_Tours_EndDestination FOREIGN KEY (EndDestinationId) REFERENCES catalog.Destinations(DestinationId),
    CONSTRAINT FK_Tours_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES auth.Users(UserId),
    CONSTRAINT FK_Tours_UpdatedBy FOREIGN KEY (UpdatedByUserId) REFERENCES auth.Users(UserId),
    CONSTRAINT CK_Tours_DurationDays CHECK (DurationDays > 0),
    CONSTRAINT CK_Tours_DurationNights CHECK (DurationNights >= 0),
    CONSTRAINT CK_Tours_BasePrice CHECK (BasePrice >= 0),
    CONSTRAINT CK_Tours_ApprovalStatus CHECK (ApprovalStatus IN (0,1,2,3))
);
GO

CREATE TABLE catalog.TourDestinations (
    TourDestinationId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TourId BIGINT NOT NULL,
    DestinationId INT NOT NULL,
    VisitOrder INT NOT NULL,
    StayNights INT NULL,
    Notes NVARCHAR(500) NULL,
    CONSTRAINT UQ_TourDestinations_Tour_Order UNIQUE (TourId, VisitOrder),
    CONSTRAINT FK_TourDestinations_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
    CONSTRAINT FK_TourDestinations_Destination FOREIGN KEY (DestinationId) REFERENCES catalog.Destinations(DestinationId)
);
GO

CREATE TABLE catalog.TourTags (
    TourTagId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TourId BIGINT NOT NULL,
    TagId INT NOT NULL,
    CONSTRAINT UQ_TourTags_TourId_TagId UNIQUE (TourId, TagId),
    CONSTRAINT FK_TourTags_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
    CONSTRAINT FK_TourTags_Tag FOREIGN KEY (TagId) REFERENCES catalog.Tags(TagId)
);
GO

CREATE TABLE catalog.TourMedia (
    TourMediaId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TourId BIGINT NOT NULL,
    MediaType TINYINT NOT NULL,
    MediaUrl NVARCHAR(500) NOT NULL,
    Caption NVARCHAR(255) NULL,
    DisplayOrder INT NOT NULL CONSTRAINT DF_TourMedia_DisplayOrder DEFAULT (0),
    IsPrimary BIT NOT NULL CONSTRAINT DF_TourMedia_IsPrimary DEFAULT (0),
    CONSTRAINT FK_TourMedia_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
    CONSTRAINT CK_TourMedia_MediaType CHECK (MediaType IN (1,2,3))
);
GO

CREATE TABLE catalog.TourInclusions (
    TourInclusionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TourId BIGINT NOT NULL,
    InclusionType TINYINT NOT NULL,
    Title NVARCHAR(150) NOT NULL,
    Description NVARCHAR(500) NULL,
    DisplayOrder INT NOT NULL CONSTRAINT DF_TourInclusions_DisplayOrder DEFAULT (0),
    CONSTRAINT FK_TourInclusions_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
    CONSTRAINT CK_TourInclusions_InclusionType CHECK (InclusionType IN (1,2))
);
GO

CREATE TABLE catalog.TourSchedules (
    TourScheduleId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TourId BIGINT NOT NULL,
    ScheduleCode NVARCHAR(50) NOT NULL,
    DepartureDate DATE NOT NULL,
    ReturnDate DATE NOT NULL,
    BookingOpenAt DATETIME2(0) NULL,
    BookingCloseAt DATETIME2(0) NULL,
    TotalSeats INT NOT NULL,
    AvailableSeats INT NOT NULL,
    ReservedSeats INT NOT NULL CONSTRAINT DF_TourSchedules_ReservedSeats DEFAULT (0),
    AdultPrice DECIMAL(18,2) NOT NULL,
    ChildPrice DECIMAL(18,2) NULL,
    InfantPrice DECIMAL(18,2) NULL,
    SingleSupplement DECIMAL(18,2) NULL,
    Status TINYINT NOT NULL CONSTRAINT DF_TourSchedules_Status DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_TourSchedules_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT UQ_TourSchedules_ScheduleCode UNIQUE (ScheduleCode),
    CONSTRAINT FK_TourSchedules_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
    CONSTRAINT CK_TourSchedules_TotalSeats CHECK (TotalSeats > 0),
    CONSTRAINT CK_TourSchedules_AvailableSeats CHECK (AvailableSeats >= 0),
    CONSTRAINT CK_TourSchedules_ReservedSeats CHECK (ReservedSeats >= 0),
    CONSTRAINT CK_TourSchedules_Dates CHECK (ReturnDate >= DepartureDate),
    CONSTRAINT CK_TourSchedules_Status CHECK (Status IN (0,1,2,3,4))
);
GO

CREATE TABLE catalog.Hotels (
    HotelId BIGINT IDENTITY(1,1) PRIMARY KEY,
    HotelCode NVARCHAR(50) NOT NULL,
    HotelName NVARCHAR(200) NOT NULL,
    DestinationId INT NOT NULL,
    StarRating DECIMAL(2,1) NULL,
    AddressLine NVARCHAR(300) NULL,
    PhoneNumber NVARCHAR(20) NULL,
    Email NVARCHAR(255) NULL,
    Description NVARCHAR(MAX) NULL,
    CheckInTime TIME(0) NULL,
    CheckOutTime TIME(0) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Hotels_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Hotels_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_Hotels_HotelCode UNIQUE (HotelCode),
    CONSTRAINT FK_Hotels_Destination FOREIGN KEY (DestinationId) REFERENCES catalog.Destinations(DestinationId)
);
GO

CREATE TABLE catalog.HotelRoomTypes (
    HotelRoomTypeId BIGINT IDENTITY(1,1) PRIMARY KEY,
    HotelId BIGINT NOT NULL,
    RoomTypeCode NVARCHAR(50) NOT NULL,
    RoomTypeName NVARCHAR(150) NOT NULL,
    CapacityAdults INT NOT NULL,
    CapacityChildren INT NOT NULL CONSTRAINT DF_HotelRoomTypes_CapacityChildren DEFAULT (0),
    BedDescription NVARCHAR(150) NULL,
    ExtraBedFee DECIMAL(18,2) NULL,
    Notes NVARCHAR(500) NULL,
    CONSTRAINT UQ_HotelRoomTypes_HotelId_RoomTypeCode UNIQUE (HotelId, RoomTypeCode),
    CONSTRAINT FK_HotelRoomTypes_Hotel FOREIGN KEY (HotelId) REFERENCES catalog.Hotels(HotelId)
);
GO

CREATE TABLE catalog.TransportationTypes (
    TransportationTypeId INT IDENTITY(1,1) PRIMARY KEY,
    TypeCode NVARCHAR(50) NOT NULL,
    TypeName NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_TransportationTypes_TypeCode UNIQUE (TypeCode)
);
GO

CREATE TABLE catalog.Transportations (
    TransportationId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TransportationCode NVARCHAR(50) NOT NULL,
    TransportationTypeId INT NOT NULL,
    ProviderName NVARCHAR(150) NOT NULL,
    VehicleName NVARCHAR(150) NULL,
    SeatCapacity INT NULL,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Transportations_IsActive DEFAULT (1),
    CONSTRAINT UQ_Transportations_TransportationCode UNIQUE (TransportationCode),
    CONSTRAINT FK_Transportations_Type FOREIGN KEY (TransportationTypeId) REFERENCES catalog.TransportationTypes(TransportationTypeId)
);
GO

CREATE TABLE catalog.TourItineraryDays (
    ItineraryDayId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TourId BIGINT NOT NULL,
    DayNumber INT NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Summary NVARCHAR(500) NULL,
    Description NVARCHAR(MAX) NULL,
    BreakfastIncluded BIT NOT NULL CONSTRAINT DF_TourItineraryDays_Breakfast DEFAULT (0),
    LunchIncluded BIT NOT NULL CONSTRAINT DF_TourItineraryDays_Lunch DEFAULT (0),
    DinnerIncluded BIT NOT NULL CONSTRAINT DF_TourItineraryDays_Dinner DEFAULT (0),
    HotelId BIGINT NULL,
    TransportationId BIGINT NULL,
    CONSTRAINT UQ_TourItineraryDays_TourId_DayNumber UNIQUE (TourId, DayNumber),
    CONSTRAINT FK_TourItineraryDays_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
    CONSTRAINT FK_TourItineraryDays_Hotel FOREIGN KEY (HotelId) REFERENCES catalog.Hotels(HotelId),
    CONSTRAINT FK_TourItineraryDays_Transportation FOREIGN KEY (TransportationId) REFERENCES catalog.Transportations(TransportationId)
);
GO
CREATE TABLE booking.Promotions (
    PromotionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PromotionCode NVARCHAR(50) NOT NULL,
    PromotionName NVARCHAR(200) NOT NULL,
    PromotionType TINYINT NOT NULL,
    Description NVARCHAR(1000) NULL,
    DiscountPercent DECIMAL(5,2) NULL,
    DiscountAmount DECIMAL(18,2) NULL,
    MaxDiscountAmount DECIMAL(18,2) NULL,
    MinOrderValue DECIMAL(18,2) NULL,
    MaxUsageCount INT NULL,
    MaxUsagePerUser INT NULL,
    StartAt DATETIME2(0) NOT NULL,
    EndAt DATETIME2(0) NOT NULL,
    IsAutoApply BIT NOT NULL CONSTRAINT DF_Promotions_IsAutoApply DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_Promotions_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Promotions_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT UQ_Promotions_PromotionCode UNIQUE (PromotionCode),
    CONSTRAINT CK_Promotions_PromotionType CHECK (PromotionType IN (1,2,3)),
    CONSTRAINT CK_Promotions_DateRange CHECK (EndAt >= StartAt)
);
GO

CREATE TABLE booking.PromotionTours (
    PromotionTourId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PromotionId BIGINT NOT NULL,
    TourId BIGINT NOT NULL,
    CONSTRAINT UQ_PromotionTours_PromotionId_TourId UNIQUE (PromotionId, TourId),
    CONSTRAINT FK_PromotionTours_Promotion FOREIGN KEY (PromotionId) REFERENCES booking.Promotions(PromotionId),
    CONSTRAINT FK_PromotionTours_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId)
);
GO

CREATE TABLE booking.Coupons (
    CouponId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CouponCode NVARCHAR(50) NOT NULL,
    PromotionId BIGINT NOT NULL,
    IssuedToUserId BIGINT NULL,
    IsUsed BIT NOT NULL CONSTRAINT DF_Coupons_IsUsed DEFAULT (0),
    UsedAt DATETIME2(0) NULL,
    ExpiresAt DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Coupons_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_Coupons_CouponCode UNIQUE (CouponCode),
    CONSTRAINT FK_Coupons_Promotion FOREIGN KEY (PromotionId) REFERENCES booking.Promotions(PromotionId),
    CONSTRAINT FK_Coupons_IssuedToUser FOREIGN KEY (IssuedToUserId) REFERENCES auth.Users(UserId)
);
GO

CREATE TABLE booking.Bookings (
    BookingId BIGINT IDENTITY(1,1) PRIMARY KEY,
    BookingCode NVARCHAR(50) NOT NULL,
    UserId BIGINT NOT NULL,
    TourId BIGINT NOT NULL,
    TourScheduleId BIGINT NOT NULL,
    ContactName NVARCHAR(150) NOT NULL,
    ContactEmail NVARCHAR(255) NOT NULL,
    ContactPhone NVARCHAR(20) NOT NULL,
    AdultCount INT NOT NULL CONSTRAINT DF_Bookings_AdultCount DEFAULT (1),
    ChildCount INT NOT NULL CONSTRAINT DF_Bookings_ChildCount DEFAULT (0),
    InfantCount INT NOT NULL CONSTRAINT DF_Bookings_InfantCount DEFAULT (0),
    BaseAmount DECIMAL(18,2) NOT NULL,
    DiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Bookings_DiscountAmount DEFAULT (0),
    TaxAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Bookings_TaxAmount DEFAULT (0),
    ServiceFee DECIMAL(18,2) NOT NULL CONSTRAINT DF_Bookings_ServiceFee DEFAULT (0),
    TotalAmount DECIMAL(18,2) NOT NULL,
    CurrencyCode NVARCHAR(10) NOT NULL CONSTRAINT DF_Bookings_CurrencyCode DEFAULT (N'VND'),
    BookingStatus TINYINT NOT NULL CONSTRAINT DF_Bookings_BookingStatus DEFAULT (0),
    PaymentStatus TINYINT NOT NULL CONSTRAINT DF_Bookings_PaymentStatus DEFAULT (0),
    SpecialRequests NVARCHAR(1000) NULL,
    CouponId BIGINT NULL,
    PromotionId BIGINT NULL,
    CancelledAt DATETIME2(0) NULL,
    CancellationReason NVARCHAR(500) NULL,
    ConfirmedAt DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Bookings_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT UQ_Bookings_BookingCode UNIQUE (BookingCode),
    CONSTRAINT FK_Bookings_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId),
    CONSTRAINT FK_Bookings_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
    CONSTRAINT FK_Bookings_TourSchedule FOREIGN KEY (TourScheduleId) REFERENCES catalog.TourSchedules(TourScheduleId),
    CONSTRAINT FK_Bookings_Coupon FOREIGN KEY (CouponId) REFERENCES booking.Coupons(CouponId),
    CONSTRAINT FK_Bookings_Promotion FOREIGN KEY (PromotionId) REFERENCES booking.Promotions(PromotionId),
    CONSTRAINT CK_Bookings_BookingStatus CHECK (BookingStatus IN (0,1,2,3,4,5)),
    CONSTRAINT CK_Bookings_PaymentStatus CHECK (PaymentStatus IN (0,1,2,3,4))
);
GO

CREATE TABLE booking.BookingTravelers (
    TravelerId BIGINT IDENTITY(1,1) PRIMARY KEY,
    BookingId BIGINT NOT NULL,
    TravelerType TINYINT NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    Gender TINYINT NULL,
    DateOfBirth DATE NULL,
    Nationality NVARCHAR(100) NULL,
    PassportNumber NVARCHAR(50) NULL,
    PassportExpiryDate DATE NULL,
    RoomSharingGroup NVARCHAR(50) NULL,
    IsLeadTraveler BIT NOT NULL CONSTRAINT DF_BookingTravelers_IsLeadTraveler DEFAULT (0),
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_BookingTravelers_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT FK_BookingTravelers_Booking FOREIGN KEY (BookingId) REFERENCES booking.Bookings(BookingId),
    CONSTRAINT CK_BookingTravelers_TravelerType CHECK (TravelerType IN (1,2,3)),
    CONSTRAINT CK_BookingTravelers_Gender CHECK (Gender IS NULL OR Gender IN (1,2,3))
);
GO

CREATE TABLE booking.BookingStatusesHistory (
    BookingStatusHistoryId BIGINT IDENTITY(1,1) PRIMARY KEY,
    BookingId BIGINT NOT NULL,
    OldStatus TINYINT NULL,
    NewStatus TINYINT NOT NULL,
    ChangedByUserId BIGINT NULL,
    Notes NVARCHAR(500) NULL,
    ChangedAt DATETIME2(0) NOT NULL CONSTRAINT DF_BookingStatusesHistory_ChangedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT FK_BookingStatusesHistory_Booking FOREIGN KEY (BookingId) REFERENCES booking.Bookings(BookingId),
    CONSTRAINT FK_BookingStatusesHistory_ChangedBy FOREIGN KEY (ChangedByUserId) REFERENCES auth.Users(UserId)
);
GO

CREATE TABLE booking.Payments (
    PaymentId BIGINT IDENTITY(1,1) PRIMARY KEY,
    BookingId BIGINT NOT NULL,
    PaymentCode NVARCHAR(50) NOT NULL,
    PaymentMethod TINYINT NOT NULL,
    PaymentGateway NVARCHAR(100) NULL,
    TransactionReference NVARCHAR(150) NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CurrencyCode NVARCHAR(10) NOT NULL CONSTRAINT DF_Payments_CurrencyCode DEFAULT (N'VND'),
    PaymentStatus TINYINT NOT NULL CONSTRAINT DF_Payments_PaymentStatus DEFAULT (0),
    PaidAt DATETIME2(0) NULL,
    FailureReason NVARCHAR(500) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT UQ_Payments_PaymentCode UNIQUE (PaymentCode),
    CONSTRAINT FK_Payments_Booking FOREIGN KEY (BookingId) REFERENCES booking.Bookings(BookingId),
    CONSTRAINT CK_Payments_PaymentMethod CHECK (PaymentMethod IN (1,2,3,4,5)),
    CONSTRAINT CK_Payments_PaymentStatus CHECK (PaymentStatus IN (0,1,2,3,4))
);
GO

CREATE TABLE booking.Refunds (
    RefundId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PaymentId BIGINT NOT NULL,
    RefundCode NVARCHAR(50) NOT NULL,
    RefundAmount DECIMAL(18,2) NOT NULL,
    RefundStatus TINYINT NOT NULL CONSTRAINT DF_Refunds_RefundStatus DEFAULT (0),
    Reason NVARCHAR(500) NULL,
    ProcessedAt DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Refunds_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_Refunds_RefundCode UNIQUE (RefundCode),
    CONSTRAINT FK_Refunds_Payment FOREIGN KEY (PaymentId) REFERENCES booking.Payments(PaymentId),
    CONSTRAINT CK_Refunds_RefundStatus CHECK (RefundStatus IN (0,1,2,3))
);
GO

CREATE TABLE booking.BookingPromotionUsages (
    BookingPromotionUsageId BIGINT IDENTITY(1,1) PRIMARY KEY,
    BookingId BIGINT NOT NULL,
    PromotionId BIGINT NOT NULL,
    CouponId BIGINT NULL,
    DiscountAmount DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_BookingPromotionUsages_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT FK_BookingPromotionUsages_Booking FOREIGN KEY (BookingId) REFERENCES booking.Bookings(BookingId),
    CONSTRAINT FK_BookingPromotionUsages_Promotion FOREIGN KEY (PromotionId) REFERENCES booking.Promotions(PromotionId),
    CONSTRAINT FK_BookingPromotionUsages_Coupon FOREIGN KEY (CouponId) REFERENCES booking.Coupons(CouponId)
);
GO
CREATE TABLE content.Reviews (
    ReviewId BIGINT IDENTITY(1,1) PRIMARY KEY,
    TourId BIGINT NOT NULL,
    UserId BIGINT NOT NULL,
    BookingId BIGINT NULL,
    Rating DECIMAL(2,1) NOT NULL,
    Title NVARCHAR(200) NULL,
    Comment NVARCHAR(2000) NULL,
    IsAnonymous BIT NOT NULL CONSTRAINT DF_Reviews_IsAnonymous DEFAULT (0),
    ModerationStatus TINYINT NOT NULL CONSTRAINT DF_Reviews_ModerationStatus DEFAULT (0),
    HelpfulCount INT NOT NULL CONSTRAINT DF_Reviews_HelpfulCount DEFAULT (0),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Reviews_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT FK_Reviews_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId),
    CONSTRAINT FK_Reviews_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId),
    CONSTRAINT FK_Reviews_Booking FOREIGN KEY (BookingId) REFERENCES booking.Bookings(BookingId),
    CONSTRAINT CK_Reviews_Rating CHECK (Rating >= 1 AND Rating <= 5),
    CONSTRAINT CK_Reviews_ModerationStatus CHECK (ModerationStatus IN (0,1,2,3))
);
GO

CREATE TABLE content.ReviewImages (
    ReviewImageId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReviewId BIGINT NOT NULL,
    ImageUrl NVARCHAR(500) NOT NULL,
    DisplayOrder INT NOT NULL CONSTRAINT DF_ReviewImages_DisplayOrder DEFAULT (0),
    CONSTRAINT FK_ReviewImages_Review FOREIGN KEY (ReviewId) REFERENCES content.Reviews(ReviewId)
);
GO

CREATE TABLE content.ReviewLikes (
    ReviewLikeId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReviewId BIGINT NOT NULL,
    UserId BIGINT NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ReviewLikes_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_ReviewLikes_ReviewId_UserId UNIQUE (ReviewId, UserId),
    CONSTRAINT FK_ReviewLikes_Review FOREIGN KEY (ReviewId) REFERENCES content.Reviews(ReviewId),
    CONSTRAINT FK_ReviewLikes_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId)
);
GO

CREATE TABLE content.ReviewReplies (
    ReviewReplyId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ReviewId BIGINT NOT NULL,
    UserId BIGINT NOT NULL,
    ReplyContent NVARCHAR(1000) NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ReviewReplies_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    CONSTRAINT FK_ReviewReplies_Review FOREIGN KEY (ReviewId) REFERENCES content.Reviews(ReviewId),
    CONSTRAINT FK_ReviewReplies_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId)
);
GO

CREATE TABLE content.Wishlists (
    WishlistId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    TourId BIGINT NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Wishlists_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_Wishlists_UserId_TourId UNIQUE (UserId, TourId),
    CONSTRAINT FK_Wishlists_User FOREIGN KEY (UserId) REFERENCES auth.Users(UserId),
    CONSTRAINT FK_Wishlists_Tour FOREIGN KEY (TourId) REFERENCES catalog.Tours(TourId)
);
GO

CREATE TABLE content.Articles (
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
);
GO

CREATE TABLE integration.Notifications (
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
);
GO

CREATE INDEX IX_Users_NormalizedEmail ON auth.Users(NormalizedEmail);
CREATE INDEX IX_UserSessions_UserId ON auth.UserSessions(UserId, ExpiresAt);
CREATE INDEX IX_Tours_CategoryId_IsPublished ON catalog.Tours(CategoryId, IsPublished, IsFeatured);
CREATE INDEX IX_Tours_StartDestination_EndDestination ON catalog.Tours(StartDestinationId, EndDestinationId);
CREATE INDEX IX_TourSchedules_TourId_DepartureDate ON catalog.TourSchedules(TourId, DepartureDate);
CREATE INDEX IX_Bookings_UserId_CreatedAt ON booking.Bookings(UserId, CreatedAt DESC);
CREATE INDEX IX_Bookings_TourScheduleId_Status ON booking.Bookings(TourScheduleId, BookingStatus);
CREATE INDEX IX_Payments_BookingId_Status ON booking.Payments(BookingId, PaymentStatus);
CREATE INDEX IX_Reviews_TourId_ModerationStatus ON content.Reviews(TourId, ModerationStatus, CreatedAt DESC);
CREATE INDEX IX_Wishlists_UserId ON content.Wishlists(UserId);
GO

INSERT INTO auth.Roles (RoleCode, RoleName, Description, IsSystemRole)
VALUES
    (N'ADMIN', N'Administrator', N'Full system access', 1),
    (N'STAFF', N'Staff', N'Operations and customer support', 1),
    (N'CUSTOMER', N'Customer', N'Normal traveler account', 1);
GO

INSERT INTO catalog.TransportationTypes (TypeCode, TypeName)
VALUES
    (N'FLIGHT', N'Flight'),
    (N'COACH', N'Coach'),
    (N'TRAIN', N'Train'),
    (N'SHIP', N'Ship'),
    (N'CAR', N'Car');
GO

