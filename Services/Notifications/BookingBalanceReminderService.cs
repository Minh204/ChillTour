using ChillTour.Data;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Services.Notifications;

public class BookingBalanceReminderService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private const byte BookingPendingPayment = 0;
    private const byte BookingCancelled = 4;
    private const byte BookingRefunded = 5;
    private const byte BookingPendingFullPaymentVerification = 7;
    private const byte BookingFullyPaid = 8;
    private const byte BookingRefundRequested = 9;
    private const byte BookingPendingRefund = 10;
    private const byte PaymentPending = 0;
    private const byte PaymentDepositPaid = 2;
    private const byte PaymentFullyPaid = 3;
    private const byte PaymentFailed = 4;
    private const int BalanceAutoCancelDaysBeforeDeparture = 3;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingBalanceReminderService> _logger;

    public BookingBalanceReminderService(IServiceScopeFactory scopeFactory, ILogger<BookingBalanceReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SendDueRemindersAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SendDueRemindersAsync(stoppingToken);
        }
    }

    private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ChillTourDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var now = DateTime.Now;

            await AutoCancelExpiredPendingBookingsAsync(dbContext, notificationService, cancellationToken);
            await AutoCancelOverdueDepositBookingsAsync(dbContext, notificationService, cancellationToken);

            var bookings = await dbContext.Bookings
                .Include(x => x.Tour)
                .Where(x => x.PaymentStatus == PaymentDepositPaid
                            && x.BookingStatus != BookingCancelled
                            && x.BookingStatus != BookingRefunded
                            && x.PaidAmount < x.TotalAmount
                            && x.BalanceDueAt != null
                            && x.BalanceDueAt <= now
                            && x.BalanceReminderSentAt == null)
                .Take(50)
                .ToListAsync(cancellationToken);

            foreach (var booking in bookings)
            {
                var remainingAmount = Math.Max(booking.TotalAmount - booking.PaidAmount, 0m);
                await notificationService.CreateAsync(
                    booking.UserId,
                    notificationType: 3,
                    title: "Đến hạn thanh toán phần còn lại",
                    message: $"Đơn {booking.BookingCode} - {booking.Tour.TourName} cần thanh toán phần còn lại {remainingAmount:N0} đ trước ngày khởi hành. Vui lòng vào trang thanh toán để hoàn tất.",
                    relatedEntityType: "Booking",
                    relatedEntityId: booking.BookingId,
                    cancellationToken: cancellationToken);

                booking.BalanceReminderSentAt = DateTime.UtcNow;
                booking.UpdatedAt = DateTime.UtcNow;
            }

            if (bookings.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking balance payment reminders.");
        }
    }

    private static async Task AutoCancelOverdueDepositBookingsAsync(
        ChillTourDbContext dbContext,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var cutoffDate = DateOnly.FromDateTime(DateTime.Today.AddDays(BalanceAutoCancelDaysBeforeDeparture));
        var now = DateTime.UtcNow;
        const string cancellationReason = "Hệ thống tự động hủy do đã cọc nhưng chưa thanh toán đủ trước 3 ngày khởi hành.";

        var bookings = await dbContext.Bookings
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .Where(x => x.PaymentStatus == PaymentDepositPaid
                        && x.PaidAmount > 0m
                        && x.PaidAmount < x.TotalAmount
                        && x.TourSchedule.DepartureDate <= cutoffDate
                        && x.BookingStatus != BookingCancelled
                        && x.BookingStatus != BookingRefunded
                        && x.BookingStatus != BookingPendingFullPaymentVerification
                        && x.BookingStatus != BookingFullyPaid
                        && x.BookingStatus != BookingRefundRequested
                        && x.BookingStatus != BookingPendingRefund)
            .Take(50)
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

            dbContext.BookingStatusHistories.Add(new Data.Entities.BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = oldStatus,
                NewStatus = BookingCancelled,
                ChangedByUserId = null,
                Notes = cancellationReason,
                ChangedAt = now
            });

            await notificationService.CreateAsync(
                booking.UserId,
                notificationType: 4,
                title: "Đơn đã tự động hủy",
                message: $"Đơn {booking.BookingCode} đã bị hủy vì chưa thanh toán đủ trước 3 ngày khởi hành. Khoản cọc không còn đủ điều kiện yêu cầu hoàn tiền theo chính sách.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);
        }

        if (bookings.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task AutoCancelExpiredPendingBookingsAsync(
        ChillTourDbContext dbContext,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        const string cancellationReason = "Hệ thống tự động hủy do quá hạn giữ chỗ nhưng chưa thanh toán.";

        var bookings = await dbContext.Bookings
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .Include(x => x.Payments)
            .Where(x => x.BookingStatus == BookingPendingPayment
                        && x.PaymentStatus == PaymentPending
                        && x.PaidAmount <= 0m
                        && x.HoldExpiresAt != null
                        && x.HoldExpiresAt <= now
                        && !x.Payments.Any(p => p.PaymentStatus == PaymentDepositPaid || p.PaymentStatus == PaymentFullyPaid || !string.IsNullOrWhiteSpace(p.TransactionReference)))
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var booking in bookings)
        {
            var oldStatus = booking.BookingStatus;
            var bookedSeats = booking.AdultCount + booking.ChildCount;

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

            dbContext.BookingStatusHistories.Add(new Data.Entities.BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = oldStatus,
                NewStatus = BookingCancelled,
                ChangedByUserId = null,
                Notes = cancellationReason,
                ChangedAt = now
            });

            await notificationService.CreateAsync(
                booking.UserId,
                notificationType: 4,
                title: "Đơn nháp đã tự động hủy",
                message: $"Đơn {booking.BookingCode} đã bị hủy vì quá hạn giữ chỗ nhưng chưa thanh toán. Ghế đã được mở bán lại.",
                relatedEntityType: "Booking",
                relatedEntityId: booking.BookingId,
                cancellationToken: cancellationToken);
        }

        if (bookings.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
