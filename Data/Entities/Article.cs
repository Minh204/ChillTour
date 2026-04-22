namespace ChillTour.Data.Entities;

public class Article
{
    public long ArticleId { get; set; }
    public string ArticleCode { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Summary { get; set; }
    public string? ContentHtml { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public byte Status { get; set; }
    public long? AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? AuthorUser { get; set; }
}
