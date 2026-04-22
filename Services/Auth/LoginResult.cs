using ChillTour.Data.Entities;

namespace ChillTour.Services.Auth;

public class LoginResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public User? User { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
}
