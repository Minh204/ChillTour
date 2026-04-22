using System.Security.Claims;
using ChillTour.Models.Admin;

namespace ChillTour.Services.Reports;

public interface IReportService
{
    Task<AdminReportSummaryViewModel> GetSummaryAsync(AdminReportFilterViewModel filter, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportExcelAsync(AdminReportFilterViewModel filter, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportPdfAsync(AdminReportFilterViewModel filter, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
