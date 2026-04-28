using System.Security.Claims;
using ChillTour.Data;
using ChillTour.Models.Admin;
using ChillTour.Security;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChillTour.Services.Reports;

public sealed class ReportService : IReportService
{
    private sealed class BookingSnapshotItem
    {
        public long BookingId { get; init; }
        public long UserId { get; init; }
        public long TourId { get; init; }
        public long TourScheduleId { get; init; }
        public string BookingCode { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public string CustomerEmail { get; init; } = string.Empty;
        public byte BookingStatus { get; init; }
        public byte PaymentStatus { get; init; }
        public int AdultCount { get; init; }
        public int ChildCount { get; init; }
        public int InfantCount { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal PaidAmount { get; init; }
        public long? PromotionId { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class PaymentSnapshotItem
    {
        public long PaymentId { get; init; }
        public long BookingId { get; init; }
        public string PaymentCode { get; init; } = string.Empty;
        public byte PaymentMethod { get; init; }
        public byte PaymentStatus { get; init; }
        public decimal Amount { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? PaidAt { get; init; }
    }

    private const byte BookingCancelled = 4;
    private const byte BookingRefunded = 5;
    private const byte BookingDepositPaid = 2;
    private const byte BookingConfirmed = 3;
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

    private readonly ChillTourDbContext _dbContext;

    public ReportService(ChillTourDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<byte[]> ExportExcelAsync(AdminReportFilterViewModel filter, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(filter, user, cancellationToken);

        using var workbook = new XLWorkbook();

        FillOverviewSheet(workbook.Worksheets.Add("TongQuan"), summary);

        FillSeriesSheet(workbook.Worksheets.Add("DoanhThu"), summary);
        FillBreakdownSheet(workbook.Worksheets.Add("TrangThaiDon"), "Trạng thái đơn", summary.BookingStatusBreakdown);
        FillBreakdownSheet(workbook.Worksheets.Add("ThanhToan"), "Trạng thái thanh toán", summary.PaymentStatusBreakdown);
        FillBreakdownSheet(workbook.Worksheets.Add("PhuongThuc"), "Phương thức thanh toán", summary.PaymentMethodBreakdown);
        FillBreakdownSheet(workbook.Worksheets.Add("DiemDen"), "Doanh thu theo diem den", summary.RevenueByDestination);
        FillBreakdownSheet(workbook.Worksheets.Add("KhuyenMai"), "Hieu qua khuyen mai", summary.PromotionBreakdown);
        FillTopToursSheet(workbook.Worksheets.Add("TopTour"), summary);
        FillTopCustomersSheet(workbook.Worksheets.Add("TopKhachHang"), summary);
        FillSchedulesSheet(workbook.Worksheets.Add("LichKhoiHanh"), summary);
        FillStaffSheet(workbook.Worksheets.Add("NhanSu"), summary);
        FillTransactionsSheet(workbook.Worksheets.Add("GiaoDich"), summary);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportPdfAsync(AdminReportFilterViewModel filter, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(filter, user, cancellationToken);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10.5f).FontColor(Colors.BlueGrey.Darken4));

                page.Header().Column(column =>
                {
                    column.Item().Text("BAO CAO THONG KE CHILLTOUR").Bold().FontSize(20).FontColor(Colors.Blue.Darken2);
                    column.Item().PaddingTop(4).Text($"Ky bao cao: {summary.FromDate:dd/MM/yyyy} - {summary.ToDate:dd/MM/yyyy}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Element(x => ComposeMetricCard(x, "Tong booking", summary.TotalBookings.ToString("N0"), Colors.Blue.Lighten4));
                        row.RelativeItem().PaddingLeft(8).Element(x => ComposeMetricCard(x, "Doanh thu da thu", $"{summary.CollectedRevenue:N0} d", Colors.Green.Lighten4));
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Element(x => ComposeMetricCard(x, "Doanh thu cho thu", $"{summary.PendingRevenue:N0} d", Colors.Orange.Lighten4));
                        row.RelativeItem().PaddingLeft(8).Element(x => ComposeMetricCard(x, "Ty le huy", $"{summary.CancellationRate:N1}%", Colors.Red.Lighten4));
                    });

                    column.Item().Text("Doanh thu theo chu ky").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        ComposeTableHeader(table, "Moc thoi gian", "Doanh thu", "Booking");

                        foreach (var point in summary.RevenueSeries)
                        {
                            ComposeTableRow(table, point.Label, $"{point.Revenue:N0} d", point.BookingCount.ToString("N0"));
                        }
                    });

                    column.Item().Text("Trang thai booking").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        ComposeTableHeader(table, "Trang thai", "So luong", "Gia tri");

                        foreach (var item in summary.BookingStatusBreakdown)
                        {
                            ComposeTableRow(table, item.Label, item.Count.ToString("N0"), $"{item.Amount:N0} d");
                        }
                    });

                    if (summary.TopTours.Count > 0)
                    {
                        column.Item().Text("Top tour").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2.2f);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            ComposeTableHeader(table, "Tour", "Booking", "Doanh thu", "Diem");

                            foreach (var item in summary.TopTours)
                            {
                                ComposeTableRow(table, item.TourName, item.BookingCount.ToString("N0"), $"{item.Revenue:N0} d", item.Rating.ToString("N1"));
                            }
                        });
                    }

                    if (summary.RecentTransactions.Count > 0)
                    {
                        column.Item().Text("Giao dich gan day").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.6f);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            ComposeTableHeader(table, "Ma GD", "Khach hang", "Trang thai", "So tien");

