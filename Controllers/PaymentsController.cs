using ChillTour.Data;
using ChillTour.Data.Entities;
using ChillTour.Models.Payments;
using ChillTour.Security;
using ChillTour.Services.Notifications;
using ChillTour.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ChillTour.Controllers;

[Authorize]
public class PaymentsController : Controller
{
    private const decimal DepositRate = 0.30m;
    private const string DepositPaymentMode = "deposit";
    private const string FullPaymentMode = "full";
    private const byte BookingPendingPayment = 0;
    private const byte BookingPendingDepositVerification = 1;
    private const byte BookingDepositPaid = 2;
    private const byte BookingConfirmed = 3;
    private const byte BookingCancelled = 4;
    private const byte BookingPendingFullPayment = 6;
    private const byte BookingPendingFullPaymentVerification = 7;
    private const byte BookingFullyPaid = 8;
    private const byte PaymentPending = 0;
    private const byte PaymentPendingVerification = 1;
    private const byte PaymentDepositPaid = 2;
    private const byte PaymentFullyPaid = 3;
    private const byte PaymentFailed = 4;

    private readonly ChillTourDbContext _dbContext;
    private readonly IVnPayService _vnPayService;
    private readonly INotificationService _notificationService;
    private readonly SePayOptions _sePayOptions;

