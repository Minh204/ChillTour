using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChillTour.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChillTour.Services.Chatbot;

public class GeminiChatbotService : IChatbotService
{
    private readonly ChillTourDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly GeminiChatbotOptions _options;
    private readonly ILogger<GeminiChatbotService> _logger;

    public GeminiChatbotService(
        ChillTourDbContext dbContext,
        HttpClient httpClient,
        IOptions<GeminiChatbotOptions> options,
        ILogger<GeminiChatbotService> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> AskAsync(string message, CancellationToken cancellationToken = default)
    {
        var trimmedMessage = message.Trim();
        if (string.IsNullOrWhiteSpace(trimmedMessage))
        {
            return "Bạn cần tư vấn tour đi đâu, ngày nào, khoảng ngân sách bao nhiêu và đi mấy người?";
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return "Chatbot AI chưa được cấu hình API key Gemini. Bạn vẫn có thể cho tôi biết điểm đến, ngày đi, số người và ngân sách để nhân viên ChillTour tư vấn tiếp.";
        }

        var dataContext = await BuildTourContextAsync(cancellationToken);
        var request = new GeminiGenerateContentRequest
        {
            SystemInstruction = new GeminiContent
            {
                Parts = [new GeminiPart { Text = BuildSystemPrompt(dataContext) }]
            },
            Contents =
            [
                new GeminiContent
                {
                    Role = "user",
                    Parts = [new GeminiPart { Text = trimmedMessage }]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = 0.35m,
                MaxOutputTokens = 700
            }
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildGenerateContentEndpoint());
            httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Gemini chatbot request failed. StatusCode: {StatusCode}. Model: {Model}. Body: {Body}",
                    (int)response.StatusCode,
                    _options.Model,
                    responseContent);

                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                {
                    return "Gemini chưa chấp nhận API key hiện tại. Vui lòng kiểm tra lại key hoặc tạo key mới, sau đó thử lại.";
                }

                return await BuildLocalFallbackAsync(trimmedMessage, cancellationToken);
            }

            var completion = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseContent);
            var answer = completion?.Candidates
                .SelectMany(x => x.Content?.Parts ?? [])
                .Select(x => x.Text)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?.Trim();

