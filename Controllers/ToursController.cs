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
using System.Security.Claims;

namespace ChillTour.Controllers;

public class ToursController : Controller
{
    private const int PageSize = 9;
    private const decimal LastMinuteDiscountRate = 0.15m;
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
    public async Task<IActionResult> Index(int page = 1, string? searchTerm = null, int? destinationId = null, int? categoryId = null, DateOnly? departureDate = null, string? budgetRange = null, string? sortBy = null, bool lastMinuteOnly = false, CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastMinuteLimit = today.AddDays(5);

        var query = _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.IsPublished)
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
            query = query.Where(x => x.Schedules.Any(s => s.Status == 1 && s.DepartureDate == departureDate.Value));
        }

        if (lastMinuteOnly)
        {
            query = query.Where(x => x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit));
        }

        if (!string.IsNullOrWhiteSpace(budgetRange))
        {
            query = budgetRange.Trim().ToLowerInvariant() switch
            {
                "under-5" => query.Where(x => (x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) < 5000000m),
                "5-10" => query.Where(x => (x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) >= 5000000m
                    && (x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) <= 10000000m),
                "10-20" => query.Where(x => (x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) > 10000000m
                    && (x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) <= 20000000m),
                "over-20" => query.Where(x => (x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice) > 20000000m),
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
            "price-asc" => query.OrderBy(x => x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice)
                .ThenByDescending(x => x.IsFeatured),
            "price-desc" => query.OrderByDescending(x => x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice)
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
                BasePrice = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.AdultPrice).Select(s => (decimal?)s.AdultPrice).FirstOrDefault() ?? x.BasePrice,
                ShortDescription = x.ShortDescription,
                IsFeatured = x.IsFeatured,
                DepartureDate = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.DepartureDate).Select(s => (DateOnly?)s.DepartureDate).FirstOrDefault(),
                AverageRating = x.Reviews.Count == 0 ? 0 : x.Reviews.Average(r => r.Rating),
                ReviewCount = x.Reviews.Count,
                IsLastMinute = x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.DepartureDate <= lastMinuteLimit),
                RemainingSeats = x.Schedules.Where(s => s.Status == 1).OrderBy(s => s.DepartureDate).Select(s => (int?)s.AvailableSeats).FirstOrDefault() ?? x.RemainingSeats,
                ImageUrl = x.MediaItems
                    .OrderByDescending(m => m.IsPrimary)
                    .ThenBy(m => m.DisplayOrder)
                    .Select(m => m.MediaUrl)
                    .FirstOrDefault() ?? x.MainImageUrl
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
            SearchTerm = searchTerm,
            SelectedDestinationId = destinationId,
            SelectedCategoryId = categoryId,
            SelectedDepartureDate = departureDate,
            SelectedBudgetRange = budgetRange,
            SelectedSortBy = sortBy,
            LastMinuteOnly = lastMinuteOnly,
            DestinationOptions = destinationOptions,
            CategoryOptions = categoryOptions,
            Tours = tours
        });
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

        var selectedSchedule = tour.Schedules
            .Where(s => s.Status == 1 && s.AvailableSeats > 0)
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
                && selectedSchedule.DepartureDate >= DateOnly.FromDateTime(DateTime.Today)
                && selectedSchedule.DepartureDate <= DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
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

        var duplicatedBookingExists = await _dbContext.Bookings.AnyAsync(
            x => x.UserId == userId
                 && x.TourId == form.TourId
                 && x.BookingStatus != 4
                 && x.BookingStatus != 5,
            cancellationToken);

        if (duplicatedBookingExists)
        {
            TempData["TourErrorMessage"] = "Bạn đã có đơn đặt tour này. Không thể đặt lại cùng một tour khi đơn cũ vẫn còn hiệu lực.";
            return await RedirectToTourDetailsAsync(form.TourId, cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tour = await _dbContext.Tours.SingleOrDefaultAsync(x => x.TourId == form.TourId && x.IsPublished, cancellationToken);
        if (tour is null)
        {
            TempData["TourErrorMessage"] = "Không tìm thấy tour hoặc tour chưa được mở bán.";
            return RedirectToAction(nameof(Index));
        }

        var schedule = await _dbContext.TourSchedules
            .SingleOrDefaultAsync(x => x.TourScheduleId == form.TourScheduleId && x.TourId == form.TourId && x.Status == 1, cancellationToken);

        if (schedule is null)
        {
            TempData["TourErrorMessage"] = "Tour này hiện chưa có lịch khởi hành khả dụng.";
            return RedirectToAction(nameof(Details), new { slug = tour.Slug });
        }

        if (tour.RemainingSeats < seatsRequested || schedule.AvailableSeats < seatsRequested)
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

        var bookingCode = await GenerateBookingCodeAsync(cancellationToken);
        var childPrice = decimal.Round(schedule.AdultPrice * 0.5m, 0, MidpointRounding.AwayFromZero);
        var infantPrice = 0m;
        var fareAmount = schedule.AdultPrice * form.AdultCount
                         + childPrice * form.ChildCount
                         + infantPrice * form.InfantCount;
        var singleSupplement = schedule.SingleSupplement ?? tour.SingleSupplement ?? 0m;
        var singleRoomSupplementAmount = singleSupplement * form.SingleRoomCount;
        var subtotal = fareAmount + singleRoomSupplementAmount;
        var isLastMinuteDeal = schedule.DepartureDate >= DateOnly.FromDateTime(DateTime.Today)
                               && schedule.DepartureDate <= DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var lastMinuteDiscountAmount = isLastMinuteDeal
            ? decimal.Round(subtotal * LastMinuteDiscountRate, 0, MidpointRounding.AwayFromZero)
            : 0m;
        var totalAmount = Math.Max(subtotal - lastMinuteDiscountAmount, 0m);
        var balanceDueAt = schedule.DepartureDate.ToDateTime(TimeOnly.MinValue).AddDays(-5);

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
            BalanceDueAt = balanceDueAt,
            CreatedAt = DateTime.UtcNow
        };

        tour.RemainingSeats -= seatsRequested;
        schedule.AvailableSeats -= seatsRequested;
        schedule.ReservedSeats += seatsRequested;
        schedule.UpdatedAt = DateTime.UtcNow;
        tour.UpdatedAt = DateTime.UtcNow;

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

