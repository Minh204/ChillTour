using System.Security.Cryptography;
using System.Text;
using ChillTour.Data;
using ChillTour.Data.Entities;
using ChillTour.Security;
using ChillTour.Services.Mail;
using ChillTour.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChillTour.Services.Contracts;

public class ContractService : IContractService
{
    public const byte Draft = 0;
    public const byte PendingCustomerSign = 1;
    public const byte PendingDirectorSign = 2;
    public const byte Signed = 3;
    public const byte Cancelled = 4;

    private readonly ChillTourDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly INotificationService _notificationService;
    private readonly IWebHostEnvironment _environment;

    public ContractService(
        ChillTourDbContext dbContext,
        IEmailSender emailSender,
        INotificationService notificationService,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _notificationService = notificationService;
        _environment = environment;
    }

    public async Task<ElectronicContract> EnsureContractForBookingAsync(long bookingId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ElectronicContracts
            .Include(x => x.Booking).ThenInclude(x => x.User)
            .Include(x => x.Booking).ThenInclude(x => x.Tour)
            .Include(x => x.Booking).ThenInclude(x => x.TourSchedule)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId && x.ContractStatus != Cancelled, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var booking = await _dbContext.Bookings
            .Include(x => x.User)
            .Include(x => x.Tour)
            .Include(x => x.TourSchedule)
            .SingleAsync(x => x.BookingId == bookingId, cancellationToken);

        var contract = new ElectronicContract
        {
            BookingId = booking.BookingId,
            ContractCode = await GenerateContractCodeAsync(cancellationToken),
            ContractStatus = PendingCustomerSign,
            ContractHtml = BuildContractHtml(booking),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ElectronicContracts.Add(contract);
        await _dbContext.SaveChangesAsync(cancellationToken);

        contract.Booking = booking;
        contract.DraftPdfPath = SavePdf(contract, final: false);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateAsync(
            booking.UserId,
            notificationType: 15,
            title: "Hợp đồng chờ bạn ký",
            message: $"Hợp đồng {contract.ContractCode} cho đơn {booking.BookingCode} đã được tạo. Vui lòng ký vẽ và xác thực OTP để tiếp tục xử lý.",
            relatedEntityType: "Contract",
            relatedEntityId: contract.ElectronicContractId,
            cancellationToken: cancellationToken);

        return contract;
    }

    public async Task<string> SendOtpAsync(ElectronicContract contract, long actorUserId, bool isDirector, CancellationToken cancellationToken = default)
    {
        var actor = await _dbContext.Users.SingleAsync(x => x.UserId == actorUserId, cancellationToken);
        var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var now = DateTime.UtcNow;

        if (isDirector)
        {
            contract.DirectorOtpHash = HashOtp(otp);
            contract.DirectorOtpExpiresAt = now.AddMinutes(10);
        }
        else
        {
            contract.CustomerOtpHash = HashOtp(otp);
            contract.CustomerOtpExpiresAt = now.AddMinutes(10);
        }

        contract.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_emailSender.IsConfigured)
        {
            await _emailSender.SendAsync(
                actor.Email,
                $"OTP ký hợp đồng {contract.ContractCode}",
                $"<p>Mã OTP ký hợp đồng <strong>{contract.ContractCode}</strong> là:</p><h2>{otp}</h2><p>Mã có hiệu lực trong 10 phút.</p>",
                cancellationToken);

            return string.Empty;
        }

        return otp;
    }

    public async Task<bool> SignAsync(ElectronicContract contract, long actorUserId, bool isDirector, string otp, string signatureDataUrl, string ipAddress, string userAgent, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        if (!IsValidSignature(signatureDataUrl))
        {
            return false;
        }

        if (isDirector)
        {
            if (contract.ContractStatus != PendingDirectorSign || !ValidateOtp(otp, contract.DirectorOtpHash, contract.DirectorOtpExpiresAt, now))
            {
                return false;
            }

            contract.DirectorSignatureDataUrl = signatureDataUrl;
            contract.DirectorSignedAt = now;
            contract.DirectorSignedIp = ipAddress;
            contract.DirectorSignedUserAgent = userAgent;
            contract.DirectorOtpHash = null;
            contract.DirectorOtpExpiresAt = null;
            contract.ContractStatus = Signed;
            contract.FinalPdfPath = SavePdf(contract, final: true);
        }
        else
        {
            if (contract.ContractStatus != PendingCustomerSign || !ValidateOtp(otp, contract.CustomerOtpHash, contract.CustomerOtpExpiresAt, now))
            {
                return false;
            }

            contract.CustomerSignatureDataUrl = signatureDataUrl;
            contract.CustomerSignedAt = now;
            contract.CustomerSignedIp = ipAddress;
            contract.CustomerSignedUserAgent = userAgent;
            contract.CustomerOtpHash = null;
            contract.CustomerOtpExpiresAt = null;
            contract.ContractStatus = PendingDirectorSign;
        }

        contract.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isDirector)
        {
            await _notificationService.CreateAsync(
                contract.Booking.UserId,
                notificationType: 15,
                title: "Hợp đồng đã ký hoàn tất",
                message: $"Hợp đồng {contract.ContractCode} đã được hai bên ký hoàn tất. Bạn có thể tải lại hợp đồng bất cứ lúc nào.",
                relatedEntityType: "Contract",
                relatedEntityId: contract.ElectronicContractId,
                cancellationToken: cancellationToken);
        }
        else
        {
            await _notificationService.CreateForRolesAsync(
                new[] { RoleConstants.Director },
                notificationType: 15,
                title: "Hợp đồng chờ Director ký",
                message: $"Khách hàng đã ký hợp đồng {contract.ContractCode}. Vui lòng kiểm tra và ký xác nhận.",
                relatedEntityType: "Contract",
                relatedEntityId: contract.ElectronicContractId,
                cancellationToken: cancellationToken);
        }

