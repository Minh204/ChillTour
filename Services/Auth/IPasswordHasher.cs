namespace ChillTour.Services.Auth;

public interface IPasswordHasher
{
    string HashPassword(string password, out string salt);
    bool VerifyPassword(string password, string passwordHash, string? salt);
}
