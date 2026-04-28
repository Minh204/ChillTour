namespace ChillTour.Models.Home;

public class HomePageViewModel
{
    public int PublishedTourCount { get; set; }
    public int ActiveDestinationCount { get; set; }
    public int UpcomingDepartureCount { get; set; }
    public int PaidBookingCount { get; set; }
    public string? HeroImageUrl { get; set; }
    public List<HomePromotionBannerViewModel> PromotionBanners { get; set; } = [];
    public List<HomeCategoryViewModel> Categories { get; set; } = [];
    public List<HomeDestinationViewModel> Destinations { get; set; } = [];
    public List<HomeSearchDestinationViewModel> SearchDestinations { get; set; } = [];
    public List<HomeTourCardViewModel> FeaturedTours { get; set; } = [];
    public List<HomeTourCardViewModel> LastMinuteTours { get; set; } = [];
    public List<HomeTourCardViewModel> ValueTours { get; set; } = [];
}

public class HomePromotionBannerViewModel
{
    public long PromotionId { get; set; }
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BannerImageUrl { get; set; } = string.Empty;
    public string? BannerAltText { get; set; }
    public string? BannerLinkUrl { get; set; }
}

public class HomeCategoryViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TourCount { get; set; }
}

public class HomeDestinationViewModel
{
    public int DestinationId { get; set; }
    public string DestinationName { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ProvinceName { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string ThemeClass { get; set; } = "theme-a";
}

public class HomeSearchDestinationViewModel
{
    public int DestinationId { get; set; }
    public string DestinationName { get; set; } = string.Empty;
}

public class HomeTourCardViewModel
{
    public string TourName { get; set; } = string.Empty;
    public string TourCode { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? ImageUrl { get; set; }
    public decimal BasePrice { get; set; }
    public DateOnly? DepartureDate { get; set; }
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public int RemainingSeats { get; set; }
    public bool IsFeatured { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int TotalSeats { get; set; }
    public bool IsLastMinute { get; set; }
    public bool IsLowStock => RemainingSeats > 0 && RemainingSeats <= 5;
}
