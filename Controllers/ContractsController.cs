using System.Security.Claims;
using ChillTour.Data;
using ChillTour.Models.Contracts;
using ChillTour.Security;
using ChillTour.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Controllers;

[Authorize]
public class ContractsController : Controller
{
    private readonly ChillTourDbContext _dbContext;
    private readonly IContractService _contractService;

    public ContractsController(ChillTourDbContext dbContext, IContractService contractService)
    {
        _dbContext = dbContext;
        _contractService = contractService;
    }

    [HttpGet("/contracts/{id:long}")]
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var contract = await LoadAuthorizedContractAsync(id, userId.Value, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        return View(BuildViewModel(contract));
    }

    [HttpPost("/contracts/{id:long}/send-otp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendOtp(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var contract = await LoadAuthorizedContractAsync(id, userId.Value, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        var isDirector = IsDirectorSigner(contract, userId.Value);
        if (!CanUserSign(contract, userId.Value))
        {
            TempData["ContractErrorMessage"] = "Bạn không thể ký hợp đồng ở trạng thái hiện tại.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var otp = await _contractService.SendOtpAsync(contract, userId.Value, isDirector, cancellationToken);
        TempData["ContractInfoMessage"] = "Đã gửi OTP đến email của bạn. OTP có hiệu lực trong 10 phút.";
        if (!string.IsNullOrWhiteSpace(otp))
        {
            TempData["ContractDevOtp"] = otp;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("/contracts/{id:long}/sign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sign(long id, string otp, string signatureDataUrl, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var contract = await LoadAuthorizedContractAsync(id, userId.Value, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        var isDirector = IsDirectorSigner(contract, userId.Value);
        if (!CanUserSign(contract, userId.Value))
        {
            TempData["ContractErrorMessage"] = "Bạn không thể ký hợp đồng ở trạng thái hiện tại.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var signed = await _contractService.SignAsync(
            contract,
            userId.Value,
            isDirector,
            otp,
            signatureDataUrl,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        TempData[signed ? "ContractInfoMessage" : "ContractErrorMessage"] = signed
            ? "Ký hợp đồng thành công."
            : "OTP không hợp lệ, đã hết hạn hoặc chữ ký chưa đúng. Vui lòng thử lại.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("/contracts/{id:long}/download")]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var contract = await LoadAuthorizedContractAsync(id, userId.Value, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        var bytes = _contractService.GeneratePdf(contract);
        return File(bytes, "application/pdf", $"{contract.ContractCode}.pdf");
    }

    private async Task<ChillTour.Data.Entities.ElectronicContract?> LoadAuthorizedContractAsync(long id, long userId, CancellationToken cancellationToken)
    {
        var query = _dbContext.ElectronicContracts
            .Include(x => x.Booking).ThenInclude(x => x.User)
            .Include(x => x.Booking).ThenInclude(x => x.Tour)
            .Include(x => x.Booking).ThenInclude(x => x.TourSchedule)
            .Where(x => x.ElectronicContractId == id);

        if (!User.IsInRole(RoleConstants.Director) && !RoleConstants.BackOffice.Any(User.IsInRole))
        {
            query = query.Where(x => x.Booking.UserId == userId);
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private ContractSignViewModel BuildViewModel(ChillTour.Data.Entities.ElectronicContract contract)
    {
        var userId = GetCurrentUserId() ?? 0;
        var isDirector = IsDirectorSigner(contract, userId);
        return new ContractSignViewModel
        {
            ContractId = contract.ElectronicContractId,
            ContractCode = contract.ContractCode,
            BookingCode = contract.Booking.BookingCode,
            TourName = contract.Booking.Tour.TourName,
            CustomerName = contract.Booking.ContactName,
            DepartureDate = contract.Booking.TourSchedule.DepartureDate,
            TotalAmount = contract.Booking.TotalAmount,
            PaidAmount = contract.Booking.PaidAmount,
            ContractStatus = contract.ContractStatus,
            StatusText = StatusText(contract.ContractStatus),
            ContractHtml = contract.ContractHtml,
            CanSign = CanUserSign(contract, userId),
            IsDirectorSigning = isDirector,
            CustomerSigned = contract.CustomerSignedAt.HasValue,
            DirectorSigned = contract.DirectorSignedAt.HasValue,
            CustomerSignedAt = contract.CustomerSignedAt,
            DirectorSignedAt = contract.DirectorSignedAt,
            PdfPath = contract.FinalPdfPath ?? contract.DraftPdfPath,
            DevOtp = TempData["ContractDevOtp"] as string
        };
    }

    private bool CanUserSign(ChillTour.Data.Entities.ElectronicContract contract, long userId)
    {
        if (contract.ContractStatus == ContractService.PendingCustomerSign)
        {
            return contract.Booking.UserId == userId;
        }

        return contract.ContractStatus == ContractService.PendingDirectorSign && IsDirectorSigner(contract, userId);
    }

    private bool IsDirectorSigner(ChillTour.Data.Entities.ElectronicContract contract, long userId) =>
        User.IsInRole(RoleConstants.Director);

    private static string StatusText(byte status) => status switch
    {
        ContractService.Draft => "Nháp",
        ContractService.PendingCustomerSign => "Chờ khách ký",
        ContractService.PendingDirectorSign => "Chờ Director ký",
        ContractService.Signed => "Đã ký hoàn tất",
        ContractService.Cancelled => "Đã hủy",
        _ => "Không xác định"
    };

    private long? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var userId) ? userId : null;
    }
}
