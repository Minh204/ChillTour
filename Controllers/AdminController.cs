using System.Security.Claims;
using ChillTour.Data;
using ChillTour.Data.Entities;
using ChillTour.Models.Admin;
using ChillTour.Security;
using ChillTour.Services.Auth;
using ChillTour.Services.Notifications;
using ChillTour.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Controllers;

[Authorize]
public class AdminController : Controller
{
    private const int TourPageSize = 10;
    private const int OrderPageSize = 10;
    private const byte BookingPendingPayment = 0;
    private const byte BookingPendingDepositVerification = 1;
    private const byte BookingDepositPaid = 2;
    private const byte BookingConfirmed = 3;
    private const byte BookingCancelled = 4;
    private const byte BookingRefunded = 5;
    private const byte BookingPendingFullPayment = 6;
    private const byte BookingPendingFullPaymentVerification = 7;
    private const byte BookingFullyPaid = 8;
    private const byte BookingRefundRequested = 9;
    private const byte BookingPendingRefund = 10;
    private const byte PaymentPending = 0;
    private const byte PaymentPendingVerification = 1;
    private const byte PaymentDepositPaid = 2;
    private const byte PaymentFullyPaid = 3;
    private const byte PaymentFailed = 4;
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    private readonly ChillTourDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IWebHostEnvironment _environment;
    private readonly INotificationService _notificationService;
    private readonly IReportService _reportService;

    public AdminController(ChillTourDbContext dbContext, IPasswordHasher passwordHasher, IWebHostEnvironment environment, INotificationService notificationService, IReportService reportService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _environment = environment;
        _notificationService = notificationService;
        _reportService = reportService;
    }

    [Authorize(Policy = PermissionConstants.AccessAdmin)]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var startOfWeek = today.AddDays(-6);
        var tomorrow = today.AddDays(1);
        var todayOnly = DateOnly.FromDateTime(today);
        var next30Days = todayOnly.AddDays(30);

        var totalUsers = await _dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        var newUsersThisWeek = await _dbContext.Users.AsNoTracking().CountAsync(x => x.CreatedAt >= startOfWeek, cancellationToken);
        var totalBookings = await _dbContext.Bookings.AsNoTracking().CountAsync(cancellationToken);
        var pendingBookings = await _dbContext.Bookings.AsNoTracking().CountAsync(x => x.BookingStatus == BookingPendingPayment || x.BookingStatus == BookingPendingDepositVerification || x.BookingStatus == BookingPendingFullPaymentVerification || x.BookingStatus == BookingRefundRequested || x.BookingStatus == BookingPendingRefund, cancellationToken);
        var confirmedBookings = await _dbContext.Bookings.AsNoTracking().CountAsync(x => x.BookingStatus == BookingConfirmed || x.BookingStatus == BookingFullyPaid, cancellationToken);
        var cancelledBookings = await _dbContext.Bookings.AsNoTracking().CountAsync(x => x.BookingStatus == BookingCancelled || x.BookingStatus == BookingRefunded || x.BookingStatus == BookingRefundRequested || x.BookingStatus == BookingPendingRefund, cancellationToken);
        var paidBookings = await _dbContext.Bookings.AsNoTracking().CountAsync(x => x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid, cancellationToken);
        var publishedTours = await _dbContext.Tours.AsNoTracking().CountAsync(x => x.IsPublished, cancellationToken);
        var upcomingSchedules = await _dbContext.TourSchedules.AsNoTracking().CountAsync(x => x.DepartureDate >= todayOnly && x.Status == 1, cancellationToken);
        var lowSeatSchedules = await _dbContext.TourSchedules.AsNoTracking().CountAsync(x => x.DepartureDate >= todayOnly && x.Status == 1 && x.AvailableSeats <= 5, cancellationToken);
        var depositRevenue = await _dbContext.Payments.AsNoTracking().Where(x => x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var expectedRevenue = await _dbContext.Bookings.AsNoTracking().Where(x => x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid).SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
        var averageRating = await _dbContext.Reviews.AsNoTracking().Where(x => x.ModerationStatus == 1).AverageAsync(x => (decimal?)x.Rating, cancellationToken) ?? 0m;
        var reviewCount = await _dbContext.Reviews.AsNoTracking().CountAsync(x => x.ModerationStatus == 1, cancellationToken);

        var bookingMetrics = await _dbContext.Bookings
            .AsNoTracking()
            .Where(x => x.CreatedAt >= startOfWeek && x.CreatedAt < tomorrow)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(x => new
            {
                Date = x.Key,
                BookingCount = x.Count()
            })
            .ToListAsync(cancellationToken);

        var userMetrics = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.CreatedAt >= startOfWeek && x.CreatedAt < tomorrow)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(x => new
            {
                Date = x.Key,
                UserCount = x.Count()
            })
            .ToListAsync(cancellationToken);

        var revenueMetrics = await _dbContext.Payments
            .AsNoTracking()
            .Where(x => x.CreatedAt >= startOfWeek && x.CreatedAt < tomorrow && (x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid))
            .GroupBy(x => x.CreatedAt.Date)
            .Select(x => new
            {
                Date = x.Key,
                DepositRevenue = x.Sum(y => y.Amount)
            })
            .ToListAsync(cancellationToken);

