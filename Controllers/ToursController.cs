using ChillTour.Data;
using ChillTour.Data.Entities;
using ChillTour.Models.Tours;
using ChillTour.Security;
using ChillTour.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace ChillTour.Controllers;

public class ToursController : Controller
{
    private const int PageSize = 12;
    private const decimal LastMinuteDiscountRate = 0.15m;
    private const int StandardHoldMinutes = 30;
    private const int LastMinuteHoldMinutes = 15;
    private static readonly string[] AllowedReviewImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private readonly ChillTourDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IWebHostEnvironment _environment;

    public ToursController(ChillTourDbContext dbContext, INotificationService notificationService, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _environment = environment;
    }

    [HttpGet("/tours")]
    public async Task<IActionResult> Index(int page = 1, string? searchTerm = null, int? destinationId = null, int? categoryId = null, DateOnly? departureDate = null, string? budgetRange = null, string? sortBy = null, bool lastMinuteOnly = false, string? urgencyFilter = null, CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastMinuteLimit = today.AddDays(5);
        var currentUserId = GetCurrentCustomerId();

        var query = _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .Where(x => x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today))
            .Include(x => x.Category)
            .Include(x => x.StartDestination)
            .Include(x => x.EndDestination)
            .Include(x => x.MediaItems)
            .Include(x => x.Schedules)
            .Include(x => x.Reviews)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim();
            query = query.Where(x =>
                x.TourName.Contains(normalizedSearch) ||
                x.TourCode.Contains(normalizedSearch) ||
                x.Category.CategoryName.Contains(normalizedSearch) ||
                x.StartDestination.DestinationName.Contains(normalizedSearch) ||
                x.EndDestination.DestinationName.Contains(normalizedSearch));
        }

        if (destinationId.HasValue)
        {
            query = query.Where(x => x.EndDestinationId == destinationId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (departureDate.HasValue)
        {
            query = query.Where(x => x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate == departureDate.Value));
        }

        urgencyFilter = NormalizeUrgencyFilter(urgencyFilter, lastMinuteOnly);

        if (urgencyFilter == "last-minute")
        {
            query = query.Where(x => x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit));
        }
        else if (urgencyFilter == "regular")
        {
            query = query.Where(x => !x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit));
        }

        if (!string.IsNullOrWhiteSpace(budgetRange))
        {
            query = budgetRange.Trim().ToLowerInvariant() switch
            {
                "under-5" => query.Where(x => (x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) < 5000000m),
                "5-10" => query.Where(x => (x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) >= 5000000m
                    && (x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) <= 10000000m),
                "10-20" => query.Where(x => (x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) > 10000000m
                    && (x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) <= 20000000m),
                "over-20" => query.Where(x => (x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) > 20000000m),
                _ => query
            };
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)PageSize);

        if (page > totalPages)
        {
            page = totalPages;
        }

        query = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "price-asc" => query.OrderBy(x => x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice)
                .ThenByDescending(x => x.IsFeatured),
            "price-desc" => query.OrderByDescending(x => x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice)
                .ThenByDescending(x => x.IsFeatured),
            "rating-desc" => query.OrderByDescending(x => x.Reviews.Count == 0 ? 0 : x.Reviews.Average(r => r.Rating))
                .ThenByDescending(x => x.Reviews.Count)
                .ThenByDescending(x => x.IsFeatured),
            _ => query.OrderByDescending(x => x.IsFeatured)
                .ThenByDescending(x => x.CreatedAt)
        };

        var tours = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new TourListItemViewModel
            {
                TourId = x.TourId,
                TourCode = x.TourCode,
                TourName = x.TourName,
                Slug = x.Slug,
                CategoryName = x.Category.CategoryName,
                RouteName = x.StartDestination.DestinationName + " -> " + x.EndDestination.DestinationName,
                DurationDays = x.DurationDays,
                DurationNights = x.DurationNights,
                BasePrice = x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice,
                ShortDescription = x.ShortDescription,
                IsFeatured = x.IsFeatured,
                DepartureDate = x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.DepartureDate).Select(s => (DateOnly?)s.DepartureDate).FirstOrDefault(),
                AverageRating = x.Reviews.Count == 0 ? 0 : x.Reviews.Average(r => r.Rating),
                ReviewCount = x.Reviews.Count,
                IsLastMinute = x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit),
                RemainingSeats = x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today).OrderBy(s => s.DepartureDate).Select(s => (int?)s.AvailableSeats).FirstOrDefault() ?? x.RemainingSeats,
                ImageUrl = x.MediaItems
                    .OrderByDescending(m => m.IsPrimary)
                    .ThenBy(m => m.DisplayOrder)
                    .Select(m => m.MediaUrl)
                    .FirstOrDefault() ?? x.MainImageUrl
            })
            .ToListAsync(cancellationToken);

        if (currentUserId.HasValue && tours.Count > 0)
        {
            var tourIds = tours.Select(x => x.TourId).ToList();
            var wishlistedTourIds = await _dbContext.Wishlists
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId.Value && tourIds.Contains(x.TourId))
                .Select(x => x.TourId)
                .ToListAsync(cancellationToken);
            var wishlistedTourIdSet = wishlistedTourIds.ToHashSet();

            foreach (var tour in tours)
            {
                tour.IsWishlisted = wishlistedTourIdSet.Contains(tour.TourId);
            }
        }

        var snapshotTours = await query
            .Select(x => new
            {
                x.IsFeatured,
                LowestPrice = x.Schedules
                    .Where(s => s.Status == 1 && s.DepartureDate >= today)
                    .OrderBy(s => s.AdultPrice)
                    .Select(s => (decimal?)s.AdultPrice)
                    .FirstOrDefault() ?? x.BasePrice,
                EarliestDepartureDate = x.Schedules
                    .Where(s => s.Status == 1 && s.DepartureDate >= today)
                    .OrderBy(s => s.DepartureDate)
                    .Select(s => (DateOnly?)s.DepartureDate)
                    .FirstOrDefault(),
                IsLastMinute = x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit)
            })
            .ToListAsync(cancellationToken);

        var destinationOptions = await _dbContext.Destinations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DestinationName)
            .Select(x => new TourFilterOptionViewModel
            {
                Id = x.DestinationId,
                Name = x.DestinationName
            })
            .ToListAsync(cancellationToken);

        var categoryOptions = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.CategoryName)
            .Select(x => new TourFilterOptionViewModel
            {
                Id = x.CategoryId,
                Name = x.CategoryName
            })
            .ToListAsync(cancellationToken);

        return View(new TourListViewModel
        {
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = PageSize,
            FeaturedCount = snapshotTours.Count(x => x.IsFeatured),
            LastMinuteCount = snapshotTours.Count(x => x.IsLastMinute),
            LowestPrice = snapshotTours.Count == 0 ? null : snapshotTours.Min(x => x.LowestPrice),
            EarliestDepartureDate = snapshotTours
                .Select(x => x.EarliestDepartureDate)
                .Where(x => x.HasValue)
                .OrderBy(x => x)
                .FirstOrDefault(),
            SearchTerm = searchTerm,
            SelectedDestinationId = destinationId,
            SelectedCategoryId = categoryId,
            SelectedDepartureDate = departureDate,
            SelectedBudgetRange = budgetRange,
            SelectedSortBy = sortBy,
            LastMinuteOnly = urgencyFilter == "last-minute",
            SelectedUrgencyFilter = urgencyFilter,
            DestinationOptions = destinationOptions,
            CategoryOptions = categoryOptions,
            Tours = tours
        });
    }

    private static string? NormalizeUrgencyFilter(string? urgencyFilter, bool lastMinuteOnly)
    {
        if (lastMinuteOnly)
        {
            return "last-minute";
        }

        return urgencyFilter?.Trim().ToLowerInvariant() switch
        {
            "last-minute" => "last-minute",
            "regular" => "regular",
            _ => null
        };
    }

    [HttpGet("/tours/{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var tour = await _dbContext.Tours
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.StartDestination)
            .Include(x => x.EndDestination)
            .Include(x => x.MediaItems)
            .Include(x => x.Schedules)
            .Include(x => x.ItineraryDays)
                .ThenInclude(x => x.Hotel)
            .Include(x => x.ItineraryDays)
                .ThenInclude(x => x.Transportation)
            .Include(x => x.Reviews)
                .ThenInclude(x => x.User)
            .Include(x => x.Reviews)
                .ThenInclude(x => x.MediaItems)
            .Where(x => x.Slug == slug && x.IsPublished)
            .SingleOrDefaultAsync(cancellationToken);

        if (tour is null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastMinuteLimit = today.AddDays(5);
        var selectedSchedule = tour.Schedules
            .Where(s => s.Status == 1 && s.DepartureDate >= today && s.AvailableSeats > 0)
            .OrderBy(s => s.DepartureDate)
            .FirstOrDefault();

        var viewModel = new TourDetailViewModel
        {
            TourId = tour.TourId,
            Slug = tour.Slug,
            TourCode = tour.TourCode,
            TourName = tour.TourName,
            CategoryName = tour.Category.CategoryName,
            StartDestinationName = tour.StartDestination.DestinationName,
            EndDestinationName = tour.EndDestination.DestinationName,
            RouteName = tour.StartDestination.DestinationName + " -> " + tour.EndDestination.DestinationName,
            DurationDays = tour.DurationDays,
            DurationNights = tour.DurationNights,
            TotalSeats = selectedSchedule?.TotalSeats ?? tour.TotalSeats,
            RemainingSeats = selectedSchedule?.AvailableSeats ?? tour.RemainingSeats,
            TourScheduleId = selectedSchedule?.TourScheduleId,
            DepartureDate = selectedSchedule?.DepartureDate,
            BasePrice = selectedSchedule?.AdultPrice ?? tour.BasePrice,
            ChildPrice = decimal.Round((selectedSchedule?.AdultPrice ?? tour.BasePrice) * 0.5m, 0, MidpointRounding.AwayFromZero),
            SingleSupplement = selectedSchedule?.SingleSupplement ?? tour.SingleSupplement,
            IsLastMinuteDeal = selectedSchedule is not null
                && selectedSchedule.DepartureDate >= today
                && selectedSchedule.DepartureDate <= lastMinuteLimit,
            DeparturePoint = tour.DeparturePoint,
            ReturnPoint = tour.ReturnPoint,
            PickupIncluded = tour.PickupIncluded,
            ShortDescription = tour.ShortDescription,
            Description = tour.Description,
            ImageUrls = tour.MediaItems
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.DisplayOrder)
                .Select(m => m.MediaUrl)
                .ToList(),
            Schedules = tour.Schedules
                .Where(s => s.DepartureDate >= today)
                .OrderBy(s => s.DepartureDate)
                .Select(s => new TourDetailScheduleViewModel
                {
                    TourScheduleId = s.TourScheduleId,
                    DepartureDate = s.DepartureDate,
                    ReturnDate = s.ReturnDate,
                    AvailableSeats = s.AvailableSeats,
                    TotalSeats = s.TotalSeats,
                    AdultPrice = s.AdultPrice,
                    ChildPrice = decimal.Round(s.AdultPrice * 0.5m, 0, MidpointRounding.AwayFromZero),
                    InfantPrice = 0m,
                    SingleSupplement = s.SingleSupplement,
                    Status = s.Status
                })
                .ToList(),
            ItineraryDays = tour.ItineraryDays
                .OrderBy(i => i.DayNumber)
                .Select(i => new TourDetailItineraryDayViewModel
                {
                    DayNumber = i.DayNumber,
                    Title = i.Title,
                    Summary = i.Summary,
                    Description = i.Description,
                    OvernightStay = i.OvernightStay,
                    BreakfastIncluded = i.BreakfastIncluded,
                    LunchIncluded = i.LunchIncluded,
                    DinnerIncluded = i.DinnerIncluded,
                    HotelName = !string.IsNullOrWhiteSpace(i.HotelName)
                        ? i.HotelName
                        : i.Hotel != null ? i.Hotel.HotelName : null,
                    TransportationName = !string.IsNullOrWhiteSpace(i.TransportationName)
                        ? i.TransportationName
                        : i.Transportation != null
                        ? string.IsNullOrWhiteSpace(i.Transportation.VehicleName)
                            ? i.Transportation.ProviderName
                            : $"{i.Transportation.ProviderName} - {i.Transportation.VehicleName}"
                        : null
                })
                .ToList(),
            Reviews = tour.Reviews
                .Where(x => x.ModerationStatus != 2)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TourReviewItemViewModel
                {
                    ReviewId = x.ReviewId,
                    FullName = x.IsAnonymous ? "Khách hàng ẩn danh" : x.User.FullName,
                    Rating = x.Rating,
                    Comment = x.Comment,
                    CreatedAt = x.CreatedAt,
                    ImageUrls = x.MediaItems
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => m.MediaUrl)
                        .ToList()
                })
                .ToList(),
            AverageRating = tour.Reviews.Count == 0 ? 0 : decimal.Round(tour.Reviews.Average(x => x.Rating), 1),
            ReviewCount = tour.Reviews.Count
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(rawUserId, out var userId))
            {
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .Select(x => new { x.FullName, x.Email, x.PhoneNumber })
                    .SingleOrDefaultAsync(cancellationToken);

                if (user is not null)
                {
                    viewModel.BookingForm.ContactName = user.FullName;
                    viewModel.BookingForm.ContactEmail = user.Email;
                    viewModel.BookingForm.ContactPhone = user.PhoneNumber ?? string.Empty;
                }

                var eligibleBooking = await FindEligibleReviewBookingAsync(userId, tour.TourId, cancellationToken);
                var hasReviewed = await _dbContext.Reviews.AnyAsync(x => x.UserId == userId && x.TourId == tour.TourId, cancellationToken);
                viewModel.CanReview = eligibleBooking is not null && !hasReviewed;
                viewModel.HasReviewed = hasReviewed;
                viewModel.IsWishlisted = await _dbContext.Wishlists
                    .AsNoTracking()
                    .AnyAsync(x => x.UserId == userId && x.TourId == tour.TourId, cancellationToken);
            }
        }

        viewModel.BookingForm.TourId = viewModel.TourId;
        viewModel.BookingForm.TourScheduleId = viewModel.TourScheduleId ?? 0;
        viewModel.ReviewForm.TourId = viewModel.TourId;

        if (viewModel.ImageUrls.Count == 0)
        {
            var fallbackImage = await _dbContext.Tours
                .AsNoTracking()
                .Where(x => x.Slug == slug && x.IsPublished)
                .Select(x => x.MainImageUrl)
                .SingleOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(fallbackImage))
            {
                viewModel.ImageUrls.Add(fallbackImage);
            }
        }

        return View(viewModel);
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpPost("/tours/{tourId:long}/wishlist/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleWishlist(long tourId, string? returnUrl, CancellationToken cancellationToken)
    {
        var userId = GetCurrentCustomerId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var tour = await _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.TourId == tourId && x.IsPublished)
            .Select(x => new { x.TourId, x.TourName })
            .SingleOrDefaultAsync(cancellationToken);

        if (tour is null)
        {
            TempData["TourErrorMessage"] = "Không tìm thấy tour để lưu vào danh sách yêu thích.";
            return RedirectToAction(nameof(Index));
        }

        var wishlist = await _dbContext.Wishlists
            .SingleOrDefaultAsync(x => x.UserId == userId.Value && x.TourId == tourId, cancellationToken);

        var isWishlisted = false;
        string message;
        if (wishlist is null)
        {
            _dbContext.Wishlists.Add(new Wishlist
            {
                UserId = userId.Value,
                TourId = tourId,
                CreatedAt = DateTime.UtcNow
            });
            isWishlisted = true;
            message = $"Đã lưu tour {tour.TourName} vào danh sách yêu thích.";
        }
        else
        {
            _dbContext.Wishlists.Remove(wishlist);
            message = $"Đã bỏ lưu tour {tour.TourName}.";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var wishlistCount = await _dbContext.Wishlists
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId.Value, cancellationToken);

        if (WantsJsonResponse())
        {
            return Json(new
            {
                succeeded = true,
                tourId,
                isWishlisted,
                wishlistCount,
                message
            });
        }

        TempData["TourSuccessMessage"] = message;
        return RedirectToLocalUrl(returnUrl);
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpPost("/tours/{slug}/reviews")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(string slug, [Bind(Prefix = "ReviewForm")] TourReviewFormViewModel form, List<IFormFile>? reviewImages, CancellationToken cancellationToken)
    {
        var tour = await _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.Slug == slug && x.IsPublished)
            .Select(x => new { x.TourId, x.TourName, x.Slug })
            .SingleOrDefaultAsync(cancellationToken);

        if (tour is null)
        {
            return NotFound();
        }

        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(rawUserId, out var userId))
        {
            return Challenge();
        }

        if (form.Rating is < 1 or > 5)
        {
            TempData["TourErrorMessage"] = "Vui lòng chọn số sao từ 1 đến 5.";
            return RedirectToAction(nameof(Details), new { slug });
        }

        if (string.IsNullOrWhiteSpace(form.Comment))
        {
            TempData["TourErrorMessage"] = "Vui lòng nhập nội dung đánh giá.";
            return RedirectToAction(nameof(Details), new { slug });
        }

        var uploadedImages = reviewImages ?? [];
        var imageValidationMessage = ValidateReviewImages(uploadedImages);
        if (!string.IsNullOrWhiteSpace(imageValidationMessage))
        {
            TempData["TourErrorMessage"] = imageValidationMessage;
            return RedirectToAction(nameof(Details), new { slug });
        }

        var eligibleBooking = await FindEligibleReviewBookingAsync(userId, tour.TourId, cancellationToken);
        if (eligibleBooking is null)
        {
            TempData["TourErrorMessage"] = "Chỉ khách hàng đã đặt và thanh toán tour mới có thể đánh giá.";
            return RedirectToAction(nameof(Details), new { slug });
        }

        var hasReviewed = await _dbContext.Reviews.AnyAsync(x => x.UserId == userId && x.TourId == tour.TourId, cancellationToken);
        if (hasReviewed)
        {
            TempData["TourErrorMessage"] = "Bạn đã đánh giá tour này rồi.";
            return RedirectToAction(nameof(Details), new { slug });
        }

        var review = new Review
        {
            TourId = tour.TourId,
            UserId = userId,
            BookingId = eligibleBooking.BookingId,
            Rating = form.Rating,
            Comment = form.Comment.Trim(),
            IsAnonymous = false,
            ModerationStatus = 1,
            HelpfulCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (uploadedImages.Count > 0)
        {
            await SyncReviewImagesAsync(review, uploadedImages, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        TempData["TourSuccessMessage"] = "Đánh giá của bạn đã được ghi nhận.";
        return RedirectToAction(nameof(Details), new { slug });
    }

    [Authorize(Roles = RoleConstants.Customer)]
    [HttpPost("/tours/book")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book([Bind(Prefix = "BookingForm")] TourBookingFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["TourErrorMessage"] = "Thông tin đặt tour chưa hợp lệ.";
            return await RedirectToTourDetailsAsync(form.TourId, cancellationToken);
        }

        var seatsRequested = form.AdultCount + form.ChildCount;
        if (seatsRequested <= 0)
        {
            TempData["TourErrorMessage"] = "Cần chọn ít nhất 1 vé người lớn hoặc trẻ em.";
            return await RedirectToTourDetailsAsync(form.TourId, cancellationToken);
        }

        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(rawUserId, out var userId))
        {
            return Challenge();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var duplicatedBookingExists = await _dbContext.Bookings.AnyAsync(
            x => x.UserId == userId
                 && x.TourId == form.TourId
                 && x.TourSchedule.DepartureDate >= today
                 && x.BookingStatus != 4
                 && x.BookingStatus != 5
                 && (x.PaymentStatus == 1 || x.PaymentStatus == 2 || x.PaymentStatus == 3),
            cancellationToken);

        if (duplicatedBookingExists)
        {
            TempData["TourErrorMessage"] = "Bạn đã có đơn đặt tour này. Không thể đặt lại cùng một tour khi đơn cũ vẫn còn hiệu lực.";
            return await RedirectToTourDetailsAsync(form.TourId, cancellationToken);
        }

        var tour = await _dbContext.Tours
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TourId == form.TourId && x.IsPublished, cancellationToken);
        if (tour is null)
        {
            TempData["TourErrorMessage"] = "Không tìm thấy tour hoặc tour chưa được mở bán.";
            return RedirectToAction(nameof(Index));
        }

        var schedule = await _dbContext.TourSchedules
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TourScheduleId == form.TourScheduleId && x.TourId == form.TourId && x.Status == 1, cancellationToken);

        if (schedule is null)
        {
            TempData["TourErrorMessage"] = "Tour này hiện chưa có lịch khởi hành khả dụng.";
            return RedirectToAction(nameof(Details), new { slug = tour.Slug });
        }

        if (schedule.DepartureDate < today)
        {
            TempData["TourErrorMessage"] = "Ngày khởi hành này đã qua, vui lòng chọn lịch khởi hành mới hơn.";
            return RedirectToAction(nameof(Details), new { slug = tour.Slug });
        }

        if (schedule.AvailableSeats < seatsRequested)
        {
            TempData["TourErrorMessage"] = "Số vé còn lại không đủ để hoàn tất đơn đặt này.";
            return RedirectToAction(nameof(Details), new { slug = tour.Slug });
        }

        if (form.SingleRoomCount < 0 || form.SingleRoomCount > seatsRequested)
        {
            TempData["TourErrorMessage"] = "Số phòng đơn phải từ 0 đến tổng số khách tính vé.";
            return RedirectToAction(nameof(Details), new { slug = tour.Slug });
        }

        if (form.SingleRoomCount > 0 && !(schedule.SingleSupplement ?? tour.SingleSupplement).HasValue)
        {
            TempData["TourErrorMessage"] = "Tour này hiện chưa cấu hình phụ thu phòng đơn.";
            return RedirectToAction(nameof(Details), new { slug = tour.Slug });
        }

        var reservedAt = DateTime.UtcNow;
        var scheduleRows = await _dbContext.TourSchedules
            .Where(x => x.TourScheduleId == schedule.TourScheduleId
                        && x.TourId == tour.TourId
                        && x.Status == 1
                        && x.AvailableSeats >= seatsRequested)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AvailableSeats, x => x.AvailableSeats - seatsRequested)
                .SetProperty(x => x.ReservedSeats, x => x.ReservedSeats + seatsRequested)
                .SetProperty(x => x.UpdatedAt, reservedAt),
                cancellationToken);

        if (scheduleRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["TourErrorMessage"] = "Rất tiếc, số vé vừa được khách khác giữ chỗ trước. Vui lòng chọn lịch khởi hành khác.";
            return RedirectToAction(nameof(Details), new { slug = tour.Slug });
        }

        await SyncTourSeatSummaryAsync(tour.TourId, reservedAt, cancellationToken);

        var bookingCode = await GenerateBookingCodeAsync(cancellationToken);
        var childPrice = decimal.Round(schedule.AdultPrice * 0.5m, 0, MidpointRounding.AwayFromZero);
        var infantPrice = 0m;
        var fareAmount = schedule.AdultPrice * form.AdultCount
                         + childPrice * form.ChildCount
                         + infantPrice * form.InfantCount;
        var singleSupplement = schedule.SingleSupplement ?? tour.SingleSupplement ?? 0m;
        var singleRoomSupplementAmount = singleSupplement * form.SingleRoomCount;
        var subtotal = fareAmount + singleRoomSupplementAmount;
        var isLastMinuteDeal = schedule.DepartureDate >= today
                               && schedule.DepartureDate <= today.AddDays(5);
        var lastMinuteDiscountAmount = isLastMinuteDeal
            ? decimal.Round(subtotal * LastMinuteDiscountRate, 0, MidpointRounding.AwayFromZero)
            : 0m;
        var totalAmount = Math.Max(subtotal - lastMinuteDiscountAmount, 0m);
        var balanceDueAt = schedule.DepartureDate.ToDateTime(TimeOnly.MinValue).AddDays(-5);
        var holdExpiresAt = reservedAt.AddMinutes(isLastMinuteDeal ? LastMinuteHoldMinutes : StandardHoldMinutes);

        var booking = new Booking
        {
            BookingCode = bookingCode,
            UserId = userId,
            TourId = tour.TourId,
            TourScheduleId = schedule.TourScheduleId,
            ContactName = form.ContactName.Trim(),
            ContactEmail = form.ContactEmail.Trim(),
            ContactPhone = form.ContactPhone.Trim(),
            AdultCount = form.AdultCount,
            ChildCount = form.ChildCount,
            InfantCount = form.InfantCount,
            SingleRoomCount = form.SingleRoomCount,
            BaseAmount = fareAmount,
            LastMinuteDiscountAmount = lastMinuteDiscountAmount,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            ServiceFee = singleRoomSupplementAmount,
            TotalAmount = totalAmount,
            PaidAmount = 0m,
            CurrencyCode = tour.CurrencyCode,
            BookingStatus = 0,
            PaymentStatus = 0,
            SpecialRequests = string.IsNullOrWhiteSpace(form.SpecialRequests) ? null : form.SpecialRequests.Trim(),
            IsLastMinuteDeal = isLastMinuteDeal,
            HoldExpiresAt = holdExpiresAt,
            BalanceDueAt = balanceDueAt,
            CreatedAt = reservedAt
        };

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notificationService.CreateAsync(
            userId,
            notificationType: 1,
            title: "Đơn đặt tour đã được tạo",
            message: isLastMinuteDeal
                ? $"Đơn {booking.BookingCode} cho tour {tour.TourName} đã được tạo. Đây là tour giờ chót nên bạn cần thanh toán toàn bộ để giữ chỗ."
                : $"Đơn {booking.BookingCode} cho tour {tour.TourName} đã được tạo. Bạn có thể thanh toán cọc 30% hoặc thanh toán toàn bộ. Phần còn lại cần hoàn tất trước {balanceDueAt:dd/MM/yyyy}.",
            relatedEntityType: "Booking",
            relatedEntityId: booking.BookingId,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RedirectToAction("Checkout", "Payments", new { bookingId = booking.BookingId });
    }

    private async Task<IActionResult> RedirectToTourDetailsAsync(long tourId, CancellationToken cancellationToken)
    {
        var slug = await _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.TourId == tourId)
            .Select(x => x.Slug)
            .SingleOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(slug)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Details), new { slug });
    }

    private async Task SyncTourSeatSummaryAsync(long tourId, DateTime updatedAt, CancellationToken cancellationToken)
    {
        var seatSummary = await _dbContext.TourSchedules
            .Where(x => x.TourId == tourId && x.Status == 1)
            .GroupBy(x => x.TourId)
            .Select(x => new
            {
                TotalSeats = x.Sum(s => s.TotalSeats),
                RemainingSeats = x.Sum(s => s.AvailableSeats)
            })
            .SingleOrDefaultAsync(cancellationToken);

        await _dbContext.Tours
            .Where(x => x.TourId == tourId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.TotalSeats, seatSummary == null ? 0 : seatSummary.TotalSeats)
                .SetProperty(x => x.RemainingSeats, seatSummary == null ? 0 : seatSummary.RemainingSeats)
                .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);
    }

    private async Task<string> GenerateBookingCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = $"BK{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(10, 99)}";
            var exists = await _dbContext.Bookings.AnyAsync(x => x.BookingCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }
    }

    private long? GetCurrentCustomerId()
    {
        if (User.Identity?.IsAuthenticated != true || !User.IsInRole(RoleConstants.Customer))
        {
            return null;
        }

        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private IActionResult RedirectToLocalUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private bool WantsJsonResponse()
    {
        return string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || Request.Headers.Accept.Any(x => x != null && x.Contains("application/json", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Booking?> FindEligibleReviewBookingAsync(long userId, long tourId, CancellationToken cancellationToken)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.TourId == tourId
                        && x.BookingStatus != 4
                        && x.BookingStatus != 5
                        && x.BookingStatus != 9
                        && x.BookingStatus != 10
                        && x.PaymentStatus > 0)
            .OrderByDescending(x => x.ConfirmedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private string? ValidateReviewImages(IReadOnlyCollection<IFormFile> uploadedImages)
    {
        foreach (var image in uploadedImages)
        {
            if (image.Length <= 0)
            {
                return "Có ảnh đánh giá không hợp lệ.";
            }

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!AllowedReviewImageExtensions.Contains(extension))
            {
                return "Chỉ chấp nhận file ảnh jpg, jpeg, png, webp hoặc gif cho đánh giá.";
            }

            if (image.Length > 5 * 1024 * 1024)
            {
                return "Mỗi ảnh đánh giá phải nhỏ hơn hoặc bằng 5MB.";
            }
        }

        return null;
    }

    private async Task SyncReviewImagesAsync(Review review, IReadOnlyCollection<IFormFile> uploadedImages, CancellationToken cancellationToken)
    {
        if (uploadedImages.Count == 0)
        {
            return;
        }

        var nextDisplayOrder = review.MediaItems.Count == 0 ? 1 : review.MediaItems.Max(x => x.DisplayOrder) + 1;
        foreach (var uploadedImage in uploadedImages)
        {
            var relativeUrl = await SaveReviewImageAsync(review.ReviewId, uploadedImage, cancellationToken);
            review.MediaItems.Add(new ReviewMedia
            {
                ReviewId = review.ReviewId,
                MediaUrl = relativeUrl,
                DisplayOrder = nextDisplayOrder++,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private async Task<string> SaveReviewImageAsync(long reviewId, IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var reviewFolder = Path.Combine(_environment.WebRootPath, "uploads", "reviews", reviewId.ToString());
        Directory.CreateDirectory(reviewFolder);

        var fullPath = Path.Combine(reviewFolder, fileName);
        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"/uploads/reviews/{reviewId}/{fileName}";
    }
}

