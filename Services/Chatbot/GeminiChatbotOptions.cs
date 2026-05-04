namespace ChillTour.Services.Chatbot;

public class GeminiChatbotOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string Model { get; set; } = "gemini-2.5-flash";
    public int MaxTours { get; set; } = 8;
}
