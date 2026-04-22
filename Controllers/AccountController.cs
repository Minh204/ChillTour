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
    public async Task<IActionResult> Bookings(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction(nameof(Login));
        }

        var bookings = await _dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .Where(x => x.UserId == userId.Value && (x.PaymentStatus == 1 || x.PaymentStatus == 2 || x.PaymentStatus == 3))
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
                BookingStatus = x.BookingStatus,
                PaymentStatus = x.PaymentStatus,
                PaidAmount = x.PaidAmount,
                CancellationReason = x.CancellationReason,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return View(bookings);
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

        if (booking.BookingStatus is 4 or 5 or 9 or 10)
        {
            TempData["BookingErrorMessage"] = "Đơn này đã đóng hoặc đang trong luồng hoàn tiền.";
            return RedirectToAction(nameof(Bookings));
        }

        var oldStatus = booking.BookingStatus;
        var bookedSeats = booking.AdultCount + booking.ChildCount;
        var hasPaidAmount = booking.PaidAmount > 0m;
        booking.BookingStatus = hasPaidAmount ? (byte)9 : (byte)4;
        booking.CancelledAt = hasPaidAmount ? null : DateTime.UtcNow;
        booking.CancellationReason = model.CancellationReason.Trim();
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
                ? $"Đơn {booking.BookingCode} đã gửi yêu cầu hoàn tiền. Staff sẽ kiểm tra và chuyển sang Accountant xử lý hoàn tiền."
                : $"Đơn {booking.BookingCode} đã được hủy thành công. Lý do: {booking.CancellationReason}",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);

        await _notificationService.CreateForRolesAsync(
            RoleConstants.ManageBookings,
            notificationType: 13,
            title: hasPaidAmount ? "Khách yêu cầu hoàn tiền" : "Khách hàng đã hủy đơn tour",
            message: hasPaidAmount
                ? $"Đơn {booking.BookingCode} đã được khách yêu cầu hoàn tiền. Lý do: {booking.CancellationReason}. Staff cần kiểm tra và chuyển trạng thái chờ hoàn tiền."
                : $"Đơn {booking.BookingCode} đã được khách hàng hủy. Lý do: {booking.CancellationReason}",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);

        TempData["BookingSuccessMessage"] = hasPaidAmount
            ? $"Đã gửi yêu cầu hoàn tiền cho đơn {booking.BookingCode}."
            : $"Đã hủy đơn {booking.BookingCode}.";
        return RedirectToAction(nameof(Bookings));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Inbox(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction(nameof(Login));
        }

        var notifications = await _dbContext.Notifications
            .Where(x => x.UserId == userId.Value)
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

        return View(notifications);
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

