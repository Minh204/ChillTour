using System.Diagnostics;
using ChillTour.Data;
using ChillTour.Models.Home;
using ChillTour.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ChillTour.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ChillTourDbContext _dbContext;

        public HomeController(ILogger<HomeController> logger, ChillTourDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
