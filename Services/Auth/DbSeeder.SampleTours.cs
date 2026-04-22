using ChillTour.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Services.Auth;

public partial class DbSeeder
{
    private async Task SeedSampleToursAsync(CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.CategoryId, x.CategoryCode, x.CategoryName, x.Slug })
            .ToListAsync(cancellationToken);

        var categoryIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in categories)
        {
            categoryIds[category.CategoryCode] = category.CategoryId;
            categoryIds[category.CategoryName] = category.CategoryId;
            categoryIds[category.Slug] = category.CategoryId;
        }

        var destinations = await _dbContext.Destinations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToDictionaryAsync(
                x => x.DestinationCode,
                x => new SeedDestinationRef(x.DestinationId, x.DestinationName),
                cancellationToken);

        var definitions = GetSampleTourDefinitions();
        var definitionCodes = definitions.Select(x => x.TourCode).ToList();
        var existingTours = await _dbContext.Tours
            .Include(x => x.MediaItems)
            .Where(x => definitionCodes.Contains(x.TourCode))
            .ToListAsync(cancellationToken);

        var existingTourByCode = existingTours.ToDictionary(x => x.TourCode, StringComparer.OrdinalIgnoreCase);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTime.UtcNow;
        var toursToAdd = new List<Tour>();

        foreach (var definition in definitions)
        {
            if (!categoryIds.TryGetValue(definition.CategoryCode, out var categoryId)
                || !destinations.TryGetValue(definition.StartDestinationCode, out var startDestination)
                || !destinations.TryGetValue(definition.EndDestinationCode, out var endDestination))
            {
                continue;
            }

            if (existingTourByCode.TryGetValue(definition.TourCode, out var existingTour))
            {
                SyncSeedTourImages(existingTour, definition, now);
                continue;
            }

            var firstSchedule = definition.Schedules.OrderBy(x => x.DepartureOffsetDays).First();
            var lowestPrice = definition.Schedules.Min(x => x.AdultPrice);
            var firstRemainingSeats = Math.Max(definition.TotalSeats - firstSchedule.ReservedSeats, 0);

            var tour = new Tour
            {
                TourCode = definition.TourCode,
                TourName = definition.TourName,
                Slug = BuildSlug($"{definition.TourName}-{definition.TourCode}"),
                CategoryId = categoryId,
                StartDestinationId = startDestination.DestinationId,
                EndDestinationId = endDestination.DestinationId,
                MainImageUrl = definition.ImageUrls[0],
                ShortDescription = definition.ShortDescription,
                Description = definition.Description,
                DurationDays = definition.DurationDays,
                DurationNights = definition.DurationNights,
                MinGroupSize = 2,
                MaxGroupSize = definition.TotalSeats,
                TotalSeats = definition.TotalSeats,
                RemainingSeats = firstRemainingSeats,
                BasePrice = lowestPrice,
                ChildPrice = Math.Round(lowestPrice * 0.5m, 0, MidpointRounding.AwayFromZero),
                SingleSupplement = definition.SingleSupplement,
                CurrencyCode = "VND",
                DeparturePoint = startDestination.DestinationName,
                ReturnPoint = endDestination.DestinationName,
                PickupIncluded = true,
                IsFeatured = definition.IsFeatured,
                IsPublished = true,
                ApprovalStatus = 1,
                SeoTitle = definition.TourName,
                SeoDescription = definition.ShortDescription,
                CreatedAt = now
            };

            for (var imageIndex = 0; imageIndex < definition.ImageUrls.Count; imageIndex++)
            {
                tour.MediaItems.Add(new TourMedia
                {
                    MediaType = 1,
                    MediaUrl = definition.ImageUrls[imageIndex],
                    Caption = $"{definition.TourName} - ảnh {imageIndex + 1}",
                    DisplayOrder = imageIndex + 1,
                    IsPrimary = imageIndex == 0
                });
            }

            for (var scheduleIndex = 0; scheduleIndex < definition.Schedules.Count; scheduleIndex++)
            {
                var schedule = definition.Schedules[scheduleIndex];
                var departureDate = today.AddDays(schedule.DepartureOffsetDays);
                var returnDate = departureDate.AddDays(Math.Max(definition.DurationDays - 1, 0));
                var availableSeats = Math.Max(definition.TotalSeats - schedule.ReservedSeats, 0);

                tour.Schedules.Add(new TourSchedule
                {
                    ScheduleCode = $"SCH{definition.TourCode[5..]}{scheduleIndex + 1:D2}",
                    DepartureDate = departureDate,
                    ReturnDate = returnDate,
                    TotalSeats = definition.TotalSeats,
                    AvailableSeats = availableSeats,
                    ReservedSeats = Math.Max(schedule.ReservedSeats, 0),
                    AdultPrice = schedule.AdultPrice,
                    ChildPrice = Math.Round(schedule.AdultPrice * 0.5m, 0, MidpointRounding.AwayFromZero),
                    InfantPrice = 0m,
                    SingleSupplement = definition.SingleSupplement,
                    Status = 1,
                    CreatedAt = now
                });
            }

            foreach (var itinerary in BuildSampleItineraries(definition, startDestination.DestinationName, endDestination.DestinationName))
            {
                tour.ItineraryDays.Add(itinerary);
            }

            toursToAdd.Add(tour);
        }

        if (toursToAdd.Count > 0)
        {
            _dbContext.Tours.AddRange(toursToAdd);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void SyncSeedTourImages(Tour tour, SampleTourDefinition definition, DateTime now)
    {
        if (definition.ImageUrls.Count == 0)
        {
            return;
        }

        tour.MainImageUrl = definition.ImageUrls[0];
        tour.UpdatedAt = now;

        var existingMedia = tour.MediaItems
            .OrderBy(x => x.DisplayOrder)
            .ToList();

        for (var imageIndex = 0; imageIndex < definition.ImageUrls.Count; imageIndex++)
        {
            if (imageIndex < existingMedia.Count)
            {
                existingMedia[imageIndex].MediaUrl = definition.ImageUrls[imageIndex];
                existingMedia[imageIndex].Caption = $"{definition.TourName} - ảnh {imageIndex + 1}";
                existingMedia[imageIndex].DisplayOrder = imageIndex + 1;
                existingMedia[imageIndex].IsPrimary = imageIndex == 0;
            }
            else
            {
                tour.MediaItems.Add(new TourMedia
                {
                    MediaType = 1,
                    MediaUrl = definition.ImageUrls[imageIndex],
                    Caption = $"{definition.TourName} - ảnh {imageIndex + 1}",
                    DisplayOrder = imageIndex + 1,
                    IsPrimary = imageIndex == 0
                });
            }
        }

        for (var index = existingMedia.Count - 1; index >= definition.ImageUrls.Count; index--)
        {
            tour.MediaItems.Remove(existingMedia[index]);
        }
    }

    private static List<TourItineraryDay> BuildSampleItineraries(
        SampleTourDefinition definition,
        string startDestinationName,
        string endDestinationName)
    {
        var itineraries = new List<TourItineraryDay>();

        for (var day = 1; day <= definition.DurationDays; day++)
        {
            var isFirstDay = day == 1;
            var isLastDay = day == definition.DurationDays;

            var title = isFirstDay
                ? $"{startDestinationName} - {endDestinationName}"
                : isLastDay
                    ? $"Tạm biệt {endDestinationName}"
                    : $"Khám phá {endDestinationName} ngày {day}";

            var summary = isFirstDay
                ? $"Khởi hành tới {endDestinationName}, bắt đầu hành trình {definition.TourName.ToLowerInvariant()}."
                : isLastDay
                    ? "Tự do buổi sáng, trả phòng và kết thúc chương trình."
                    : $"Tham quan các điểm nổi bật, ẩm thực địa phương và hoạt động đặc trưng tại {endDestinationName}.";

            var description = isFirstDay
                ? $"Đoàn tập trung tại {startDestinationName}, di chuyển tới {endDestinationName}. Sau khi nhận phòng, khách có thời gian nghỉ ngơi và làm quen với nhịp sống địa phương."
                : isLastDay
                    ? $"Buổi sáng khách tự do mua sắm đặc sản. Xe đưa đoàn ra điểm khởi hành để trở về, kết thúc hành trình {definition.TourName.ToLowerInvariant()}."
                    : $"Ngày {day} tập trung trải nghiệm các điểm tham quan nổi bật của tour, kết hợp thời gian chụp ảnh, thưởng thức món địa phương và nghỉ dưỡng đúng nhịp độ của chương trình.";

            itineraries.Add(new TourItineraryDay
            {
                DayNumber = day,
                Title = title,
                Summary = summary,
                Description = description,
                OvernightStay = isLastDay ? null : endDestinationName,
                BreakfastIncluded = day > 1,
                LunchIncluded = !isLastDay,
                DinnerIncluded = !isLastDay,
                HotelName = isLastDay ? null : "Khách sạn tiêu chuẩn 4 sao",
                TransportationName = isFirstDay || isLastDay ? "Máy bay + xe du lịch" : "Xe du lịch"
            });
        }

        return itineraries;
    }

    private static List<SampleTourDefinition> GetSampleTourDefinitions()
    {
        const string mountainLake = "https://images.pexels.com/photos/15692362/pexels-photo-15692362.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string cableCar = "https://images.pexels.com/photos/30989777/pexels-photo-30989777.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string daNangBeach = "https://images.pexels.com/photos/32715907/pexels-photo-32715907.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string phuQuocBeach = "https://images.pexels.com/photos/28254353/pexels-photo-28254353.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string haGiangMountain = "https://images.pexels.com/photos/15798431/pexels-photo-15798431.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string bangkokTemple = "https://images.pexels.com/photos/14963594/pexels-photo-14963594.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string singaporeBridge = "https://images.pexels.com/photos/29917748/pexels-photo-29917748.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string singaporeBay = "https://images.pexels.com/photos/20768136/pexels-photo-20768136.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string hoiAnLantern = "https://images.pexels.com/photos/30091117/pexels-photo-30091117.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string haLongBay = "https://images.pexels.com/photos/18304975/pexels-photo-18304975.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string coupleBeachSunset = "https://images.pexels.com/photos/1024960/pexels-photo-1024960.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string coupleCafeHill = "https://images.pexels.com/photos/2387873/pexels-photo-2387873.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string coupleCityNight = "https://images.pexels.com/photos/169647/pexels-photo-169647.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string soloBackpack = "https://images.pexels.com/photos/346885/pexels-photo-346885.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string soloRoadTrip = "https://images.pexels.com/photos/21014/pexels-photo.jpg?auto=compress&cs=tinysrgb&w=1600";
        const string soloTempleWalk = "https://images.pexels.com/photos/3538245/pexels-photo-3538245.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string soloTrainView = "https://images.pexels.com/photos/417074/pexels-photo-417074.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string islandPier = "https://images.pexels.com/photos/753626/pexels-photo-753626.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string forestCabin = "https://images.pexels.com/photos/803975/pexels-photo-803975.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string oldTownStreet = "https://images.pexels.com/photos/2087391/pexels-photo-2087391.jpeg?auto=compress&cs=tinysrgb&w=1600";
        const string rooftopDinner = "https://images.pexels.com/photos/67468/pexels-photo-67468.jpeg?auto=compress&cs=tinysrgb&w=1600";

        var definitions = new List<SampleTourDefinition>
        {
            new("TSEED001", "CAT0001", "DES0001", "DES0003", "Nghỉ dưỡng Đà Nẵng - Hội An - Bà Nà Hills", "Kỳ nghỉ biển kết hợp phố cổ và lịch trình nhẹ.", "Hành trình nghỉ dưỡng đưa khách từ Hà Nội tới Đà Nẵng, ghé biển Mỹ Khê, Bà Nà Hills và phố cổ Hội An.", 4, 3, 20, 1_200_000m, true, [daNangBeach, hoiAnLantern, mountainLake], [new(2, 5_200_000m), new(16, 6_100_000m), new(42, 5_800_000m)]),
            new("TSEED002", "CAT0001", "DES0008", "DES0007", "Phú Quốc Sunset Town và Grand World", "Combo nghỉ dưỡng đảo ngọc với resort và nhiều khoảng nghỉ tự do.", "Tour kết hợp biển đẹp, resort tiêu chuẩn cao và các điểm check-in nổi bật như Sunset Town, Grand World, bãi Sao.", 4, 3, 20, 1_500_000m, true, [phuQuocBeach, cableCar, mountainLake], [new(4, 6_200_000m), new(18, 7_100_000m), new(46, 6_800_000m)]),
            new("TSEED003", "CAT0001", "DES0008", "DES0005", "Nha Trang nghỉ dưỡng biển xanh và đảo ngọc", "Chuyến biển dễ đi, nhiều thời gian nghỉ và chụp ảnh.", "Lịch trình tập trung vào trải nghiệm nghỉ dưỡng tại Nha Trang, tắm biển, đảo ngọc và vui chơi giải trí nhẹ.", 4, 3, 20, 1_000_000m, false, [daNangBeach, haLongBay, cableCar], [new(1, 4_900_000m), new(20, 5_600_000m), new(35, 5_200_000m)]),
            new("TSEED004", "CAT0001", "DES0008", "DES0006", "Đà Lạt săn mây và nghỉ dưỡng cao nguyên", "Tour nhẹ nhàng cho khách yêu không khí lạnh và nhiều góc check-in.", "Lộ trình đưa khách chạm vào vẻ dịu dàng của Đà Lạt với hồ Tuyền Lâm, đồi chè, săn mây và những góc cafe đặc trưng.", 3, 2, 18, 900_000m, false, [mountainLake, haGiangMountain, cableCar], [new(5, 4_600_000m), new(24, 5_100_000m), new(50, 4_800_000m)]),

            new("TSEED005", "CAT0002", "DES0001", "DES0002", "Hạ Long - du thuyền vịnh xanh 5 sao", "Tour biển đảo miền Bắc với trải nghiệm nghỉ đêm cao cấp.", "Chương trình dành cho khách muốn nghỉ ngơi trên du thuyền, ngắm cảnh vịnh, tham quan hang động và chèo kayak.", 3, 2, 20, 1_300_000m, true, [haLongBay, mountainLake, daNangBeach], [new(6, 5_500_000m), new(19, 6_400_000m), new(41, 6_000_000m)]),
            new("TSEED006", "CAT0002", "DES0008", "DES0007", "Phú Quốc 3 đảo - cáp treo Hòn Thơm", "Chuyến đi biển đảo năng động cho khách thích vui chơi và lặn biển.", "Tour biển đảo nổi bật với hành trình 3 đảo, cáp treo Hòn Thơm, cano và các hoạt động ngoài trời.", 4, 3, 20, 1_600_000m, true, [phuQuocBeach, cableCar, daNangBeach], [new(7, 6_800_000m), new(22, 7_500_000m), new(47, 7_200_000m)]),
            new("TSEED007", "CAT0002", "DES0008", "DES0005", "Nha Trang - VinWonders - Hòn Mun", "Lịch trình cân bằng giữa vui chơi và nghỉ dưỡng.", "Tour đưa khách đến thiên đường biển Nha Trang với hoạt động vui chơi tại VinWonders, Hòn Mun và chợ đêm biển.", 4, 3, 20, 1_100_000m, false, [daNangBeach, cableCar, phuQuocBeach], [new(11, 5_900_000m), new(27, 6_400_000m), new(52, 6_100_000m)]),
            new("TSEED008", "CAT0002", "DES0001", "DES0004", "Đà Nẵng - biển Mỹ Khê - phố cổ Hội An", "Chuyến đi ngắn ngày, nhiều ảnh đẹp, hợp đi nghỉ cuối tuần.", "Hành trình kết hợp biển xanh và phố cổ, nhấn mạnh trải nghiệm nghỉ dưỡng, ẩm thực miền Trung và đêm đèn lồng Hội An.", 4, 3, 18, 1_200_000m, false, [daNangBeach, hoiAnLantern, mountainLake], [new(14, 5_000_000m), new(30, 5_700_000m), new(57, 5_400_000m)]),

            new("TSEED009", "CAT0003", "DES0008", "DES0002", "Hà Nội - Hạ Long - hành trình kỳ quan miền Bắc", "Tour khám phá miền Bắc với điểm nhấn cảnh quan và văn hóa.", "Hành trình đưa khách ghé Hà Nội, tham quan Hạ Long và thưởng thức nhịp sống miền Bắc qua những điểm dừng quen thuộc.", 4, 3, 20, 1_100_000m, false, [haLongBay, mountainLake, hoiAnLantern], [new(8, 4_800_000m), new(21, 5_300_000m), new(45, 5_000_000m)]),
            new("TSEED010", "CAT0003", "DES0008", "DES0006", "Đà Lạt - rừng thông - hồ Tuyền Lâm - săn mây", "Chuyến khám phá nhiều thiên nhiên và góc ảnh đẹp.", "Tour tập trung vào các điểm thiên nhiên và trải nghiệm địa phương đặc trưng của Đà Lạt như săn mây, cafe rừng, đồi chè.", 4, 3, 18, 800_000m, false, [haGiangMountain, mountainLake, cableCar], [new(9, 4_700_000m), new(28, 5_200_000m), new(49, 5_000_000m)]),
            new("TSEED011", "CAT0003", "DES0008", "DES0009", "Khám phá Bangkok - chùa Vàng - du thuyền Chao Phraya", "Tour quốc tế dễ đi, giá tốt, tập trung điểm biểu tượng của Bangkok.", "Hành trình khám phá Bangkok với chùa Vàng, chợ đêm, IconSiam và bữa tối trên du thuyền sông Chao Phraya.", 5, 4, 20, 1_800_000m, true, [bangkokTemple, singaporeBridge, singaporeBay], [new(10, 9_900_000m), new(26, 11_200_000m), new(53, 10_500_000m)]),
            new("TSEED012", "CAT0003", "DES0008", "DES0010", "Singapore city tour - Marina Bay - Sentosa", "Tour khám phá nhịp đô thị hiện đại và nhiều điểm check-in quen thuộc.", "Hành trình đưa khách chạm vào Singapore hiện đại với Merlion, Marina Bay, Garden by the Bay và đảo Sentosa.", 5, 4, 20, 2_200_000m, true, [singaporeBridge, singaporeBay, mountainLake], [new(12, 12_500_000m), new(31, 13_800_000m), new(58, 13_200_000m)]),

            new("TSEED013", "CAT0004", "DES0001", "DES0003", "Gia đình vui chơi Đà Nẵng - Hội An - Sun World", "Thiết kế cho gia đình có trẻ nhỏ với nhịp độ vừa phải.", "Chương trình tối ưu cho gia đình, xen kẽ nghỉ dưỡng, vui chơi ở Sun World và thời gian tự do bên biển.", 4, 3, 20, 1_300_000m, true, [daNangBeach, hoiAnLantern, cableCar], [new(13, 5_400_000m), new(32, 6_000_000m), new(61, 5_700_000m)]),
            new("TSEED014", "CAT0004", "DES0008", "DES0007", "Phú Quốc gia đình - Safari - Grand World", "Lịch trình rộng rãi, phù hợp nhà có trẻ em.", "Tour gia đình nổi bật tại Phú Quốc với Safari, Grand World và nhiều khoảng nghỉ linh hoạt.", 4, 3, 20, 1_600_000m, true, [phuQuocBeach, cableCar, singaporeBay], [new(15, 6_600_000m), new(34, 7_300_000m), new(62, 7_000_000m)]),
            new("TSEED015", "CAT0004", "DES0008", "DES0010", "Singapore gia đình - Universal - Gardens by the Bay", "Tour quốc tế gia đình với các điểm vui chơi lớn và lịch trình không quá dồn.", "Chương trình kết hợp các công viên giải trí và điểm tham quan hiện đại của Singapore, tối ưu cho gia đình có trẻ em.", 5, 4, 20, 2_400_000m, true, [singaporeBay, singaporeBridge, mountainLake], [new(16, 13_200_000m), new(36, 14_400_000m), new(64, 13_900_000m)]),
            new("TSEED016", "CAT0004", "DES0008", "DES0009", "Bangkok gia đình - Safari World - IconSiam", "Chuyến đi nước ngoài dễ tiếp cận cho gia đình lần đầu du lịch Thái Lan.", "Tour thiết kế cho gia đình với nhiều điểm trải nghiệm nhẹ, ăn uống dễ hợp khẩu vị và lịch trình dễ theo.", 5, 4, 20, 1_900_000m, true, [bangkokTemple, singaporeBridge, singaporeBay], [new(17, 10_600_000m), new(38, 11_800_000m), new(66, 11_200_000m)]),

            new("TSEED017", "CAT0005", "DES0008", "DES0009", "Bangkok shopping và ẩm thực đêm", "Tour quốc tế giá tốt tập trung mua sắm và chợ đêm nổi tiếng.", "Hành trình phù hợp nhóm bạn hoặc khách trẻ muốn khám phá Bangkok qua góc nhìn mua sắm, ẩm thực và các điểm check-in hiện đại.", 4, 3, 20, 1_700_000m, false, [bangkokTemple, singaporeBridge, singaporeBay], [new(18, 8_800_000m), new(39, 9_500_000m), new(68, 9_000_000m)]),
            new("TSEED018", "CAT0005", "DES0008", "DES0010", "Singapore cao cấp - Marina Bay Sands - Jewel", "Tour quốc tế tiêu chuẩn cao, ưu tiên trải nghiệm đẹp và nhịp độ vừa phải.", "Chương trình khai thác những điểm sang trọng, hiện đại và đáng check-in nhất của Singapore như Marina Bay Sands, Jewel và Clarke Quay.", 4, 3, 18, 2_800_000m, true, [singaporeBridge, singaporeBay, mountainLake], [new(23, 14_900_000m), new(43, 16_200_000m), new(71, 15_600_000m)]),
            new("TSEED019", "CAT0005", "DES0008", "DES0010", "Singapore - Bangkok liên tuyến Đông Nam Á", "Tour ghép hai thành phố lớn trong một chuyến đi.", "Hành trình liên tuyến kết hợp Singapore hiện đại và Bangkok sôi động, phù hợp khách thích trải nghiệm đa dạng trong thời gian ngắn.", 6, 5, 20, 2_900_000m, true, [singaporeBay, singaporeBridge, bangkokTemple], [new(25, 15_900_000m), new(48, 17_200_000m), new(73, 16_500_000m)]),
            new("TSEED020", "CAT0005", "DES0001", "DES0010", "Hà Nội - Singapore nghỉ lễ cao cấp", "Gói nghỉ lễ dành cho khách miền Bắc muốn đi quốc tế với lịch trình gọn.", "Tour nghỉ lễ với nhịp độ vừa phải, khách sạn trung tâm và điểm tham quan nổi bật tại Singapore, thuận tiện cho khách khởi hành từ Hà Nội.", 4, 3, 18, 2_600_000m, true, [singaporeBridge, singaporeBay, mountainLake], [new(27, 13_800_000m), new(51, 15_000_000m), new(76, 14_300_000m)])
            ,
            new("TSEED021", "tour-cap-doi", "DES0008", "DES0007", "Phú Quốc hoàng hôn biển riêng cho cặp đôi", "Kỳ nghỉ lãng mạn với sunset town, bãi biển đẹp và thời gian riêng tư nhiều hơn.", "Tour thiết kế cho cặp đôi muốn thư giãn nhẹ, ngắm hoàng hôn, ăn tối lãng mạn và tận hưởng không gian biển đảo riêng tư tại Phú Quốc.", 4, 3, 16, 1_800_000m, true, [coupleBeachSunset, islandPier, rooftopDinner], [new(3, 7_400_000m), new(17, 8_100_000m), new(44, 7_800_000m)]),
            new("TSEED022", "tour-cap-doi", "DES0008", "DES0006", "Đà Lạt nghỉ dưỡng đôi - săn mây - cafe rừng", "Không khí cao nguyên, lịch trình chậm và nhiều điểm hẹn hò đẹp mắt.", "Hành trình dành cho hai người với đồi thông, săn mây, hồ Tuyền Lâm, cafe rừng và các khoảng nghỉ riêng tư phù hợp cho chuyến đi cặp đôi.", 4, 3, 14, 1_200_000m, true, [coupleCafeHill, forestCabin, mountainLake], [new(6, 5_600_000m), new(20, 6_100_000m), new(47, 5_900_000m)]),
            new("TSEED023", "tour-cap-doi", "DES0001", "DES0004", "Đà Nẵng - Hội An đèn lồng và dinner bên sông", "Chuyến đi ngắn ngày nhiều khoảnh khắc đẹp cho cặp đôi thích biển và phố cổ.", "Tour kết hợp biển Mỹ Khê, phố cổ Hội An, đêm đèn lồng, bữa tối bên sông và thời gian tự do để cặp đôi tận hưởng chuyến đi theo nhịp riêng.", 4, 3, 16, 1_400_000m, true, [oldTownStreet, hoiAnLantern, daNangBeach], [new(8, 6_300_000m), new(24, 6_900_000m), new(55, 6_500_000m)]),
            new("TSEED024", "tour-cap-doi", "DES0008", "DES0010", "Singapore city romance - Marina Bay về đêm", "Hành trình hiện đại với skyline đẹp, rooftop và nhiều điểm check-in cho hai người.", "Tour cặp đôi tại Singapore tập trung vào các điểm ngắm cảnh đêm, Marina Bay, Garden by the Bay, Jewel và bữa tối phong cách thành thị hiện đại.", 5, 4, 14, 2_600_000m, true, [coupleCityNight, singaporeBay, singaporeBridge], [new(11, 12_900_000m), new(29, 13_600_000m), new(60, 13_200_000m)]),
            new("TSEED025", "tour-tu-do", "DES0008", "DES0006", "Đà Lạt tự do khám phá - homestay - xe riêng", "Linh hoạt thời gian, nhiều khoảng trống để tự đi và tự chọn trải nghiệm.", "Tour tự do cho khách thích chủ động lịch trình tại Đà Lạt, kết hợp các điểm chính cùng nhiều khung giờ riêng để tự khám phá quán cafe, chợ đêm và các cung đường đẹp.", 3, 2, 18, 850_000m, false, [soloBackpack, coupleCafeHill, forestCabin], [new(4, 4_500_000m), new(18, 4_900_000m), new(42, 4_700_000m)]),
            new("TSEED026", "tour-tu-do", "DES0008", "DES0009", "Bangkok tự do mua sắm - chợ đêm - city walk", "Phù hợp khách thích tự đi, tự ăn uống và ghé các khu mua sắm nổi tiếng.", "Chuyến đi thiên về trải nghiệm cá nhân, giữ các điểm chính của Bangkok và dành nhiều thời gian rảnh cho khách tự do city walk, shopping và khám phá ẩm thực.", 4, 3, 18, 1_500_000m, false, [soloTempleWalk, bangkokTemple, rooftopDinner], [new(9, 8_600_000m), new(26, 9_200_000m), new(48, 8_900_000m)]),
            new("TSEED027", "tour-tu-do", "DES0001", "DES0002", "Hạ Long tự do nghỉ đêm du thuyền và khám phá vịnh", "Nhịp độ nhẹ, nhiều thời gian ngắm cảnh và tận hưởng không gian riêng.", "Tour tự do Hạ Long phù hợp khách muốn nghỉ dưỡng trên vịnh nhưng vẫn có đủ khoảng trống để tự thư giãn, chụp ảnh và tham gia hoạt động theo sở thích.", 3, 2, 18, 1_250_000m, false, [soloTrainView, haLongBay, islandPier], [new(5, 5_300_000m), new(22, 5_900_000m), new(46, 5_600_000m)]),
            new("TSEED028", "tour-tu-do", "DES0008", "DES0007", "Phú Quốc tự do biển xanh - grand world - sunset", "Tour mở với nhiều giờ tự do cho khách thích nghỉ biển và đi chơi linh hoạt.", "Hành trình Phú Quốc xây theo phong cách tự do, chỉ giữ các điểm chính và để khách chủ động thời gian nghỉ biển, khám phá quán đẹp, Sunset Town và Grand World.", 4, 3, 18, 1_350_000m, false, [soloRoadTrip, phuQuocBeach, cableCar], [new(7, 6_100_000m), new(23, 6_700_000m), new(52, 6_400_000m)])
        };

        return definitions
            .Select(definition => definition with
            {
                ImageUrls = BuildUniqueSeedImageSet(definition.TourCode, definition.TourName)
            })
            .ToList();
    }

    private static IReadOnlyList<string> BuildUniqueSeedImageSet(string tourCode, string tourName)
    {
        var slug = BuildSlug(tourName);
        return
        [
            $"https://picsum.photos/seed/{tourCode.ToLowerInvariant()}-{slug}-1/1600/900",
            $"https://picsum.photos/seed/{tourCode.ToLowerInvariant()}-{slug}-2/1600/900",
            $"https://picsum.photos/seed/{tourCode.ToLowerInvariant()}-{slug}-3/1600/900"
        ];
    }

    private sealed record SeedDestinationRef(int DestinationId, string DestinationName);

    private sealed record SampleTourDefinition(
        string TourCode,
        string CategoryCode,
        string StartDestinationCode,
        string EndDestinationCode,
        string TourName,
        string ShortDescription,
        string Description,
        int DurationDays,
        int DurationNights,
        int TotalSeats,
        decimal SingleSupplement,
        bool IsFeatured,
        IReadOnlyList<string> ImageUrls,
        IReadOnlyList<SampleScheduleDefinition> Schedules);

    private sealed record SampleScheduleDefinition(
        int DepartureOffsetDays,
        decimal AdultPrice,
        int ReservedSeats = 2);
}
