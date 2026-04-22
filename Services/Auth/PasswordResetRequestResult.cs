namespace ChillTour.Services.Auth;

public sealed class PasswordResetRequestResult
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
}
