namespace ChillTour.Models.Admin;

public class AdminUsersFilterViewModel
{
    public string? SearchTerm { get; set; }
    public string? RoleCode { get; set; }
    public bool? IsLocked { get; set; }
}

public class AdminOrdersFilterViewModel
{
    public string? SearchTerm { get; set; }
    public byte? BookingStatus { get; set; }
    public byte? PaymentStatus { get; set; }
    public DateOnly? DepartureFrom { get; set; }
    public DateOnly? DepartureTo { get; set; }
}

public class AdminPromotionsFilterViewModel
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public bool? IsAutoApply { get; set; }
}

public class AdminArticlesFilterViewModel
{
    public string? SearchTerm { get; set; }
    public bool? IsPublished { get; set; }
}

public class AdminCategoriesFilterViewModel
{
    public string? SearchTerm { get; set; }
    public bool? IsActive { get; set; }
}

public class AdminDestinationsFilterViewModel
{
    public string? SearchTerm { get; set; }
    public byte? DestinationType { get; set; }
    public string? CountryCode { get; set; }
    public bool? IsFeatured { get; set; }
    public bool? IsActive { get; set; }
}

public class AdminToursFilterViewModel
{
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsFeatured { get; set; }
    public string? ScheduleState { get; set; }
}