        var dailyMetrics = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = startOfWeek.AddDays(offset).Date;
                return new AdminDashboardDailyMetricViewModel
                {
                    Date = DateOnly.FromDateTime(date),
                    BookingCount = bookingMetrics.FirstOrDefault(x => x.Date == date)?.BookingCount ?? 0,
                    UserCount = userMetrics.FirstOrDefault(x => x.Date == date)?.UserCount ?? 0,
                    DepositRevenue = revenueMetrics.FirstOrDefault(x => x.Date == date)?.DepositRevenue ?? 0m
                };
            })
            .ToList();

        var maxBookings = Math.Max(dailyMetrics.Max(x => x.BookingCount), 1);
        var maxUsers = Math.Max(dailyMetrics.Max(x => x.UserCount), 1);
        foreach (var metric in dailyMetrics)
        {
            metric.BookingPercent = Math.Max(8, (int)Math.Round(metric.BookingCount * 100m / maxBookings));
            metric.UserPercent = Math.Max(8, (int)Math.Round(metric.UserCount * 100m / maxUsers));
        }

        var recentBookings = await _dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Select(x => new AdminDashboardRecentBookingViewModel
            {
                BookingCode = x.BookingCode,
                CustomerName = x.User.FullName,
                TourName = x.Tour.TourName,
                DepartureDate = x.TourSchedule.DepartureDate,
                TotalAmount = x.TotalAmount,
                BookingStatus = x.BookingStatus,
                PaymentStatus = x.PaymentStatus,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var topTours = await _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.Bookings.Any())
            .OrderByDescending(x => x.Bookings.Count)
            .ThenByDescending(x => x.Bookings.Sum(b => b.TotalAmount))
            .Take(5)
            .Select(x => new AdminDashboardTopTourViewModel
            {
                TourName = x.TourName,
                BookingCount = x.Bookings.Count,
                Revenue = x.Bookings.Where(b => b.PaymentStatus == PaymentDepositPaid || b.PaymentStatus == PaymentFullyPaid).Sum(b => b.TotalAmount),
                Rating = x.Reviews.Where(r => r.ModerationStatus == 1).Average(r => (decimal?)r.Rating) ?? 0m,
                ReviewCount = x.Reviews.Count(r => r.ModerationStatus == 1)
            })
            .ToListAsync(cancellationToken);

        var upcomingDepartures = await _dbContext.TourSchedules
            .AsNoTracking()
            .Include(x => x.Tour)
            .Where(x => x.DepartureDate >= todayOnly && x.DepartureDate <= next30Days && x.Status == 1)
            .OrderBy(x => x.DepartureDate)
            .Take(5)
            .Select(x => new AdminDashboardUpcomingScheduleViewModel
            {
                TourName = x.Tour.TourName,
                DepartureDate = x.DepartureDate,
                AvailableSeats = x.AvailableSeats,
                TotalSeats = x.TotalSeats,
                AdultPrice = x.AdultPrice
            })
            .ToListAsync(cancellationToken);

        var operationalBookings = confirmedBookings + cancelledBookings;

        return View(new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            NewUsersThisWeek = newUsersThisWeek,
            TotalBookings = totalBookings,
            PendingBookings = pendingBookings,
            ConfirmedBookings = confirmedBookings,
            CancelledBookings = cancelledBookings,
            PaidBookings = paidBookings,
            PublishedTours = publishedTours,
            UpcomingSchedules = upcomingSchedules,
            LowSeatSchedules = lowSeatSchedules,
            DepositRevenue = depositRevenue,
            ExpectedRevenue = expectedRevenue,
            AverageRating = averageRating,
            ReviewCount = reviewCount,
            ConfirmationRate = operationalBookings == 0 ? 0m : decimal.Round(confirmedBookings * 100m / operationalBookings, 1),
            DailyMetrics = dailyMetrics,
            RecentBookings = recentBookings,
            TopTours = topTours,
            UpcomingDepartures = upcomingDepartures
        });
    }

    [Authorize(Policy = PermissionConstants.ViewReports)]
    [HttpGet]
    public async Task<IActionResult> Reports([FromQuery] AdminReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var summary = await _reportService.GetSummaryAsync(filter, User, cancellationToken);
        return View(await BuildReportPageViewModelAsync(filter, summary, cancellationToken));
    }

    [Authorize(Policy = PermissionConstants.ViewReports)]
    [HttpGet]
    public async Task<IActionResult> ExportReportsExcel([FromQuery] AdminReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var fileBytes = await _reportService.ExportExcelAsync(filter, User, cancellationToken);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ChillTour-Report-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
    }

    [Authorize(Policy = PermissionConstants.ViewReports)]
    [HttpGet]
    public async Task<IActionResult> ExportReportsPdf([FromQuery] AdminReportFilterViewModel filter, CancellationToken cancellationToken)
    {
        var fileBytes = await _reportService.ExportPdfAsync(filter, User, cancellationToken);
        return File(
            fileBytes,
            "application/pdf",
            $"ChillTour-Report-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
    }

    [Authorize(Policy = PermissionConstants.ManageUsers)]
    [HttpGet]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminUserItemViewModel
            {
                UserId = x.UserId,
                FullName = x.FullName,
                Email = x.Email,
                PhoneNumber = x.PhoneNumber,
                RoleCode = x.UserRoles.Select(r => r.Role.RoleCode).FirstOrDefault() ?? RoleConstants.Customer,
                StatusLabel = x.Status == 2 ? "Đã khóa" : "Đang hoạt động",
                IsLocked = x.Status == 2,
                CreatedAt = x.CreatedAt,
                LastLoginAt = x.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            user.RoleCode = NormalizeRoleCode(user.RoleCode);
        }

        return View(new AdminUsersViewModel
        {
            TotalUsers = users.Count,
            ActiveUsers = users.Count(x => !x.IsLocked),
            LockedUsers = users.Count(x => x.IsLocked),
            Users = users
        });
    }

    private async Task<AdminReportPageViewModel> BuildReportPageViewModelAsync(AdminReportFilterViewModel filter, AdminReportSummaryViewModel summary, CancellationToken cancellationToken)
    {
        var tours = await _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.IsPublished || RoleConstants.ManageTours.Any(User.IsInRole))
            .OrderBy(x => x.TourName)
            .Select(x => new SelectListItem
            {
                Value = x.TourId.ToString(),
                Text = x.TourCode + " - " + x.TourName
            })
            .ToListAsync(cancellationToken);

        var isAdmin = User.IsInRole(RoleConstants.Admin);
        var isDirector = User.IsInRole(RoleConstants.Director);
        var isManager = User.IsInRole(RoleConstants.Manager);
        var isAccountant = User.IsInRole(RoleConstants.Accountant);
        var isEmployee = User.IsInRole(RoleConstants.Employee);

        return new AdminReportPageViewModel
        {
            Filter = filter,
            Summary = summary,
            RoleLabel = isAdmin ? "Admin" :
                isDirector ? "Director" :
                isManager ? "Manager" :
                isAccountant ? "Accountant" :
                isEmployee ? "Employee" : "BackOffice",
            CanSeeBusiness = isAdmin || isDirector,
            CanSeeOperations = isAdmin || isDirector || isManager,
            CanSeeFinance = isAdmin || isAccountant,
            CanSeeCustomer = isAdmin || isDirector,
            CanSeeStaff = isAdmin || isManager || isEmployee,
            PeriodOptions =
            [
                new SelectListItem("Theo ngày", "day", string.Equals(filter.Period, "day", StringComparison.OrdinalIgnoreCase)),
                new SelectListItem("Theo tháng", "month", string.IsNullOrWhiteSpace(filter.Period) || string.Equals(filter.Period, "month", StringComparison.OrdinalIgnoreCase)),
                new SelectListItem("Theo năm", "year", string.Equals(filter.Period, "year", StringComparison.OrdinalIgnoreCase)),
                new SelectListItem("Tùy chọn", "custom", string.Equals(filter.Period, "custom", StringComparison.OrdinalIgnoreCase))
            ],
            TourOptions = [new SelectListItem("Tất cả tour", string.Empty, !filter.TourId.HasValue), .. tours.Select(x => new SelectListItem(x.Text, x.Value, x.Value == filter.TourId?.ToString()))],
            BookingStatusOptions =
            [
                new SelectListItem("Tất cả trạng thái đơn", string.Empty, !filter.BookingStatus.HasValue),
                new SelectListItem("Chờ thanh toán", "0", filter.BookingStatus == 0),
                new SelectListItem("Chờ xác nhận cọc", "1", filter.BookingStatus == 1),
                new SelectListItem("Đã cọc", "2", filter.BookingStatus == 2),
                new SelectListItem("Đã xác nhận", "3", filter.BookingStatus == 3),
                new SelectListItem("Đã hủy", "4", filter.BookingStatus == 4),
                new SelectListItem("Đã hoàn tiền", "5", filter.BookingStatus == 5),
                new SelectListItem("Chờ thanh toán còn lại", "6", filter.BookingStatus == 6),
                new SelectListItem("Chờ xác nhận thanh toán đủ", "7", filter.BookingStatus == 7),
                new SelectListItem("Đã thanh toán đủ", "8", filter.BookingStatus == 8),
                new SelectListItem("Yêu cầu hoàn tiền", "9", filter.BookingStatus == 9),
                new SelectListItem("Chờ hoàn tiền", "10", filter.BookingStatus == 10)
            ],
            PaymentStatusOptions =
            [
                new SelectListItem("Tất cả trạng thái thanh toán", string.Empty, !filter.PaymentStatus.HasValue),
                new SelectListItem("Chưa thanh toán", "0", filter.PaymentStatus == 0),
                new SelectListItem("Chờ đối soát", "1", filter.PaymentStatus == 1),
                new SelectListItem("Đã cọc", "2", filter.PaymentStatus == 2),
                new SelectListItem("Đã thanh toán đủ", "3", filter.PaymentStatus == 3),
                new SelectListItem("Thanh toán lỗi", "4", filter.PaymentStatus == 4)
            ],
            PaymentMethodOptions =
            [
                new SelectListItem("Tất cả phương thức", string.Empty, !filter.PaymentMethod.HasValue),
                new SelectListItem("VNPay", "1", filter.PaymentMethod == 1),
                new SelectListItem("Chuyển khoản", "2", filter.PaymentMethod == 2),
                new SelectListItem("Tiền mặt", "3", filter.PaymentMethod == 3)
            ]
        };
    }

    [Authorize(Policy = PermissionConstants.ManageUsers)]
    [HttpGet]
    public IActionResult CreateUser()
    {
        return View(new CreateUserViewModel());
    }

    [Authorize(Policy = PermissionConstants.ManageUsers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToUpperInvariant();
        var emailExists = await _dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
            return View(model);
        }

        var role = await FindRoleByCodeAsync(model.RoleCode, cancellationToken);
        if (role is null)
        {
            ModelState.AddModelError(nameof(model.RoleCode), "Không tìm thấy quyền được chọn.");
            return View(model);
        }

        var passwordHash = _passwordHasher.HashPassword(model.Password, out var salt);
        var user = new User
        {
            Email = model.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            PasswordHash = passwordHash,
            PasswordSalt = salt,
            Status = 1,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = role.RoleId,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = GetCurrentUserId()
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = "Tạo tài khoản thành công.";
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Policy = PermissionConstants.ManageUsers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(long userId, string roleCode, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (user is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy tài khoản.";
            return RedirectToAction(nameof(Users));
        }

        var role = await FindRoleByCodeAsync(roleCode, cancellationToken);
        if (role is null)
        {
            TempData["AdminErrorMessage"] = "Quyền không hợp lệ.";
            return RedirectToAction(nameof(Users));
        }

        _dbContext.UserRoles.RemoveRange(user.UserRoles);
        _dbContext.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = role.RoleId,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = GetCurrentUserId()
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = $"Đã đổi quyền của {user.FullName} sang {NormalizeRoleCode(role.RoleCode)}.";
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Policy = PermissionConstants.ManageUsers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(long userId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == userId)
        {
            TempData["AdminErrorMessage"] = "Bạn không thể tự khóa tài khoản của chính mình.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy tài khoản.";
            return RedirectToAction(nameof(Users));
        }

        var isLocked = user.Status == 2;
        user.Status = isLocked ? (byte)1 : (byte)2;
        user.LockoutEndAt = isLocked ? null : DateTime.UtcNow.AddYears(100);
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminSuccessMessage"] = isLocked
            ? $"Đã mở khóa tài khoản {user.Email}."
            : $"Đã khóa tài khoản {user.Email}.";

        return RedirectToAction(nameof(Users));
    }

    [Authorize(Policy = PermissionConstants.ViewBookings)]
    [HttpGet]
    public async Task<IActionResult> Orders(int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var query = _dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .Include(x => x.Payments)
            .OrderByDescending(x => x.CreatedAt);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)OrderPageSize);

        if (page > totalPages)
        {
            page = totalPages;
        }

        var bookingEntities = await query
            .Skip((page - 1) * OrderPageSize)
            .Take(OrderPageSize)
            .ToListAsync(cancellationToken);

        var bookings = bookingEntities
            .Select(x =>
            {
                var latestPayment = x.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
                var remainingAmount = Math.Max(x.TotalAmount - x.PaidAmount, 0m);

                return new BookingItemViewModel
                {
                    BookingId = x.BookingId,
                    BookingCode = x.BookingCode,
                    CustomerName = x.User.FullName,
                    ContactName = x.ContactName,
                    ContactEmail = x.ContactEmail,
                    ContactPhone = x.ContactPhone,
                    TourName = x.Tour.TourName,
                    TourCode = x.Tour.TourCode,
                    DepartureDate = x.TourSchedule.DepartureDate,
                    ReturnDate = x.TourSchedule.ReturnDate,
                    AdultCount = x.AdultCount,
                    ChildCount = x.ChildCount,
                    InfantCount = x.InfantCount,
                    Travelers = x.AdultCount + x.ChildCount + x.InfantCount,
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    RemainingAmount = remainingAmount,
                    BookingStatus = x.BookingStatus,
                    PaymentStatus = x.PaymentStatus,
                    BalanceDueAt = x.BalanceDueAt,
                    LatestPaymentId = latestPayment?.PaymentId,
                    LatestPaymentCode = latestPayment?.PaymentCode ?? string.Empty,
                    LatestPaymentAmount = latestPayment?.Amount ?? 0m,
                    LatestPaymentStatus = latestPayment?.PaymentStatus,
                    LatestPaymentTransactionReference = latestPayment?.TransactionReference,
                    HasPaymentToVerify = latestPayment is not null
                        && latestPayment.PaymentStatus == PaymentPending
                        && !string.IsNullOrWhiteSpace(latestPayment.TransactionReference),
                    CanStaffProcess = x.PaymentStatus is PaymentDepositPaid or PaymentFullyPaid,
                    SpecialRequests = x.SpecialRequests,
                    CreatedAt = x.CreatedAt
                };
            })
            .ToList();

        var statusCounts = await _dbContext.Bookings
            .AsNoTracking()
            .GroupBy(x => 1)
            .Select(x => new
            {
                PendingOrders = x.Count(y => y.BookingStatus == BookingPendingPayment || y.BookingStatus == BookingPendingDepositVerification || y.BookingStatus == BookingPendingFullPayment || y.BookingStatus == BookingPendingFullPaymentVerification || y.BookingStatus == BookingRefundRequested || y.BookingStatus == BookingPendingRefund),
                PendingPaymentVerifications = x.Count(y => y.PaymentStatus == PaymentPendingVerification),
                ConfirmedOrders = x.Count(y => y.BookingStatus == BookingConfirmed || y.BookingStatus == BookingFullyPaid),
                CancelledOrders = x.Count(y => y.BookingStatus == BookingCancelled || y.BookingStatus == BookingRefunded || y.BookingStatus == BookingRefundRequested || y.BookingStatus == BookingPendingRefund)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return View(new EmployeeOrdersViewModel
        {
            PendingOrders = statusCounts?.PendingOrders ?? 0,
            PendingPaymentVerifications = statusCounts?.PendingPaymentVerifications ?? 0,
            ConfirmedOrders = statusCounts?.ConfirmedOrders ?? 0,
            CancelledOrders = statusCounts?.CancelledOrders ?? 0,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = OrderPageSize,
            Bookings = bookings
        });
    }

    [Authorize(Policy = PermissionConstants.ManagePromotions)]
    [HttpGet]
    public async Task<IActionResult> Promotions(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var items = await _dbContext.Promotions
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.EndAt)
            .Select(x => new AdminPromotionItemViewModel
            {
                PromotionId = x.PromotionId,
                PromotionCode = x.PromotionCode,
                PromotionName = x.PromotionName,
                DiscountPercent = x.DiscountPercent,
                DiscountAmount = x.DiscountAmount,
                EndAt = x.EndAt,
                IsActive = x.IsActive,
                ClaimedCount = x.UserPromotions.Count,
                UsedCount = x.Bookings.Count
            })
            .ToListAsync(cancellationToken);

        return View(new AdminPromotionListViewModel
        {
            TotalPromotions = items.Count,
            ActivePromotions = items.Count(x => x.IsActive && x.EndAt >= now),
            ExpiredPromotions = items.Count(x => x.EndAt < now || !x.IsActive),
            Promotions = items
        });
    }

    [Authorize(Policy = PermissionConstants.ManageContent)]
    [HttpGet]
    public async Task<IActionResult> Articles(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Articles
            .AsNoTracking()
            .Include(x => x.AuthorUser)
            .OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new AdminArticleItemViewModel
            {
                ArticleId = x.ArticleId,
                ArticleCode = x.ArticleCode,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                PublishedAt = x.PublishedAt,
                IsPublished = x.Status == 1,
                AuthorName = x.AuthorUser != null ? x.AuthorUser.FullName : null
            })
            .ToListAsync(cancellationToken);

        return View(new AdminArticleListViewModel
        {
            TotalArticles = items.Count,
            PublishedArticles = items.Count(x => x.IsPublished),
            DraftArticles = items.Count(x => !x.IsPublished),
            Articles = items
        });
    }

    [Authorize(Policy = PermissionConstants.ManageContent)]
    [HttpGet]
    public IActionResult CreateArticle()
    {
        return View(new ArticleFormViewModel
        {
            PublishedAt = DateTime.Now,
            IsPublished = true
        });
    }

    [Authorize(Policy = PermissionConstants.ManageContent)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateArticle(ArticleFormViewModel model, CancellationToken cancellationToken)
    {
        ValidateArticleForm(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var code = model.ArticleCode.Trim().ToUpperInvariant();
        var slug = model.Slug.Trim().ToLowerInvariant();

        var duplicateCode = await _dbContext.Articles.AnyAsync(x => x.ArticleCode == code, cancellationToken);
        if (duplicateCode)
        {
            ModelState.AddModelError(nameof(model.ArticleCode), "Mã bài viết đã tồn tại.");
            return View(model);
        }

        var duplicateSlug = await _dbContext.Articles.AnyAsync(x => x.Slug == slug, cancellationToken);
        if (duplicateSlug)
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug bài viết đã tồn tại.");
            return View(model);
        }

        _dbContext.Articles.Add(new Article
        {
            ArticleCode = code,
            Title = model.Title.Trim(),
            Slug = slug,
            Summary = model.Summary.Trim(),
            ContentHtml = model.ContentHtml.Trim(),
            ThumbnailUrl = EmptyToNull(model.ThumbnailUrl),
            PublishedAt = model.PublishedAt,
            Status = model.IsPublished ? (byte)1 : (byte)0,
            AuthorUserId = GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = "Tạo bài cẩm nang thành công.";
        return RedirectToAction(nameof(Articles));
    }

    [Authorize(Policy = PermissionConstants.ManageContent)]
    [HttpGet]
    public async Task<IActionResult> EditArticle(long id, CancellationToken cancellationToken)
    {
        var article = await _dbContext.Articles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ArticleId == id, cancellationToken);

        if (article is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy bài cẩm nang.";
            return RedirectToAction(nameof(Articles));
        }

        return View(new ArticleFormViewModel
        {
            ArticleId = article.ArticleId,
            ArticleCode = article.ArticleCode,
            Title = article.Title,
            Slug = article.Slug,
            Summary = article.Summary ?? string.Empty,
            ContentHtml = article.ContentHtml ?? string.Empty,
            ThumbnailUrl = article.ThumbnailUrl,
            PublishedAt = article.PublishedAt ?? DateTime.Now,
            IsPublished = article.Status == 1
        });
    }

    [Authorize(Policy = PermissionConstants.ManageContent)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditArticle(long id, ArticleFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.ArticleId)
        {
            TempData["AdminErrorMessage"] = "Yêu cầu cập nhật bài cẩm nang không hợp lệ.";
            return RedirectToAction(nameof(Articles));
        }

        ValidateArticleForm(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var article = await _dbContext.Articles.SingleOrDefaultAsync(x => x.ArticleId == id, cancellationToken);
        if (article is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy bài cẩm nang.";
            return RedirectToAction(nameof(Articles));
        }

        var code = model.ArticleCode.Trim().ToUpperInvariant();
        var slug = model.Slug.Trim().ToLowerInvariant();

        var duplicateCode = await _dbContext.Articles.AnyAsync(x => x.ArticleId != id && x.ArticleCode == code, cancellationToken);
        if (duplicateCode)
        {
            ModelState.AddModelError(nameof(model.ArticleCode), "Mã bài viết đã tồn tại.");
            return View(model);
        }

        var duplicateSlug = await _dbContext.Articles.AnyAsync(x => x.ArticleId != id && x.Slug == slug, cancellationToken);
        if (duplicateSlug)
        {
            ModelState.AddModelError(nameof(model.Slug), "Slug bài viết đã tồn tại.");
            return View(model);
        }

        article.ArticleCode = code;
        article.Title = model.Title.Trim();
        article.Slug = slug;
        article.Summary = model.Summary.Trim();
        article.ContentHtml = model.ContentHtml.Trim();
        article.ThumbnailUrl = EmptyToNull(model.ThumbnailUrl);
        article.PublishedAt = model.PublishedAt;
        article.Status = model.IsPublished ? (byte)1 : (byte)0;
        article.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = "Cập nhật bài cẩm nang thành công.";
        return RedirectToAction(nameof(Articles));
    }

    [Authorize(Policy = PermissionConstants.ManageContent)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteArticle(long id, CancellationToken cancellationToken)
    {
        var article = await _dbContext.Articles.SingleOrDefaultAsync(x => x.ArticleId == id, cancellationToken);
        if (article is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy bài cẩm nang.";
            return RedirectToAction(nameof(Articles));
        }

        _dbContext.Articles.Remove(article);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = "Đã xóa bài cẩm nang.";
        return RedirectToAction(nameof(Articles));
    }

    [Authorize(Policy = PermissionConstants.ManagePromotions)]
    [HttpGet]
    public IActionResult CreatePromotion()
    {
        return View(new PromotionFormViewModel());
    }

    [Authorize(Policy = PermissionConstants.ManagePromotions)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePromotion(PromotionFormViewModel model, CancellationToken cancellationToken)
    {
        ValidatePromotionForm(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var code = model.PromotionCode.Trim().ToUpperInvariant();
        var exists = await _dbContext.Promotions.AnyAsync(x => x.PromotionCode == code, cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.PromotionCode), "Mã ưu đãi đã tồn tại.");
            return View(model);
        }

        var promotion = new Promotion
        {
            PromotionCode = code,
            PromotionName = model.PromotionName.Trim(),
            Description = EmptyToNull(model.Description),
            PromotionType = 1,
            DiscountPercent = model.DiscountPercent,
            DiscountAmount = null,
            MaxDiscountAmount = model.MaxDiscountAmount,
            MinOrderValue = model.MinOrderValue,
            MaxUsageCount = null,
            MaxUsagePerUser = 1,
            StartAt = model.StartAt,
            EndAt = model.EndAt,
            IsAutoApply = false,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Promotions.Add(promotion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminSuccessMessage"] = "Tạo mã ưu đãi thành công.";
        return RedirectToAction(nameof(Promotions));
    }

    [Authorize(Policy = PermissionConstants.ManagePromotions)]
    [HttpGet]
    public async Task<IActionResult> EditPromotion(long id, CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions.AsNoTracking().SingleOrDefaultAsync(x => x.PromotionId == id, cancellationToken);
        if (promotion is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy mã ưu đãi.";
            return RedirectToAction(nameof(Promotions));
        }

        return View(new PromotionFormViewModel
        {
            PromotionId = promotion.PromotionId,
            PromotionCode = promotion.PromotionCode,
            PromotionName = promotion.PromotionName,
            Description = promotion.Description,
            DiscountPercent = promotion.DiscountPercent,
            MaxDiscountAmount = promotion.MaxDiscountAmount,
            MinOrderValue = promotion.MinOrderValue,
            StartAt = promotion.StartAt,
            EndAt = promotion.EndAt,
            IsActive = promotion.IsActive
        });
    }

    [Authorize(Policy = PermissionConstants.ManagePromotions)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPromotion(long id, PromotionFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.PromotionId)
        {
            TempData["AdminErrorMessage"] = "Yêu cầu cập nhật ưu đãi không hợp lệ.";
            return RedirectToAction(nameof(Promotions));
        }

        ValidatePromotionForm(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var promotion = await _dbContext.Promotions.SingleOrDefaultAsync(x => x.PromotionId == id, cancellationToken);
        if (promotion is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy mã ưu đãi.";
            return RedirectToAction(nameof(Promotions));
        }

        var code = model.PromotionCode.Trim().ToUpperInvariant();
        var exists = await _dbContext.Promotions.AnyAsync(x => x.PromotionId != id && x.PromotionCode == code, cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.PromotionCode), "Mã ưu đãi đã tồn tại.");
            return View(model);
        }

        promotion.PromotionCode = code;
        promotion.PromotionName = model.PromotionName.Trim();
        promotion.Description = EmptyToNull(model.Description);
        promotion.DiscountPercent = model.DiscountPercent;
        promotion.DiscountAmount = null;
        promotion.MaxDiscountAmount = model.MaxDiscountAmount;
        promotion.MinOrderValue = model.MinOrderValue;
        promotion.StartAt = model.StartAt;
        promotion.EndAt = model.EndAt;
        promotion.IsActive = model.IsActive;
        promotion.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = "Cập nhật mã ưu đãi thành công.";
        return RedirectToAction(nameof(Promotions));
    }

    [Authorize(Policy = PermissionConstants.ManagePromotions)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePromotion(long id, CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions
            .Include(x => x.Bookings)
            .Include(x => x.UserPromotions)
            .SingleOrDefaultAsync(x => x.PromotionId == id, cancellationToken);

        if (promotion is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy mã ưu đãi.";
            return RedirectToAction(nameof(Promotions));
        }

        if (promotion.Bookings.Any())
        {
            promotion.IsActive = false;
            promotion.EndAt = DateTime.UtcNow;
            promotion.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["AdminSuccessMessage"] = "Ưu đãi đã được dùng trước đó, hệ thống đã chuyển sang ngừng hiệu lực thay vì xóa cứng.";
            return RedirectToAction(nameof(Promotions));
        }

        if (promotion.UserPromotions.Count > 0)
        {
            _dbContext.UserPromotions.RemoveRange(promotion.UserPromotions);
        }

        _dbContext.Promotions.Remove(promotion);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = "Đã xóa mã ưu đãi.";
        return RedirectToAction(nameof(Promotions));
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.CategoryName)
            .Select(x => new AdminCategoryItemViewModel
            {
                CategoryId = x.CategoryId,
                CategoryCode = x.CategoryCode,
                CategoryName = x.CategoryName,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive,
                TourCount = x.Tours.Count
            })
            .ToListAsync(cancellationToken);

        return View(items);
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public IActionResult CreateCategory()
    {
        return View(new CategoryFormViewModel());
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var slug = BuildSlug(model.CategoryName);
        var slugExists = await _dbContext.Categories.AnyAsync(x => x.Slug == slug, cancellationToken);
        if (slugExists)
        {
            ModelState.AddModelError(nameof(model.CategoryName), "Danh mục này đã tồn tại.");
            return View(model);
        }

        var category = new Category
        {
            CategoryCode = $"TEMP-{Guid.NewGuid():N}",
            CategoryName = model.CategoryName.Trim(),
            Slug = slug,
            Description = EmptyToNull(model.Description),
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        category.CategoryCode = $"CAT{category.CategoryId:D4}";
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminSuccessMessage"] = "Đã thêm danh mục mới.";
        return RedirectToAction(nameof(Categories));
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public async Task<IActionResult> EditCategory(int id, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.CategoryId == id, cancellationToken);
        if (category is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy danh mục.";
            return RedirectToAction(nameof(Categories));
        }

        return View(new CategoryFormViewModel
        {
            CategoryId = category.CategoryId,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive
        });
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.CategoryId)
        {
            TempData["AdminErrorMessage"] = "Yêu cầu cập nhật danh mục không hợp lệ.";
            return RedirectToAction(nameof(Categories));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var category = await _dbContext.Categories.SingleOrDefaultAsync(x => x.CategoryId == id, cancellationToken);
        if (category is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy danh mục.";
            return RedirectToAction(nameof(Categories));
        }

        var slug = BuildSlug(model.CategoryName);
        var slugExists = await _dbContext.Categories.AnyAsync(x => x.CategoryId != id && x.Slug == slug, cancellationToken);
        if (slugExists)
        {
            ModelState.AddModelError(nameof(model.CategoryName), "Danh mục này đã tồn tại.");
            return View(model);
        }

        category.CategoryName = model.CategoryName.Trim();
        category.Slug = slug;
        category.Description = EmptyToNull(model.Description);
        category.DisplayOrder = model.DisplayOrder;
        category.IsActive = model.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = "Đã cập nhật danh mục.";
        return RedirectToAction(nameof(Categories));
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public async Task<IActionResult> Destinations(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Destinations
            .AsNoTracking()
            .OrderBy(x => x.DestinationName)
            .Select(x => new AdminDestinationItemViewModel
            {
                DestinationId = x.DestinationId,
                DestinationCode = x.DestinationCode,
                DestinationName = x.DestinationName,
                DestinationType = x.DestinationType,
                CountryCode = x.CountryCode,
                ProvinceName = x.ProvinceName,
                IsFeatured = x.IsFeatured,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return View(items);
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public IActionResult CreateDestination()
    {
        return View(new DestinationFormViewModel());
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDestination(DestinationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var slug = BuildSlug(model.DestinationName);
        var slugExists = await _dbContext.Destinations.AnyAsync(x => x.Slug == slug, cancellationToken);
        if (slugExists)
        {
            ModelState.AddModelError(nameof(model.DestinationName), "Điểm đến này đã tồn tại.");
            return View(model);
        }

        var destination = new Destination
        {
            DestinationCode = $"TEMP-{Guid.NewGuid():N}",
            DestinationName = model.DestinationName.Trim(),
            DestinationType = model.DestinationType,
            Slug = slug,
            CountryCode = model.CountryCode.Trim().ToUpperInvariant(),
            ProvinceName = EmptyToNull(model.ProvinceName),
            Summary = EmptyToNull(model.Summary),
            Description = EmptyToNull(model.Description),
            IsFeatured = model.IsFeatured,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Destinations.Add(destination);
        await _dbContext.SaveChangesAsync(cancellationToken);

        destination.DestinationCode = $"DES{destination.DestinationId:D4}";
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminSuccessMessage"] = "Đã thêm điểm đến mới.";
        return RedirectToAction(nameof(Destinations));
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public async Task<IActionResult> EditDestination(int id, CancellationToken cancellationToken)
    {
        var destination = await _dbContext.Destinations.AsNoTracking().SingleOrDefaultAsync(x => x.DestinationId == id, cancellationToken);
        if (destination is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy điểm đến.";
            return RedirectToAction(nameof(Destinations));
        }

        return View(new DestinationFormViewModel
        {
            DestinationId = destination.DestinationId,
            DestinationCode = destination.DestinationCode,
            DestinationName = destination.DestinationName,
            DestinationType = destination.DestinationType,
            CountryCode = destination.CountryCode,
            ProvinceName = destination.ProvinceName,
            Summary = destination.Summary,
            Description = destination.Description,
            IsFeatured = destination.IsFeatured,
            IsActive = destination.IsActive
        });
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDestination(int id, DestinationFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.DestinationId)
        {
            TempData["AdminErrorMessage"] = "Yêu cầu cập nhật điểm đến không hợp lệ.";
            return RedirectToAction(nameof(Destinations));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var destination = await _dbContext.Destinations.SingleOrDefaultAsync(x => x.DestinationId == id, cancellationToken);
        if (destination is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy điểm đến.";
            return RedirectToAction(nameof(Destinations));
        }

        var slug = BuildSlug(model.DestinationName);
        var slugExists = await _dbContext.Destinations.AnyAsync(x => x.DestinationId != id && x.Slug == slug, cancellationToken);
        if (slugExists)
        {
            ModelState.AddModelError(nameof(model.DestinationName), "Điểm đến này đã tồn tại.");
            return View(model);
        }

        destination.DestinationName = model.DestinationName.Trim();
        destination.DestinationType = model.DestinationType;
        destination.Slug = slug;
        destination.CountryCode = model.CountryCode.Trim().ToUpperInvariant();
        destination.ProvinceName = EmptyToNull(model.ProvinceName);
        destination.Summary = EmptyToNull(model.Summary);
        destination.Description = EmptyToNull(model.Description);
        destination.IsFeatured = model.IsFeatured;
        destination.IsActive = model.IsActive;
        destination.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AdminSuccessMessage"] = "Đã cập nhật điểm đến.";
        return RedirectToAction(nameof(Destinations));
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public async Task<IActionResult> Tours(int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var totalItems = await _dbContext.Tours.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)TourPageSize);

        if (page > totalPages)
        {
            page = totalPages;
        }

        var tours = await _dbContext.Tours
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.StartDestination)
            .Include(x => x.EndDestination)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * TourPageSize)
            .Take(TourPageSize)
            .Select(x => new AdminTourItemViewModel
            {
                TourId = x.TourId,
                TourCode = x.TourCode,
                TourName = x.TourName,
                CategoryName = x.Category.CategoryName,
                RouteName = x.StartDestination.DestinationName + " -> " + x.EndDestination.DestinationName,
                DurationDays = x.DurationDays,
                BasePrice = x.BasePrice,
                IsPublished = x.IsPublished,
                IsFeatured = x.IsFeatured,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return View(new AdminTourListViewModel
        {
            CurrentPage = page,
            PageSize = TourPageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Tours = tours
        });
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public async Task<IActionResult> CreateTour(CancellationToken cancellationToken)
    {
        var model = new TourFormViewModel();
        await PopulateTourSelectionsAsync(model, cancellationToken);
        EnsureTourEditorDefaults(model);
        return View(model);
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTour(TourFormViewModel model, CancellationToken cancellationToken)
    {
        await ValidateTourSelectionsAsync(model, cancellationToken);
        ValidateUploadedImages(model, existingImageCount: 0);
        ValidateSeatInventory(model);
        ValidateTourSchedules(model);
        ValidateTourItineraryDays(model);

        if (!ModelState.IsValid)
        {
            await PopulateTourSelectionsAsync(model, cancellationToken);
            EnsureTourEditorDefaults(model);
            return View(model);
        }

        var tour = new Tour
        {
            TourCode = $"TEMP-{Guid.NewGuid():N}",
            TourName = model.TourName.Trim(),
            Slug = await EnsureUniqueTourSlugAsync(model.TourName, null, cancellationToken),
            CategoryId = model.CategoryId,
            StartDestinationId = model.StartDestinationId,
            EndDestinationId = model.EndDestinationId,
            ShortDescription = EmptyToNull(model.ShortDescription),
            Description = EmptyToNull(model.Description),
            DurationDays = model.DurationDays,
            DurationNights = model.DurationNights,
            MinGroupSize = model.MinGroupSize,
            MaxGroupSize = model.MaxGroupSize,
            TotalSeats = model.TotalSeats,
            RemainingSeats = model.RemainingSeats,
            BasePrice = model.BasePrice,
            ChildPrice = null,
            SingleSupplement = model.SingleSupplement,
            CurrencyCode = model.CurrencyCode.Trim().ToUpperInvariant(),
            DeparturePoint = EmptyToNull(model.DeparturePoint),
            ReturnPoint = EmptyToNull(model.ReturnPoint),
            PickupIncluded = model.PickupIncluded,
            IsFeatured = model.IsFeatured,
            IsPublished = model.IsPublished,
            ApprovalStatus = model.ApprovalStatus,
            SeoTitle = EmptyToNull(model.SeoTitle),
            SeoDescription = EmptyToNull(model.SeoDescription),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tours.Add(tour);
        await _dbContext.SaveChangesAsync(cancellationToken);

        tour.TourCode = GenerateTourCode(tour.TourId);
        SyncTourSchedules(tour, model);
        SyncTourItineraryDays(tour, model);
        await SyncTourImagesAsync(tour, model.UploadedImages, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminSuccessMessage"] = "Đã tạo tour mới.";
        return RedirectToAction(nameof(Tours));
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpGet]
    public async Task<IActionResult> EditTour(long id, CancellationToken cancellationToken)
    {
        var tour = await _dbContext.Tours
            .AsNoTracking()
            .Include(x => x.MediaItems)
            .Include(x => x.Schedules)
            .Include(x => x.ItineraryDays)
            .SingleOrDefaultAsync(x => x.TourId == id, cancellationToken);

        if (tour is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy tour.";
            return RedirectToAction(nameof(Tours));
        }

        var model = new TourFormViewModel
        {
            TourId = tour.TourId,
            TourCode = tour.TourCode,
            TourName = tour.TourName,
            CategoryId = tour.CategoryId,
            StartDestinationId = tour.StartDestinationId,
            EndDestinationId = tour.EndDestinationId,
            ShortDescription = tour.ShortDescription,
            Description = tour.Description,
            DurationDays = tour.DurationDays,
            DurationNights = tour.DurationNights,
            MinGroupSize = tour.MinGroupSize,
            MaxGroupSize = tour.MaxGroupSize,
            TotalSeats = tour.TotalSeats,
            RemainingSeats = tour.RemainingSeats,
            BasePrice = tour.BasePrice,
            ChildPrice = null,
            SingleSupplement = tour.SingleSupplement,
            CurrencyCode = tour.CurrencyCode,
            DeparturePoint = tour.DeparturePoint,
            ReturnPoint = tour.ReturnPoint,
            PickupIncluded = tour.PickupIncluded,
            IsFeatured = tour.IsFeatured,
            IsPublished = tour.IsPublished,
            ApprovalStatus = tour.ApprovalStatus,
            SeoTitle = tour.SeoTitle,
            SeoDescription = tour.SeoDescription,
            ExistingImages = tour.MediaItems
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new AdminTourImageViewModel
                {
                    TourMediaId = x.TourMediaId,
                    MediaUrl = x.MediaUrl,
                    IsPrimary = x.IsPrimary,
                    DisplayOrder = x.DisplayOrder
                })
                .ToList(),
            Schedules = tour.Schedules
                .OrderBy(x => x.DepartureDate)
                .Select(x => new TourDepartureEditorViewModel
                {
                    TourScheduleId = x.TourScheduleId,
                    DepartureDate = x.DepartureDate,
                    AdultPrice = x.AdultPrice
                })
                .ToList(),
            ItineraryDays = tour.ItineraryDays
                .OrderBy(x => x.DayNumber)
                .Select(x => new TourItineraryDayEditorViewModel
                {
                    ItineraryDayId = x.ItineraryDayId,
                    DayNumber = x.DayNumber,
                    Title = x.Title,
                    Summary = x.Summary,
                    Description = x.Description,
                    OvernightStay = x.OvernightStay,
                    BreakfastIncluded = x.BreakfastIncluded,
                    LunchIncluded = x.LunchIncluded,
                    DinnerIncluded = x.DinnerIncluded,
                    HotelName = x.HotelName,
                    TransportationName = x.TransportationName,
                    HotelId = x.HotelId,
                    TransportationId = x.TransportationId
                })
                .ToList()
        };

        await PopulateTourSelectionsAsync(model, cancellationToken);
        EnsureTourEditorDefaults(model);
        return View(model);
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTour(long id, TourFormViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.TourId)
        {
            TempData["AdminErrorMessage"] = "Yêu cầu cập nhật tour không hợp lệ.";
            return RedirectToAction(nameof(Tours));
        }

        var tour = await _dbContext.Tours
            .Include(x => x.MediaItems)
            .Include(x => x.Schedules)
            .ThenInclude(x => x.Bookings)
            .Include(x => x.ItineraryDays)
            .SingleOrDefaultAsync(x => x.TourId == id, cancellationToken);

        if (tour is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy tour.";
            return RedirectToAction(nameof(Tours));
        }

        await ValidateTourSelectionsAsync(model, cancellationToken);
        var remainingExistingImages = tour.MediaItems.Count(x => !model.DeleteImageIds.Contains(x.TourMediaId));
        ValidateUploadedImages(model, remainingExistingImages);
        ValidateSeatInventory(model);
        ValidateTourSchedules(model);
        ValidateTourItineraryDays(model);

        if (!ModelState.IsValid)
        {
            await PopulateTourSelectionsAsync(model, cancellationToken);
            model.TourCode = tour.TourCode;
            model.ExistingImages = tour.MediaItems
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new AdminTourImageViewModel
                {
                    TourMediaId = x.TourMediaId,
                    MediaUrl = x.MediaUrl,
                    IsPrimary = x.IsPrimary,
                    DisplayOrder = x.DisplayOrder
                })
                .ToList();
            EnsureTourEditorDefaults(model);
            return View(model);
        }

        tour.TourName = model.TourName.Trim();
        tour.Slug = await EnsureUniqueTourSlugAsync(model.TourName, tour.TourId, cancellationToken);
        tour.CategoryId = model.CategoryId;
        tour.StartDestinationId = model.StartDestinationId;
        tour.EndDestinationId = model.EndDestinationId;
        tour.ShortDescription = EmptyToNull(model.ShortDescription);
        tour.Description = EmptyToNull(model.Description);
        tour.DurationDays = model.DurationDays;
        tour.DurationNights = model.DurationNights;
        tour.MinGroupSize = model.MinGroupSize;
        tour.MaxGroupSize = model.MaxGroupSize;
        tour.TotalSeats = model.TotalSeats;
        tour.RemainingSeats = model.RemainingSeats;
        tour.BasePrice = model.BasePrice;
        tour.ChildPrice = null;
        tour.SingleSupplement = model.SingleSupplement;
        tour.CurrencyCode = model.CurrencyCode.Trim().ToUpperInvariant();
        tour.DeparturePoint = EmptyToNull(model.DeparturePoint);
        tour.ReturnPoint = EmptyToNull(model.ReturnPoint);
        tour.PickupIncluded = model.PickupIncluded;
        tour.IsFeatured = model.IsFeatured;
        tour.IsPublished = model.IsPublished;
        tour.ApprovalStatus = model.ApprovalStatus;
        tour.SeoTitle = EmptyToNull(model.SeoTitle);
        tour.SeoDescription = EmptyToNull(model.SeoDescription);
        tour.UpdatedAt = DateTime.UtcNow;

        RemoveDeletedTourImages(tour, model.DeleteImageIds);
        SyncTourSchedules(tour, model);
        SyncTourItineraryDays(tour, model);
        await SyncTourImagesAsync(tour, model.UploadedImages, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["AdminSuccessMessage"] = "Đã cập nhật tour.";
        return RedirectToAction(nameof(Tours));
    }

    [Authorize(Policy = PermissionConstants.ManageTours)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTour(long id, CancellationToken cancellationToken)
    {
        var tour = await _dbContext.Tours
            .Include(x => x.MediaItems)
            .Include(x => x.Schedules)
                .ThenInclude(x => x.Bookings)
            .Include(x => x.ItineraryDays)
            .Include(x => x.Bookings)
                .ThenInclude(x => x.Payments)
            .Include(x => x.Bookings)
                .ThenInclude(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.TourId == id, cancellationToken);

        if (tour is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy tour.";
            return RedirectToAction(nameof(Tours));
        }

        if (tour.Bookings.Any())
        {
            TempData["AdminErrorMessage"] = $"Tour {tour.TourName} đã có đơn đặt liên quan nên không thể xóa. Hãy hủy xuất bản hoặc xử lý các booking trước.";
            return RedirectToAction(nameof(Tours));
        }

        if (tour.MediaItems.Count > 0)
        {
            foreach (var media in tour.MediaItems)
            {
                DeleteTourImageFile(media.MediaUrl);
            }

            _dbContext.TourMedia.RemoveRange(tour.MediaItems);
        }

        if (tour.ItineraryDays.Count > 0)
        {
            _dbContext.TourItineraryDays.RemoveRange(tour.ItineraryDays);
        }

        if (tour.Schedules.Count > 0)
        {
            _dbContext.TourSchedules.RemoveRange(tour.Schedules);
        }

        _dbContext.Tours.Remove(tour);
        await _dbContext.SaveChangesAsync(cancellationToken);
        DeleteTourImageDirectory(tour.TourId);

        TempData["AdminSuccessMessage"] = $"Đã xóa tour {tour.TourName}.";
        return RedirectToAction(nameof(Tours));
    }

    [Authorize(Policy = PermissionConstants.ManageFinance)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPayment(long paymentId, bool isValid, int currentPage, CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments
            .Include(x => x.Booking)
            .ThenInclude(x => x.Tour)
            .Include(x => x.Booking)
            .ThenInclude(x => x.TourSchedule)
            .SingleOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken);

        if (payment is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy giao dịch cần xác nhận.";
            return RedirectToAction(nameof(Orders), new { page = currentPage <= 0 ? 1 : currentPage });
        }

        var booking = payment.Booking;
        if (payment.PaymentStatus is PaymentDepositPaid or PaymentFullyPaid)
        {
            TempData["AdminErrorMessage"] = $"Giao dịch {payment.PaymentCode} đã được xác nhận trước đó.";
            return RedirectToAction(nameof(Orders), new { page = currentPage <= 0 ? 1 : currentPage });
        }

        if (payment.PaymentStatus != PaymentPending || string.IsNullOrWhiteSpace(payment.TransactionReference))
        {
            TempData["AdminErrorMessage"] = $"Giao dịch {payment.PaymentCode} chưa có mã giao dịch VNPay để đối soát.";
            return RedirectToAction(nameof(Orders), new { page = currentPage <= 0 ? 1 : currentPage });
        }

        var oldBookingStatus = booking.BookingStatus;
        var oldPaymentStatus = booking.PaymentStatus;

        if (isValid)
        {
            var newPaidAmount = Math.Min(booking.PaidAmount + payment.Amount, booking.TotalAmount);
            var isFullyPaid = newPaidAmount >= booking.TotalAmount;

            payment.PaymentStatus = isFullyPaid ? PaymentFullyPaid : PaymentDepositPaid;
            payment.PaidAt = DateTime.UtcNow;
            payment.FailureReason = null;
            payment.UpdatedAt = DateTime.UtcNow;

            booking.PaidAmount = newPaidAmount;
            booking.PaymentStatus = isFullyPaid ? PaymentFullyPaid : PaymentDepositPaid;
            booking.BookingStatus = isFullyPaid ? BookingFullyPaid : BookingDepositPaid;
            booking.FullyPaidAt = isFullyPaid ? DateTime.UtcNow : booking.FullyPaidAt;
            booking.UpdatedAt = DateTime.UtcNow;

            if (booking.PromotionId.HasValue)
            {
                var userPromotion = await _dbContext.UserPromotions
                    .SingleOrDefaultAsync(x => x.UserId == booking.UserId && x.PromotionId == booking.PromotionId.Value, cancellationToken);

                if (userPromotion is not null && userPromotion.UsedAt == null)
                {
                    userPromotion.UsedAt = DateTime.UtcNow;
                }
            }

            _dbContext.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = oldBookingStatus,
                NewStatus = booking.BookingStatus,
                ChangedByUserId = GetCurrentUserId(),
                Notes = $"Accountant xác nhận thanh toán {payment.Amount:N0} đ. PaymentStatus: {oldPaymentStatus} -> {booking.PaymentStatus}.",
                ChangedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            var remainingAmount = Math.Max(booking.TotalAmount - booking.PaidAmount, 0m);
            var dueDateText = booking.BalanceDueAt?.ToString("dd/MM/yyyy") ?? booking.TourSchedule.DepartureDate.AddDays(-5).ToString("dd/MM/yyyy");

            await _notificationService.CreateAsync(
                booking.UserId,
                notificationType: 3,
                title: isFullyPaid ? "Đã xác nhận thanh toán toàn bộ" : "Đã xác nhận thanh toán cọc",
                message: isFullyPaid
                    ? $"Accountant đã xác nhận đơn {booking.BookingCode} thanh toán đủ {booking.PaidAmount:N0} đ. Staff sẽ xử lý và xác nhận booking."
                    : $"Accountant đã xác nhận đơn {booking.BookingCode} đã cọc {booking.PaidAmount:N0} đ. Bạn cần thanh toán phần còn lại {remainingAmount:N0} đ trước {dueDateText}.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);

            await _notificationService.CreateForRolesAsync(
                RoleConstants.ManageBookings,
                notificationType: 13,
                title: isFullyPaid ? "Đơn đã thanh toán đủ, chờ Staff xử lý" : "Đơn đã cọc, chờ Staff xác nhận",
                message: isFullyPaid
                    ? $"Đơn {booking.BookingCode} đã được Accountant xác nhận thanh toán toàn bộ. Staff có thể xác nhận booking và chuẩn bị dịch vụ."
                    : $"Đơn {booking.BookingCode} đã được Accountant xác nhận cọc. Staff có thể liên hệ khách, kiểm tra thông tin và giữ chỗ.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);

            TempData["AdminSuccessMessage"] = $"Đã xác nhận thanh toán cho đơn {booking.BookingCode}.";
        }
        else
        {
            payment.PaymentStatus = PaymentFailed;
            payment.FailureReason = "Accountant đối soát không hợp lệ.";
            payment.UpdatedAt = DateTime.UtcNow;

            booking.PaymentStatus = PaymentFailed;
            booking.BookingStatus = booking.PaidAmount > 0m ? BookingPendingFullPayment : BookingPendingPayment;
            booking.UpdatedAt = DateTime.UtcNow;

            _dbContext.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = oldBookingStatus,
                NewStatus = booking.BookingStatus,
                ChangedByUserId = GetCurrentUserId(),
                Notes = $"Accountant từ chối giao dịch {payment.PaymentCode}. PaymentStatus: {oldPaymentStatus} -> {booking.PaymentStatus}.",
                ChangedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _notificationService.CreateAsync(
                booking.UserId,
                notificationType: 4,
                title: "Thanh toán chưa hợp lệ",
                message: $"Giao dịch cho đơn {booking.BookingCode} chưa được Accountant xác nhận. Vui lòng kiểm tra lại và thanh toán lại nếu cần.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);

            TempData["AdminErrorMessage"] = $"Đã từ chối giao dịch của đơn {booking.BookingCode}.";
        }

        return RedirectToAction(nameof(Orders), new { page = currentPage <= 0 ? 1 : currentPage });
    }

    [Authorize(Policy = PermissionConstants.ManageFinance)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRefund(long bookingId, int currentPage, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings
            .SingleOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

        if (booking is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy đơn cần hoàn tiền.";
            return RedirectToAction(nameof(Orders), new { page = currentPage <= 0 ? 1 : currentPage });
        }

        if (booking.BookingStatus != BookingPendingRefund)
        {
            TempData["AdminErrorMessage"] = $"Đơn {booking.BookingCode} chưa ở trạng thái chờ hoàn tiền.";
            return RedirectToAction(nameof(Orders), new { page = currentPage <= 0 ? 1 : currentPage });
        }

        var oldStatus = booking.BookingStatus;
        booking.BookingStatus = BookingRefunded;
        booking.CancelledAt ??= DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        _dbContext.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.BookingId,
            OldStatus = oldStatus,
            NewStatus = BookingRefunded,
            ChangedByUserId = GetCurrentUserId(),
            Notes = $"Accountant đã hoàn tiền {booking.PaidAmount:N0} đ.",
            ChangedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateAsync(
            booking.UserId,
            notificationType: 4,
            title: "Đã hoàn tiền",
            message: $"Đơn {booking.BookingCode} đã được Accountant xử lý hoàn tiền {booking.PaidAmount:N0} đ.",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);

        TempData["AdminSuccessMessage"] = $"Đã hoàn tiền cho đơn {booking.BookingCode}.";
        return RedirectToAction(nameof(Orders), new { page = currentPage <= 0 ? 1 : currentPage });
    }

    [Authorize(Policy = PermissionConstants.ManageBookings)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(UpdateBookingStatusViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminErrorMessage"] = "Dữ liệu cập nhật đơn không hợp lệ.";
            return RedirectToAction(nameof(Orders));
        }

        var booking = await _dbContext.Bookings
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .SingleOrDefaultAsync(x => x.BookingId == model.BookingId, cancellationToken);
        if (booking is null)
        {
            TempData["AdminErrorMessage"] = "Không tìm thấy đơn đặt tour.";
            return RedirectToAction(nameof(Orders));
        }

        if (booking.BookingStatus is BookingCancelled or BookingRefunded)
        {
            TempData["AdminErrorMessage"] = $"Đơn {booking.BookingCode} đã đóng và không thể cập nhật trạng thái nữa.";
            return RedirectToAction(nameof(Orders), new { page = model.CurrentPage <= 0 ? 1 : model.CurrentPage });
        }

        if (model.BookingStatus == BookingPendingRefund && booking.BookingStatus != BookingRefundRequested)
        {
            TempData["AdminErrorMessage"] = "Chỉ đơn đã có yêu cầu hoàn tiền mới được chuyển sang chờ hoàn tiền.";
            return RedirectToAction(nameof(Orders), new { page = model.CurrentPage <= 0 ? 1 : model.CurrentPage });
        }

        if (model.BookingStatus != BookingPendingRefund && booking.PaymentStatus is not (PaymentDepositPaid or PaymentFullyPaid))
        {
            TempData["AdminErrorMessage"] = $"Đơn {booking.BookingCode} chưa được Accountant xác nhận thanh toán. Staff chưa được xử lý booking.";
            return RedirectToAction(nameof(Orders), new { page = model.CurrentPage <= 0 ? 1 : model.CurrentPage });
        }

        if (model.BookingStatus is not (BookingConfirmed or BookingCancelled or BookingPendingRefund))
        {
            TempData["AdminErrorMessage"] = "Staff chỉ được xác nhận booking, hủy đơn hoặc chuyển đơn sang chờ hoàn tiền.";
            return RedirectToAction(nameof(Orders), new { page = model.CurrentPage <= 0 ? 1 : model.CurrentPage });
        }

        var oldStatus = booking.BookingStatus;
        var bookedSeats = booking.AdultCount + booking.ChildCount;

        if (!IsCancelledStatus(oldStatus) && IsCancelledStatus(model.BookingStatus))
        {
            booking.Tour.RemainingSeats += bookedSeats;
            booking.TourSchedule.AvailableSeats += bookedSeats;
            booking.TourSchedule.ReservedSeats = Math.Max(booking.TourSchedule.ReservedSeats - bookedSeats, 0);
        }
        else if (IsCancelledStatus(oldStatus) && !IsCancelledStatus(model.BookingStatus))
        {
            if (booking.Tour.RemainingSeats < bookedSeats || booking.TourSchedule.AvailableSeats < bookedSeats)
            {
                TempData["AdminErrorMessage"] = "Không đủ vé để kích hoạt lại đơn đã hủy.";
                return RedirectToAction(nameof(Orders));
            }

            booking.Tour.RemainingSeats -= bookedSeats;
            booking.TourSchedule.AvailableSeats -= bookedSeats;
            booking.TourSchedule.ReservedSeats += bookedSeats;
        }

        booking.BookingStatus = model.BookingStatus;
        booking.UpdatedAt = DateTime.UtcNow;
        booking.Tour.UpdatedAt = DateTime.UtcNow;
        booking.TourSchedule.UpdatedAt = DateTime.UtcNow;

        if (model.BookingStatus == BookingConfirmed)
        {
            booking.ConfirmedAt ??= DateTime.UtcNow;
        }

        if (model.BookingStatus is BookingCancelled or BookingPendingRefund)
        {
            booking.CancelledAt ??= DateTime.UtcNow;
            booking.CancellationReason = string.IsNullOrWhiteSpace(model.Notes)
                ? booking.CancellationReason ?? "Xử lý bởi nhân viên."
                : model.Notes.Trim();
        }

        _dbContext.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.BookingId,
            OldStatus = oldStatus,
            NewStatus = model.BookingStatus,
            ChangedByUserId = GetCurrentUserId(),
            Notes = model.Notes,
            ChangedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (model.BookingStatus == BookingConfirmed)
        {
            await _notificationService.CreateAsync(
                booking.UserId,
                notificationType: 3,
                title: "Đặt vé thành công",
                message: booking.PaymentStatus == PaymentFullyPaid
                    ? $"Đơn {booking.BookingCode} đã được Staff xác nhận. Bạn đã thanh toán đủ tiền, ChillTour sẽ chuẩn bị dịch vụ cho chuyến đi."
                    : $"Đơn {booking.BookingCode} đã được Staff xác nhận sau khi cọc. Vé của bạn đã được giữ chỗ, vui lòng thanh toán phần còn lại trước hạn.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);
        }
        else if (model.BookingStatus == BookingCancelled)
        {
            await _notificationService.CreateAsync(
                booking.UserId,
                notificationType: 4,
                title: "Đơn đặt tour đã bị hủy",
                message: $"Đơn {booking.BookingCode} đã được hủy. Nếu cần hỗ trợ thêm, vui lòng liên hệ bộ phận chăm sóc khách hàng của ChillTour.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);
        }
        else if (model.BookingStatus == BookingPendingRefund)
        {
            await _notificationService.CreateAsync(
                booking.UserId,
                notificationType: 4,
                title: "Đơn đang chờ hoàn tiền",
                message: $"Đơn {booking.BookingCode} đã được Staff chuyển sang trạng thái chờ hoàn tiền. Accountant sẽ xử lý hoàn tiền.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);

            await _notificationService.CreateForRolesAsync(
                RoleConstants.ManageFinance,
                notificationType: 14,
                title: "Có đơn chờ hoàn tiền",
                message: $"Đơn {booking.BookingCode} cần Accountant hoàn lại {booking.PaidAmount:N0} đ cho khách.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);
        }

        TempData["AdminSuccessMessage"] = $"Đã cập nhật đơn {booking.BookingCode}.";
        return RedirectToAction(nameof(Orders), new { page = model.CurrentPage <= 0 ? 1 : model.CurrentPage });
    }

    private long? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var userId) ? userId : null;
    }

    private async Task<Role?> FindRoleByCodeAsync(string roleCode, CancellationToken cancellationToken)
    {
        var normalized = roleCode.Trim().ToUpperInvariant();
        return await _dbContext.Roles.FirstOrDefaultAsync(x => x.RoleCode.ToUpper() == normalized, cancellationToken);
    }

    private static string NormalizeRoleCode(string roleCode)
    {
        return roleCode.Trim().ToUpperInvariant() switch
        {
            "ADMIN" => RoleConstants.Admin,
            "DIRECTOR" => RoleConstants.Director,
            "MANAGER" => RoleConstants.Manager,
            "ACCOUNTANT" => RoleConstants.Accountant,
            "STAFF" => RoleConstants.Employee,
            "EMPLOYEE" => RoleConstants.Employee,
            "CUSTOMER" => RoleConstants.Customer,
            _ => roleCode
        };
    }

    private void ValidatePromotionForm(PromotionFormViewModel model)
    {
        if (!model.DiscountPercent.HasValue || model.DiscountPercent.Value <= 0)
        {
            ModelState.AddModelError(nameof(model.DiscountPercent), "Vui lòng nhập % giảm giá lớn hơn 0.");
        }

        if (model.EndAt <= model.StartAt)
        {
            ModelState.AddModelError(nameof(model.EndAt), "Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");
        }
    }

    private void ValidateArticleForm(ArticleFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ArticleCode))
        {
            ModelState.AddModelError(nameof(model.ArticleCode), "Vui lòng nhập mã bài viết.");
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Vui lòng nhập tiêu đề bài viết.");
        }

        if (string.IsNullOrWhiteSpace(model.Slug))
        {
            ModelState.AddModelError(nameof(model.Slug), "Vui lòng nhập slug bài viết.");
        }

        if (string.IsNullOrWhiteSpace(model.Summary))
        {
            ModelState.AddModelError(nameof(model.Summary), "Vui lòng nhập tóm tắt bài viết.");
        }

        if (string.IsNullOrWhiteSpace(model.ContentHtml))
        {
            ModelState.AddModelError(nameof(model.ContentHtml), "Vui lòng nhập nội dung bài viết.");
        }

        if (!model.PublishedAt.HasValue)
        {
            ModelState.AddModelError(nameof(model.PublishedAt), "Vui lòng chọn thời gian xuất bản.");
        }
    }

    private async Task PopulateTourSelectionsAsync(TourFormViewModel model, CancellationToken cancellationToken)
    {
        model.Categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.CategoryName)
            .Select(x => new SelectListItem
            {
                Value = x.CategoryId.ToString(),
                Text = x.CategoryName
            })
            .ToListAsync(cancellationToken);

        model.Destinations = await _dbContext.Destinations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DestinationName)
            .Select(x => new SelectListItem
            {
                Value = x.DestinationId.ToString(),
                Text = x.DestinationName
            })
            .ToListAsync(cancellationToken);

        model.Hotels = await _dbContext.Hotels
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.HotelName)
            .Select(x => new SelectListItem
            {
                Value = x.HotelId.ToString(),
                Text = x.HotelName
            })
            .ToListAsync(cancellationToken);

        model.Transportations = await _dbContext.Transportations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ProviderName)
            .ThenBy(x => x.VehicleName)
            .Select(x => new SelectListItem
            {
                Value = x.TransportationId.ToString(),
                Text = (x.ProviderName + (x.VehicleName != null ? " - " + x.VehicleName : string.Empty)).Trim()
            })
            .ToListAsync(cancellationToken);
    }

    private static void EnsureTourEditorDefaults(TourFormViewModel model)
    {
        model.Schedules ??= [];
        model.ItineraryDays ??= [];

        if (model.Schedules.Count == 0)
        {
            model.Schedules.Add(new TourDepartureEditorViewModel
            {
                DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7))
            });
        }

        if (model.ItineraryDays.Count == 0)
        {
            model.ItineraryDays.Add(new TourItineraryDayEditorViewModel
            {
                DayNumber = 1,
                Title = "Ngày 1"
            });
        }
    }

    private async Task ValidateTourSelectionsAsync(TourFormViewModel model, CancellationToken cancellationToken)
    {
        var categoryExists = await _dbContext.Categories.AnyAsync(x => x.CategoryId == model.CategoryId && x.IsActive, cancellationToken);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không hợp lệ.");
        }

        var startExists = await _dbContext.Destinations.AnyAsync(x => x.DestinationId == model.StartDestinationId && x.IsActive, cancellationToken);
        if (!startExists)
        {
            ModelState.AddModelError(nameof(model.StartDestinationId), "Điểm đi không hợp lệ.");
        }

        var endExists = await _dbContext.Destinations.AnyAsync(x => x.DestinationId == model.EndDestinationId && x.IsActive, cancellationToken);
        if (!endExists)
        {
            ModelState.AddModelError(nameof(model.EndDestinationId), "Điểm đến không hợp lệ.");
        }
    }

    private void ValidateUploadedImages(TourFormViewModel model, int existingImageCount)
    {
        if (model.UploadedImages.Count == 0 && existingImageCount == 0)
        {
            ModelState.AddModelError(nameof(model.UploadedImages), "Tour phải có ít nhất 1 ảnh.");
            return;
        }

        foreach (var image in model.UploadedImages)
        {
            if (image.Length <= 0)
            {
                ModelState.AddModelError(nameof(model.UploadedImages), "Có ảnh tải lên không hợp lệ.");
                continue;
            }

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(model.UploadedImages), "Chỉ chấp nhận file ảnh jpg, jpeg, png, webp hoặc gif.");
                break;
            }

            if (image.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(model.UploadedImages), "Mỗi ảnh phải nhỏ hơn hoặc bằng 5MB.");
                break;
            }
        }
    }

    private void ValidateSeatInventory(TourFormViewModel model)
    {
        if (model.RemainingSeats > model.TotalSeats)
        {
            ModelState.AddModelError(nameof(model.RemainingSeats), "Số vé còn lại không được lớn hơn tổng số vé.");
        }
    }

    private void ValidateTourSchedules(TourFormViewModel model)
    {
        var schedules = model.Schedules.Where(x => !x.IsDeleted).ToList();
        if (schedules.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Schedules), "Tour phải có ít nhất 1 lịch khởi hành.");
            return;
        }

        var duplicateDates = schedules
            .Where(x => x.DepartureDate.HasValue)
            .GroupBy(x => x.DepartureDate!.Value)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key.ToString("dd/MM/yyyy"))
            .ToList();

        if (duplicateDates.Count > 0)
        {
            ModelState.AddModelError(nameof(model.Schedules), $"Ngày khởi hành bị trùng: {string.Join(", ", duplicateDates)}.");
        }

        for (var i = 0; i < model.Schedules.Count; i++)
        {
            var schedule = model.Schedules[i];
            if (schedule.IsDeleted)
            {
                continue;
            }

            if (!schedule.DepartureDate.HasValue)
            {
                ModelState.AddModelError($"Schedules[{i}].DepartureDate", "Vui lòng chọn ngày khởi hành.");
            }

            if (schedule.AdultPrice <= 0)
            {
                ModelState.AddModelError($"Schedules[{i}].AdultPrice", "Vui lòng nhập giá người lớn lớn hơn 0.");
            }

        }
    }

    private void ValidateTourItineraryDays(TourFormViewModel model)
    {
        var itineraryDays = model.ItineraryDays.Where(x => !x.IsDeleted).ToList();
        if (itineraryDays.Count == 0)
        {
            ModelState.AddModelError(nameof(model.ItineraryDays), "Tour phải có ít nhất 1 ngày lịch trình.");
            return;
        }

        var duplicateDayNumbers = itineraryDays
            .GroupBy(x => x.DayNumber)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key.ToString())
            .ToList();

        if (duplicateDayNumbers.Count > 0)
        {
            ModelState.AddModelError(nameof(model.ItineraryDays), $"Ngày lịch trình bị trùng: {string.Join(", ", duplicateDayNumbers)}.");
        }

        for (var i = 0; i < model.ItineraryDays.Count; i++)
        {
            var itinerary = model.ItineraryDays[i];
            if (itinerary.IsDeleted)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(itinerary.Title))
            {
                ModelState.AddModelError($"ItineraryDays[{i}].Title", "Vui lòng nhập tiêu đề ngày.");
            }

            if (!string.IsNullOrWhiteSpace(itinerary.OvernightStay) && itinerary.OvernightStay.Length > 200)
            {
                ModelState.AddModelError($"ItineraryDays[{i}].OvernightStay", "Nơi nghỉ đêm không được vượt quá 200 ký tự.");
            }

            if (!string.IsNullOrWhiteSpace(itinerary.HotelName) && itinerary.HotelName.Length > 200)
            {
                ModelState.AddModelError($"ItineraryDays[{i}].HotelName", "Tên khách sạn không được vượt quá 200 ký tự.");
            }

            if (!string.IsNullOrWhiteSpace(itinerary.TransportationName) && itinerary.TransportationName.Length > 200)
            {
                ModelState.AddModelError($"ItineraryDays[{i}].TransportationName", "Tên phương tiện không được vượt quá 200 ký tự.");
            }
        }
    }

    private void RemoveDeletedTourImages(Tour tour, IReadOnlyCollection<long> deleteImageIds)
    {
        if (deleteImageIds.Count == 0)
        {
            return;
        }

        var mediaToDelete = tour.MediaItems
            .Where(x => deleteImageIds.Contains(x.TourMediaId))
            .ToList();

        foreach (var media in mediaToDelete)
        {
            DeleteTourImageFile(media.MediaUrl);
            tour.MediaItems.Remove(media);
            _dbContext.TourMedia.Remove(media);
        }

        if (mediaToDelete.Count > 0)
        {
            tour.MainImageUrl = null;
        }
    }

    private void SyncTourSchedules(Tour tour, TourFormViewModel model)
    {
        var postedIds = model.Schedules
            .Where(x => x.TourScheduleId.HasValue)
            .Select(x => x.TourScheduleId!.Value)
            .ToHashSet();

        var schedulesToDelete = tour.Schedules
            .Where(x => !postedIds.Contains(x.TourScheduleId) || model.Schedules.Any(s => s.TourScheduleId == x.TourScheduleId && s.IsDeleted))
            .ToList();

        foreach (var schedule in schedulesToDelete)
        {
            if (schedule.Bookings.Any())
            {
                continue;
            }

            tour.Schedules.Remove(schedule);
            _dbContext.TourSchedules.Remove(schedule);
        }

        foreach (var item in model.Schedules.Where(x => !x.IsDeleted))
        {
            var schedule = item.TourScheduleId.HasValue
                ? tour.Schedules.FirstOrDefault(x => x.TourScheduleId == item.TourScheduleId.Value)
                : null;

            if (schedule is null)
            {
                schedule = new TourSchedule
                {
                    TourId = tour.TourId,
                    ScheduleCode = GenerateScheduleCode(tour.TourId),
                    CreatedAt = DateTime.UtcNow
                };

                tour.Schedules.Add(schedule);
            }

            var reservedSeats = schedule.ReservedSeats;
            var totalSeats = tour.TotalSeats;
            var availableSeats = Math.Max(totalSeats - reservedSeats, 0);

            schedule.DepartureDate = item.DepartureDate!.Value;
            schedule.ReturnDate = item.DepartureDate.Value.AddDays(Math.Max(tour.DurationDays - 1, 0));
            schedule.TotalSeats = totalSeats;
            schedule.AvailableSeats = availableSeats;
            schedule.AdultPrice = item.AdultPrice;
            schedule.ChildPrice = decimal.Round(item.AdultPrice * 0.5m, 0, MidpointRounding.AwayFromZero);
            schedule.InfantPrice = 0m;
            schedule.SingleSupplement = tour.SingleSupplement;
            schedule.Status = 1;
            schedule.UpdatedAt = DateTime.UtcNow;
        }

        var activeSchedulePrices = model.Schedules
            .Where(x => !x.IsDeleted && x.AdultPrice > 0)
            .Select(x => x.AdultPrice)
            .ToList();

        if (activeSchedulePrices.Count > 0)
        {
            tour.BasePrice = activeSchedulePrices.Min();
        }
    }

    private void SyncTourItineraryDays(Tour tour, TourFormViewModel model)
    {
        var postedIds = model.ItineraryDays
            .Where(x => x.ItineraryDayId.HasValue)
            .Select(x => x.ItineraryDayId!.Value)
            .ToHashSet();

        var itineraryToDelete = tour.ItineraryDays
            .Where(x => !postedIds.Contains(x.ItineraryDayId) || model.ItineraryDays.Any(i => i.ItineraryDayId == x.ItineraryDayId && i.IsDeleted))
            .ToList();

        foreach (var itineraryDay in itineraryToDelete)
        {
            tour.ItineraryDays.Remove(itineraryDay);
            _dbContext.TourItineraryDays.Remove(itineraryDay);
        }

        foreach (var item in model.ItineraryDays.Where(x => !x.IsDeleted))
        {
            var itineraryDay = item.ItineraryDayId.HasValue
                ? tour.ItineraryDays.FirstOrDefault(x => x.ItineraryDayId == item.ItineraryDayId.Value)
                : null;

            if (itineraryDay is null)
            {
                itineraryDay = new TourItineraryDay
                {
                    TourId = tour.TourId
                };

                tour.ItineraryDays.Add(itineraryDay);
            }

            itineraryDay.DayNumber = item.DayNumber;
            itineraryDay.Title = item.Title.Trim();
            itineraryDay.Summary = EmptyToNull(item.Summary);
            itineraryDay.Description = EmptyToNull(item.Description);
            itineraryDay.OvernightStay = EmptyToNull(item.OvernightStay);
            itineraryDay.BreakfastIncluded = item.BreakfastIncluded;
            itineraryDay.LunchIncluded = item.LunchIncluded;
            itineraryDay.DinnerIncluded = item.DinnerIncluded;
            itineraryDay.HotelName = EmptyToNull(item.HotelName);
            itineraryDay.TransportationName = EmptyToNull(item.TransportationName);
            itineraryDay.HotelId = null;
            itineraryDay.TransportationId = null;
        }
    }

    private async Task SyncTourImagesAsync(Tour tour, IReadOnlyCollection<IFormFile> uploadedImages, CancellationToken cancellationToken)
    {
        if (uploadedImages.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(tour.MainImageUrl))
            {
                tour.MainImageUrl = tour.MediaItems
                    .OrderByDescending(x => x.IsPrimary)
                    .ThenBy(x => x.DisplayOrder)
                    .Select(x => x.MediaUrl)
                    .FirstOrDefault();
            }

            return;
        }

        var hasPrimary = tour.MediaItems.Any(x => x.IsPrimary);
        var nextDisplayOrder = tour.MediaItems.Count == 0 ? 1 : tour.MediaItems.Max(x => x.DisplayOrder) + 1;
        var savedImageUrls = new List<string>();

        foreach (var uploadedImage in uploadedImages)
        {
            var relativeUrl = await SaveTourImageAsync(tour.TourId, uploadedImage, cancellationToken);
            var isPrimary = !hasPrimary && savedImageUrls.Count == 0;

            var media = new TourMedia
            {
                TourId = tour.TourId,
                MediaType = 1,
                MediaUrl = relativeUrl,
                DisplayOrder = nextDisplayOrder++,
                IsPrimary = isPrimary
            };

            tour.MediaItems.Add(media);
            savedImageUrls.Add(relativeUrl);
        }

        tour.MainImageUrl = tour.MediaItems
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.DisplayOrder)
            .Select(x => x.MediaUrl)
            .FirstOrDefault() ?? savedImageUrls.FirstOrDefault();
    }

    private void DeleteTourImageFile(string? mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            return;
        }

        var relativePath = mediaUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(_environment.WebRootPath, relativePath);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private async Task<string> SaveTourImageAsync(long tourId, IFormFile image, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "tours", tourId.ToString());
        Directory.CreateDirectory(uploadsRoot);

        var filePath = Path.Combine(uploadsRoot, fileName);
        await using var stream = new FileStream(filePath, FileMode.Create);
        await image.CopyToAsync(stream, cancellationToken);

        return $"/uploads/tours/{tourId}/{fileName}";
    }

    private void DeleteTourImageDirectory(long tourId)
    {
        var directoryPath = Path.Combine(_environment.WebRootPath, "uploads", "tours", tourId.ToString());
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }
    }

    private static string GenerateTourCode(long tourId)
    {
        return $"TOUR{tourId:D6}";
    }

    private static string GenerateScheduleCode(long tourId)
    {
        return $"SCH{tourId:D6}{DateTime.UtcNow:HHmmss}{Random.Shared.Next(10, 99)}";
    }

    private static bool IsCancelledStatus(byte status)
    {
        return status is 4 or 5 or 10;
    }

    private async Task<string> EnsureUniqueTourSlugAsync(string tourName, long? excludedTourId, CancellationToken cancellationToken)
    {
        var baseSlug = BuildSlug(tourName);
        baseSlug = string.IsNullOrWhiteSpace(baseSlug) ? $"tour-{Guid.NewGuid():N}" : baseSlug;
        var slug = baseSlug;
        var suffix = 2;

        while (await _dbContext.Tours.AnyAsync(
                   x => x.Slug == slug && (!excludedTourId.HasValue || x.TourId != excludedTourId.Value),
                   cancellationToken))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
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

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}