    public PaymentsController(
        ChillTourDbContext dbContext,
        IVnPayService vnPayService,
        INotificationService notificationService,
        IOptions<SePayOptions> sePayOptions)
    {
        _dbContext = dbContext;
        _vnPayService = vnPayService;
        _notificationService = notificationService;
        _sePayOptions = sePayOptions.Value;
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpGet]
    public async Task<IActionResult> Checkout(long bookingId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        await EnsureFirstOrderPromotionAsync(userId.Value, cancellationToken);

        var booking = await _dbContext.Bookings
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .Include(x => x.Payments)
            .Include(x => x.Promotion)
            .SingleOrDefaultAsync(x => x.BookingId == bookingId && x.UserId == userId.Value, cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        NormalizeBookingPaidAmount(booking);
        var remainingAmount = CalculateRemainingAmount(booking);
        var hasPaymentWaitingForVerification = booking.PaymentStatus == PaymentPendingVerification;
        var canPay = booking.BookingStatus is not (BookingCancelled or 5)
                     && remainingAmount > 0m
                     && !hasPaymentWaitingForVerification;
        var mustPayFull = MustPayFull(booking);

        var latestPayment = booking.Payments
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var canCancelPendingBooking = CanCancelPendingBooking(booking);

        var model = new PaymentCheckoutViewModel
        {
            BookingId = booking.BookingId,
            PaymentId = latestPayment?.PaymentId ?? 0,
            BookingCode = booking.BookingCode,
            TourName = booking.Tour.TourName,
            DepartureDate = booking.TourSchedule.DepartureDate,
            ContactName = booking.ContactName,
            Travelers = booking.AdultCount + booking.ChildCount + booking.InfantCount,
            BaseAmount = booking.BaseAmount,
            SingleRoomCount = booking.SingleRoomCount,
            SingleRoomSupplementAmount = booking.ServiceFee,
            LastMinuteDiscountAmount = booking.LastMinuteDiscountAmount,
            IsLastMinuteDeal = booking.IsLastMinuteDeal,
            DiscountAmount = booking.DiscountAmount,
            TotalAmount = booking.TotalAmount,
            PaidAmount = booking.PaidAmount,
            RemainingAmount = remainingAmount,
            DepositAmount = Math.Min(CalculateDepositAmount(booking), remainingAmount),
            FullPaymentAmount = remainingAmount,
            MustPayFull = mustPayFull,
            CanPayDeposit = canPay && !mustPayFull && booking.PaidAmount <= 0m,
            CanPayFull = canPay,
            IsFullyPaid = remainingAmount <= 0m || booking.PaymentStatus == PaymentFullyPaid,
            BalanceDueAt = booking.BalanceDueAt,
            BookingStatus = booking.BookingStatus,
            PaymentStatus = booking.PaymentStatus,
            CanPay = canPay,
            CanCancelPendingBooking = canCancelPendingBooking,
            PaymentCode = latestPayment?.PaymentCode ?? string.Empty,
            BankName = _sePayOptions.BankName,
            BankAccountNo = _sePayOptions.AccountNumber,
            BankAccountName = _sePayOptions.AccountName,
            AppliedPromotionCode = booking.Promotion?.PromotionCode,
            AppliedPromotionName = booking.Promotion?.PromotionName,
            AvailablePromotions = await _dbContext.UserPromotions
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value
                            && x.UsedAt == null
                            && x.Promotion.IsActive
                            && x.Promotion.StartAt <= DateTime.UtcNow
                            && x.Promotion.EndAt >= DateTime.UtcNow)
                .OrderBy(x => x.Promotion.EndAt)
                .Select(x => new PaymentPromotionOptionViewModel
                {
                    PromotionCode = x.Promotion.PromotionCode,
                    PromotionName = x.Promotion.PromotionName
                })
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelPendingBooking(long bookingId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = await _dbContext.Bookings
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.BookingId == bookingId && x.UserId == userId.Value, cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        if (!CanCancelPendingBooking(booking))
        {
            TempData["PaymentErrorMessage"] = "Đơn đã ghi nhận thanh toán hoặc đang chờ xác nhận, không thể hủy tại bước checkout.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var oldStatus = booking.BookingStatus;
        var bookedSeats = booking.AdultCount + booking.ChildCount;
        var now = DateTime.UtcNow;
        const string cancellationReason = "Khách hủy đơn nháp trước khi thanh toán.";

        booking.BookingStatus = BookingCancelled;
        booking.PaymentStatus = PaymentFailed;
        booking.CancelledAt = now;
        booking.CancellationReason = cancellationReason;
        booking.UpdatedAt = now;

        booking.Tour.RemainingSeats += bookedSeats;
        booking.Tour.UpdatedAt = now;
        booking.TourSchedule.AvailableSeats += bookedSeats;
        booking.TourSchedule.ReservedSeats = Math.Max(booking.TourSchedule.ReservedSeats - bookedSeats, 0);
        booking.TourSchedule.UpdatedAt = now;

        foreach (var payment in booking.Payments.Where(x => x.PaymentStatus == PaymentPending && string.IsNullOrWhiteSpace(x.TransactionReference)))
        {
            payment.PaymentStatus = PaymentFailed;
            payment.PaidAt = null;
            payment.FailureReason = cancellationReason;
            payment.UpdatedAt = now;
        }

        _dbContext.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.BookingId,
            OldStatus = oldStatus,
            NewStatus = booking.BookingStatus,
            ChangedByUserId = userId.Value,
            Notes = cancellationReason,
            ChangedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["TourSuccessMessage"] = $"Đã hủy đơn nháp {booking.BookingCode}. Bạn có thể đặt lại tour khi cần.";

        await _notificationService.CreateAsync(
            booking.UserId,
            notificationType: 4,
            title: "Đơn nháp đã hủy",
            message: $"Đơn {booking.BookingCode} đã được hủy trước khi thanh toán. Đơn không còn giữ chỗ và không được tính là đã đặt tour.",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);

        return RedirectToAction("Details", "Tours", new { slug = booking.Tour.Slug });
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyPromotion(long bookingId, string promotionCode, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        await EnsureFirstOrderPromotionAsync(userId.Value, cancellationToken);

        var booking = await _dbContext.Bookings
            .Include(x => x.Promotion)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.BookingId == bookingId && x.UserId == userId.Value, cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        if (booking.PaymentStatus is PaymentPendingVerification or PaymentDepositPaid or PaymentFullyPaid || booking.PaidAmount > 0m)
        {
            TempData["PaymentErrorMessage"] = "Đơn đã ghi nhận thanh toán, không thể thay đổi mã ưu đãi.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        promotionCode = (promotionCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(promotionCode))
        {
            booking.PromotionId = null;
            booking.DiscountAmount = 0m;
            booking.TotalAmount = CalculateBookingTotal(booking.BaseAmount, booking.TaxAmount, booking.ServiceFee, booking.LastMinuteDiscountAmount, 0m);
            await SyncPendingPaymentAmountAsync(booking, cancellationToken);
            TempData["PaymentInfoMessage"] = "Đã xóa mã ưu đãi khỏi đơn hàng.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var promotion = await _dbContext.Promotions
            .SingleOrDefaultAsync(x => x.PromotionCode == promotionCode, cancellationToken);

        if (promotion is null)
        {
            TempData["PaymentErrorMessage"] = "Không tìm thấy mã ưu đãi này.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var validationError = await ValidatePromotionAsync(booking, promotion, userId.Value, cancellationToken);
        if (validationError is not null)
        {
            TempData["PaymentErrorMessage"] = validationError;
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var subtotal = booking.BaseAmount + booking.TaxAmount + booking.ServiceFee;
        var discountAmount = CalculatePromotionDiscount(subtotal, promotion);

        booking.PromotionId = promotion.PromotionId;
        booking.DiscountAmount = discountAmount;
        booking.TotalAmount = CalculateBookingTotal(booking.BaseAmount, booking.TaxAmount, booking.ServiceFee, booking.LastMinuteDiscountAmount, discountAmount);
        booking.UpdatedAt = DateTime.UtcNow;

        await SyncPendingPaymentAmountAsync(booking, cancellationToken);

        TempData["PaymentInfoMessage"] = $"Áp dụng mã {promotion.PromotionCode} thành công.";
        return RedirectToAction(nameof(Checkout), new { bookingId });
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartVnPay(long bookingId, string paymentMode, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var booking = await _dbContext.Bookings
            .Include(x => x.Payments)
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .SingleOrDefaultAsync(x => x.BookingId == bookingId && x.UserId == userId.Value, cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        NormalizeBookingPaidAmount(booking);
        var remainingAmount = CalculateRemainingAmount(booking);
        if (booking.PaymentStatus == PaymentPendingVerification)
        {
            TempData["PaymentInfoMessage"] = "Đơn này đang chờ Accountant xác nhận giao dịch. Bạn chưa cần thanh toán thêm.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        if (remainingAmount <= 0m || booking.PaymentStatus == PaymentFullyPaid)
        {
            TempData["PaymentInfoMessage"] = "Đơn này đã được thanh toán toàn bộ.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var mustPayFull = MustPayFull(booking);
        paymentMode = mustPayFull ? FullPaymentMode : NormalizePaymentMode(paymentMode);
        if (paymentMode == DepositPaymentMode && booking.PaidAmount > 0m)
        {
            TempData["PaymentErrorMessage"] = "Đơn đã thanh toán cọc. Bạn chỉ có thể thanh toán toàn bộ phần còn lại.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var paymentAmount = paymentMode == FullPaymentMode
            ? remainingAmount
            : Math.Min(CalculateDepositAmount(booking), remainingAmount);

        if (paymentAmount <= 0m)
        {
            TempData["PaymentErrorMessage"] = "Số tiền thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        foreach (var stalePayment in booking.Payments
                     .Where(x => x.PaymentGateway == "VNPay"
                                 && x.PaymentStatus == PaymentPending
                                 && x.TransactionReference == null))
        {
            stalePayment.PaymentStatus = PaymentFailed;
            stalePayment.FailureReason = "Đã tạo giao dịch VNPay mới thay thế giao dịch chưa hoàn tất.";
            stalePayment.UpdatedAt = DateTime.UtcNow;
        }

        var pendingPayment = new Payment
        {
            BookingId = booking.BookingId,
            PaymentCode = await GeneratePaymentCodeAsync(cancellationToken),
            PaymentMethod = 1,
            PaymentGateway = "VNPay",
            Amount = paymentAmount,
            CurrencyCode = booking.CurrencyCode,
            PaymentStatus = PaymentPending,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(pendingPayment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var paymentUrl = _vnPayService.CreatePaymentUrl(new VnPayRequest
        {
            TxnRef = pendingPayment.PaymentCode.Replace("PAY", string.Empty, StringComparison.OrdinalIgnoreCase),
            Amount = pendingPayment.Amount,
            OrderInfo = paymentMode == FullPaymentMode
                ? $"Thanh toan toan bo don {booking.BookingCode}"
                : $"Thanh toan coc don {booking.BookingCode}",
            IpAddress = GetClientIpAddress()
        });

        return Redirect(paymentUrl);
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartSePay(long bookingId, string paymentMode, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var booking = await _dbContext.Bookings
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.BookingId == bookingId && x.UserId == userId.Value, cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        NormalizeBookingPaidAmount(booking);
        var remainingAmount = CalculateRemainingAmount(booking);
        if (booking.PaymentStatus == PaymentPendingVerification)
        {
            TempData["PaymentInfoMessage"] = "Đơn này đang chờ Accountant xác nhận giao dịch. Bạn chưa cần thanh toán thêm.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        if (remainingAmount <= 0m || booking.PaymentStatus == PaymentFullyPaid)
        {
            TempData["PaymentInfoMessage"] = "Đơn này đã được thanh toán toàn bộ.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var mustPayFull = MustPayFull(booking);
        paymentMode = mustPayFull ? FullPaymentMode : NormalizePaymentMode(paymentMode);
        if (paymentMode == DepositPaymentMode && booking.PaidAmount > 0m)
        {
            TempData["PaymentErrorMessage"] = "Đơn đã thanh toán cọc. Bạn chỉ có thể thanh toán toàn bộ phần còn lại.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        var paymentAmount = paymentMode == FullPaymentMode
            ? remainingAmount
            : Math.Min(CalculateDepositAmount(booking), remainingAmount);

        if (paymentAmount <= 0m)
        {
            TempData["PaymentErrorMessage"] = "Số tiền thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Checkout), new { bookingId });
        }

        foreach (var stalePayment in booking.Payments
                     .Where(x => x.PaymentGateway == "SePay"
                                 && x.PaymentStatus == PaymentPending
                                 && string.IsNullOrWhiteSpace(x.TransactionReference)))
        {
            stalePayment.PaymentStatus = PaymentFailed;
            stalePayment.FailureReason = "Đã tạo giao dịch SePay mới thay thế giao dịch chưa hoàn tất.";
            stalePayment.UpdatedAt = DateTime.UtcNow;
        }

        var pendingPayment = new Payment
        {
            BookingId = booking.BookingId,
            PaymentCode = await GeneratePaymentCodeAsync(cancellationToken),
            PaymentMethod = 2,
            PaymentGateway = "SePay",
            Amount = paymentAmount,
            CurrencyCode = booking.CurrencyCode,
            PaymentStatus = PaymentPending,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(pendingPayment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(SePayPayment), new { paymentId = pendingPayment.PaymentId });
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpGet]
    public async Task<IActionResult> SePayPayment(long paymentId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var payment = await _dbContext.Payments
            .Include(x => x.Booking)
            .ThenInclude(x => x.Tour)
            .SingleOrDefaultAsync(x => x.PaymentId == paymentId
                                       && x.PaymentGateway == "SePay"
                                       && x.Booking.UserId == userId.Value,
                cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        var transferContent = BuildSePayTransferContent(payment.PaymentCode, payment.Booking.BookingCode);
        return View(new SePayPaymentViewModel
        {
            BookingId = payment.BookingId,
            BookingCode = payment.Booking.BookingCode,
            TourName = payment.Booking.Tour.TourName,
            PaymentCode = payment.PaymentCode,
            Amount = payment.Amount,
            TransferContent = transferContent,
            BankName = _sePayOptions.BankName,
            BankAccountNo = _sePayOptions.AccountNumber,
            BankAccountName = _sePayOptions.AccountName,
            QrImageUrl = BuildSePayQrUrl(payment.Amount, transferContent)
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var result = _vnPayService.ParseResponse(Request.Query);
        var processed = await ProcessVnPayResultAsync(result, cancellationToken);

        if (processed.BookingId == 0)
        {
            TempData["PaymentErrorMessage"] = processed.Message;
            return RedirectToAction("Index", "Tours");
        }

        TempData[processed.IsSuccess ? "PaymentInfoMessage" : "PaymentErrorMessage"] = processed.Message;
        return RedirectToAction(nameof(Result), new { bookingId = processed.BookingId });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> VnPayIpn(CancellationToken cancellationToken)
    {
        var result = _vnPayService.ParseResponse(Request.Query);
        var processed = await ProcessVnPayResultAsync(result, cancellationToken);

        if (!result.IsValidSignature)
        {
            return Json(new { RspCode = "97", Message = "Invalid signature" });
        }

        if (processed.BookingId == 0)
        {
            return Json(new { RspCode = "01", Message = "Order not found" });
        }

        return Json(new { RspCode = "00", Message = "Confirm Success" });
    }

    [AllowAnonymous]
    [HttpPost]
    [Route("Payments/SePayWebhook")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidSePayRequest())
        {
            return Unauthorized(new { success = false, message = "Invalid API key" });
        }

        if (!string.Equals(request.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { success = true, message = "Ignored non-incoming transaction" });
        }

        if (!string.Equals(request.AccountNumber, _sePayOptions.AccountNumber, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Invalid receiving account" });
        }

        var paymentCode = ExtractSePayPaymentCode(request);
        if (string.IsNullOrWhiteSpace(paymentCode))
        {
            return BadRequest(new { success = false, message = "Payment code not found" });
        }

        var payment = await _dbContext.Payments
            .Include(x => x.Booking)
            .SingleOrDefaultAsync(x => x.PaymentCode == paymentCode && x.PaymentGateway == "SePay", cancellationToken);

        if (payment is null)
        {
            return NotFound(new { success = false, message = "Payment not found" });
        }

        if (payment.TransactionReference == request.Id.ToString(CultureInfo.InvariantCulture)
            || payment.TransactionReference == request.ReferenceCode)
        {
            return Ok(new { success = true, message = "Transaction already processed" });
        }

        if (request.TransferAmount < payment.Amount)
        {
            return BadRequest(new { success = false, message = "Transfer amount is less than payment amount" });
        }

        var processed = await MarkPaymentWaitingForVerificationAsync(
            payment,
            request.ReferenceCode,
            $"SePay đã ghi nhận giao dịch {request.TransferAmount:N0} đ, mã tham chiếu {request.ReferenceCode}.",
            cancellationToken);

        return Ok(new
        {
            success = processed.IsSuccess,
            bookingId = processed.BookingId,
            message = processed.Message
        });
    }

    [AllowAnonymous]
    [HttpGet]
    [Route("Payments/SePayWebhook")]
    public IActionResult SePayWebhookInfo()
    {
        return Json(new
        {
            ok = true,
            endpoint = "/Payments/SePayWebhook",
            method = "POST",
            message = "SePay webhook endpoint is available. Send JSON with Authorization Bearer or X-API-KEY."
        });
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpGet]
    public async Task<IActionResult> Result(long bookingId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var payment = await _dbContext.Payments
            .Include(x => x.Booking)
            .Where(x => x.BookingId == bookingId && x.Booking.UserId == userId.Value)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        var isSuccess = payment.PaymentStatus is PaymentDepositPaid or PaymentFullyPaid;
        var isPendingVerification = payment.PaymentStatus == PaymentPending && !string.IsNullOrWhiteSpace(payment.TransactionReference);
        var isFullPayment = payment.Booking.PaymentStatus == PaymentFullyPaid || payment.PaymentStatus == PaymentFullyPaid;
        return View(new PaymentResultViewModel
        {
            BookingId = payment.BookingId,
            IsSuccess = isSuccess || isPendingVerification,
            BookingCode = payment.Booking.BookingCode,
            PaymentCode = payment.PaymentCode,
            TransactionReference = payment.TransactionReference,
            Amount = payment.Amount,
            Message = isSuccess
                ? (isFullPayment
                    ? "Hệ thống đã ghi nhận thanh toán toàn bộ qua VNPay. Đơn hiện chờ bộ phận vận hành xác nhận."
                    : "Hệ thống đã ghi nhận thanh toán cọc qua VNPay. Vui lòng thanh toán phần còn lại trước hạn 5 ngày trước khởi hành.")
                : isPendingVerification
                    ? "Hệ thống đã nhận giao dịch VNPay và đang chờ Accountant đối soát, xác nhận thanh toán. Staff sẽ xử lý booking sau khi thanh toán được xác nhận."
                : $"Thanh toán qua VNPay chưa thành công. {(string.IsNullOrWhiteSpace(payment.FailureReason) ? "Bạn có thể quay lại trang checkout để thử lại." : payment.FailureReason)}"
        });
    }

    private long? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var userId) ? userId : null;
    }

    private string GetClientIpAddress()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip;
    }

    private bool IsValidSePayRequest()
    {
        if (string.IsNullOrWhiteSpace(_sePayOptions.ApiKey))
        {
            return true;
        }

        var rawAuthorization = Request.Headers.Authorization.ToString();
        var bearerToken = rawAuthorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? rawAuthorization["Bearer ".Length..].Trim()
            : rawAuthorization.Trim();

        var apiKey = Request.Headers["X-API-KEY"].FirstOrDefault()
                     ?? Request.Headers["X-SePay-Api-Key"].FirstOrDefault()
                     ?? Request.Query["apiKey"].FirstOrDefault()
                     ?? bearerToken;

        return string.Equals(apiKey, _sePayOptions.ApiKey, StringComparison.Ordinal);
    }

    private string ExtractSePayPaymentCode(SePayWebhookRequest request)
    {
        var prefix = string.IsNullOrWhiteSpace(_sePayOptions.PaymentCodePrefix)
            ? "PAY"
            : Regex.Escape(_sePayOptions.PaymentCodePrefix.Trim());
        var pattern = $@"\b{prefix}[A-Za-z0-9]+\b";

        var candidates = new[]
        {
            request.Code,
            request.Content,
            request.Description
        };

        foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var match = Regex.Match(candidate!, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }
        }

        return string.Empty;
    }

    private string BuildSePayTransferContent(string paymentCode, string bookingCode)
    {
        return $"SEVQR Thanh toan don hang {paymentCode}";
    }

    private string BuildSePayQrUrl(decimal amount, string transferContent)
    {
        var query = new Dictionary<string, string>
        {
            ["bank"] = _sePayOptions.BankName,
            ["acc"] = _sePayOptions.AccountNumber,
            ["template"] = _sePayOptions.QrTemplate,
            ["amount"] = decimal.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture),
            ["des"] = transferContent
        };

        return "https://qr.sepay.vn/img?" + string.Join("&", query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    }

    private async Task<(long BookingId, bool IsSuccess, string Message)> ProcessVnPayResultAsync(VnPayResult result, CancellationToken cancellationToken)
    {
        if (!result.IsValidSignature)
        {
            return (0, false, "Chữ ký phản hồi từ VNPay không hợp lệ.");
        }

        var payment = await _dbContext.Payments
            .Include(x => x.Booking)
            .SingleOrDefaultAsync(x => (x.PaymentCode == result.TxnRef || x.PaymentCode == $"PAY{result.TxnRef}") && x.PaymentGateway == "VNPay", cancellationToken);

        if (payment is null)
        {
            return (0, false, "Không tìm thấy giao dịch thanh toán tương ứng.");
        }

        var booking = await _dbContext.Bookings
            .Include(x => x.TourSchedule)
            .SingleAsync(x => x.BookingId == payment.BookingId, cancellationToken);

        if (payment.PaymentStatus is PaymentDepositPaid or PaymentFullyPaid)
        {
            return (booking.BookingId, true, "Giao dịch này đã được ghi nhận trước đó.");
        }

        payment.TransactionReference = result.TransactionNo;
        payment.UpdatedAt = DateTime.UtcNow;

        if (result.IsSuccess)
        {
            return await MarkPaymentWaitingForVerificationAsync(
                payment,
                result.TransactionNo,
                $"VNPay đã ghi nhận giao dịch {payment.Amount:N0} đ.",
                cancellationToken);
        }

        payment.PaymentStatus = PaymentFailed;
        payment.PaidAt = null;
        payment.FailureReason = $"VNPay trả về mã {result.ResponseCode ?? "--"} / {result.TransactionStatus ?? "--"}.";
        booking.PaymentStatus = booking.PaidAmount > 0m ? PaymentDepositPaid : PaymentFailed;
        booking.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (booking.BookingId, false, "Thanh toán VNPay chưa thành công.");
    }

    private async Task<(long BookingId, bool IsSuccess, string Message)> MarkPaymentWaitingForVerificationAsync(
        Payment payment,
        string? transactionReference,
        string notificationMessage,
        CancellationToken cancellationToken)
    {
        var booking = payment.Booking;
        var willBeFullyPaid = booking.PaidAmount + payment.Amount >= booking.TotalAmount;

        payment.TransactionReference = string.IsNullOrWhiteSpace(transactionReference)
            ? payment.TransactionReference
            : transactionReference;
        payment.PaymentStatus = PaymentPending;
        payment.PaidAt = null;
        payment.FailureReason = null;
        payment.UpdatedAt = DateTime.UtcNow;

        booking.PaymentStatus = PaymentPendingVerification;
        booking.BookingStatus = willBeFullyPaid
            ? BookingPendingFullPaymentVerification
            : BookingPendingDepositVerification;
        booking.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateAsync(
            booking.UserId,
            notificationType: 2,
            title: $"Đã nhận giao dịch {payment.PaymentGateway}",
            message: $"Đơn {booking.BookingCode} đã ghi nhận giao dịch {payment.PaymentGateway} {payment.Amount:N0} đ. Accountant sẽ đối soát và xác nhận thanh toán trước khi Staff xử lý booking.",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);

        await _notificationService.CreateForRolesAsync(
            RoleConstants.ManageFinance,
            notificationType: 12,
            title: "Giao dịch chờ Accountant xác nhận",
            message: $"Đơn {booking.BookingCode} có giao dịch {payment.PaymentGateway} {payment.Amount:N0} đ đang chờ đối soát. {notificationMessage}",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);

        return (booking.BookingId, true, $"{payment.PaymentGateway} đã ghi nhận giao dịch. Đơn đang chờ Accountant xác nhận thanh toán.");
    }

    private async Task<string> GeneratePaymentCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = $"PAY{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(10, 99)}";
            var exists = await _dbContext.Payments.AnyAsync(x => x.PaymentCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }
    }

    private async Task<string?> ValidatePromotionAsync(Booking booking, Promotion promotion, long userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!promotion.IsActive || promotion.StartAt > now || promotion.EndAt < now)
        {
            return "Mã ưu đãi đã hết hiệu lực hoặc chưa đến thời gian áp dụng.";
        }

        var hasClaimedPromotion = await _dbContext.UserPromotions
            .AnyAsync(x => x.UserId == userId && x.PromotionId == promotion.PromotionId && x.UsedAt == null, cancellationToken);

        if (!hasClaimedPromotion)
        {
            return "Bạn chưa nhận mã ưu đãi này trong kho ưu đãi cá nhân.";
        }

        var subtotal = booking.BaseAmount + booking.TaxAmount + booking.ServiceFee;
        if (promotion.MinOrderValue.HasValue && subtotal < promotion.MinOrderValue.Value)
        {
            return $"Đơn hàng cần tối thiểu {promotion.MinOrderValue.Value:N0} đ để áp dụng mã này.";
        }

        if (promotion.MaxUsageCount.HasValue)
        {
            var usageCount = await _dbContext.Bookings.CountAsync(
                x => x.PromotionId == promotion.PromotionId
                     && (x.PaidAmount > 0m
                         || x.PaymentStatus == PaymentDepositPaid
                         || x.PaymentStatus == PaymentFullyPaid
                         || (x.PaymentStatus == PaymentPendingVerification
                             && (x.BookingStatus == BookingPendingDepositVerification || x.BookingStatus == BookingPendingFullPaymentVerification)
                             && x.Payments.Any(p => !string.IsNullOrWhiteSpace(p.TransactionReference)))),
                cancellationToken);
            if (usageCount >= promotion.MaxUsageCount.Value)
            {
                return "Mã ưu đãi đã hết lượt sử dụng.";
            }
        }

        if (promotion.MaxUsagePerUser.HasValue)
        {
            var userUsageCount = await _dbContext.Bookings.CountAsync(
                x => x.PromotionId == promotion.PromotionId
                     && x.UserId == userId
                     && (x.PaidAmount > 0m
                         || x.PaymentStatus == PaymentDepositPaid
                         || x.PaymentStatus == PaymentFullyPaid
                         || (x.PaymentStatus == PaymentPendingVerification
                             && (x.BookingStatus == BookingPendingDepositVerification || x.BookingStatus == BookingPendingFullPaymentVerification)
                             && x.Payments.Any(p => !string.IsNullOrWhiteSpace(p.TransactionReference)))),
                cancellationToken);

            if (userUsageCount >= promotion.MaxUsagePerUser.Value)
            {
                return "Bạn đã dùng hết số lần áp dụng mã ưu đãi này.";
            }
        }

        return null;
    }

    private static decimal CalculatePromotionDiscount(decimal subtotal, Promotion promotion)
    {
        decimal discount = 0m;

        if (promotion.DiscountPercent.HasValue && promotion.DiscountPercent.Value > 0)
        {
            discount = decimal.Round(subtotal * (promotion.DiscountPercent.Value / 100m), 0, MidpointRounding.AwayFromZero);
        }
        else if (promotion.DiscountAmount.HasValue && promotion.DiscountAmount.Value > 0)
        {
            discount = promotion.DiscountAmount.Value;
        }

        if (promotion.MaxDiscountAmount.HasValue && discount > promotion.MaxDiscountAmount.Value)
        {
            discount = promotion.MaxDiscountAmount.Value;
        }

        if (discount > subtotal)
        {
            discount = subtotal;
        }

        return discount;
    }

    private static decimal CalculateBookingTotal(decimal baseAmount, decimal taxAmount, decimal serviceFee, decimal lastMinuteDiscountAmount, decimal discountAmount)
    {
        return Math.Max(baseAmount + taxAmount + serviceFee - lastMinuteDiscountAmount - discountAmount, 0m);
    }

    private async Task SyncPendingPaymentAmountAsync(Booking booking, CancellationToken cancellationToken)
    {
        var pendingPayment = booking.Payments
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault(x => x.PaymentGateway == "VNPay" && x.PaymentStatus == PaymentPending && x.TransactionReference == null);

        if (pendingPayment is not null)
        {
            pendingPayment.Amount = MustPayFull(booking)
                ? CalculateRemainingAmount(booking)
                : Math.Min(CalculateDepositAmount(booking), CalculateRemainingAmount(booking));
            pendingPayment.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizePaymentMode(string? paymentMode)
    {
        return string.Equals(paymentMode, FullPaymentMode, StringComparison.OrdinalIgnoreCase)
            ? FullPaymentMode
            : DepositPaymentMode;
    }

    private static decimal CalculateDepositAmount(Booking booking)
    {
        return decimal.Round(booking.TotalAmount * DepositRate, 0, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateRemainingAmount(Booking booking)
    {
        return Math.Max(booking.TotalAmount - booking.PaidAmount, 0m);
    }

    private static bool MustPayFull(Booking booking)
    {
        if (booking.IsLastMinuteDeal)
        {
            return true;
        }

        return booking.BalanceDueAt.HasValue && DateTime.Today >= booking.BalanceDueAt.Value.Date;
    }

    private static void NormalizeBookingPaidAmount(Booking booking)
    {
        if (booking.PaidAmount > 0m || booking.Payments.Count == 0)
        {
            return;
        }

        booking.PaidAmount = booking.Payments
            .Where(x => x.PaymentStatus is PaymentDepositPaid or PaymentFullyPaid)
            .Sum(x => x.Amount);
    }

    private static bool CanCancelPendingBooking(Booking booking)
    {
        if (booking.BookingStatus is BookingCancelled or 5 || booking.PaidAmount > 0m)
        {
            return false;
        }

        if (booking.PaymentStatus is PaymentPendingVerification or PaymentDepositPaid or PaymentFullyPaid)
        {
            return false;
        }

        return !booking.Payments.Any(x =>
            x.PaymentStatus is PaymentDepositPaid or PaymentFullyPaid
            || (x.PaymentStatus == PaymentPending && !string.IsNullOrWhiteSpace(x.TransactionReference)));
    }

    private async Task EnsureFirstOrderPromotionAsync(long userId, CancellationToken cancellationToken)
    {
        var hasPaidBooking = await _dbContext.Bookings
            .AnyAsync(x => x.UserId == userId && (x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid), cancellationToken);

        if (hasPaidBooking)
        {
            return;
        }

        var promotion = await _dbContext.Promotions
            .SingleOrDefaultAsync(x => x.PromotionCode == "FIRST15" && x.IsActive && x.EndAt >= DateTime.UtcNow, cancellationToken);

        if (promotion is null)
        {
            return;
        }

        var claimed = await _dbContext.UserPromotions
            .AnyAsync(x => x.UserId == userId && x.PromotionId == promotion.PromotionId, cancellationToken);

        if (claimed)
        {
            return;
        }

        _dbContext.UserPromotions.Add(new UserPromotion
        {
            UserId = userId,
            PromotionId = promotion.PromotionId,
            ClaimedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
