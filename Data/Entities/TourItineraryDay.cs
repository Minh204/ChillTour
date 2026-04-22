namespace ChillTour.Data.Entities;

public class TourItineraryDay
{
    public long ItineraryDayId { get; set; }
    public long TourId { get; set; }
    public int DayNumber { get; set; }
    public string Title { get; set; } = null!;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? OvernightStay { get; set; }
    public bool BreakfastIncluded { get; set; }
    public bool LunchIncluded { get; set; }
    public bool DinnerIncluded { get; set; }
    public string? HotelName { get; set; }
    public string? TransportationName { get; set; }
    public long? HotelId { get; set; }
    public long? TransportationId { get; set; }

    public Tour Tour { get; set; } = null!;
    public Hotel? Hotel { get; set; }
    public Transportation? Transportation { get; set; }
}
