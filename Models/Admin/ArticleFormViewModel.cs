using System.ComponentModel.DataAnnotations;

namespace ChillTour.Models.Admin;

public class ArticleFormViewModel
{
    public long? ArticleId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã bài viết.")]
    public string ArticleCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập slug bài viết.")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tóm tắt bài viết.")]
    public string Summary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung bài viết.")]
    public string ContentHtml { get; set; } = string.Empty;

    [Url(ErrorMessage = "Ảnh thumbnail không hợp lệ.")]
    public string? ThumbnailUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian xuất bản.")]
    public DateTime? PublishedAt { get; set; } = DateTime.Now;

    public bool IsPublished { get; set; } = true;
}
