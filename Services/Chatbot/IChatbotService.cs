namespace ChillTour.Services.Chatbot;

public interface IChatbotService
{
    Task<string> AskAsync(string message, CancellationToken cancellationToken = default);
}
