using ChillTour.Data;
using ChillTour.Data.Entities;
using ChillTour.Models.Account;
using ChillTour.Security;
using ChillTour.Services.Mail;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ChillTour.Services.Auth;

public class AuthService : IAuthService
{
    private readonly ChillTourDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ChillTourDbContext dbContext, IPasswordHasher passwordHasher, IEmailSender emailSender, ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<(bool Succeeded, string? Error)> RegisterAsync(RegisterViewModel model, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = model.Email.Trim().ToUpperInvariant();

        var emailExists = await _dbContext.Users
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return (false, "Email đã tồn tại.");
        }

        var customerRole = await _dbContext.Roles
            .SingleAsync(x => x.RoleCode.ToUpper() == RoleConstants.Customer.ToUpper(), cancellationToken);

        var passwordHash = _passwordHasher.HashPassword(model.Password, out var salt);

        var user = new User
        {
            Email = model.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            FullName = model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            PasswordHash = passwordHash,
            PasswordSalt = salt,
            Status = 1,
            EmailVerified = false,
            PhoneVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = customerRole.RoleId,
            AssignedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<LoginResult> LoginAsync(LoginViewModel model, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = model.Email.Trim().ToUpperInvariant();

        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return new LoginResult { Error = "Email hoặc mật khẩu không đúng." };
        }

        if (user.Status != 1)
        {
            return new LoginResult { Error = "Tài khoản hiện không thể đăng nhập." };
        }

        var isValidPassword = _passwordHasher.VerifyPassword(model.Password, user.PasswordHash, user.PasswordSalt);
        if (!isValidPassword)
        {
            user.FailedLoginCount += 1;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new LoginResult { Error = "Email hoặc mật khẩu không đúng." };
        }

        user.FailedLoginCount = 0;
        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResult
        {
            Succeeded = true,
            User = user,
            Roles = user.UserRoles.Select(x => NormalizeRoleCode(x.Role.RoleCode)).ToArray()
        };
    }