                            foreach (var item in summary.RecentTransactions.Take(10))
                            {
                                ComposeTableRow(table, item.PaymentCode, item.CustomerName, item.PaymentStatusLabel, $"{item.Amount:N0} d");
                            }
                        });
                    }
                });

                page.Footer()
                    .AlignRight()
                    .Text(text =>
                    {
                        text.Span("Tao luc ");
                        text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).SemiBold();
                    });
            });
        }).GeneratePdf();
    }

    public async Task<AdminReportSummaryViewModel> GetSummaryAsync(AdminReportFilterViewModel filter, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeFilter(filter);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var next30Days = today.AddDays(30);
        var next3Days = today.AddDays(3);
        var isAdmin = user.IsInRole(RoleConstants.Admin);
        var isDirector = user.IsInRole(RoleConstants.Director);
        var isManager = user.IsInRole(RoleConstants.Manager);
        var isAccountant = user.IsInRole(RoleConstants.Accountant);
        var isEmployeeOnly = user.IsInRole(RoleConstants.Employee) && !isAdmin && !isDirector && !isManager && !isAccountant;
        var currentUserId = GetCurrentUserId(user);

        var bookings = _dbContext.Bookings.AsNoTracking()
            .Where(x => x.CreatedAt >= normalized.FromUtc && x.CreatedAt < normalized.ToUtcExclusive);

        if (isEmployeeOnly)
        {
            bookings = currentUserId.HasValue
                ? bookings.Where(x => x.StatusHistory.Any(h => h.ChangedByUserId == currentUserId.Value))
                : bookings.Where(x => false);
        }

        if (normalized.TourId.HasValue)
        {
            bookings = bookings.Where(x => x.TourId == normalized.TourId.Value);
        }

        if (normalized.BookingStatus.HasValue)
        {
            bookings = bookings.Where(x => x.BookingStatus == normalized.BookingStatus.Value);
        }

        if (normalized.PaymentStatus.HasValue)
        {
            bookings = bookings.Where(x => x.PaymentStatus == normalized.PaymentStatus.Value);
        }

        if (normalized.PaymentMethod.HasValue)
        {
            bookings = bookings.Where(x => x.Payments.Any(p => p.PaymentMethod == normalized.PaymentMethod.Value));
        }

        if (!string.IsNullOrWhiteSpace(normalized.Filter.CustomerSearch))
        {
            var customerSearch = normalized.Filter.CustomerSearch.Trim();
            bookings = bookings.Where(x =>
                x.ContactName.Contains(customerSearch) ||
                x.ContactEmail.Contains(customerSearch) ||
                x.User.FullName.Contains(customerSearch) ||
                x.User.Email.Contains(customerSearch));
        }

        var bookingSnapshot = await bookings
            .Select(x => new BookingSnapshotItem
            {
                BookingId = x.BookingId,
                UserId = x.UserId,
                TourId = x.TourId,
                TourScheduleId = x.TourScheduleId,
                BookingCode = x.BookingCode,
                CustomerName = x.ContactName,
                CustomerEmail = x.ContactEmail,
                BookingStatus = x.BookingStatus,
                PaymentStatus = x.PaymentStatus,
                AdultCount = x.AdultCount,
                ChildCount = x.ChildCount,
                InfantCount = x.InfantCount,
                TotalAmount = x.TotalAmount,
                PaidAmount = x.PaidAmount,
                PromotionId = x.PromotionId,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var filteredBookingIds = bookingSnapshot.Select(x => x.BookingId).ToList();
        var filteredTourIds = bookingSnapshot.Select(x => x.TourId).Distinct().ToList();

        var payments = _dbContext.Payments.AsNoTracking()
            .Where(x => (x.PaidAt ?? x.CreatedAt) >= normalized.FromUtc && (x.PaidAt ?? x.CreatedAt) < normalized.ToUtcExclusive);

        if (filteredBookingIds.Count == 0)
        {
            payments = payments.Where(x => false);
        }
        else
        {
            payments = payments.Where(x => filteredBookingIds.Contains(x.BookingId));
        }

        if (normalized.PaymentMethod.HasValue)
        {
            payments = payments.Where(x => x.PaymentMethod == normalized.PaymentMethod.Value);
        }

        if (normalized.PaymentStatus.HasValue)
        {
            payments = payments.Where(x => x.PaymentStatus == normalized.PaymentStatus.Value);
        }

        var paymentSnapshot = await payments
            .Select(x => new PaymentSnapshotItem
            {
                PaymentId = x.PaymentId,
                BookingId = x.BookingId,
                PaymentCode = x.PaymentCode,
                PaymentMethod = x.PaymentMethod,
                PaymentStatus = x.PaymentStatus,
                Amount = x.Amount,
                CreatedAt = x.CreatedAt,
                PaidAt = x.PaidAt
            })
            .ToListAsync(cancellationToken);

        var reviews = _dbContext.Reviews.AsNoTracking()
            .Where(x => x.ModerationStatus == 1 && x.CreatedAt >= normalized.FromUtc && x.CreatedAt < normalized.ToUtcExclusive);

        if (normalized.TourId.HasValue)
        {
            reviews = reviews.Where(x => x.TourId == normalized.TourId.Value);
        }
        else if (filteredTourIds.Count > 0)
        {
            reviews = reviews.Where(x => filteredTourIds.Contains(x.TourId));
        }

        var reviewSnapshot = await reviews
            .Select(x => new { x.TourId, x.Rating })
            .ToListAsync(cancellationToken);

        var customerUsers = _dbContext.Users.AsNoTracking()
            .Where(x => x.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Customer));
        var relatedCustomerIds = bookingSnapshot.Select(x => x.UserId).Distinct().ToList();

        var newCustomers = isEmployeeOnly
            ? await customerUsers.CountAsync(x => relatedCustomerIds.Contains(x.UserId) && x.CreatedAt >= normalized.FromUtc && x.CreatedAt < normalized.ToUtcExclusive, cancellationToken)
            : await customerUsers.CountAsync(x => x.CreatedAt >= normalized.FromUtc && x.CreatedAt < normalized.ToUtcExclusive, cancellationToken);

        var returningCustomers = bookingSnapshot
            .GroupBy(x => x.UserId)
            .Count(g => g.Count() >= 2);

        var bookingStatusBreakdown = bookingSnapshot
            .GroupBy(x => x.BookingStatus)
            .Select(g => new AdminReportBreakdownItemViewModel
            {
                Label = BookingStatusLabel(g.Key),
                Count = g.Count(),
                Amount = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var paymentStatusBreakdown = paymentSnapshot
            .GroupBy(x => x.PaymentStatus)
            .Select(g => new AdminReportBreakdownItemViewModel
            {
                Label = PaymentStatusLabel(g.Key),
                Count = g.Count(),
                Amount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var paymentMethodBreakdown = paymentSnapshot
            .GroupBy(x => x.PaymentMethod)
            .Select(g => new AdminReportBreakdownItemViewModel
            {
                Label = PaymentMethodLabel(g.Key),
                Count = g.Count(),
                Amount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        ApplyPercents(bookingStatusBreakdown, bookingStatusBreakdown.Any() ? bookingStatusBreakdown.Max(x => x.Count) : 0);
        ApplyPercents(paymentStatusBreakdown, paymentStatusBreakdown.Any() ? paymentStatusBreakdown.Max(x => x.Count) : 0);
        ApplyPercents(paymentMethodBreakdown, paymentMethodBreakdown.Any() ? paymentMethodBreakdown.Max(x => x.Count) : 0);

        var revenueSeries = BuildRevenueSeries(normalized, bookingSnapshot, paymentSnapshot);
        var successfulPaymentSnapshot = paymentSnapshot
            .Where(x => x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid)
            .ToList();
        var todayStart = DateTime.Today;
        var tomorrowStart = todayStart.AddDays(1);
        var weekStart = todayStart.AddDays(-6);
        var monthStart = new DateTime(todayStart.Year, todayStart.Month, 1);
        var totalCustomers = isEmployeeOnly
            ? relatedCustomerIds.Count
            : await customerUsers.CountAsync(cancellationToken);
        var activeTours = await _dbContext.Tours.AsNoTracking().CountAsync(x => x.IsPublished, cancellationToken);

        var totalRevenue = successfulPaymentSnapshot.Sum(x => x.Amount);
        var todayRevenue = successfulPaymentSnapshot
            .Where(x => (x.PaidAt ?? x.CreatedAt) >= todayStart && (x.PaidAt ?? x.CreatedAt) < tomorrowStart)
            .Sum(x => x.Amount);
        var weekRevenue = successfulPaymentSnapshot
            .Where(x => (x.PaidAt ?? x.CreatedAt) >= weekStart && (x.PaidAt ?? x.CreatedAt) < tomorrowStart)
            .Sum(x => x.Amount);
        var monthRevenue = successfulPaymentSnapshot
            .Where(x => (x.PaidAt ?? x.CreatedAt) >= monthStart && (x.PaidAt ?? x.CreatedAt) < tomorrowStart)
            .Sum(x => x.Amount);
        var averageGuestsPerBooking = bookingSnapshot.Count == 0
            ? 0m
            : decimal.Round((decimal)bookingSnapshot.Average(x => x.AdultCount + x.ChildCount + x.InfantCount), 1);
        var repeatBookingFrequency = bookingSnapshot.Select(x => x.UserId).Distinct().Count() == 0
            ? 0m
            : decimal.Round((decimal)bookingSnapshot.Count / bookingSnapshot.Select(x => x.UserId).Distinct().Count(), 2);

        var revenueByDestination = filteredBookingIds.Count == 0
            ? new List<AdminReportBreakdownItemViewModel>()
            : await _dbContext.Bookings.AsNoTracking()
                .Where(x => filteredBookingIds.Contains(x.BookingId))
                .GroupBy(x => x.Tour.EndDestination.DestinationName)
                .Select(g => new AdminReportBreakdownItemViewModel
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.PaidAmount)
                })
                .OrderByDescending(x => x.Amount)
                .Take(6)
                .ToListAsync(cancellationToken);

        ApplyPercents(revenueByDestination, revenueByDestination.Any() ? revenueByDestination.Max(x => x.Count) : 0);

        var promotionBreakdown = filteredBookingIds.Count == 0
            ? new List<AdminReportBreakdownItemViewModel>()
            : await _dbContext.Bookings.AsNoTracking()
                .Where(x => filteredBookingIds.Contains(x.BookingId) && x.PromotionId != null)
                .GroupBy(x => new
                {
                    x.Promotion!.PromotionCode,
                    x.Promotion.PromotionName
                })
                .Select(g => new AdminReportBreakdownItemViewModel
                {
                    Label = g.Key.PromotionCode + " - " + g.Key.PromotionName,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.PaidAmount)
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.Amount)
                .Take(6)
                .ToListAsync(cancellationToken);

        ApplyPercents(promotionBreakdown, promotionBreakdown.Any() ? promotionBreakdown.Max(x => x.Count) : 0);

        var topTours = await _dbContext.Tours.AsNoTracking()
            .Where(x => filteredTourIds.Count == 0 ? false : filteredTourIds.Contains(x.TourId))
            .Select(x => new AdminReportTopTourViewModel
            {
                TourId = x.TourId,
                TourName = x.TourName,
                TourCode = x.TourCode,
                BookingCount = x.Bookings.Count(b => filteredBookingIds.Contains(b.BookingId)),
                Revenue = x.Bookings.Where(b => filteredBookingIds.Contains(b.BookingId)).Sum(b => b.PaidAmount),
                Rating = x.Reviews.Where(r => r.ModerationStatus == 1).Average(r => (decimal?)r.Rating) ?? 0m,
                CancelledBookings = x.Bookings.Count(b => filteredBookingIds.Contains(b.BookingId) && (b.BookingStatus == BookingCancelled || b.BookingStatus == BookingRefunded)),
                FillRate = x.Schedules.Sum(s => s.TotalSeats) <= 0
                    ? 0m
                    : decimal.Round(x.Schedules.Sum(s => s.ReservedSeats) * 100m / x.Schedules.Sum(s => s.TotalSeats), 1)
            })
            .OrderByDescending(x => x.BookingCount)
            .ThenByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync(cancellationToken);

        var lowBookingTours = await _dbContext.Tours.AsNoTracking()
            .Where(x => x.IsPublished)
            .Select(x => new AdminReportTopTourViewModel
            {
                TourId = x.TourId,
                TourName = x.TourName,
                TourCode = x.TourCode,
                BookingCount = x.Bookings.Count(b => b.CreatedAt >= normalized.FromUtc && b.CreatedAt < normalized.ToUtcExclusive),
                Revenue = x.Bookings.Where(b => b.CreatedAt >= normalized.FromUtc && b.CreatedAt < normalized.ToUtcExclusive).Sum(b => b.PaidAmount),
                Rating = x.Reviews.Where(r => r.ModerationStatus == 1).Average(r => (decimal?)r.Rating) ?? 0m,
                CancelledBookings = x.Bookings.Count(b => b.CreatedAt >= normalized.FromUtc && b.CreatedAt < normalized.ToUtcExclusive && (b.BookingStatus == BookingCancelled || b.BookingStatus == BookingRefunded)),
                FillRate = x.Schedules.Sum(s => s.TotalSeats) <= 0
                    ? 0m
                    : decimal.Round(x.Schedules.Sum(s => s.ReservedSeats) * 100m / x.Schedules.Sum(s => s.TotalSeats), 1)
            })
            .OrderBy(x => x.BookingCount)
            .ThenBy(x => x.Revenue)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topCustomers = bookingSnapshot
            .GroupBy(x => new { x.UserId, x.CustomerName, x.CustomerEmail })
            .Select(g => new AdminReportTopCustomerViewModel
            {
                UserId = g.Key.UserId,
                CustomerName = g.Key.CustomerName,
                Email = g.Key.CustomerEmail,
                BookingCount = g.Count(),
                TotalSpent = g.Sum(x => x.PaidAmount),
                LastBookingAt = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => x.TotalSpent)
            .ThenByDescending(x => x.BookingCount)
            .Take(6)
            .ToList();

        var schedules = _dbContext.TourSchedules.AsNoTracking()
            .Include(x => x.Tour)
            .Where(x => x.DepartureDate >= today && x.DepartureDate <= next30Days);

        if (normalized.TourId.HasValue)
        {
            schedules = schedules.Where(x => x.TourId == normalized.TourId.Value);
        }

        var scheduleSnapshot = await schedules
            .OrderBy(x => x.DepartureDate)
            .Take(6)
            .Select(x => new AdminReportScheduleViewModel
            {
                TourName = x.Tour.TourName,
                DepartureDate = x.DepartureDate,
                AvailableSeats = x.AvailableSeats,
                ReservedSeats = x.ReservedSeats,
                TotalSeats = x.TotalSeats,
                AdultPrice = x.AdultPrice
            })
            .ToListAsync(cancellationToken);

        var staffPerformance = await _dbContext.BookingStatusHistories.AsNoTracking()
            .Where(x => x.ChangedAt >= normalized.FromUtc && x.ChangedAt < normalized.ToUtcExclusive && x.ChangedByUserId != null)
            .Where(x => x.ChangedByUser!.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Employee))
            .GroupBy(x => new
            {
                x.ChangedByUserId,
                x.ChangedByUser!.FullName,
                x.ChangedByUser.Email
            })
            .Select(g => new AdminReportStaffPerformanceViewModel
            {
                StaffName = g.Key.FullName,
                Email = g.Key.Email,
                HandledActions = g.Count(),
                ConfirmedBookings = g.Count(x => x.NewStatus == BookingConfirmed || x.NewStatus == BookingFullyPaid),
                RefundCases = g.Count(x => x.NewStatus == BookingPendingRefund || x.NewStatus == BookingRefunded)
            })
            .OrderByDescending(x => x.HandledActions)
            .Take(6)
            .ToListAsync(cancellationToken);

        var recentTransactions = await _dbContext.Payments.AsNoTracking()
            .Include(x => x.Booking)
            .ThenInclude(x => x.User)
            .Where(x => (x.PaidAt ?? x.CreatedAt) >= normalized.FromUtc && (x.PaidAt ?? x.CreatedAt) < normalized.ToUtcExclusive)
            .Where(x => filteredBookingIds.Contains(x.BookingId))
            .OrderByDescending(x => x.PaidAt ?? x.CreatedAt)
            .Take(8)
            .Select(x => new AdminReportTransactionViewModel
            {
                PaymentCode = x.PaymentCode,
                BookingCode = x.Booking.BookingCode,
                CustomerName = x.Booking.User.FullName,
                Amount = x.Amount,
                PaymentMethodLabel = PaymentMethodLabel(x.PaymentMethod),
                PaymentStatusLabel = PaymentStatusLabel(x.PaymentStatus),
                CreatedAt = x.PaidAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (normalized.TourId.HasValue)
        {
            recentTransactions = recentTransactions
                .Where(x => bookingSnapshot.Any(b => b.BookingCode == x.BookingCode))
                .ToList();
        }

        var contractsQuery = _dbContext.ElectronicContracts.AsNoTracking()
            .Where(x => x.CreatedAt >= normalized.FromUtc && x.CreatedAt < normalized.ToUtcExclusive);

        if (filteredBookingIds.Count > 0)
        {
            contractsQuery = contractsQuery.Where(x => filteredBookingIds.Contains(x.BookingId));
        }

        var contractSnapshot = await contractsQuery
            .Select(x => new
            {
                x.ContractStatus,
                x.CreatedAt,
                x.CustomerSignedAt,
                x.DirectorSignedAt
            })
            .ToListAsync(cancellationToken);

        var contractsSigned = contractSnapshot.Count(x => x.ContractStatus == 3 || x.DirectorSignedAt != null);
        var contractsPending = contractSnapshot.Count(x => x.ContractStatus is 1 or 2);
        var contractSignSuccessRate = contractSnapshot.Count == 0
            ? 0m
            : decimal.Round(contractsSigned * 100m / contractSnapshot.Count, 1);
        var signedDurations = contractSnapshot
            .Where(x => x.DirectorSignedAt.HasValue)
            .Select(x => (decimal)(x.DirectorSignedAt!.Value - x.CreatedAt).TotalHours)
            .ToList();
        var averageContractSigningHours = signedDurations.Count == 0 ? 0m : decimal.Round(signedDurations.Average(), 1);

        var pendingStaffTasks = bookingSnapshot.Count(x =>
            x.BookingStatus == BookingDepositPaid ||
            x.BookingStatus == BookingFullyPaid ||
            x.BookingStatus == BookingRefundRequested ||
            x.BookingStatus == BookingPendingRefund);

        var upcomingSchedulesCount = await schedules.CountAsync(cancellationToken);
        var lowSeatSchedulesCount = await schedules.CountAsync(x => x.AvailableSeats <= 5, cancellationToken);

        var staffTasks = new List<AdminReportTaskItemViewModel>
        {
            new()
            {
                Label = "Booking cần Staff xác nhận",
                Count = bookingSnapshot.Count(x => x.BookingStatus == BookingDepositPaid || x.BookingStatus == BookingFullyPaid),
                Hint = "Các đơn đã được kế toán xác nhận thanh toán và chờ Staff xử lý vận hành."
            },
            new()
            {
                Label = "Yêu cầu hoàn tiền",
                Count = bookingSnapshot.Count(x => x.BookingStatus == BookingRefundRequested),
                Hint = "Khách đã gửi yêu cầu hủy/hoàn tiền và cần Staff chuyển luồng xử lý."
            },
            new()
            {
                Label = "Đơn chờ Accountant đối soát",
                Count = bookingSnapshot.Count(x => x.PaymentStatus == PaymentPendingVerification),
                Hint = "Giao dịch đã ghi nhận nhưng cần xác minh từ kế toán."
            },
            new()
            {
                Label = "Lịch khởi hành trong 3 ngày",
                Count = await schedules.CountAsync(x => x.DepartureDate >= today && x.DepartureDate <= next3Days, cancellationToken),
                Hint = "Các lịch cần rà soát chỗ, danh sách khách và vận hành sớm."
            }
        };

        var cancellationRate = bookingSnapshot.Count == 0 ? 0m : decimal.Round(bookingSnapshot.Count(x => x.BookingStatus == BookingCancelled || x.BookingStatus == BookingRefunded) * 100m / bookingSnapshot.Count, 1);
        var alerts = new List<AdminReportAlertViewModel>();
        if (lowSeatSchedulesCount > 0)
        {
            alerts.Add(new AdminReportAlertViewModel
            {
                Title = "Tour sắp hết chỗ",
                Message = $"{lowSeatSchedulesCount} lịch khởi hành còn từ 5 chỗ trở xuống trong 30 ngày tới.",
                Severity = "warning"
            });
        }

        if (cancellationRate >= 20)
        {
            alerts.Add(new AdminReportAlertViewModel
            {
                Title = "Tỷ lệ hủy cao",
                Message = $"Tỷ lệ hủy hiện là {cancellationRate:N1}%, cần rà soát nguyên nhân hủy và chính sách thanh toán.",
                Severity = "danger"
            });
        }

        if (revenueSeries.Count >= 2 && revenueSeries.Last().Revenue < revenueSeries.ElementAt(revenueSeries.Count - 2).Revenue)
        {
            alerts.Add(new AdminReportAlertViewModel
            {
                Title = "Doanh thu giảm",
                Message = "Mốc gần nhất thấp hơn mốc trước đó. Nên kiểm tra nguồn đơn mới và hiệu quả ưu đãi.",
                Severity = "info"
            });
        }

        var expiringPromotions = await _dbContext.Promotions.AsNoTracking()
            .CountAsync(x => x.IsActive && x.EndAt >= DateTime.UtcNow && x.EndAt <= DateTime.UtcNow.AddDays(7), cancellationToken);
        if (expiringPromotions > 0)
        {
            alerts.Add(new AdminReportAlertViewModel
            {
                Title = "Mã giảm giá sắp hết hạn",
                Message = $"{expiringPromotions} ưu đãi sẽ hết hạn trong 7 ngày tới.",
                Severity = "warning"
            });
        }

        return new AdminReportSummaryViewModel
        {
            FromDate = normalized.FromDate,
            ToDate = normalized.ToDate,
            TotalBookings = bookingSnapshot.Count,
            PaidBookings = bookingSnapshot.Count(x => x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid),
            CancelledBookings = bookingSnapshot.Count(x => x.BookingStatus == BookingCancelled || x.BookingStatus == BookingRefunded),
            TotalCustomers = totalCustomers,
            ActiveTours = activeTours,
            NewBookings = bookingSnapshot.Count(x => x.CreatedAt >= todayStart && x.CreatedAt < tomorrowStart),
            TotalRevenue = totalRevenue,
            TodayRevenue = todayRevenue,
            WeekRevenue = weekRevenue,
            MonthRevenue = monthRevenue,
            CollectedRevenue = totalRevenue,
            PendingRevenue = bookingSnapshot.Where(x => x.BookingStatus == BookingPendingFullPayment || x.BookingStatus == BookingPendingFullPaymentVerification).Sum(x => x.TotalAmount - x.PaidAmount),
            RefundedAmount = paymentSnapshot.Where(x => x.PaymentStatus == PaymentFailed).Sum(x => x.Amount),
            AverageGuestsPerBooking = averageGuestsPerBooking,
            RepeatBookingFrequency = repeatBookingFrequency,
            SuccessfulPayments = paymentSnapshot.Count(x => x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid),
            FailedPayments = paymentSnapshot.Count(x => x.PaymentStatus == PaymentFailed),
            ContractsSigned = contractsSigned,
            ContractsPending = contractsPending,
            ContractSignSuccessRate = contractSignSuccessRate,
            AverageContractSigningHours = averageContractSigningHours,
            CancellationRate = cancellationRate,
            NewCustomers = newCustomers,
            ReturningCustomers = returningCustomers,
            AverageRating = reviewSnapshot.Count == 0 ? 0m : decimal.Round(reviewSnapshot.Average(x => x.Rating), 1),
            ReviewCount = reviewSnapshot.Count,
            UpcomingSchedules = upcomingSchedulesCount,
            LowSeatSchedules = lowSeatSchedulesCount,
            PendingStaffTasks = pendingStaffTasks,
            RevenueSeries = revenueSeries,
            BookingStatusBreakdown = bookingStatusBreakdown,
            PaymentStatusBreakdown = paymentStatusBreakdown,
            PaymentMethodBreakdown = paymentMethodBreakdown,
            RevenueByDestination = revenueByDestination,
            PromotionBreakdown = promotionBreakdown,
            TopTours = topTours,
            LowBookingTours = lowBookingTours,
            TopCustomers = topCustomers,
            ScheduleSnapshot = scheduleSnapshot,
            StaffPerformance = staffPerformance,
            RecentTransactions = recentTransactions,
            StaffTasks = staffTasks,
            Alerts = alerts
        };
    }

    private static (AdminReportFilterViewModel Filter, DateTime FromUtc, DateTime ToUtcExclusive, DateOnly FromDate, DateOnly ToDate, long? TourId, byte? BookingStatus, byte? PaymentStatus, byte? PaymentMethod) NormalizeFilter(AdminReportFilterViewModel filter)
    {
        var normalized = new AdminReportFilterViewModel
        {
            Period = string.IsNullOrWhiteSpace(filter.Period) ? "month" : filter.Period.Trim().ToLowerInvariant(),
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            TourId = filter.TourId,
            BookingStatus = filter.BookingStatus,
            PaymentStatus = filter.PaymentStatus,
            PaymentMethod = filter.PaymentMethod,
            CustomerSearch = string.IsNullOrWhiteSpace(filter.CustomerSearch) ? null : filter.CustomerSearch.Trim()
        };

        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly fromDate;
        DateOnly toDate;

        switch (normalized.Period)
        {
            case "day":
                fromDate = today;
                toDate = today;
                break;
            case "week":
                fromDate = today.AddDays(-6);
                toDate = today;
                break;
            case "year":
                fromDate = new DateOnly(today.Year, 1, 1);
                toDate = today;
                break;
            case "custom":
                fromDate = normalized.FromDate ?? today.AddDays(-29);
                toDate = normalized.ToDate ?? today;
                break;
            case "month":
            default:
                fromDate = new DateOnly(today.Year, today.Month, 1);
                toDate = today;
                normalized.Period = "month";
                break;
        }

        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        return (
            normalized,
            fromDate.ToDateTime(TimeOnly.MinValue),
            toDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            fromDate,
            toDate,
            normalized.TourId,
            normalized.BookingStatus,
            normalized.PaymentStatus,
            normalized.PaymentMethod);
    }

    private static IReadOnlyCollection<AdminReportSeriesPointViewModel> BuildRevenueSeries(
        (AdminReportFilterViewModel Filter, DateTime FromUtc, DateTime ToUtcExclusive, DateOnly FromDate, DateOnly ToDate, long? TourId, byte? BookingStatus, byte? PaymentStatus, byte? PaymentMethod) normalized,
        IReadOnlyCollection<BookingSnapshotItem> bookings,
        IReadOnlyCollection<PaymentSnapshotItem> payments)
    {
        var totalDays = normalized.ToDate.DayNumber - normalized.FromDate.DayNumber + 1;
        var useMonthly = totalDays > 40;

        var points = useMonthly
            ? Enumerable.Range(0, ((normalized.ToDate.Year - normalized.FromDate.Year) * 12) + normalized.ToDate.Month - normalized.FromDate.Month + 1)
                .Select(offset => new DateOnly(normalized.FromDate.Year, normalized.FromDate.Month, 1).AddMonths(offset))
                .Select(month => new AdminReportSeriesPointViewModel
                {
                    Label = month.ToString("MM/yyyy"),
                    Revenue = payments.Where(x => (x.PaidAt ?? x.CreatedAt).Year == month.Year && (x.PaidAt ?? x.CreatedAt).Month == month.Month && (x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid)).Sum(x => x.Amount),
                    BookingCount = bookings.Count(x => x.CreatedAt.Year == month.Year && x.CreatedAt.Month == month.Month)
                })
                .ToList()
            : Enumerable.Range(0, totalDays)
                .Select(offset => normalized.FromDate.AddDays(offset))
                .Select(day => new AdminReportSeriesPointViewModel
                {
                    Label = day.ToString("dd/MM"),
                    Revenue = payments.Where(x => DateOnly.FromDateTime(x.PaidAt ?? x.CreatedAt) == day && (x.PaymentStatus == PaymentDepositPaid || x.PaymentStatus == PaymentFullyPaid)).Sum(x => x.Amount),
                    BookingCount = bookings.Count(x => DateOnly.FromDateTime(x.CreatedAt) == day)
                })
                .ToList();

        var maxRevenue = points.Any() ? points.Max(x => x.Revenue) : 0m;
        foreach (var point in points)
        {
            point.Percent = maxRevenue <= 0 ? 8 : Math.Max(8, (int)Math.Round(point.Revenue * 100m / maxRevenue));
        }

        return points;
    }

    private static void ApplyPercents(ICollection<AdminReportBreakdownItemViewModel> items, int maxCount)
    {
        foreach (var item in items)
        {
            item.Percent = maxCount <= 0 ? 0 : Math.Max(8, (int)Math.Round(item.Count * 100m / maxCount));
        }
    }

    private static void FillSeriesSheet(IXLWorksheet sheet, AdminReportSummaryViewModel summary)
    {
        sheet.Cell(1, 1).Value = "Doanh thu theo chu ky";
        sheet.Cell(2, 1).Value = "Moc thoi gian";
        sheet.Cell(2, 2).Value = "Doanh thu";
        sheet.Cell(2, 3).Value = "Booking";

        var row = 3;
        foreach (var item in summary.RevenueSeries)
        {
            sheet.Cell(row, 1).Value = item.Label;
            sheet.Cell(row, 2).Value = item.Revenue;
            sheet.Cell(row, 3).Value = item.BookingCount;
            row++;
        }

        StyleWorksheet(sheet, 3);
    }

    private static void FillOverviewSheet(IXLWorksheet sheet, AdminReportSummaryViewModel summary)
    {
        sheet.Cell(1, 1).Value = "Bao cao ChillTour";
        sheet.Cell(2, 1).Value = "Tu ngay";
        sheet.Cell(2, 2).Value = summary.FromDate.ToString("dd/MM/yyyy");
        sheet.Cell(3, 1).Value = "Den ngay";
        sheet.Cell(3, 2).Value = summary.ToDate.ToString("dd/MM/yyyy");
        sheet.Cell(5, 1).Value = "Chi so";
        sheet.Cell(5, 2).Value = "Gia tri";

        var overviewRows = new (string Label, object Value)[]
        {
            ("Tong booking", summary.TotalBookings),
            ("Tong doanh thu", summary.TotalRevenue),
            ("Doanh thu hom nay", summary.TodayRevenue),
            ("Doanh thu 7 ngay", summary.WeekRevenue),
            ("Doanh thu thang", summary.MonthRevenue),
            ("Tong khach hang", summary.TotalCustomers),
            ("Tour dang hoat dong", summary.ActiveTours),
            ("Don moi hom nay", summary.NewBookings),
            ("Don da thanh toan", summary.PaidBookings),
            ("Don da huy", summary.CancelledBookings),
            ("Doanh thu da thu", summary.CollectedRevenue),
            ("Doanh thu cho thu", summary.PendingRevenue),
            ("Da hoan tien", summary.RefundedAmount),
            ("Khach trung binh / booking", summary.AverageGuestsPerBooking),
            ("Tan suat dat / khach", summary.RepeatBookingFrequency),
            ("Giao dich thanh cong", summary.SuccessfulPayments),
            ("Giao dich that bai", summary.FailedPayments),
            ("Hop dong da ky", summary.ContractsSigned),
            ("Hop dong cho ky", summary.ContractsPending),
            ("Ty le ky thanh cong (%)", summary.ContractSignSuccessRate),
            ("Ty le huy (%)", summary.CancellationRate),
            ("Khach moi", summary.NewCustomers),
            ("Khach quay lai", summary.ReturningCustomers),
            ("Danh gia trung binh", summary.AverageRating),
            ("So danh gia", summary.ReviewCount),
            ("Lich khoi hanh sap toi", summary.UpcomingSchedules),
            ("Lich gan het cho", summary.LowSeatSchedules),
            ("Viec can xu ly", summary.PendingStaffTasks)
        };

        for (var index = 0; index < overviewRows.Length; index++)
        {
            sheet.Cell(index + 6, 1).Value = overviewRows[index].Label;
            sheet.Cell(index + 6, 2).Value = XLCellValue.FromObject(overviewRows[index].Value);
        }

        sheet.Range(1, 1, 1, 2).Merge().Style.Font.Bold = true;
        sheet.Range(1, 1, 1, 2).Style.Font.FontSize = 16;
        sheet.Range(5, 1, 5, 2).Style.Font.Bold = true;
        sheet.Range(5, 1, 5, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF3FF");
        sheet.Range(5, 1, Math.Max(sheet.LastRowUsed()?.RowNumber() ?? 5, 5), 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(5, 1, Math.Max(sheet.LastRowUsed()?.RowNumber() ?? 5, 5), 2).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Columns().AdjustToContents();
    }

    private static void FillBreakdownSheet(IXLWorksheet sheet, string title, IReadOnlyCollection<AdminReportBreakdownItemViewModel> items)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(2, 1).Value = "Hang muc";
        sheet.Cell(2, 2).Value = "So luong";
        sheet.Cell(2, 3).Value = "Gia tri";

        var row = 3;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Label;
            sheet.Cell(row, 2).Value = item.Count;
            sheet.Cell(row, 3).Value = item.Amount;
            row++;
        }

        StyleWorksheet(sheet, 3);
    }

    private static void FillTopToursSheet(IXLWorksheet sheet, AdminReportSummaryViewModel summary)
    {
        sheet.Cell(1, 1).Value = "Top tour";
        sheet.Cell(2, 1).Value = "Ma tour";
        sheet.Cell(2, 2).Value = "Ten tour";
        sheet.Cell(2, 3).Value = "Booking";
        sheet.Cell(2, 4).Value = "Doanh thu";
        sheet.Cell(2, 5).Value = "Diem";
        sheet.Cell(2, 6).Value = "Huy";

        var row = 3;
        foreach (var item in summary.TopTours)
        {
            sheet.Cell(row, 1).Value = item.TourCode;
            sheet.Cell(row, 2).Value = item.TourName;
            sheet.Cell(row, 3).Value = item.BookingCount;
            sheet.Cell(row, 4).Value = item.Revenue;
            sheet.Cell(row, 5).Value = item.Rating;
            sheet.Cell(row, 6).Value = item.CancelledBookings;
            row++;
        }

        StyleWorksheet(sheet, 6);
    }

    private static void FillTopCustomersSheet(IXLWorksheet sheet, AdminReportSummaryViewModel summary)
    {
        sheet.Cell(1, 1).Value = "Top khach hang";
        sheet.Cell(2, 1).Value = "Khach hang";
        sheet.Cell(2, 2).Value = "Email";
        sheet.Cell(2, 3).Value = "Booking";
        sheet.Cell(2, 4).Value = "Tong chi";
        sheet.Cell(2, 5).Value = "Lan dat gan nhat";

        var row = 3;
        foreach (var item in summary.TopCustomers)
        {
            sheet.Cell(row, 1).Value = item.CustomerName;
            sheet.Cell(row, 2).Value = item.Email;
            sheet.Cell(row, 3).Value = item.BookingCount;
            sheet.Cell(row, 4).Value = item.TotalSpent;
            sheet.Cell(row, 5).Value = item.LastBookingAt.ToString("dd/MM/yyyy HH:mm");
            row++;
        }

        StyleWorksheet(sheet, 5);
    }

    private static void FillSchedulesSheet(IXLWorksheet sheet, AdminReportSummaryViewModel summary)
    {
        sheet.Cell(1, 1).Value = "Lich khoi hanh sap toi";
        sheet.Cell(2, 1).Value = "Tour";
        sheet.Cell(2, 2).Value = "Ngay";
        sheet.Cell(2, 3).Value = "Cho trong";
        sheet.Cell(2, 4).Value = "Da giu";
        sheet.Cell(2, 5).Value = "Tong cho";
        sheet.Cell(2, 6).Value = "Gia nguoi lon";

        var row = 3;
        foreach (var item in summary.ScheduleSnapshot)
        {
            sheet.Cell(row, 1).Value = item.TourName;
            sheet.Cell(row, 2).Value = item.DepartureDate.ToString("dd/MM/yyyy");
            sheet.Cell(row, 3).Value = item.AvailableSeats;
            sheet.Cell(row, 4).Value = item.ReservedSeats;
            sheet.Cell(row, 5).Value = item.TotalSeats;
            sheet.Cell(row, 6).Value = item.AdultPrice;
            row++;
        }

        StyleWorksheet(sheet, 6);
    }

    private static void FillStaffSheet(IXLWorksheet sheet, AdminReportSummaryViewModel summary)
    {
        sheet.Cell(1, 1).Value = "Hieu suat nhan su";
        sheet.Cell(2, 1).Value = "Nhan su";
        sheet.Cell(2, 2).Value = "Email";
        sheet.Cell(2, 3).Value = "Tac vu";
        sheet.Cell(2, 4).Value = "Xac nhan";
        sheet.Cell(2, 5).Value = "Hoan tien";

        var row = 3;
        foreach (var item in summary.StaffPerformance)
        {
            sheet.Cell(row, 1).Value = item.StaffName;
            sheet.Cell(row, 2).Value = item.Email;
            sheet.Cell(row, 3).Value = item.HandledActions;
            sheet.Cell(row, 4).Value = item.ConfirmedBookings;
            sheet.Cell(row, 5).Value = item.RefundCases;
            row++;
        }

        StyleWorksheet(sheet, 5);
    }

    private static void FillTransactionsSheet(IXLWorksheet sheet, AdminReportSummaryViewModel summary)
    {
        sheet.Cell(1, 1).Value = "Giao dich gan day";
        sheet.Cell(2, 1).Value = "Ma GD";
        sheet.Cell(2, 2).Value = "Ma don";
        sheet.Cell(2, 3).Value = "Khach hang";
        sheet.Cell(2, 4).Value = "So tien";
        sheet.Cell(2, 5).Value = "Phuong thuc";
        sheet.Cell(2, 6).Value = "Trang thai";
        sheet.Cell(2, 7).Value = "Thoi gian";

        var row = 3;
        foreach (var item in summary.RecentTransactions)
        {
            sheet.Cell(row, 1).Value = item.PaymentCode;
            sheet.Cell(row, 2).Value = item.BookingCode;
            sheet.Cell(row, 3).Value = item.CustomerName;
            sheet.Cell(row, 4).Value = item.Amount;
            sheet.Cell(row, 5).Value = item.PaymentMethodLabel;
            sheet.Cell(row, 6).Value = item.PaymentStatusLabel;
            sheet.Cell(row, 7).Value = item.CreatedAt.ToString("dd/MM/yyyy HH:mm");
            row++;
        }

        StyleWorksheet(sheet, 7);
    }

    private static void StyleWorksheet(IXLWorksheet sheet, int columnCount)
    {
        sheet.Range(1, 1, 1, columnCount).Merge().Style.Font.Bold = true;
        sheet.Range(1, 1, 1, columnCount).Style.Font.FontSize = 16;
        sheet.Range(1, 1, 2, columnCount).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(2, 1, 2, columnCount).Style.Font.Bold = true;
        sheet.Range(2, 1, 2, columnCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF3FF");
        sheet.Range(2, 1, Math.Max(sheet.LastRowUsed()?.RowNumber() ?? 2, 2), columnCount).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(2, 1, Math.Max(sheet.LastRowUsed()?.RowNumber() ?? 2, 2), columnCount).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Columns().AdjustToContents();
    }

    private static void ComposeMetricCard(IContainer container, string label, string value, string backgroundColor)
    {
        container
            .Background(backgroundColor)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(12)
            .Column(column =>
            {
                column.Spacing(4);
                column.Item().Text(label).SemiBold().FontColor(Colors.BlueGrey.Darken2);
                column.Item().Text(value).Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
            });
    }

    private static void ComposeTableHeader(TableDescriptor table, params string[] headers)
    {
        foreach (var header in headers)
        {
            table.Cell().Background(Colors.Blue.Lighten4).PaddingVertical(6).PaddingHorizontal(8)
                .Text(header).SemiBold().FontColor(Colors.Blue.Darken2);
        }
    }

    private static void ComposeTableRow(TableDescriptor table, params string[] cells)
    {
        foreach (var cell in cells)
        {
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(8)
                .Text(cell);
        }
    }

    public static string BookingStatusLabel(byte status) => status switch
    {
        0 => "Chờ thanh toán",
        1 => "Chờ xác nhận cọc",
        2 => "Đã cọc",
        3 => "Đã xác nhận",
        4 => "Đã hủy",
        5 => "Đã hoàn tiền",
        6 => "Chờ thanh toán còn lại",
        7 => "Chờ xác nhận thanh toán đủ",
        8 => "Đã thanh toán đủ",
        9 => "Yêu cầu hoàn tiền",
        10 => "Chờ hoàn tiền",
        _ => "Không rõ"
    };

    public static string PaymentStatusLabel(byte status) => status switch
    {
        0 => "Chưa thanh toán",
        1 => "Chờ đối soát",
        2 => "Đã cọc",
        3 => "Đã thanh toán đủ",
        4 => "Thanh toán lỗi",
        _ => "Không rõ"
    };

    public static string PaymentMethodLabel(byte status) => status switch
    {
        1 => "VNPay",
        2 => "Chuyển khoản",
        3 => "Tiền mặt",
        _ => "Khác"
    };

    private static long? GetCurrentUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var userId) ? userId : null;
    }
}
