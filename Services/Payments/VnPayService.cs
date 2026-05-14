using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChillTour.Services.Payments;

public class VnPayService : IVnPayService
{
    private readonly VnPayOptions _options;
    private readonly ILogger<VnPayService> _logger;

    public VnPayService(IOptions<VnPayOptions> options, ILogger<VnPayService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string CreatePaymentUrl(VnPayRequest request)
    {
        var createdAt = request.CreatedAtLocal == default
            ? GetVnPayNow()
            : request.CreatedAtLocal;

        var pay = new VnPayLibrary();
        pay.AddRequestData("vnp_Version", "2.1.0");
        pay.AddRequestData("vnp_Command", "pay");
        pay.AddRequestData("vnp_TmnCode", _options.TmnCode.Trim());
        pay.AddRequestData("vnp_Amount", ToVnPayAmount(request.Amount));
        pay.AddRequestData("vnp_CreateDate", createdAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
        pay.AddRequestData("vnp_CurrCode", "VND");
        pay.AddRequestData("vnp_IpAddr", NormalizeIpAddress(request.IpAddress));
        pay.AddRequestData("vnp_Locale", "vn");
        pay.AddRequestData("vnp_OrderInfo", request.OrderInfo.Trim());
        pay.AddRequestData("vnp_OrderType", "other");
        pay.AddRequestData("vnp_ReturnUrl", _options.ReturnUrl.Trim());
        pay.AddRequestData("vnp_TxnRef", request.TxnRef.Trim());

        var paymentUrl = pay.CreateRequestUrl(_options.PaymentUrl.Trim(), _options.HashSecret.Trim(), out var hashData, out var secureHash);

        _logger.LogInformation(
            "VNPay request created. TmnCode={TmnCode}, TxnRef={TxnRef}, Amount={Amount}, IpAddr={IpAddr}, HashData={HashData}, SecureHash={SecureHash}",
            _options.TmnCode.Trim(),
            request.TxnRef.Trim(),
            ToVnPayAmount(request.Amount),
            NormalizeIpAddress(request.IpAddress),
            hashData,
            secureHash);

        return paymentUrl;
    }

    public VnPayResult ParseResponse(IQueryCollection query)
    {
        var pay = new VnPayLibrary();
        foreach (var item in query)
        {
            if (!string.IsNullOrWhiteSpace(item.Key) && item.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
            {
                pay.AddResponseData(item.Key, item.Value.ToString());
            }
        }

        var receivedHash = query.FirstOrDefault(x => string.Equals(x.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase)).Value.ToString();
        var isValidSignature = pay.ValidateSignature(receivedHash, _options.HashSecret.Trim(), out var hashData, out var computedHash);

        var txnRef = pay.GetResponseData("vnp_TxnRef");
        var responseCode = pay.GetResponseData("vnp_ResponseCode");
        var transactionStatus = pay.GetResponseData("vnp_TransactionStatus");
        var transactionNo = pay.GetResponseData("vnp_TransactionNo");
        var orderInfo = pay.GetResponseData("vnp_OrderInfo");
        var amountRaw = pay.GetResponseData("vnp_Amount");

        _logger.Log(
            isValidSignature ? LogLevel.Information : LogLevel.Warning,
            "VNPay response parsed. TxnRef={TxnRef}, ResponseCode={ResponseCode}, TransactionStatus={TransactionStatus}, HashData={HashData}, ReceivedHash={ReceivedHash}, ComputedHash={ComputedHash}, IsValidSignature={IsValidSignature}",
            txnRef,
            responseCode,
            transactionStatus,
            hashData,
            receivedHash,
            computedHash,
            isValidSignature);

        return new VnPayResult
        {
            IsValidSignature = isValidSignature,
            IsSuccess = responseCode == "00" && transactionStatus == "00",
            TxnRef = txnRef,
            ResponseCode = responseCode,
            TransactionStatus = transactionStatus,
            TransactionNo = transactionNo,
            OrderInfo = orderInfo,
            Amount = long.TryParse(amountRaw, out var amount) ? amount : 0L,
            RawData = pay.ResponseData
        };
    }

    private static string ToVnPayAmount(decimal amount)
    {
        var value = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string NormalizeIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return "127.0.0.1";
        }

        if (!IPAddress.TryParse(ipAddress, out var parsedIp))
        {
            return ipAddress.Trim();
        }

        if (parsedIp.AddressFamily == AddressFamily.InterNetworkV6 && parsedIp.IsIPv4MappedToIPv6)
        {
            return parsedIp.MapToIPv4().ToString();
        }

        if (IPAddress.IsLoopback(parsedIp))
        {
            return "127.0.0.1";
        }

        return parsedIp.ToString();
    }

    private DateTime GetVnPayNow()
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(_options.TimeZoneId)
                ? "SE Asia Standard Time"
                : _options.TimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.Now;
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.Now;
        }
    }

    private sealed class VnPayLibrary
    {
        private readonly SortedList<string, string> _requestData = new(new VnPayCompare());
        private readonly SortedList<string, string> _responseData = new(new VnPayCompare());

        public IReadOnlyDictionary<string, string> ResponseData => _responseData.ToDictionary(x => x.Key, x => x.Value);

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _requestData[key] = value;
            }
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _responseData[key] = value;
            }
        }

        public string GetResponseData(string key)
        {
            return _responseData.TryGetValue(key, out var value) ? value : string.Empty;
        }

        public string CreateRequestUrl(string baseUrl, string hashSecret, out string signData, out string secureHash)
        {
            var data = new StringBuilder();
            foreach (var item in _requestData.Where(x => !string.IsNullOrEmpty(x.Value)))
            {
                data.Append(WebUtility.UrlEncode(item.Key));
                data.Append('=');
                data.Append(WebUtility.UrlEncode(item.Value));
                data.Append('&');
            }

            var queryString = data.ToString();
            signData = queryString;
            if (signData.Length > 0)
            {
                signData = signData.Remove(signData.Length - 1, 1);
            }

            secureHash = HmacSha512(hashSecret, signData);
            return $"{baseUrl}?{queryString}vnp_SecureHash={secureHash}";
        }

        public bool ValidateSignature(string inputHash, string secretKey, out string responseData, out string computedHash)
        {
            responseData = GetResponseData();
            computedHash = HmacSha512(secretKey, responseData);
            return computedHash.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        private string GetResponseData()
        {
            _responseData.Remove("vnp_SecureHashType");
            _responseData.Remove("vnp_SecureHash");

            var data = new StringBuilder();
            foreach (var item in _responseData.Where(x => !string.IsNullOrEmpty(x.Value)))
            {
                data.Append(WebUtility.UrlEncode(item.Key));
                data.Append('=');
                data.Append(WebUtility.UrlEncode(item.Value));
                data.Append('&');
            }

            if (data.Length > 0)
            {
                data.Remove(data.Length - 1, 1);
            }

            return data.ToString();
        }

        private static string HmacSha512(string key, string inputData)
        {
            var hash = new StringBuilder();
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);

            using var hmac = new HMACSHA512(keyBytes);
            var hashValue = hmac.ComputeHash(inputBytes);
            foreach (var theByte in hashValue)
            {
                hash.Append(theByte.ToString("x2", CultureInfo.InvariantCulture));
            }

            return hash.ToString();
        }
    }

    private sealed class VnPayCompare : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == y)
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var vnpCompare = CompareInfo.GetCompareInfo("en-US");
            return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
        }
    }
}
