using System.Text;
using GBILET.Core.Service.Sms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Services.Sms;

public class NetGsmSmsService : ISmsService
{
    private readonly HttpClient _http;
    private readonly ILogger<NetGsmSmsService> _logger;
    private readonly string _userCode;
    private readonly string _password;
    private readonly string _senderName;
    private const string ApiUrl = "https://api.netgsm.com.tr/sms/send/xml";

    public NetGsmSmsService(HttpClient http, IConfiguration config, ILogger<NetGsmSmsService> logger)
    {
        _http = http;
        _logger = logger;
        _userCode   = config["NetGsm:UserCode"]   ?? throw new InvalidOperationException("NetGsm:UserCode eksik");
        _password   = config["NetGsm:Password"]   ?? throw new InvalidOperationException("NetGsm:Password eksik");
        _senderName = config["NetGsm:SenderName"] ?? "ATABILET";
    }

    public async Task<bool> SendOtpAsync(string phone, string code, string purposeLabel, CancellationToken ct = default)
    {
        var message = $"Atabilet {purposeLabel} kodunuz: {code}. Kod 5 dakika gecerlidir. Paylasmayin.";
        return await SendAsync(NormalizePhone(phone), message, ct);
    }

    public async Task<bool> SendTicketConfirmationAsync(
        string phone,
        string passengerName,
        string pnr,
        string origin,
        string destination,
        DateTime departureTime,
        CancellationToken ct = default)
    {
        var date = departureTime.ToString("dd.MM.yyyy HH:mm");
        var message = $"Sayin {passengerName}, {date} tarihli {origin}-{destination} ucusunuzun bileti Atabilet tarafindan kesilmistir. PNR: {pnr}. Iletisim: info@atabilet.com veya web sitemiz uzerinden destek talebi acabilirsiniz. Iyi gunler.";
        return await SendAsync(NormalizePhone(phone), message, ct);
    }

    private async Task<bool> SendAsync(string gsm, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(gsm))
        {
            _logger.LogWarning("[NetGSM] Telefon numarası boş, SMS gönderilmedi.");
            return false;
        }

        var xml = BuildXml(gsm, message);
        try
        {
            var content = new StringContent(xml, Encoding.UTF8, "application/xml");
            var response = await _http.PostAsync(ApiUrl, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            // NetGSM başarılı yanıt olarak job ID (sayısal) döndürür; hata kodları 30, 20, 50 gibi sabitlerdir
            if (response.IsSuccessStatusCode && long.TryParse(body.Trim(), out _))
            {
                _logger.LogInformation("[NetGSM] SMS gönderildi. GSM={Gsm}, JobId={JobId}", Mask(gsm), body.Trim());
                return true;
            }

            _logger.LogWarning("[NetGSM] SMS gönderilemedi. GSM={Gsm}, StatusCode={Status}, Body={Body}",
                Mask(gsm), (int)response.StatusCode, body.Trim());
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NetGSM] SMS gönderimi sırasında hata. GSM={Gsm}", Mask(gsm));
            return false;
        }
    }

    private string BuildXml(string gsm, string message)
    {
        // SMS metni ASCII sınırlı tutulur; Türkçe karakterler transliterate edildi (mesaj şablonlarında)
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <mainbody>
              <header>
                <company dil="TR">Netgsm</company>
                <usercode>{_userCode}</usercode>
                <password>{_password}</password>
                <type>1:n</type>
                <msgheader>{_senderName}</msgheader>
              </header>
              <body>
                <msg><![CDATA[{message}]]></msg>
                <no>{gsm}</no>
              </body>
            </mainbody>
            """;
    }

    // NetGSM numarayı 5XXXXXXXXX formatında bekler (ülke kodu olmadan, başta 0 olmadan)
    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        // +905XXXXXXXXX → 5XXXXXXXXX
        if (digits.StartsWith("90") && digits.Length == 12)
            return digits[2..];
        // 05XXXXXXXXX → 5XXXXXXXXX
        if (digits.StartsWith("0") && digits.Length == 11)
            return digits[1..];
        return digits;
    }

    private static string Mask(string phone) =>
        phone.Length > 4 ? phone[..^4] + "****" : "****";
}
