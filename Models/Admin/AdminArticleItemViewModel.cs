namespace ChillTour.Models.Admin;

public class AdminArticleItemViewModel
{
    public long ArticleId { get; set; }
    public string ArticleCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsPublished { get; set; }
    public string? AuthorName { get; set; }
}
