using ChillTour.Data;
using ChillTour.Models.Promotions;
using ChillTour.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChillTour.Controllers;

public class PromotionsController : Controller
{
    private readonly ChillTourDbContext _dbContext;

    public PromotionsController(ChillTourDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm = null, string? type = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var userId = GetCurrentUserId();
        var hasPaidBooking = false;

        if (userId.HasValue && User.IsInRole(RoleConstants.Customer))
        {
            await EnsureFirstOrderPromotionAsync(userId.Value, cancellationToken);
            hasPaidBooking = await _dbContext.Bookings
                .AnyAsync(x => x.UserId == userId.Value && (x.PaymentStatus == 2 || x.PaymentStatus == 3), cancellationToken);
        }

        var claimedPromotionIds = userId.HasValue && User.IsInRole(RoleConstants.Customer)
            ? await _dbContext.UserPromotions
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value && x.UsedAt == null && x.Promotion.IsActive && x.Promotion.EndAt >= now)
                .Select(x => x.PromotionId)
                .ToListAsync(cancellationToken)
            : [];

        var query = _dbContext.Promotions
            .AsNoTracking()
            .Where(x => x.IsActive && x.StartAt <= now && x.EndAt >= now)
            .Where(x => !hasPaidBooking || x.PromotionCode != "FIRST15")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim();
            query = query.Where(x =>
                x.PromotionCode.Contains(normalizedSearch) ||
                x.PromotionName.Contains(normalizedSearch) ||
                (x.Description != null && x.Description.Contains(normalizedSearch)));
        }

        query = (type ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "auto" => query.Where(x => x.IsAutoApply),
            "manual" => query.Where(x => !x.IsAutoApply),
            "claimed" when userId.HasValue && User.IsInRole(RoleConstants.Customer) => query.Where(x => claimedPromotionIds.Contains(x.PromotionId)),
            _ => query
        };

        var promotions = await query
            .OrderByDescending(x => x.IsAutoApply)
            .ThenBy(x => x.EndAt)
            .Select(x => new PromotionListItemViewModel
            {
                PromotionId = x.PromotionId,
                PromotionCode = x.PromotionCode,
                PromotionName = x.PromotionName,
                Description = x.Description,
                DiscountPercent = x.DiscountPercent,
                DiscountAmount = x.DiscountAmount,
                MaxDiscountAmount = x.MaxDiscountAmount,
                MinOrderValue = x.MinOrderValue,
                EndAt = x.EndAt,
                IsAutoApply = x.IsAutoApply
            })
            .ToListAsync(cancellationToken);

        foreach (var promotion in promotions)
        {
            promotion.IsClaimed = claimedPromotionIds.Contains(promotion.PromotionId);
        }

        var model = new PromotionPageViewModel
        {
            SearchTerm = searchTerm,
            SelectedType = type,
            IsCustomer = User.IsInRole(RoleConstants.Customer),
            ActivePromotions = promotions,
            MyPromotions = promotions.Where(x => x.IsClaimed).OrderBy(x => x.EndAt).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(long promotionId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue || !User.IsInRole(RoleConstants.Customer))
        {
            return Challenge();
        }

        await EnsureFirstOrderPromotionAsync(userId.Value, cancellationToken);

        var now = DateTime.UtcNow;
        var promotion = await _dbContext.Promotions
            .SingleOrDefaultAsync(x => x.PromotionId == promotionId && x.IsActive && x.StartAt <= now && x.EndAt >= now, cancellationToken);

        if (promotion is null)
        {
            TempData["PromotionErrorMessage"] = "Mã ưu đãi không còn hiệu lực.";
            return RedirectToAction(nameof(Index));
        }

        var alreadyClaimed = await _dbContext.UserPromotions
            .AnyAsync(x => x.UserId == userId.Value && x.PromotionId == promotionId, cancellationToken);

        if (!alreadyClaimed)
        {
            _dbContext.UserPromotions.Add(new ChillTour.Data.Entities.UserPromotion
            {
                UserId = userId.Value,
                PromotionId = promotionId,
                ClaimedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        TempData["PromotionSuccessMessage"] = $"Đã lưu mã {promotion.PromotionCode} vào kho ưu đãi của bạn.";
        return RedirectToAction(nameof(Index));
    }

    private long? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var userId) ? userId : null;
    }

    private async Task EnsureFirstOrderPromotionAsync(long userId, CancellationToken cancellationToken)
    {
        var hasPaidBooking = await _dbContext.Bookings
            .AnyAsync(x => x.UserId == userId && (x.PaymentStatus == 2 || x.PaymentStatus == 3), cancellationToken);

        if (hasPaidBooking)
        {
            return;
        }

        var promotion = await _dbContext.Promotions
            .SingleOrDefaultAsync(x => x.PromotionCode == "FIRST15" && x.IsActive && x.EndAt >= DateTime.UtcNow, cancellationToken);

        if (promotion is null)
        {
            return;
        }

        var alreadyClaimed = await _dbContext.UserPromotions
            .AnyAsync(x => x.UserId == userId && x.PromotionId == promotion.PromotionId, cancellationToken);

        if (alreadyClaimed)
        {
            return;
        }

        _dbContext.UserPromotions.Add(new ChillTour.Data.Entities.UserPromotion
        {
            UserId = userId,
            PromotionId = promotion.PromotionId,
            ClaimedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