            return !string.IsNullOrWhiteSpace(answer)
                ? answer
                : "Tôi chưa có đủ dữ liệu để tư vấn chính xác. Bạn cho tôi biết điểm đến, ngày đi, số người và ngân sách dự kiến nhé.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini chatbot request threw an exception.");
            return await BuildLocalFallbackAsync(trimmedMessage, cancellationToken);
        }
    }

    private async Task<string> BuildTourContextAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var maxTours = Math.Clamp(_options.MaxTours, 3, 15);

        var tours = await _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .Where(x => x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.AvailableSeats > 0))
            .Include(x => x.Category)
            .Include(x => x.StartDestination)
            .Include(x => x.EndDestination)
            .Include(x => x.Schedules)
            .OrderByDescending(x => x.IsFeatured)
            .ThenBy(x => x.Schedules.Where(s => s.Status == 1 && s.DepartureDate >= today && s.AvailableSeats > 0).Min(s => s.AdultPrice))
            .Take(maxTours)
            .Select(x => new
            {
                x.TourName,
                x.Slug,
                Category = x.Category.CategoryName,
                Route = x.StartDestination.DestinationName + " -> " + x.EndDestination.DestinationName,
                x.DurationDays,
                x.DurationNights,
                NextSchedule = x.Schedules
                    .Where(s => s.Status == 1 && s.DepartureDate >= today && s.AvailableSeats > 0)
                    .OrderBy(s => s.DepartureDate)
                    .Select(s => new
                    {
                        s.DepartureDate,
                        s.AdultPrice,
                        s.AvailableSeats,
                        s.TotalSeats
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var promotions = await _dbContext.Promotions
            .AsNoTracking()
            .Where(x => x.IsActive && x.StartAt <= DateTime.UtcNow && x.EndAt >= DateTime.UtcNow)
            .OrderBy(x => x.EndAt)
            .Take(5)
            .Select(x => new
            {
                x.PromotionCode,
                x.PromotionName,
                x.DiscountPercent,
                x.DiscountAmount,
                x.MinOrderValue,
                x.EndAt
            })
            .ToListAsync(cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("DỮ LIỆU TOUR ĐANG MỞ BÁN:");
        if (tours.Count == 0)
        {
            builder.AppendLine("- Chưa có tour đang mở bán trong dữ liệu hệ thống.");
        }
        else
        {
            foreach (var tour in tours)
            {
                var schedule = tour.NextSchedule;
                if (schedule is null)
                {
                    continue;
                }

                builder.AppendLine($"- {tour.TourName} | {tour.Category} | {tour.Route} | {tour.DurationDays} ngày {tour.DurationNights} đêm | Khởi hành {schedule.DepartureDate:dd/MM/yyyy} | Giá từ {schedule.AdultPrice:N0} đ | Còn {schedule.AvailableSeats}/{schedule.TotalSeats} chỗ | Link: /tours/{tour.Slug}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("ƯU ĐÃI HIỆN CÓ:");
        if (promotions.Count == 0)
        {
            builder.AppendLine("- Chưa có mã ưu đãi đang hiệu lực.");
        }
        else
        {
            foreach (var promotion in promotions)
            {
                var discount = promotion.DiscountPercent > 0
                    ? $"{promotion.DiscountPercent:0.#}%"
                    : $"{promotion.DiscountAmount:N0} đ";
                builder.AppendLine($"- {promotion.PromotionCode}: {promotion.PromotionName}, giảm {discount}, đơn tối thiểu {promotion.MinOrderValue:N0} đ, hết hạn {promotion.EndAt:dd/MM/yyyy}.");
            }
        }

        return builder.ToString();
    }

    private async Task<string> BuildLocalFallbackAsync(string message, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tours = await _dbContext.Tours
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .Where(x => x.Schedules.Any(s => s.Status == 1 && s.DepartureDate >= today && s.AvailableSeats > 0))
            .Include(x => x.StartDestination)
            .Include(x => x.EndDestination)
            .Include(x => x.Schedules)
            .Select(x => new
            {
                x.TourName,
                x.Slug,
                Route = x.StartDestination.DestinationName + " -> " + x.EndDestination.DestinationName,
                x.DurationDays,
                x.DurationNights,
                Schedule = x.Schedules
                    .Where(s => s.Status == 1 && s.DepartureDate >= today && s.AvailableSeats > 0)
                    .OrderBy(s => s.AdultPrice)
                    .ThenBy(s => s.DepartureDate)
                    .Select(s => new
                    {
                        s.DepartureDate,
                        s.AdultPrice,
                        s.AvailableSeats
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var availableTours = tours
            .Where(x => x.Schedule is not null)
            .OrderBy(x => x.Schedule!.AdultPrice)
            .Take(3)
            .ToList();

        if (availableTours.Count == 0)
        {
            return "Hiện Gemini chưa phản hồi được và hệ thống chưa có tour còn chỗ để gợi ý. Bạn vui lòng thử lại sau hoặc liên hệ ChillTour qua Zalo.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Gemini hiện chưa phản hồi được, tôi tạm gợi ý theo dữ liệu tour đang mở bán của ChillTour:");

        foreach (var tour in availableTours)
        {
            var schedule = tour.Schedule!;
            builder.AppendLine($"- {tour.TourName}: {tour.Route}, {tour.DurationDays} ngày {tour.DurationNights} đêm, khởi hành {schedule.DepartureDate:dd/MM/yyyy}, giá từ {schedule.AdultPrice:N0} đ, còn {schedule.AvailableSeats} chỗ. Link: /tours/{tour.Slug}");
        }

        if (!message.Contains("rẻ", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("giá", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("Bạn cho tôi thêm điểm đến, ngày đi, số người và ngân sách để lọc sát nhu cầu hơn.");
        }

        return builder.ToString().Trim();
    }

    private string BuildSystemPrompt(string dataContext)
    {
        return $"""
Bạn là chatbot tư vấn bán tour của website ChillTour.

Nhiệm vụ:
- Tư vấn tour du lịch cho khách hàng.
- Gợi ý tour phù hợp dựa trên điểm đến, ngân sách, số người, ngày đi.
- Trả lời thông tin lịch trình, giá tour, đặt cọc, thanh toán, hủy tour dựa trên dữ liệu hệ thống.
- Không tự bịa thông tin nếu dữ liệu không có.
- Nếu khách muốn đặt tour, hướng dẫn khách bấm nút đặt tour hoặc mở link tour phù hợp.

Nguyên tắc:
- Trả lời thân thiện, ngắn gọn, dễ hiểu bằng tiếng Việt.
- Ưu tiên chốt nhu cầu khách hàng.
- Luôn hỏi thêm nếu thiếu điểm đến, ngày đi, số người hoặc ngân sách.
- Không cam kết còn chỗ nếu chưa dựa trên dữ liệu hệ thống.
- Chính sách chung: có thể thanh toán cọc 30% hoặc toàn bộ; tour giờ chót cần thanh toán toàn bộ; hủy trước 7 ngày hoàn 100% tiền đã thanh toán, sau 7 ngày hoàn 60%.

{dataContext}
""";
    }

    private Uri BuildGenerateContentEndpoint()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : _options.BaseUrl.TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? "gemini-2.5-flash"
            : _options.Model.Trim();

        return new Uri($"{baseUrl}/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
    }

    private sealed class GeminiGenerateContentRequest
    {
        [JsonPropertyName("system_instruction")]
        public GeminiContent? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig GenerationConfig { get; set; } = new();
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public decimal Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    private sealed class GeminiGenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate> Candidates { get; set; } = [];
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }
}
