namespace ChillTour.Models.Guides;

public class GuideDetailViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ContentHtml { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? AuthorName { get; set; }
    public List<GuideListItemViewModel> RelatedArticles { get; set; } = [];
}
