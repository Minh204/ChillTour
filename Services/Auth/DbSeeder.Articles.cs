using ChillTour.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChillTour.Services.Auth;

public partial class DbSeeder
{
    private async Task SeedArticlesAsync(CancellationToken cancellationToken)
    {
        var articleDefinitions = GetArticleDefinitions();
        var existingCodes = await _dbContext.Articles
            .AsNoTracking()
            .Where(x => articleDefinitions.Select(article => article.ArticleCode).Contains(x.ArticleCode))
            .Select(x => x.ArticleCode)
            .ToListAsync(cancellationToken);

        var existingCodeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var managerUserId = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Email == "manager@chilltour.local")
            .Select(x => (long?)x.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var articlesToAdd = articleDefinitions
            .Where(article => !existingCodeSet.Contains(article.ArticleCode))
            .Select((article, index) => new Article
            {
                ArticleCode = article.ArticleCode,
                Title = article.Title,
                Slug = article.Slug,
                Summary = article.Summary,
                ContentHtml = article.ContentHtml,
                ThumbnailUrl = article.ThumbnailUrl,
                PublishedAt = now.AddDays(-(index + 1) * 2),
                Status = 1,
                AuthorUserId = managerUserId,
                CreatedAt = now.AddDays(-(index + 1) * 2)
            })
            .ToList();

        if (articlesToAdd.Count == 0)
        {
            return;
        }

        _dbContext.Articles.AddRange(articlesToAdd);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static List<ArticleSeedDefinition> GetArticleDefinitions()
    {
        return
        [
            new(
                "ART0001",
                "Kinh nghiệm săn tour giá tốt mà không bị hụt lịch đẹp",
                "kinh-nghiem-san-tour-gia-tot-khong-bi-hut-lich-dep",
                "Cách chọn lịch khởi hành, so sánh giá và chốt tour đúng thời điểm để vừa tiết kiệm vừa không mất chỗ đẹp.",
                "https://images.pexels.com/photos/15692362/pexels-photo-15692362.jpeg?auto=compress&cs=tinysrgb&w=1400",
                """
                <p>Đặt tour không chỉ là so giá rẻ nhất. Điều khách thường bỏ lỡ là <strong>lịch khởi hành đẹp</strong> và số chỗ còn lại. Khi một lịch có mức giá tốt nhưng còn quá ít chỗ, bạn nên chốt nhanh thay vì chờ giảm thêm.</p>
                <p>Trên ChillTour, bạn nên ưu tiên kiểm tra ba yếu tố trước khi đặt:</p>
                <ul>
                    <li>Ngày khởi hành có còn nhiều chỗ hay không.</li>
                    <li>Giá đang áp dụng cho chính lịch đó, không phải giá chung của tour.</li>
                    <li>Tour có ưu đãi giờ chót hoặc mã giảm giá cá nhân hay không.</li>
                </ul>
                <p>Với những tour lễ hoặc cuối tuần, nên đặt sớm từ 2 đến 4 tuần. Với tour giờ chót, nếu lịch đi còn trong 5 ngày tới thì khách vừa giữ được chỗ, vừa có thể được giảm tự động 15% theo cấu hình hệ thống.</p>
                """
            ),
            new(
                "ART0002",
                "Nên đi Đà Lạt mùa nào để có ảnh đẹp và thời tiết dễ chịu",
                "nen-di-da-lat-mua-nao-de-co-anh-dep-va-thoi-tiet-de-chiu",
                "Gợi ý thời điểm đi Đà Lạt, cách chọn lịch 3 ngày 2 đêm hay 4 ngày 3 đêm và những điểm săn mây dễ đi.",
                "https://images.pexels.com/photos/15798431/pexels-photo-15798431.jpeg?auto=compress&cs=tinysrgb&w=1400",
                """
                <p>Đà Lạt có thể đi quanh năm, nhưng thời điểm đẹp nhất thường là từ <strong>tháng 11 đến tháng 3</strong>. Giai đoạn này trời lạnh vừa phải, ít mưa và ánh sáng đẹp cho ảnh.</p>
                <p>Nếu bạn thích nhịp độ nghỉ dưỡng nhẹ, tour 3 ngày 2 đêm là đủ. Nếu muốn thêm săn mây, đồi chè, hồ Tuyền Lâm và thời gian ngồi cafe view đẹp, nên chọn tour 4 ngày 3 đêm.</p>
                <p>Khi đặt tour Đà Lạt, nên xem kỹ các yếu tố sau:</p>
                <ul>
                    <li>Giờ bay đi và về có quá sớm hoặc quá muộn không.</li>
                    <li>Lịch trình có quá dày điểm check-in khiến khách mệt không.</li>
                    <li>Khách sạn có nằm trung tâm để tiện đi chợ đêm hay không.</li>
                </ul>
                """
            ),
            new(
                "ART0003",
                "Lần đầu đi Singapore nên chuẩn bị gì để không bị rối",
                "lan-dau-di-singapore-nen-chuan-bi-gi-de-khong-bi-roi",
                "Checklist ngắn gọn cho khách lần đầu đi Singapore: giấy tờ, đổi tiền, internet và chọn lịch trình hợp lý.",
                "https://images.pexels.com/photos/29917748/pexels-photo-29917748.jpeg?auto=compress&cs=tinysrgb&w=1400",
                """
                <p>Singapore là điểm đến phù hợp cho khách mới đi nước ngoài vì giao thông rõ ràng, an toàn và lịch trình gọn. Tuy nhiên, bạn vẫn nên chuẩn bị trước để tránh phát sinh không cần thiết.</p>
                <p>Những việc nên làm trước ngày đi:</p>
                <ul>
                    <li>Kiểm tra hộ chiếu còn hạn trên 6 tháng.</li>
                    <li>Chuẩn bị sim hoặc eSIM dữ liệu để tiện tra bản đồ và liên lạc.</li>
                    <li>Ưu tiên hành lý gọn vì di chuyển trong thành phố khá linh hoạt.</li>
                </ul>
                <p>Với gia đình có trẻ nhỏ, nên chọn tour có ít nhất một ngày linh hoạt để nghỉ ngơi. Với nhóm bạn trẻ, có thể ưu tiên chương trình có Sentosa hoặc khung giờ tự do mua sắm ở Orchard và Jewel.</p>
                """
            ),
            new(
                "ART0004",
                "Cách tính tiền tour cho người lớn, trẻ em, em bé và phụ thu phòng đơn",
                "cach-tinh-tien-tour-nguoi-lon-tre-em-em-be-va-phu-thu-phong-don",
                "Giải thích rõ cách ChillTour tính tổng tiền: người lớn 100%, trẻ em 50%, em bé miễn phí và cộng thêm phụ thu phòng đơn.",
                "https://images.pexels.com/photos/32715907/pexels-photo-32715907.jpeg?auto=compress&cs=tinysrgb&w=1400",
                """
                <p>ChillTour tính giá theo từng thành phần để khách nhìn rõ tổng tiền trước khi chuyển khoản cọc:</p>
                <ul>
                    <li><strong>Người lớn</strong>: 100% giá lịch khởi hành.</li>
                    <li><strong>Trẻ em</strong>: 50% giá người lớn.</li>
                    <li><strong>Em bé</strong>: miễn phí.</li>
                    <li><strong>Phòng đơn</strong>: cộng thêm theo mức phụ thu của lịch khởi hành.</li>
                </ul>
                <p>Ví dụ một lịch có giá 5.000.000 đ/người lớn, phụ thu phòng đơn 1.200.000 đ. Nếu đặt 2 người lớn, 1 trẻ em và 1 phòng đơn thì tổng tiền sẽ là 5.000.000 x 2 + 2.500.000 + 1.200.000.</p>
                <p>Tiền cọc được tính bằng 30% sau khi đã trừ các ưu đãi hợp lệ và cộng các phụ phí phát sinh.</p>
                """
            )
        ];
    }

    private sealed record ArticleSeedDefinition(
        string ArticleCode,
        string Title,
        string Slug,
        string Summary,
        string ThumbnailUrl,
        string ContentHtml);
}
