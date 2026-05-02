using System.Security.Claims;
using ChillTour.Data;
using ChillTour.Models.Account;
using ChillTour.Security;
using ChillTour.Services.Auth;
using ChillTour.Services.Notifications;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChillTour.Controllers;

public class AccountController : Controller
{
    private const byte BookingCancelled = 4;
    private const byte BookingConfirmed = 3;
    private const byte BookingRefunded = 5;
    private const byte BookingPendingFullPaymentVerification = 7;
    private const byte BookingFullyPaid = 8;
    private const byte BookingRefundRequested = 9;
    private const byte BookingPendingRefund = 10;
    private const byte PaymentDepositPaid = 2;
    private const byte PaymentFullyPaid = 3;
    private const int BalanceAutoCancelDaysBeforeDeparture = 3;

    private readonly IAuthService _authService;
    private readonly ChillTourDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly INotificationService _notificationService;
    private readonly GoogleAuthOptions _googleAuthOptions;

    public AccountController(IAuthService authService, ChillTourDbContext dbContext, IPasswordHasher passwordHasher, INotificationService notificationService, IOptions<GoogleAuthOptions> googleAuthOptions)
    {
        _authService = authService;
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _notificationService = notificationService;
        _googleAuthOptions = googleAuthOptions.Value;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewBag.GoogleLoginEnabled = IsGoogleLoginEnabled();
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.RegisterAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "ÄÄƒng kÃ½ tháº¥t báº¡i.");
            return View(model);
        }

        TempData["SuccessMessage"] = "ÄÄƒng kÃ½ thÃ nh cÃ´ng. Báº¡n cÃ³ thá»ƒ Ä‘Äƒng nháº­p ngay.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewBag.GoogleLoginEnabled = IsGoogleLoginEnabled();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resetUrlTemplate = Url.Action(
            nameof(ResetPassword),
            "Account",
            new { email = model.Email.Trim(), token = "__TOKEN__" },
            Request.Scheme);

        if (string.IsNullOrWhiteSpace(resetUrlTemplate))
        {
            ModelState.AddModelError(string.Empty, "Không thể tạo liên kết đặt lại mật khẩu.");
            return View(model);
        }

        var result = await _authService.RequestPasswordResetAsync(
            model.Email,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            resetUrlTemplate,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể gửi email đặt lại mật khẩu.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Nếu email tồn tại trong hệ thống, chúng tôi đã gửi liên kết đặt lại mật khẩu.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrWhiteSpace(_googleAuthOptions.ClientId) || string.IsNullOrWhiteSpace(_googleAuthOptions.ClientSecret))
        {
            TempData["ErrorMessage"] = "Hệ thống chưa cấu hình đăng nhập Google.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var redirectUrl = Url.Action(nameof(GoogleResponse), new { returnUrl }) ?? Url.Action(nameof(Login))!;
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Challenge(properties, AuthSchemeConstants.Google);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.LoginAsync(model, cancellationToken);
        if (!result.Succeeded || result.User is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "ÄÄƒng nháº­p tháº¥t báº¡i.");
            return View(model);
        }
        return await RedirectToSignedInDestinationAsync(result, model.RememberMe, model.ReturnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GoogleResponse(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(AuthSchemeConstants.External);
        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            TempData["ErrorMessage"] = "Đăng nhập Google không thành công. Vui lòng thử lại.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var principal = authenticateResult.Principal;
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var fullName = principal.FindFirstValue(ClaimTypes.Name);
        var avatarUrl = principal.FindFirstValue("picture");

        await HttpContext.SignOutAsync(AuthSchemeConstants.External);

        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Google không trả về email hợp lệ để đăng nhập.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var result = await _authService.LoginWithExternalProviderAsync(
            new ExternalLoginRequest
            {
                Provider = AuthSchemeConstants.Google,
                ProviderKey = providerKey,
                Email = email,
                FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName,
                AvatarUrl = avatarUrl
            },
            cancellationToken);

        if (!result.Succeeded || result.User is null)
        {
            TempData["ErrorMessage"] = result.Error ?? "Đăng nhập Google thất bại.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        return await RedirectToSignedInDestinationAsync(result, rememberMe: true, returnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string email, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var model = new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        };

        if (!await _authService.IsPasswordResetTokenValidAsync(email, token, cancellationToken))
        {
            TempData["ErrorMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.ResetPasswordAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể đặt lại mật khẩu.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(AuthSchemeConstants.External);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new AccountProfilePageViewModel
        {
            Profile = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            }
        });
    }

    [Authorize(Roles = ChillTour.Security.RoleConstants.Customer)]
    [HttpGet]
    public async Task<IActionResult> Bookings(string? searchTerm = null, byte? bookingStatus = null, byte? paymentStatus = null, DateOnly? departureFrom = null, DateOnly? departureTo = null, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction(nameof(Login));
        }

        await AutoCancelOverdueDepositBookingsAsync(userId.Value, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fullRefundCutoffDate = today.AddDays(7);

        var query = _dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .Where(x => x.UserId == userId.Value && (x.PaymentStatus == 1 || x.PaymentStatus == 2 || x.PaymentStatus == 3))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim();
            query = query.Where(x => x.BookingCode.Contains(normalizedSearch) || x.Tour.TourName.Contains(normalizedSearch));
        }

        if (bookingStatus.HasValue)
        {
            query = query.Where(x => x.BookingStatus == bookingStatus.Value);
        }

        if (paymentStatus.HasValue)
        {
            query = query.Where(x => x.PaymentStatus == paymentStatus.Value);
        }

        if (departureFrom.HasValue)
        {
            query = query.Where(x => x.TourSchedule.DepartureDate >= departureFrom.Value);
        }

        if (departureTo.HasValue)
        {
            query = query.Where(x => x.TourSchedule.DepartureDate <= departureTo.Value);
        }

        var bookings = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CustomerBookingItemViewModel
            {
                BookingId = x.BookingId,
                BookingCode = x.BookingCode,
                TourName = x.Tour.TourName,
                TourSlug = x.Tour.Slug,
                DepartureDate = x.TourSchedule.DepartureDate,
                Travelers = x.AdultCount + x.ChildCount + x.InfantCount,
                TotalAmount = x.TotalAmount,
                RemainingAmount = Math.Max(x.TotalAmount - x.PaidAmount, 0m),
                BookingStatus = x.BookingStatus,
                PaymentStatus = x.PaymentStatus,
                PaidAmount = x.PaidAmount,
                CancellationReason = x.CancellationReason,
                CreatedAt = x.CreatedAt,
                PaymentConfirmedByName = x.StatusHistory
                    .Where(h => h.Notes != null && h.Notes.Contains("Accountant xác nhận"))
                    .OrderByDescending(h => h.ChangedAt)
                    .Select(h => h.ChangedByUser != null ? h.ChangedByUser.FullName : null)
                    .FirstOrDefault(),
                PaymentConfirmedAt = x.StatusHistory
                    .Where(h => h.Notes != null && h.Notes.Contains("Accountant xác nhận"))
                    .OrderByDescending(h => h.ChangedAt)
                    .Select(h => (DateTime?)h.ChangedAt)
                    .FirstOrDefault(),
                BookingConfirmedByName = x.StatusHistory
                    .Where(h => h.NewStatus == BookingConfirmed)
                    .OrderByDescending(h => h.ChangedAt)
                    .Select(h => h.ChangedByUser != null ? h.ChangedByUser.FullName : null)
                    .FirstOrDefault(),
                BookingConfirmedAt = x.StatusHistory
                    .Where(h => h.NewStatus == BookingConfirmed)
                    .OrderByDescending(h => h.ChangedAt)
                    .Select(h => (DateTime?)h.ChangedAt)
                    .FirstOrDefault(),
                CanCancel = x.BookingStatus != BookingCancelled
                    && x.BookingStatus != BookingRefunded
                    && x.BookingStatus != BookingRefundRequested
                    && x.BookingStatus != BookingPendingRefund
                    && x.TourSchedule.DepartureDate >= today,
                RequiresRefundRequest = x.PaidAmount > 0m
                    && x.TourSchedule.DepartureDate >= today,
                RefundPercent = x.PaidAmount > 0m && x.TourSchedule.DepartureDate >= today
                    ? (x.TourSchedule.DepartureDate >= fullRefundCutoffDate ? 100 : 60)
                    : 0,
                EstimatedRefundAmount = x.PaidAmount > 0m && x.TourSchedule.DepartureDate >= today
                    ? Math.Round(x.PaidAmount * (x.TourSchedule.DepartureDate >= fullRefundCutoffDate ? 1m : 0.6m), 0)
                    : 0m
            })
            .ToListAsync(cancellationToken);

        return View(new CustomerBookingsPageViewModel
        {
            SearchTerm = searchTerm,
            BookingStatus = bookingStatus,
            PaymentStatus = paymentStatus,
            DepartureFrom = departureFrom,
            DepartureTo = departureTo,
            Bookings = bookings
        });
    }

    [Authorize(Roles = ChillTour.Security.RoleConstants.Customer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(CancelBookingViewModel model, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction(nameof(Login));
        }

        await AutoCancelOverdueDepositBookingsAsync(userId.Value, cancellationToken);

        if (!ModelState.IsValid)
        {
            TempData["BookingErrorMessage"] = "Vui lÃ²ng nháº­p lÃ½ do há»§y Ä‘Æ¡n.";
            return RedirectToAction(nameof(Bookings));
        }

        var booking = await _dbContext.Bookings
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .SingleOrDefaultAsync(x => x.BookingId == model.BookingId && x.UserId == userId.Value, cancellationToken);

        if (booking is null)
        {
            TempData["BookingErrorMessage"] = "Không tìm thấy đơn cần xử lý.";
            return RedirectToAction(nameof(Bookings));
        }

        if (booking.BookingStatus is BookingCancelled or BookingRefunded or BookingRefundRequested or BookingPendingRefund)
        {
            TempData["BookingErrorMessage"] = "Đơn này đã đóng hoặc đang trong luồng hoàn tiền.";
            return RedirectToAction(nameof(Bookings));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (booking.TourSchedule.DepartureDate < today)
        {
            TempData["BookingErrorMessage"] = "Đơn đã qua ngày khởi hành nên không còn đủ điều kiện hủy/hoàn tiền.";
            return RedirectToAction(nameof(Bookings));
        }

        var oldStatus = booking.BookingStatus;
        var bookedSeats = booking.AdultCount + booking.ChildCount;
        var hasPaidAmount = booking.PaidAmount > 0m;
        var refundPercent = hasPaidAmount && booking.TourSchedule.DepartureDate >= today.AddDays(7) ? 100 : 60;
        var estimatedRefundAmount = hasPaidAmount
            ? Math.Round(booking.PaidAmount * (refundPercent == 100 ? 1m : 0.6m), 0)
            : 0m;
        var cancellationReason = model.CancellationReason.Trim();

        booking.BookingStatus = hasPaidAmount ? (byte)9 : (byte)4;
        booking.CancelledAt = DateTime.UtcNow;
        booking.CancellationReason = hasPaidAmount
            ? $"{cancellationReason} | Chính sách hoàn tiền: {refundPercent}% số tiền đã thanh toán, dự kiến {estimatedRefundAmount:N0} {booking.CurrencyCode}."
            : cancellationReason;
        booking.UpdatedAt = DateTime.UtcNow;

        if (!hasPaidAmount)
        {
            booking.Tour.RemainingSeats += bookedSeats;
            booking.Tour.UpdatedAt = DateTime.UtcNow;
            booking.TourSchedule.AvailableSeats += bookedSeats;
            booking.TourSchedule.ReservedSeats = Math.Max(booking.TourSchedule.ReservedSeats - bookedSeats, 0);
            booking.TourSchedule.UpdatedAt = DateTime.UtcNow;
        }

        _dbContext.BookingStatusHistories.Add(new Data.Entities.BookingStatusHistory
        {
            BookingId = booking.BookingId,
            OldStatus = oldStatus,
            NewStatus = booking.BookingStatus,
            ChangedByUserId = userId.Value,
            Notes = booking.CancellationReason,
            ChangedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateAsync(
            booking.UserId,
            notificationType: 4,
            title: hasPaidAmount ? "Đã gửi yêu cầu hoàn tiền" : "Bạn đã hủy đơn đặt tour",
            message: hasPaidAmount
                ? $"Đơn {booking.BookingCode} đã gửi yêu cầu hoàn tiền. Dự kiến hoàn {refundPercent}% số tiền đã thanh toán ({estimatedRefundAmount:N0} đ). Staff sẽ kiểm tra và chuyển sang Accountant xử lý."
                : $"Đơn {booking.BookingCode} đã được hủy thành công. Lý do: {booking.CancellationReason}",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);

        await _notificationService.CreateForRolesAsync(
            RoleConstants.ManageBookings,
            notificationType: 13,
            title: hasPaidAmount ? "Khách yêu cầu hoàn tiền" : "Khách hàng đã hủy đơn tour",
            message: hasPaidAmount
                ? $"Đơn {booking.BookingCode} đã được khách yêu cầu hoàn tiền {refundPercent}% ({estimatedRefundAmount:N0} đ). Lý do: {booking.CancellationReason}. Staff cần kiểm tra và chuyển trạng thái chờ hoàn tiền."
                : $"Đơn {booking.BookingCode} đã được khách hàng hủy. Lý do: {booking.CancellationReason}",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);

        TempData["BookingSuccessMessage"] = hasPaidAmount
            ? $"Đã gửi yêu cầu hoàn tiền cho đơn {booking.BookingCode}. Dự kiến hoàn {refundPercent}% ({estimatedRefundAmount:N0} đ)."
            : $"Đã hủy đơn {booking.BookingCode}.";
        return RedirectToAction(nameof(Bookings));
    }

    private async Task AutoCancelOverdueDepositBookingsAsync(long userId, CancellationToken cancellationToken)
    {
        var cutoffDate = DateOnly.FromDateTime(DateTime.Today.AddDays(BalanceAutoCancelDaysBeforeDeparture));
        var now = DateTime.UtcNow;
        const string cancellationReason = "Hệ thống tự động hủy do đã cọc nhưng chưa thanh toán đủ trước 3 ngày khởi hành.";

        var bookings = await _dbContext.Bookings
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .Where(x => x.UserId == userId
                        && x.PaymentStatus == PaymentDepositPaid
                        && x.PaidAmount > 0m
                        && x.PaidAmount < x.TotalAmount
                        && x.TourSchedule.DepartureDate <= cutoffDate
                        && x.BookingStatus != BookingCancelled
                        && x.BookingStatus != BookingRefunded
                        && x.BookingStatus != BookingPendingFullPaymentVerification
                        && x.BookingStatus != BookingFullyPaid
                        && x.BookingStatus != BookingRefundRequested
                        && x.BookingStatus != BookingPendingRefund)
            .ToListAsync(cancellationToken);

        foreach (var booking in bookings)
        {
            var oldStatus = booking.BookingStatus;
            var bookedSeats = booking.AdultCount + booking.ChildCount;

            booking.BookingStatus = BookingCancelled;
            booking.CancelledAt = now;
            booking.CancellationReason = cancellationReason;
            booking.UpdatedAt = now;

            booking.Tour.RemainingSeats += bookedSeats;
            booking.Tour.UpdatedAt = now;
            booking.TourSchedule.AvailableSeats += bookedSeats;
            booking.TourSchedule.ReservedSeats = Math.Max(booking.TourSchedule.ReservedSeats - bookedSeats, 0);
            booking.TourSchedule.UpdatedAt = now;

            _dbContext.BookingStatusHistories.Add(new Data.Entities.BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = oldStatus,
                NewStatus = BookingCancelled,
                ChangedByUserId = null,
                Notes = cancellationReason,
                ChangedAt = now
            });
        }

        if (bookings.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Inbox(string? searchTerm = null, string? relatedEntityType = null, bool? isRead = null, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction(nameof(Login));
        }

        var query = _dbContext.Notifications
            .Where(x => x.UserId == userId.Value)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim();
            query = query.Where(x => x.Title.Contains(normalizedSearch) || x.Message.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(relatedEntityType))
        {
            query = query.Where(x => x.RelatedEntityType == relatedEntityType.Trim());
        }

        if (isRead.HasValue)
        {
            query = query.Where(x => x.IsRead == isRead.Value);
        }

          var notifications = await query
              .OrderByDescending(x => x.CreatedAt)
              .Select(x => new CustomerNotificationItemViewModel
            {
                NotificationId = x.NotificationId,
                Title = x.Title,
                Message = x.Message,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt,
                  RelatedEntityType = x.RelatedEntityType,
                  RelatedEntityId = x.RelatedEntityId
              })
              .ToListAsync(cancellationToken);

          var relatedBookingIds = notifications
              .Where(x => x.RelatedEntityType == "Booking" && x.RelatedEntityId.HasValue)
              .Select(x => x.RelatedEntityId!.Value)
              .Distinct()
              .ToList();

          if (relatedBookingIds.Count > 0)
          {
              var bookingStatusById = await _dbContext.Bookings
                  .AsNoTracking()
                  .Where(x => relatedBookingIds.Contains(x.BookingId))
                  .Select(x => new { x.BookingId, x.BookingStatus })
                  .ToDictionaryAsync(x => x.BookingId, x => (byte?)x.BookingStatus, cancellationToken);

              foreach (var notification in notifications)
              {
                  if (notification.RelatedEntityId.HasValue
                      && bookingStatusById.TryGetValue(notification.RelatedEntityId.Value, out var relatedBookingStatus))
                  {
                      notification.RelatedBookingStatus = relatedBookingStatus;
                  }
              }
          }

        var unreadIds = await _dbContext.Notifications
            .Where(x => x.UserId == userId.Value && !x.IsRead)
            .Select(x => x.NotificationId)
            .ToListAsync(cancellationToken);

        if (unreadIds.Count > 0)
        {
            var entities = await _dbContext.Notifications.Where(x => unreadIds.Contains(x.NotificationId)).ToListAsync(cancellationToken);
            foreach (var entity in entities)
            {
                entity.IsRead = true;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return View(new CustomerInboxPageViewModel
        {
            SearchTerm = searchTerm,
            RelatedEntityType = relatedEntityType,
            IsRead = isRead,
            Notifications = notifications
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileViewModel profile, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View("Profile", new AccountProfilePageViewModel
            {
                Profile = profile,
                ChangePassword = new ChangePasswordViewModel()
            });
        }

        user.FullName = profile.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(profile.PhoneNumber) ? null : profile.PhoneNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["ProfileSuccessMessage"] = "ÄÃ£ cáº­p nháº­t thÃ´ng tin tÃ i khoáº£n.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel changePassword, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        if (!_passwordHasher.VerifyPassword(changePassword.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            ModelState.AddModelError("ChangePassword.CurrentPassword", "Máº­t kháº©u hiá»‡n táº¡i khÃ´ng Ä‘Ãºng.");
        }

        if (!ModelState.IsValid)
        {
            return View("Profile", new AccountProfilePageViewModel
            {
                Profile = new ProfileViewModel
                {
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber
                },
                ChangePassword = changePassword
            });
        }

        user.PasswordHash = _passwordHasher.HashPassword(changePassword.NewPassword, out var salt);
        user.PasswordSalt = salt;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["ProfileSuccessMessage"] = "Äá»•i máº­t kháº©u thÃ nh cÃ´ng.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<Data.Entities.User?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return null;
        }

        return await _dbContext.Users.SingleOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);
    }

    private long? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private bool IsGoogleLoginEnabled()
    {
        return !string.IsNullOrWhiteSpace(_googleAuthOptions.ClientId)
            && !string.IsNullOrWhiteSpace(_googleAuthOptions.ClientSecret);
    }

    private async Task<IActionResult> RedirectToSignedInDestinationAsync(LoginResult result, bool rememberMe, string? returnUrl)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User!.UserId.ToString()),
            new(ClaimTypes.Name, result.User.FullName),
            new(ClaimTypes.Email, result.User.Email)
        };

        claims.AddRange(result.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8)
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (result.Roles.Any(role => RoleConstants.BackOffice.Contains(role)))
        {
            return RedirectToAction("Index", "Admin");
        }

        return RedirectToAction(nameof(Profile));
    }
}

