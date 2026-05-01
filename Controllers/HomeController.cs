using System.Diagnostics;
using System.Net;
using ChillTour.Data;
using ChillTour.Models.Home;
using ChillTour.Models;
using ChillTour.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ChillTour.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ChillTourDbContext _dbContext;
        private readonly IEmailSender _emailSender;

        public HomeController(ILogger<HomeController> logger, ChillTourDbContext dbContext, IEmailSender emailSender)
        {
            _logger = logger;
            _dbContext = dbContext;
            _emailSender = emailSender;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var publishedTours = _dbContext.Tours.AsNoTracking().Where(x => x.IsPublished);
            var today = DateOnly.FromDateTime(DateTime.Today);
            var lastMinuteLimit = today.AddDays(5);
            const string heroBannerUrl = "https://images.pexels.com/photos/15692362/pexels-photo-15692362.jpeg?auto=compress&cs=tinysrgb&w=1600";

            var model = new HomePageViewModel
            {
                PublishedTourCount = await publishedTours.CountAsync(cancellationToken),
                ActiveDestinationCount = await _dbContext.Destinations.AsNoTracking().CountAsync(x => x.IsActive, cancellationToken),
                UpcomingDepartureCount = await _dbContext.TourSchedules.AsNoTracking().CountAsync(x => x.Status == 1 && x.DepartureDate >= today, cancellationToken),
                PaidBookingCount = await _dbContext.Bookings.AsNoTracking().CountAsync(x => x.PaymentStatus == 2 || x.PaymentStatus == 3, cancellationToken),
                HeroImageUrl = heroBannerUrl,
                PromotionBanners = await _dbContext.Promotions
                    .AsNoTracking()
                    .Where(x => x.IsActive
                        && x.ShowOnHomeBanner
                        && x.StartAt <= DateTime.UtcNow
                        && x.EndAt >= DateTime.UtcNow
                        && x.BannerImageUrl != null
                        && x.BannerImageUrl != string.Empty)
                    .OrderBy(x => x.BannerDisplayOrder)
                    .ThenBy(x => x.EndAt)
                    .Select(x => new HomePromotionBannerViewModel
                    {
                        PromotionId = x.PromotionId,
                        PromotionCode = x.PromotionCode,
                        PromotionName = x.PromotionName,
                        Description = x.Description,
                        BannerImageUrl = x.BannerImageUrl!,
                        BannerAltText = x.BannerAltText,
                        BannerLinkUrl = x.BannerLinkUrl
                    })
                    .Take(6)
                    .ToListAsync(cancellationToken),
                Destinations = await _dbContext.Destinations
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.IsFeatured)
                    .ThenBy(x => x.DestinationName)
                    .Select(x => new HomeDestinationViewModel
                    {
                        DestinationId = x.DestinationId,
                        DestinationName = x.DestinationName,
                        Summary = x.Summary,
                        ProvinceName = x.ProvinceName,
                        ThumbnailUrl = x.ThumbnailUrl
                            ?? _dbContext.Tours
                                .Where(t => t.IsPublished && (t.StartDestinationId == x.DestinationId || t.EndDestinationId == x.DestinationId))
                                .Select(t => t.MediaItems.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.DisplayOrder).Select(m => m.MediaUrl).FirstOrDefault() ?? t.MainImageUrl)
                                .FirstOrDefault()
                    })
                    .Take(8)
                    .ToListAsync(cancellationToken),
                SearchDestinations = await _dbContext.Destinations
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.DestinationName)
                    .Select(x => new HomeSearchDestinationViewModel
                    {
                        DestinationId = x.DestinationId,
                        DestinationName = x.DestinationName
                    })
                    .ToListAsync(cancellationToken),
                FeaturedTours = await publishedTours
                    .Include(x => x.Category)
                    .Include(x => x.StartDestination)
                    .Include(x => x.EndDestination)
                    .Include(x => x.MediaItems)
                    .Include(x => x.Schedules)
                    .Include(x => x.Reviews)
                    .OrderByDescending(x => x.Reviews.Count == 0 ? 0 : x.Reviews.Average(r => r.Rating))
                    .ThenByDescending(x => x.Reviews.Count)
                    .ThenByDescending(x => x.CreatedAt)
                    .Select(x => new HomeTourCardViewModel
                    {
                        TourName = x.TourName,
                        TourCode = x.TourCode,
                        Slug = x.Slug,
                        CategoryName = x.Category.CategoryName,
                        RouteName = x.StartDestination.DestinationName + " - " + x.EndDestination.DestinationName,
                        ShortDescription = x.ShortDescription,
                        ImageUrl = x.MediaItems.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.DisplayOrder).Select(m => m.MediaUrl).FirstOrDefault() ?? x.MainImageUrl,
                        BasePrice = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice,
                        DepartureDate = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.DepartureDate).Select(s => (DateOnly?)s.DepartureDate).FirstOrDefault(),
                        DurationDays = x.DurationDays,
                        DurationNights = x.DurationNights,
                        RemainingSeats = x.RemainingSeats,
                        IsFeatured = x.IsFeatured,
                        AverageRating = x.Reviews.Count == 0 ? 0 : x.Reviews.Average(r => r.Rating),
                        ReviewCount = x.Reviews.Count,
                        TotalSeats = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.DepartureDate).Select(s => (int?)s.TotalSeats).FirstOrDefault() ?? x.TotalSeats,
                        IsLastMinute = x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit)
                    })
                    .Take(8)
                    .ToListAsync(cancellationToken),
                LastMinuteTours = await publishedTours
                    .Include(x => x.Category)
                    .Include(x => x.StartDestination)
                    .Include(x => x.EndDestination)
                    .Include(x => x.MediaItems)
                    .Include(x => x.Schedules)
                    .Include(x => x.Reviews)
                    .Where(x => x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit))
                    .OrderBy(x => x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit).Min(s => s.DepartureDate))
                    .ThenByDescending(x => x.Reviews.Count == 0 ? 0 : x.Reviews.Average(r => r.Rating))
                    .Select(x => new HomeTourCardViewModel
                    {
                        TourName = x.TourName,
                        TourCode = x.TourCode,
                        Slug = x.Slug,
                        CategoryName = x.Category.CategoryName,
                        RouteName = x.StartDestination.DestinationName + " - " + x.EndDestination.DestinationName,
                        ShortDescription = x.ShortDescription,
                        ImageUrl = x.MediaItems.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.DisplayOrder).Select(m => m.MediaUrl).FirstOrDefault() ?? x.MainImageUrl,
                        BasePrice = x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit).OrderBy(s => s.DepartureDate).Select(s => (decimal?)s.AdultPrice).FirstOrDefault()
                            ?? x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault()
                            ?? x.BasePrice,
                        DepartureDate = x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit).OrderBy(s => s.DepartureDate).Select(s => (DateOnly?)s.DepartureDate).FirstOrDefault(),
                        DurationDays = x.DurationDays,
                        DurationNights = x.DurationNights,
                        RemainingSeats = x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit).OrderBy(s => s.DepartureDate).Select(s => (int?)s.AvailableSeats).FirstOrDefault() ?? x.RemainingSeats,
                        TotalSeats = x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit).OrderBy(s => s.DepartureDate).Select(s => (int?)s.TotalSeats).FirstOrDefault() ?? x.TotalSeats,
                        IsFeatured = x.IsFeatured,
                        AverageRating = x.Reviews.Count == 0 ? 0 : x.Reviews.Average(r => r.Rating),
                        ReviewCount = x.Reviews.Count,
                        IsLastMinute = true
                    })
                    .Take(8)
                    .ToListAsync(cancellationToken),
                ValueTours = await publishedTours
                    .Include(x => x.Category)
                    .Include(x => x.StartDestination)
                    .Include(x => x.EndDestination)
                    .Include(x => x.MediaItems)
                    .Include(x => x.Schedules)
                    .Include(x => x.Reviews)
                    .OrderBy(x => x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice)
                    .ThenByDescending(x => x.IsFeatured)
                    .Select(x => new HomeTourCardViewModel
                    {
                        TourName = x.TourName,
                        TourCode = x.TourCode,
                        Slug = x.Slug,
                        CategoryName = x.Category.CategoryName,
                        RouteName = x.StartDestination.DestinationName + " - " + x.EndDestination.DestinationName,
                        ShortDescription = x.ShortDescription,
                        ImageUrl = x.MediaItems.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.DisplayOrder).Select(m => m.MediaUrl).FirstOrDefault() ?? x.MainImageUrl,
                        BasePrice = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice,
                        DepartureDate = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.DepartureDate).Select(s => (DateOnly?)s.DepartureDate).FirstOrDefault(),
                        DurationDays = x.DurationDays,
                        DurationNights = x.DurationNights,
                        RemainingSeats = x.RemainingSeats,
                        IsFeatured = x.IsFeatured,
                        AverageRating = x.Reviews.Count == 0 ? 0 : x.Reviews.Average(r => r.Rating),
                        ReviewCount = x.Reviews.Count,
                        TotalSeats = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.DepartureDate).Select(s => (int?)s.TotalSeats).FirstOrDefault() ?? x.TotalSeats,
                        IsLastMinute = x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit)
                    })
                    .Take(8)
                    .ToListAsync(cancellationToken)
            };

            return View("TravelHome", model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Contact()
        {
            return View(new ContactFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactFormViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!_emailSender.IsConfigured)
            {
                ModelState.AddModelError(string.Empty, "Hệ thống email chưa được cấu hình. Vui lòng liên hệ quản trị viên.");
                return View(model);
            }

            try
            {
                await _emailSender.SendAsync(
                    "minhabc2004@gmail.com",
                    $"[ChillTour] Liên hệ: {model.Subject.Trim()}",
                    BuildContactEmailBody(model),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send contact email for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Không thể gửi email liên hệ lúc này. Vui lòng thử lại sau.");
                return View(model);
            }

            TempData["ContactSuccessMessage"] = "ChillTour đã nhận thông tin liên hệ của bạn. Bộ phận tư vấn sẽ phản hồi trong thời gian sớm nhất.";
            ModelState.Clear();
            return View(new ContactFormViewModel());
        }

        private static string BuildContactEmailBody(ContactFormViewModel model)
        {
            static string E(string? value) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "Không có" : value.Trim());

            return $"""
                <div style="font-family:Arial,sans-serif;color:#111827;line-height:1.6">
                    <h2 style="margin:0 0 16px;color:#0b63b6">Thông tin liên hệ mới từ ChillTour</h2>
                    <table cellpadding="8" cellspacing="0" style="border-collapse:collapse;width:100%;max-width:720px">
                        <tr><td style="font-weight:700;width:180px">Loại thông tin</td><td>{E(model.ContactType)}</td></tr>
                        <tr><td style="font-weight:700">Họ tên</td><td>{E(model.FullName)}</td></tr>
                        <tr><td style="font-weight:700">Email</td><td>{E(model.Email)}</td></tr>
                        <tr><td style="font-weight:700">Điện thoại</td><td>{E(model.Phone)}</td></tr>
                        <tr><td style="font-weight:700">Tên công ty</td><td>{E(model.CompanyName)}</td></tr>
                        <tr><td style="font-weight:700">Số khách</td><td>{model.GuestCount}</td></tr>
                        <tr><td style="font-weight:700">Địa chỉ</td><td>{E(model.Address)}</td></tr>
                        <tr><td style="font-weight:700">Tiêu đề</td><td>{E(model.Subject)}</td></tr>
                        <tr><td style="font-weight:700;vertical-align:top">Nội dung</td><td>{E(model.Message).Replace("\n", "<br>")}</td></tr>
                    </table>
                    <p style="margin-top:18px;color:#64748b">Thời gian gửi: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
                </div>
                """;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