    public async Task<LoginResult> LoginWithExternalProviderAsync(ExternalLoginRequest request, CancellationToken cancellationToken = default)
    {
        var provider = request.Provider.Trim();
        var providerKey = request.ProviderKey.Trim();
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(provider) ||
            string.IsNullOrWhiteSpace(providerKey) ||
            string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new LoginResult { Error = "Không lấy được thông tin tài khoản Google hợp lệ." };
        }

        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.ExternalProvider == provider && x.ExternalProviderKey == providerKey,
                cancellationToken);

        if (user is null)
        {
            user = await _dbContext.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

            if (user is not null &&
                !string.IsNullOrWhiteSpace(user.ExternalProvider) &&
                !string.Equals(user.ExternalProvider, provider, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(user.ExternalProviderKey))
            {
                return new LoginResult
                {
                    Error = "Email này đã liên kết với một phương thức đăng nhập khác. Vui lòng dùng cách đăng nhập đã liên kết trước đó."
                };
            }
        }

        if (user is null)
        {
            var customerRole = await _dbContext.Roles
                .SingleAsync(x => x.RoleCode.ToUpper() == RoleConstants.Customer.ToUpper(), cancellationToken);

            var generatedPassword = $"Google@{Guid.NewGuid():N}";
            var passwordHash = _passwordHasher.HashPassword(generatedPassword, out var salt);

            user = new User
            {
                Email = request.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                FullName = string.IsNullOrWhiteSpace(request.FullName) ? request.Email.Trim() : request.FullName.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim(),
                PasswordHash = passwordHash,
                PasswordSalt = salt,
                ExternalProvider = provider,
                ExternalProviderKey = providerKey,
                Status = 1,
                EmailVerified = true,
                PhoneVerified = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = customerRole.RoleId,
                AssignedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            user = await _dbContext.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .SingleAsync(x => x.UserId == user.UserId, cancellationToken);
        }

        if (user.Status != 1)
        {
            return new LoginResult { Error = "Tài khoản hiện không thể đăng nhập." };
        }

        var hasChanged = false;

        if (!string.Equals(user.ExternalProvider, provider, StringComparison.Ordinal))
        {
            user.ExternalProvider = provider;
            hasChanged = true;
        }

        if (!string.Equals(user.ExternalProviderKey, providerKey, StringComparison.Ordinal))
        {
            user.ExternalProviderKey = providerKey;
            hasChanged = true;
        }

        if (!user.EmailVerified)
        {
            user.EmailVerified = true;
            hasChanged = true;
        }

        var incomingFullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(incomingFullName) &&
            string.IsNullOrWhiteSpace(user.FullName))
        {
            user.FullName = incomingFullName;
            hasChanged = true;
        }

        if (string.IsNullOrWhiteSpace(user.AvatarUrl) &&
            !string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            user.AvatarUrl = request.AvatarUrl.Trim();
            hasChanged = true;
        }

        user.FailedLoginCount = 0;
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        hasChanged = true;

        if (hasChanged)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new LoginResult
        {
            Succeeded = true,
            User = user,
            Roles = user.UserRoles.Select(x => NormalizeRoleCode(x.Role.RoleCode)).ToArray()
        };
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, string? requesterIp, string resetPasswordUrlTemplate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return new PasswordResetRequestResult { Succeeded = true };
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail && x.Status == 1, cancellationToken);

        if (user is null)
        {
            return new PasswordResetRequestResult { Succeeded = true };
        }

        if (!_emailSender.IsConfigured)
        {
            _logger.LogWarning("Password reset requested for {Email} but SMTP is not configured.", user.Email);
            return new PasswordResetRequestResult
            {
                Succeeded = false,
                Error = "Hệ thống chưa cấu hình email gửi lại mật khẩu."
            };
        }

        var activeTokens = await _dbContext.PasswordResetTokens
            .Where(x => x.UserId == user.UserId && x.UsedAt == null && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAt = DateTime.UtcNow;
        }

        var rawToken = GenerateResetToken();
        var tokenHash = ComputeSha256(rawToken);

        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            RequestedIp = string.IsNullOrWhiteSpace(requesterIp) ? null : requesterIp.Trim()
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var resetLink = resetPasswordUrlTemplate.Replace("__TOKEN__", Uri.EscapeDataString(rawToken), StringComparison.Ordinal);
        var subject = "ChillTour - Đặt lại mật khẩu";
        var htmlBody = BuildResetPasswordEmail(user.FullName, resetLink);

        await _emailSender.SendAsync(user.Email, subject, htmlBody, cancellationToken);
        return new PasswordResetRequestResult { Succeeded = true };
    }

    public async Task<(bool Succeeded, string? Error)> ResetPasswordAsync(ResetPasswordViewModel model, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await GetValidPasswordResetTokenAsync(model.Email, model.Token, cancellationToken);
        if (tokenEntity is null)
        {
            return (false, "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        }

        var user = await _dbContext.Users.SingleAsync(x => x.UserId == tokenEntity.UserId, cancellationToken);
        var passwordHash = _passwordHasher.HashPassword(model.NewPassword, out var salt);

        user.PasswordHash = passwordHash;
        user.PasswordSalt = salt;
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        tokenEntity.UsedAt = DateTime.UtcNow;

        var otherTokens = await _dbContext.PasswordResetTokens
            .Where(x => x.UserId == user.UserId && x.PasswordResetTokenId != tokenEntity.PasswordResetTokenId && x.UsedAt == null && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var otherToken in otherTokens)
        {
            otherToken.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<bool> IsPasswordResetTokenValidAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        return await GetValidPasswordResetTokenAsync(email, token, cancellationToken) is not null;
    }

    private static string NormalizeRoleCode(string roleCode)
    {
        return roleCode.Trim().ToUpperInvariant() switch
        {
            "ADMIN" => RoleConstants.Admin,
            "DIRECTOR" => RoleConstants.Director,
            "MANAGER" => RoleConstants.Manager,
            "ACCOUNTANT" => RoleConstants.Accountant,
            "STAFF" => RoleConstants.Employee,
            "EMPLOYEE" => RoleConstants.Employee,
            "CUSTOMER" => RoleConstants.Customer,
            _ => roleCode
        };
    }

    private async Task<PasswordResetToken?> GetValidPasswordResetTokenAsync(string email, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var tokenHash = ComputeSha256(token.Trim());

        return await _dbContext.PasswordResetTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash
                    && x.User.NormalizedEmail == normalizedEmail
                    && x.UsedAt == null
                    && x.RevokedAt == null
                    && x.ExpiresAt > DateTime.UtcNow,
                cancellationToken);
    }

    private static string GenerateResetToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static string BuildResetPasswordEmail(string fullName, string resetLink)
    {
        var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "bạn" : fullName);
        var safeLink = System.Net.WebUtility.HtmlEncode(resetLink);

        return $"""
                <div style="font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:1.6;color:#1f2a44;">
                    <p>Xin chào {safeName},</p>
                    <p>ChillTour đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
                    <p>Nhấn vào liên kết bên dưới để tạo mật khẩu mới:</p>
                    <p><a href="{safeLink}" style="display:inline-block;padding:12px 20px;background:#1677ff;color:#ffffff;text-decoration:none;border-radius:8px;">Đặt lại mật khẩu</a></p>
                    <p>Nếu nút không hoạt động, bạn có thể mở liên kết sau:</p>
                    <p><a href="{safeLink}">{safeLink}</a></p>
                    <p>Liên kết có hiệu lực trong 1 giờ và chỉ dùng được 1 lần.</p>
                    <p>Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email.</p>
                    <p>ChillTour</p>
                </div>
                """;
    }
}
