namespace ChillTour.Models.Admin;

public class AdminArticleListViewModel
{
    public int TotalArticles { get; set; }
    public int PublishedArticles { get; set; }
    public int DraftArticles { get; set; }
    public AdminArticlesFilterViewModel Filter { get; set; } = new();
    public List<AdminArticleItemViewModel> Articles { get; set; } = [];
}
