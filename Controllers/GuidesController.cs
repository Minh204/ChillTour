using ChillTour.Data;
using ChillTour.Models.Guides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Controllers;

[Route("cam-nang")]
public class GuidesController : Controller
{
    private readonly ChillTourDbContext _dbContext;

    public GuidesController(ChillTourDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? searchTerm = null, int? year = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Articles
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.PublishedAt != null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim();
            query = query.Where(x =>
                x.Title.Contains(normalizedSearch) ||
                (x.Summary != null && x.Summary.Contains(normalizedSearch)) ||
                x.Slug.Contains(normalizedSearch));
        }

        if (year.HasValue)
        {
            query = query.Where(x => x.PublishedAt!.Value.Year == year.Value);
        }

        var availableYears = await _dbContext.Articles
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.PublishedAt != null)
            .Select(x => x.PublishedAt!.Value.Year)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync(cancellationToken);

        var articles = await query
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => new GuideListItemViewModel
            {
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                ThumbnailUrl = x.ThumbnailUrl,
                PublishedAt = x.PublishedAt
            })
            .ToListAsync(cancellationToken);

        return View(new GuidePageViewModel
        {
            SearchTerm = searchTerm,
            SelectedYear = year,
            AvailableYears = availableYears,
            Articles = articles
        });
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var article = await _dbContext.Articles
            .AsNoTracking()
            .Include(x => x.AuthorUser)
            .Where(x => x.Status == 1 && x.PublishedAt != null && x.Slug == slug)
            .Select(x => new GuideDetailViewModel
            {
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                ThumbnailUrl = x.ThumbnailUrl,
                ContentHtml = x.ContentHtml,
                PublishedAt = x.PublishedAt,
                AuthorName = x.AuthorUser != null ? x.AuthorUser.FullName : null
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (article is null)
        {
            return NotFound();
        }

        article.RelatedArticles = await _dbContext.Articles
            .AsNoTracking()
            .Where(x => x.Status == 1 && x.PublishedAt != null && x.Slug != slug)
            .OrderByDescending(x => x.PublishedAt)
            .Take(3)
            .Select(x => new GuideListItemViewModel
            {
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                ThumbnailUrl = x.ThumbnailUrl,
                PublishedAt = x.PublishedAt
            })
            .ToListAsync(cancellationToken);

        return View(article);
    }
}
