using ChillTour.Models.Account;

namespace ChillTour.Services.Auth;

public interface IAuthService
{
    Task<(bool Succeeded, string? Error)> RegisterAsync(RegisterViewModel model, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(LoginViewModel model, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginWithExternalProviderAsync(ExternalLoginRequest request, CancellationToken cancellationToken = default);
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, string? requesterIp, string resetPasswordUrlTemplate, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? Error)> ResetPasswordAsync(ResetPasswordViewModel model, CancellationToken cancellationToken = default);
    Task<bool> IsPasswordResetTokenValidAsync(string email, string token, CancellationToken cancellationToken = default);
}
