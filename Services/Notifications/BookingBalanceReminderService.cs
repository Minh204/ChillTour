using ChillTour.Data;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Services.Notifications;

public class BookingBalanceReminderService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
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

            var bookings = await dbContext.Bookings
                .Include(x => x.Tour)
                .Where(x => x.PaymentStatus == 2
                            && x.BookingStatus != 4
                            && x.BookingStatus != 5
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
}
