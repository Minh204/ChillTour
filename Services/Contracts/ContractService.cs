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
            .Include(x => x.Booking).ThenInclude(x => x.Tour).ThenInclude(x => x.EndDestination)
            .Include(x => x.Booking).ThenInclude(x => x.TourSchedule)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId && x.ContractStatus != Cancelled, cancellationToken);

        if (existing is not null)
        {
            if (existing.ContractStatus == PendingCustomerSign
                && !existing.CustomerSignedAt.HasValue
                && !existing.DirectorSignedAt.HasValue)
            {
                existing.ContractStatus = PendingDirectorSign;
                existing.DraftPdfPath = SavePdf(existing, final: false);
                existing.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return existing;
        }

        var booking = await _dbContext.Bookings
            .Include(x => x.User)
            .Include(x => x.Tour).ThenInclude(x => x.EndDestination)
            .Include(x => x.TourSchedule)
            .SingleAsync(x => x.BookingId == bookingId, cancellationToken);

        var contract = new ElectronicContract
        {
            BookingId = booking.BookingId,
            ContractCode = await GenerateContractCodeAsync(cancellationToken),
            ContractStatus = PendingDirectorSign,
            ContractHtml = BuildContractHtml(booking),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ElectronicContracts.Add(contract);
        await _dbContext.SaveChangesAsync(cancellationToken);

        contract.Booking = booking;
        contract.DraftPdfPath = SavePdf(contract, final: false);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateForRolesAsync(
            new[] { RoleConstants.Director },
            notificationType: 15,
            title: "Hợp đồng chờ Giám đốc ký",
            message: $"Hợp đồng {contract.ContractCode} cho đơn {booking.BookingCode} đã được tạo sau khi khách đặt cọc. Vui lòng xem PDF preview và ký xác nhận trước khi chuyển cho khách hàng.",
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

    public async Task<bool> SignAsync(
        ElectronicContract contract,
        long actorUserId,
        bool isDirector,
        string otp,
        string signatureDataUrl,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default)
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
            if (contract.CustomerSignedAt.HasValue)
            {
                contract.ContractStatus = Signed;
                contract.FinalPdfPath = SavePdf(contract, final: true);
            }
            else
            {
                contract.ContractStatus = PendingCustomerSign;
                contract.DraftPdfPath = SavePdf(contract, final: false);
            }
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
            contract.ContractStatus = Signed;
            contract.FinalPdfPath = SavePdf(contract, final: true);
        }

        contract.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isDirector)
        {
            if (contract.ContractStatus == Signed)
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
                await _notificationService.CreateAsync(
                    contract.Booking.UserId,
                    notificationType: 15,
                    title: "Hợp đồng đã được Giám đốc ký",
                    message: $"Giám đốc đã ký hợp đồng {contract.ContractCode}. Vui lòng xem trước PDF, ký vẽ và xác thực OTP để hoàn tất hợp đồng.",
                    relatedEntityType: "Contract",
                    relatedEntityId: contract.ElectronicContractId,
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await _notificationService.CreateAsync(
                contract.Booking.UserId,
                notificationType: 15,
                title: "Hợp đồng đã ký hoàn tất",
                message: $"Hợp đồng {contract.ContractCode} đã được hai bên ký hoàn tất. Bạn có thể tải lại hợp đồng bất cứ lúc nào.",
                relatedEntityType: "Contract",
                relatedEntityId: contract.ElectronicContractId,
                cancellationToken: cancellationToken);

            await _notificationService.CreateForRolesAsync(
                new[] { RoleConstants.Director },
                notificationType: 15,
                title: "Hợp đồng đã ký hoàn tất",
                message: $"Khách hàng đã ký hợp đồng {contract.ContractCode}. Hợp đồng đã hoàn tất và được khóa nội dung.",
                relatedEntityType: "Contract",
                relatedEntityId: contract.ElectronicContractId,
                cancellationToken: cancellationToken);
        }

        return true;
    }

    public byte[] GeneratePdf(ElectronicContract contract)
    {
        var booking = contract.Booking;
        var tour = booking.Tour;
        var schedule = booking.TourSchedule;
        var remainingAmount = Math.Max(booking.TotalAmount - booking.PaidAmount, 0);
        var destination = tour.EndDestination?.DestinationName ?? tour.ReturnPoint ?? tour.TourName;
        var signedDate = DateTime.UtcNow.ToLocalTime();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(42);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontFamily("Times New Roman").FontSize(12));

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    column.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold().FontSize(13);
                    column.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc").Bold().FontSize(12);
                    column.Item().AlignCenter().Text("-----------------------------");
                    column.Item().PaddingTop(8).AlignCenter().Text("HỢP ĐỒNG DỊCH VỤ DU LỊCH").Bold().FontSize(16);
                    column.Item().AlignCenter().Text($"Số: {contract.ContractCode}/HĐDL/{signedDate:yyyy}").Bold();

                    column.Item().PaddingTop(10).Text("BÊN A: CÔNG TY TNHH CHILLTOUR").Bold();
                    column.Item().Text(text =>
                    {
                        text.Span("Đại diện: ").Bold();
                        text.Span("Giám đốc ChillTour");
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Chức vụ: ").Bold();
                        text.Span("Giám đốc");
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Điện thoại: ").Bold();
                        text.Span("1900 1234");
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Email: ").Bold();
                        text.Span("support@chilltour.vn");
                    });

                    column.Item().PaddingTop(6).Text("BÊN B: KHÁCH HÀNG").Bold();
                    column.Item().Text(text =>
                    {
                        text.Span("Họ và tên: ").Bold();
                        text.Span(booking.ContactName);
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Số điện thoại: ").Bold();
                        text.Span(booking.ContactPhone);
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Email: ").Bold();
                        text.Span(booking.ContactEmail);
                    });

                    column.Item().PaddingTop(6).Text("Hai bên thống nhất ký kết hợp đồng với các điều khoản sau:");
                    AddClause(column, "Điều 1: Nội dung dịch vụ",
                        $"- Tên tour: {tour.TourName}",
                        $"- Điểm đến: {destination}",
                        $"- Thời gian: {tour.DurationDays} ngày {tour.DurationNights} đêm",
                        $"- Ngày khởi hành: {schedule.DepartureDate:dd/MM/yyyy}",
                        $"- Số lượng khách: {booking.AdultCount + booking.ChildCount + booking.InfantCount} khách ({booking.AdultCount} người lớn, {booking.ChildCount} trẻ em, {booking.InfantCount} em bé)");
                    AddClause(column, "Điều 2: Giá trị hợp đồng",
                        $"- Tổng giá trị: {booking.TotalAmount:N0} {booking.CurrencyCode}",
                        $"- Đặt cọc/đã thanh toán: {booking.PaidAmount:N0} {booking.CurrencyCode}",
                        $"- Còn lại: {remainingAmount:N0} {booking.CurrencyCode}");
                    AddClause(column, "Điều 3: Thanh toán",
                        "- Thanh toán cọc 30% tổng số tiền.",
                        "- Thanh toán phần còn lại trước 2 ngày khởi hành.");
                    AddClause(column, "Điều 4: Quyền và nghĩa vụ",
                        "- Bên A: cung cấp dịch vụ đúng cam kết, hỗ trợ khách hàng trong quá trình sử dụng dịch vụ.",
                        "- Bên B: thanh toán đúng hạn, cung cấp thông tin chính xác và tuân thủ lịch trình.");
                    AddClause(column, "Điều 5: Chính sách hủy tour và hoàn tiền",
                        "- Bên A: khi hủy tour phải hoàn tiền 100% hoặc đề xuất sang tour khác với giá trị tương đương nếu Bên B đồng ý.",
                        "- Bên B: hủy tour trước 7 ngày sẽ được hoàn tiền 100%. Trường hợp đặt tour mà ngày khởi hành gần nhất dưới 7 ngày sẽ được hoàn 60%.",
                        "- Các trường hợp hủy tour do thiên tai hoặc bất khả kháng như người thân mất, tai nạn, sinh con sẽ được hoàn tiền 100% theo hồ sơ xác minh.");
                    AddClause(column, "Điều 6: Hiệu lực hợp đồng",
                        "- Hợp đồng có hiệu lực khi hai bên đã ký.",
                        "- Hợp đồng điện tử có giá trị tương đương bản giấy trong phạm vi giao dịch trên hệ thống ChillTour.",
                        "- Hợp đồng được ký bằng chữ ký vẽ kết hợp OTP qua email; hệ thống lưu thời gian ký, IP và thiết bị sử dụng.");

                    column.Item().PaddingTop(10).AlignRight().Text($"Ngày {signedDate:dd}, tháng {signedDate:MM}, năm {signedDate:yyyy}");
                    column.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(sig =>
                        {
                            sig.Item().Text("Đại diện bên B").Bold();
                            sig.Item().Text("(Khách hàng)");
                            AddSignature(sig, contract.CustomerSignatureDataUrl);
                            sig.Item().Text(booking.ContactName).Bold();
                            sig.Item().Text(contract.CustomerSignedAt.HasValue ? $"Ký lúc {contract.CustomerSignedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}" : "Chưa ký").FontSize(9);
                        });

                        row.RelativeItem().AlignCenter().Column(sig =>
                        {
                            sig.Item().Text("Đại diện bên A").Bold();
                            sig.Item().Text("(Giám đốc)");
                            AddSignature(sig, contract.DirectorSignatureDataUrl);
                            sig.Item().Text("Giám đốc ChillTour").Bold();
                            sig.Item().Text(contract.DirectorSignedAt.HasValue ? $"Ký lúc {contract.DirectorSignedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}" : "Chưa ký").FontSize(9);
                        });
                    });
                });

                page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(9)).Text(text =>
                {
                    text.Span("ChillTour electronic contract - ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }

    private static void AddClause(ColumnDescriptor column, string title, params string[] lines)
    {
        column.Item().PaddingTop(6).Text(title).Bold();
        foreach (var line in lines)
        {
            column.Item().Text(line);
        }
    }

    private static void AddSignature(ColumnDescriptor column, string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            column.Item()
                .Height(58)
                .PaddingVertical(8)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten1)
                .AlignMiddle()
                .AlignCenter()
                .Text("Chưa ký")
                .Italic()
                .FontSize(10);
            return;
        }

        var bytes = Convert.FromBase64String(dataUrl[(dataUrl.IndexOf(',') + 1)..]);
        column.Item().Height(58).Image(bytes).FitArea();
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