        return true;
    }

    public byte[] GeneratePdf(ElectronicContract contract)
    {
        var booking = contract.Booking;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("HỢP ĐỒNG DỊCH VỤ DU LỊCH CHILLTOUR").Bold().FontSize(18).AlignCenter();
                    col.Item().Text($"Mã hợp đồng: {contract.ContractCode}").AlignCenter();
                });
                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Mã đơn: {booking.BookingCode}");
                    col.Item().Text($"Khách hàng: {booking.ContactName} - {booking.ContactEmail} - {booking.ContactPhone}");
                    col.Item().Text($"Tour: {booking.Tour.TourName}");
                    col.Item().Text($"Khởi hành: {booking.TourSchedule.DepartureDate:dd/MM/yyyy} - Kết thúc: {booking.TourSchedule.ReturnDate:dd/MM/yyyy}");
                    col.Item().Text($"Số khách: {booking.AdultCount} người lớn, {booking.ChildCount} trẻ em, {booking.InfantCount} em bé");
                    col.Item().Text($"Tổng giá trị: {booking.TotalAmount:N0} {booking.CurrencyCode}; Đã thanh toán: {booking.PaidAmount:N0} {booking.CurrencyCode}");
                    col.Item().Text("Điều khoản: Khách hàng cam kết cung cấp thông tin chính xác, thanh toán đúng hạn và tuân thủ lịch trình. ChillTour cung cấp dịch vụ theo thông tin tour đã công bố và hỗ trợ khách hàng trong quá trình sử dụng dịch vụ.");
                    col.Item().Text("Xác thực điện tử: Hợp đồng được ký bằng chữ ký vẽ kết hợp OTP gửi qua email. Hệ thống lưu thời gian ký, IP và thiết bị sử dụng.");
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(sig =>
                        {
                            sig.Item().Text("Khách hàng").Bold();
                            AddSignature(sig, contract.CustomerSignatureDataUrl);
                            sig.Item().Text(contract.CustomerSignedAt.HasValue ? $"Ký lúc {contract.CustomerSignedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}" : "Chưa ký");
                            sig.Item().Text($"IP: {contract.CustomerSignedIp ?? "--"}").FontSize(8);
                        });
                        row.RelativeItem().Column(sig =>
                        {
                            sig.Item().Text("Director").Bold();
                            AddSignature(sig, contract.DirectorSignatureDataUrl);
                            sig.Item().Text(contract.DirectorSignedAt.HasValue ? $"Ký lúc {contract.DirectorSignedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}" : "Chưa ký");
                            sig.Item().Text($"IP: {contract.DirectorSignedIp ?? "--"}").FontSize(8);
                        });
                    });
                });
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("ChillTour electronic contract - ");
                    x.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }

    private static void AddSignature(ColumnDescriptor column, string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            column.Item().Height(70).Text("Chưa có chữ ký");
            return;
        }

        var bytes = Convert.FromBase64String(dataUrl[(dataUrl.IndexOf(',') + 1)..]);
        column.Item().Height(70).Image(bytes).FitArea();
    }

    private string SavePdf(ElectronicContract contract, bool final)
    {
        var folder = Path.Combine(_environment.WebRootPath, "contracts");
        Directory.CreateDirectory(folder);
        var fileName = $"{contract.ContractCode}-{(final ? "signed" : "draft")}.pdf";
        var relativePath = $"/contracts/{fileName}";
        File.WriteAllBytes(Path.Combine(folder, fileName), GeneratePdf(contract));
        return relativePath;
    }

    private async Task<string> GenerateContractCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = $"HD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}";
            if (!await _dbContext.ElectronicContracts.AnyAsync(x => x.ContractCode == code, cancellationToken))
            {
                return code;
            }
        }
    }

    private static string BuildContractHtml(Booking booking) =>
        $"Hợp đồng dịch vụ du lịch cho đơn {booking.BookingCode}, tour {booking.Tour.TourName}, khách hàng {booking.ContactName}.";

    private static bool IsValidSignature(string signatureDataUrl) =>
        signatureDataUrl.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
        && signatureDataUrl.Length > 500;

    private static bool ValidateOtp(string otp, string? hash, DateTime? expiresAt, DateTime now) =>
        !string.IsNullOrWhiteSpace(otp)
        && !string.IsNullOrWhiteSpace(hash)
        && expiresAt.HasValue
        && expiresAt.Value >= now
        && string.Equals(HashOtp(otp.Trim()), hash, StringComparison.Ordinal);

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }
}
