namespace ChillTour.Services.Mail;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
