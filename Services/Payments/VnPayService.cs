using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Net;

namespace ChillTour.Services.Payments;

public class VnPayService : IVnPayService
{
    private readonly VnPayOptions _options;

    public VnPayService(IOptions<VnPayOptions> options)
    {
        _options = options.Value;
    }

    public string CreatePaymentUrl(VnPayRequest request)
    {
        var amount = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero);
        var createDate = request.CreatedAtLocal.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var expireDate = request.CreatedAtLocal.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = amount.ToString(CultureInfo.InvariantCulture),
            ["vnp_Command"] = "pay",
            ["vnp_CreateDate"] = createDate,
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = request.IpAddress,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = request.OrderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_TxnRef"] = request.TxnRef,
            ["vnp_Version"] = "2.1.0",
            ["vnp_ExpireDate"] = expireDate
        };

        var hashData = BuildQueryString(data);
        var secureHash = ComputeHmacSha512(_options.HashSecret, hashData);

        var queryString = BuildQueryString(data);
        return $"{_options.PaymentUrl}?{queryString}&vnp_SecureHash={secureHash}";
    }

    public VnPayResult ParseResponse(IQueryCollection query)
    {
        var data = query
            .Where(x => x.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);

        data.TryGetValue("vnp_SecureHash", out var secureHash);
        data.Remove("vnp_SecureHash");
        data.Remove("vnp_SecureHashType");

        var hashData = BuildQueryString(new SortedDictionary<string, string>(data, StringComparer.Ordinal));
        var computedHash = ComputeHmacSha512(_options.HashSecret, hashData);

        data.TryGetValue("vnp_TxnRef", out var txnRef);
        data.TryGetValue("vnp_ResponseCode", out var responseCode);
        data.TryGetValue("vnp_TransactionStatus", out var transactionStatus);
        data.TryGetValue("vnp_TransactionNo", out var transactionNo);
        data.TryGetValue("vnp_OrderInfo", out var orderInfo);
        data.TryGetValue("vnp_Amount", out var amountRaw);

        return new VnPayResult
        {
            IsValidSignature = string.Equals(secureHash, computedHash, StringComparison.OrdinalIgnoreCase),
            IsSuccess = responseCode == "00" && transactionStatus == "00",
            TxnRef = txnRef ?? string.Empty,
            ResponseCode = responseCode,
            TransactionStatus = transactionStatus,
            TransactionNo = transactionNo,
            OrderInfo = orderInfo,
            Amount = long.TryParse(amountRaw, out var amount) ? amount : 0L,
            RawData = new Dictionary<string, string>(data)
        };
    }

    private static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> data)
    {
        return string.Join("&", data.Select(kvp =>
        {
            var key = WebUtility.UrlEncode(kvp.Key);
            var value = WebUtility.UrlEncode(kvp.Value);
            return $"{key}={value}";
        }));
    }

    private static string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
