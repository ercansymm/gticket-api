using System.Globalization;
using GBILET.Core.DTOs.Payment;
using GBILET.Core.Entities;
using GBILET.Core.Helpers;
using GBILET.Core.Interfaces;
using GBILET.Core.Models.Flight;
using GBILET.Core.Service;
using GBILET.Core.Service.Flight;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GBILET.Infrastructure.Services;

/// <summary>
/// Odeme orkestrasyon servisi. BiletBank MakePayment / Complete3DPayment cagrilarini sarar
/// ve ayni transaction icinde Payment + Booking + BookingLog tablolarina yazar.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IFlightService _flightService;
    private readonly IBookingRepository _bookingRepository;
    private readonly GTicketDbContext _db;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IFlightService flightService,
        IBookingRepository bookingRepository,
        GTicketDbContext db,
        ILogger<PaymentService> logger)
    {
        _flightService = flightService;
        _bookingRepository = bookingRepository;
        _db = db;
        _logger = logger;
    }

    public async Task<PaymentProcessResult> ProcessPaymentAsync(PaymentProcessRequest request, CancellationToken ct = default)
    {
        if (request?.Payment == null)
            throw new ArgumentNullException(nameof(request), "PaymentProcessRequest.Payment null olamaz.");

        var pay = request.Payment;

        // 3D callback URL'ini olustur — BiletBank'a verilen ContinueUrl bunu kullanir.
        // Frontend ContinueUrl gondermis olsa bile backend kendi callback URL'i ile override eder.
        // Aksi halde Lidio direkt frontend'e doner ve Complete3DPayment cagrilmaz; odeme onaysiz kalir.
        if (!string.IsNullOrEmpty(request.CallbackBaseUrl))
        {
            pay.ContinueUrl = $"{request.CallbackBaseUrl}" +
                              $"?sid={Uri.EscapeDataString(pay.SessionId ?? "")}" +
                              $"&stk={Uri.EscapeDataString(pay.SessionToken ?? "")}" +
                              $"&sfid={Uri.EscapeDataString(pay.ShoppingFileId ?? "")}" +
                              $"&bid={pay.BookingId}";
        }

        MakePaymentResponse bbResponse;
        Exception? caught = null;
        try
        {
            bbResponse = await _flightService.MakePaymentAsync(pay);
        }
        catch (Exception ex)
        {
            caught = ex;
            _logger.LogError(ex, "[PaymentService] BiletBank MakePayment exception. BookingId={BookingId}", pay.BookingId);
            bbResponse = new MakePaymentResponse
            {
                HasError = true,
                ErrorMessage = $"MakePayment exception: {ex.Message}"
            };
        }

        // ── Payment kaydi ── (basari, hata veya 3D-bekliyor — her durumda yaz)
        var payment = BuildPaymentEntity(pay, bbResponse);
        _db.Payments.Add(payment);

        // Booking'i (mumkunse) cek
        Booking? booking = null;
        if (pay.BookingId.HasValue && pay.BookingId.Value != Guid.Empty)
        {
            booking = await _db.Bookings
                .Include(b => b.Passengers)
                .Include(b => b.FlightSegments)
                .FirstOrDefaultAsync(b => b.Id == pay.BookingId.Value, ct);
        }

        var logOp = "MakePayment";
        var result = new PaymentProcessResult
        {
            HasError = bbResponse.HasError,
            ErrorMessage = bbResponse.ErrorMessage,
            IsPaymentSuccessful = bbResponse.IsPaymentSuccessful,
            Status = bbResponse.Status,
            ShoppingFileId = bbResponse.ShoppingFileId,
            RemainingSum = bbResponse.RemainingSum,
            Currency = bbResponse.Currency,
            PaymentReferenceId = bbResponse.PaymentReferenceId,
            PNR = bbResponse.PNR,
            BookingStatus = bbResponse.BookingStatus,
            RunningAccountBalance = bbResponse.RunningAccountBalance,
            GrandTotal = bbResponse.GrandTotal,
            ThreeDSecureUrl = bbResponse.ThreeDSecureUrl,
            Is3DSecureRequired = bbResponse.Is3DSecureRequired,
            ThreeDSecureHtml = bbResponse.ThreeDSecureHtml,
            InstallmentOptions = bbResponse.InstallmentOptions ?? new(),
            PaymentId = payment.Id
        };

        // Hata var
        if (bbResponse.HasError)
        {
            payment.Status = "Failed";
            if (booking != null)
            {
                booking.LastError = Truncate(bbResponse.ErrorMessage, 500);
                booking.Status = "PaymentFailed";
                booking.PaymentAttemptCount++;
                booking.UpdatedAt = DateTime.UtcNow;
            }
            _db.BookingLogs.Add(BuildLog(pay.BookingId, pay.SessionId, pay.SessionToken,
                logOp, isSuccess: false, error: Truncate(bbResponse.ErrorMessage, 500)));

            await _db.SaveChangesAsync(ct);
            return result;
        }

        // 3D bekliyor (HTML / RedirectUrl geldiyse, IsPaymentSuccessful=false)
        if (bbResponse.Is3DSecureRequired || (!bbResponse.IsPaymentSuccessful &&
            (!string.IsNullOrEmpty(bbResponse.ThreeDSecureHtml) || !string.IsNullOrEmpty(bbResponse.ThreeDSecureUrl))))
        {
            payment.Status = "Pending3D";
            payment.Is3DSecure = true;
            payment.RedirectUrl = bbResponse.ThreeDSecureUrl;
            _db.BookingLogs.Add(BuildLog(pay.BookingId, pay.SessionId, pay.SessionToken,
                "MakePayment_Init3D", isSuccess: true, error: null));

            await _db.SaveChangesAsync(ct);
            return result;
        }

        // Basarili (3D'siz dogrudan tamam)
        if (bbResponse.IsPaymentSuccessful)
        {
            payment.Status = "Success";
            if (booking != null)
            {
                booking.Status = "Paid";
                booking.PaidAt = DateTime.UtcNow;
                booking.UpdatedAt = DateTime.UtcNow;
                booking.LastError = null;
            }
            _db.BookingLogs.Add(BuildLog(pay.BookingId, pay.SessionId, pay.SessionToken,
                logOp, isSuccess: true, error: null));

            // Auto-finalize: 3D gerektirmeyen odemelerde biletlemeyi backend'de yap
            if (!string.IsNullOrEmpty(pay.ProductId))
            {
                await TryAutoFinalizeAsync(pay.SessionId, pay.SessionToken, pay.ShoppingFileId,
                    pay.ProductId, pay.BookingId, pay.BillingInfo, booking, result, ct);
            }

            await _db.SaveChangesAsync(ct);
            return result;
        }

        // Beklenmeyen durum — yine de sonuc don
        payment.Status = "Failed";
        _db.BookingLogs.Add(BuildLog(pay.BookingId, pay.SessionId, pay.SessionToken,
            logOp, isSuccess: false, error: Truncate(bbResponse.ErrorMessage, 500) ?? "Bilinmeyen odeme durumu."));
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<PaymentProcessResult> ProcessComplete3DAsync(Complete3DRequest request, CancellationToken ct = default)
    {
        if (request?.Payment == null)
            throw new ArgumentNullException(nameof(request), "Complete3DRequest.Payment null olamaz.");

        var pay = request.Payment;

        // BiletBank'in Lidio gateway'i 3DS'i kendi tamamlar; callback'te Approved/Ok=true geldiyse
        // ayrica MakePayment_Complete3DPayment SOAP cagrisina gerek yok (zaten servis bu action'i
        // ContractFilter mismatch ile reddediyor). Sadece callback parametrelerini degerlendiriyoruz.
        var bankParams = pay.BankResponseParameters ?? new Dictionary<string, string>();
        var isApproved = ParseBoolParam(bankParams, "Approved") || ParseBoolParam(bankParams, "Ok");
        var isFail = ParseBoolParam(bankParams, "Fail");
        var bankErrorMessage = bankParams.TryGetValue("ErrorMessage", out var em) ? em : null;
        var bankPaymentId = bankParams.TryGetValue("PaymentId", out var pid) ? pid : null;

        var bbResponse = new MakePaymentResponse
        {
            HasError = !isApproved || isFail,
            IsPaymentSuccessful = isApproved && !isFail,
            ErrorMessage = (!isApproved || isFail)
                ? (bankErrorMessage ?? "3D Secure dogrulamasi onaylanmadi.")
                : null,
            PaymentReferenceId = bankPaymentId,
            ShoppingFileId = pay.ShoppingFileId
        };

        _logger.LogInformation(
            "[PaymentService] Complete3D callback evaluation. BookingId={BookingId}, Approved={Approved}, Fail={Fail}, IsSuccessful={IsSuccessful}",
            pay.BookingId, isApproved, isFail, bbResponse.IsPaymentSuccessful);

        // Bu booking icin Pending3D olan Payment kaydini bul, yoksa yeni olustur.
        Payment payment = await FindOrCreatePaymentForBookingAsync(pay.BookingId, pay.ShoppingFileId, bbResponse, ct);

        Booking? booking = null;
        if (pay.BookingId.HasValue && pay.BookingId.Value != Guid.Empty)
        {
            booking = await _db.Bookings
                .Include(b => b.Passengers)
                .Include(b => b.FlightSegments)
                .FirstOrDefaultAsync(b => b.Id == pay.BookingId.Value, ct);
        }

        // Idempotency: Lidio bazen ayni callback'i iki kez tetikler (GET redirect + POST notify),
        // kullanici sayfayi yenileyebilir veya "Tekrar Dene" ile tekrar gelebilir. Bu Payment
        // daha onceki cagride basariyla isleme alindiysa, ayni state'i dondur ve EF concurrency
        // hatasina sebep olabilecek tekrar update'lerden kacin.
        if (string.Equals(payment.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "[PaymentService] Complete3D idempotent skip. Payment already Success. BookingId={BookingId}, PaymentId={PaymentId}",
                pay.BookingId, payment.Id);
            return new PaymentProcessResult
            {
                HasError = false,
                IsPaymentSuccessful = true,
                Status = booking?.Status ?? "Paid",
                ShoppingFileId = pay.ShoppingFileId,
                PaymentReferenceId = payment.BiletBankPaymentId,
                PNR = booking?.PNR,
                InternalPnr = booking?.InternalPnr,
                AutoFinalized = booking?.IsFinalized ?? false,
                PaymentId = payment.Id
            };
        }

        var result = new PaymentProcessResult
        {
            HasError = bbResponse.HasError,
            ErrorMessage = bbResponse.ErrorMessage,
            IsPaymentSuccessful = bbResponse.IsPaymentSuccessful,
            Status = bbResponse.Status,
            ShoppingFileId = bbResponse.ShoppingFileId ?? pay.ShoppingFileId,
            RemainingSum = bbResponse.RemainingSum,
            Currency = bbResponse.Currency,
            PaymentReferenceId = bbResponse.PaymentReferenceId,
            PNR = bbResponse.PNR,
            BookingStatus = bbResponse.BookingStatus,
            GrandTotal = bbResponse.GrandTotal,
            ThreeDSecureUrl = bbResponse.ThreeDSecureUrl,
            Is3DSecureRequired = bbResponse.Is3DSecureRequired,
            ThreeDSecureHtml = bbResponse.ThreeDSecureHtml,
            PaymentId = payment.Id
        };

        if (bbResponse.HasError || !bbResponse.IsPaymentSuccessful)
        {
            payment.Status = "Failed";
            payment.ErrorMessage = Truncate(bbResponse.ErrorMessage, 500);
            payment.ErrorCode = CategorizeErrorCode(bbResponse.ErrorMessage);
            UpdateRawXml(payment, bbResponse);

            if (booking != null)
            {
                booking.Status = "PaymentFailed";
                booking.LastError = Truncate(bbResponse.ErrorMessage, 500);
                booking.PaymentAttemptCount++;
                booking.UpdatedAt = DateTime.UtcNow;
            }
            _db.BookingLogs.Add(BuildLog(pay.BookingId, pay.SessionId, pay.SessionToken,
                "Complete3DPayment", isSuccess: false, error: Truncate(bbResponse.ErrorMessage, 500)));
            await _db.SaveChangesAsync(ct);
            return result;
        }

        // Basarili
        payment.Status = "Success";
        UpdateRawXml(payment, bbResponse);
        if (!string.IsNullOrEmpty(bbResponse.PaymentReferenceId))
            payment.BiletBankPaymentId = bbResponse.PaymentReferenceId;

        if (booking != null)
        {
            booking.Status = "Paid";
            booking.PaidAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;
            booking.LastError = null;

            // Odeme onaylandi → ATA PNR'i hemen burada uret. TryAutoFinalize asagida
            // calismasa bile (ProductId cache'ten dusmus, BB hata vermis vs.)
            // booking'imiz icin internal referans hazir olur. Cari akisi ile esit davranis.
            if (string.IsNullOrEmpty(booking.InternalPnr))
            {
                try
                {
                    var internalPnr = await PnrGenerator.GenerateUniqueAsync(_bookingRepository);
                    booking.InternalPnr = internalPnr;
                    result.InternalPnr = internalPnr;
                }
                catch (Exception pnrEx)
                {
                    _logger.LogWarning(pnrEx, "[PaymentService] InternalPnr uretme basarisiz; finalize akisinda tekrar denenecek. BookingId={BookingId}", booking.Id);
                }
            }
            else
            {
                result.InternalPnr = booking.InternalPnr;
            }
        }
        _db.BookingLogs.Add(BuildLog(pay.BookingId, pay.SessionId, pay.SessionToken,
            "Complete3DPayment", isSuccess: true, error: null));

        // Auto-finalize (controller eskiden 3d-callback icinde yapiyordu)
        if (!string.IsNullOrEmpty(request.ProductId) && booking?.IsFinalized != true)
        {
            await TryAutoFinalizeAsync(pay.SessionId, pay.SessionToken,
                pay.ShoppingFileId, request.ProductId, pay.BookingId, request.BillingInfo,
                booking, result, ct, refreshSegments: true);
        }
        else if (booking?.IsFinalized == true)
        {
            _logger.LogInformation("[PaymentService] Booking already finalized, skipping auto-finalize. BookingId={BookingId}", pay.BookingId);
            result.AutoFinalized = true;
            result.PNR = booking.PNR;
            result.InternalPnr = booking.InternalPnr;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException concEx)
        {
            // BiletBank tarafinda odeme zaten alindi (Lidio Approved=true). DB'de bir entity
            // baska bir cagri tarafindan degistirildiyse (cift callback / retry) tracker'i
            // detach edip kritik alanlari raw SQL ile zorla guncelliyoruz. Aksi halde Payment
            // "Pending3D" kalir ve admin panelde "Beklemede" gozukur.
            _logger.LogWarning(concEx,
                "[PaymentService] Complete3D SaveChanges concurrency conflict; forcing critical fields via raw SQL. BookingId={BookingId}",
                pay.BookingId);

            var newPnr = booking?.InternalPnr;
            var newPaymentRefId = payment.BiletBankPaymentId;

            // Tracker'i temizle (sonraki sorgular fresh olsun)
            foreach (var entry in _db.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;

            // 1) Payment row'unu Success'e cek (concurrency'den bagimsiz raw UPDATE)
            try
            {
                await _db.Payments
                    .Where(p => p.Id == payment.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Status, "Success")
                        .SetProperty(p => p.ErrorMessage, (string?)null)
                        .SetProperty(p => p.ErrorCode, (string?)null)
                        .SetProperty(p => p.BiletBankPaymentId, newPaymentRefId), ct);
            }
            catch (Exception ex1)
            {
                _logger.LogWarning(ex1, "[PaymentService] Payment force-update basarisiz. PaymentId={PaymentId}", payment.Id);
            }

            // 2) Booking durumunu ve InternalPnr'i zorla yaz
            if (pay.BookingId.HasValue && pay.BookingId.Value != Guid.Empty)
            {
                try
                {
                    // InternalPnr yoksa simdi uret
                    if (string.IsNullOrEmpty(newPnr))
                    {
                        try { newPnr = await PnrGenerator.GenerateUniqueAsync(_bookingRepository); }
                        catch { /* yine de devam */ }
                    }

                    await _db.Bookings
                        .Where(b => b.Id == pay.BookingId.Value)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(b => b.Status, b => b.Status == "Paid" || b.IsFinalized ? b.Status : "Paid")
                            .SetProperty(b => b.PaidAt, b => b.PaidAt ?? DateTime.UtcNow)
                            .SetProperty(b => b.InternalPnr, b => b.InternalPnr ?? newPnr)
                            .SetProperty(b => b.UpdatedAt, DateTime.UtcNow)
                            .SetProperty(b => b.LastError, (string?)null), ct);
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning(ex2, "[PaymentService] Booking force-update basarisiz. BookingId={BookingId}", pay.BookingId);
                }

                var fresh = await _db.Bookings.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == pay.BookingId.Value, ct);
                if (fresh != null)
                {
                    result.PNR = fresh.PNR;
                    result.InternalPnr = fresh.InternalPnr;
                    result.AutoFinalized = fresh.IsFinalized;
                    result.BookingStatus = fresh.Status;
                }
            }
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Postgres varchar(N) overflow'u onlemek icin string'i guvenle kirpar.</summary>
    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    /// <summary>Lidio callback bool parametresini parse eder ("true"/"True"/"1" -> true).</summary>
    private static bool ParseBoolParam(IDictionary<string, string> dict, string key)
    {
        if (dict == null) return false;
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                var v = kvp.Value?.Trim();
                if (string.IsNullOrEmpty(v)) return false;
                return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
            }
        }
        return false;
    }

    private Payment BuildPaymentEntity(MakePaymentRequest req, MakePaymentResponse resp)
    {
        var maskedCard = MaskCardNumber(req.CreditCard?.CardNumber);
        var lastFour = ExtractLastFour(req.CreditCard?.CardNumber);
        var rawReq = MaskRawXml(resp.RawSoapRequest);

        return new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = req.BookingId ?? Guid.Empty,
            Amount = req.Amount,
            Currency = req.Currency ?? "TRY",
            CardHolderName = req.CreditCard?.CardHolderName,
            CardHolder = req.CreditCard?.CardHolderName,
            MaskedCardNumber = maskedCard,
            CardLastFour = lastFour,
            InstallmentCount = 1, // BiletBank'tan donen taksit sayisi parse edilmiyor henuz
            Status = "Pending",
            TransactionDate = DateTime.UtcNow,
            Is3DSecure = req.PaymentType == "CreditCard",
            ProviderTransactionId = resp.PaymentReferenceId,
            BiletBankPaymentId = resp.PaymentReferenceId,
            PaymentType = req.PaymentType,
            RedirectUrl = resp.ThreeDSecureUrl,
            ErrorMessage = Truncate(resp.ErrorMessage, 500),
            ErrorCode = CategorizeErrorCode(resp.ErrorMessage),
            RawRequest = rawReq,
            RawResponse = resp.RawSoapResponse
        };
    }

    private async Task<Payment> FindOrCreatePaymentForBookingAsync(
        Guid? bookingId, string? shoppingFileId, MakePaymentResponse resp, CancellationToken ct)
    {
        if (bookingId.HasValue && bookingId.Value != Guid.Empty)
        {
            var existing = await _db.Payments
                .Where(p => p.BookingId == bookingId.Value && p.Status == "Pending3D")
                .OrderByDescending(p => p.TransactionDate)
                .FirstOrDefaultAsync(ct);
            if (existing != null)
                return existing;
        }

        var fresh = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId ?? Guid.Empty,
            Amount = resp.GrandTotal,
            Currency = resp.Currency ?? "TRY",
            Status = "Pending",
            Is3DSecure = true,
            TransactionDate = DateTime.UtcNow,
            ProviderTransactionId = resp.PaymentReferenceId,
            BiletBankPaymentId = resp.PaymentReferenceId,
            PaymentType = "CreditCard",
            ErrorMessage = Truncate(resp.ErrorMessage, 500),
            ErrorCode = CategorizeErrorCode(resp.ErrorMessage),
            RawRequest = MaskRawXml(resp.RawSoapRequest),
            RawResponse = resp.RawSoapResponse
        };
        _db.Payments.Add(fresh);
        return fresh;
    }

    private static void UpdateRawXml(Payment payment, MakePaymentResponse resp)
    {
        if (!string.IsNullOrEmpty(resp.RawSoapResponse))
            payment.RawResponse = resp.RawSoapResponse;
        if (!string.IsNullOrEmpty(resp.RawSoapRequest))
            payment.RawRequest = MaskRawXml(resp.RawSoapRequest);
    }

    private async Task TryAutoFinalizeAsync(
        string? sessionId,
        string? sessionToken,
        string? shoppingFileId,
        string productId,
        Guid? bookingId,
        ShoppingBillingInfo? billingInfo,
        Booking? booking,
        PaymentProcessResult result,
        CancellationToken ct,
        bool refreshSegments = false)
    {
        try
        {
            var finalize = await _flightService.FinalizeShoppingAsync(new FinalizeShoppingRequest
            {
                SessionId = sessionId ?? "",
                SessionToken = sessionToken ?? "",
                ShoppingFileId = shoppingFileId ?? "",
                ProductId = productId,
                BookingId = bookingId,
                BillingInfo = billingInfo
            });

            // Cari odeme akisindaki FinalizeShopping endpoint'i ile ayni semantik:
            // Sadece HasError=false yetmez \u2014 Status'un da basarili biletleme degerlerinden
            // birisi olmasi sart. Aksi halde \"Pending\"/\"PartialBooking\" gibi ara durumlar
            // hatali sekilde finalized olarak isaretlenir.
            var successStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Booking", "Ticketed", "Reservation" };
            var isFinalized = !finalize.HasError
                && !string.IsNullOrEmpty(finalize.Status)
                && successStatuses.Contains(finalize.Status);

            _logger.LogInformation(
                "[PaymentService] Auto-finalize result. BookingId={BookingId}, HasError={HasError}, Status={Status}, IsFinalized={IsFinalized}, Error={Error}",
                bookingId, finalize.HasError, finalize.Status, isFinalized, finalize.ErrorMessage);

            if (!isFinalized)
            {
                _logger.LogWarning("[PaymentService] Auto-finalize basarisiz veya status uygunsuz: {Error}", finalize.ErrorMessage);
                _db.BookingLogs.Add(BuildLog(bookingId, sessionId, sessionToken,
                    "FinalizeShopping_Auto", isSuccess: false,
                    error: Truncate(finalize.ErrorMessage ?? $"Status '{finalize.Status}' biletleme icin uygun degil.", 500)));
                return;
            }

            result.AutoFinalized = true;
            result.FinalizeStatus = finalize.Status;
            result.PNR = finalize.BookingCode ?? result.PNR;
            result.Tickets = finalize.Tickets ?? new();

            if (booking == null) return;

            // ReadShoppingFile ile segmentleri tazele (RT akiminda donus segmentleri eksik gelebilir)
            if (refreshSegments)
            {
                try
                {
                    var read = await _flightService.ReadShoppingFileAsync(new ReadShoppingFileRequest
                    {
                        SessionId = sessionId ?? "",
                        SessionToken = sessionToken ?? "",
                        ShoppingFileId = shoppingFileId ?? ""
                    });

                    if (!read.HasError && read.Segments?.Count > 0)
                    {
                        // Eski segment'leri EF'e acikca Deleted olarak isaretle (Clear() bazen
                        // sadece collection'dan cikarir, FK update'i deniyor olabilir).
                        if (booking.FlightSegments.Count > 0)
                        {
                            _db.FlightSegments.RemoveRange(booking.FlightSegments.ToList());
                            booking.FlightSegments.Clear();
                        }
                        foreach (var seg in read.Segments)
                        {
                            booking.FlightSegments.Add(new GBILET.Core.Entities.FlightSegment
                            {
                                Id = Guid.NewGuid(),
                                BookingId = booking.Id,
                                SequenceNo = booking.FlightSegments.Count + 1,
                                MarketingAirline = seg.MarketingAirline ?? "",
                                FlightNumber = seg.FlightNumber ?? "",
                                OriginCode = seg.OriginCode ?? "",
                                DestinationCode = seg.DestinationCode ?? "",
                                DepartureDate = DateTime.TryParse(seg.DepartureDay, out var rd) ? rd : DateTime.MinValue,
                                DepartureTime = seg.DepartureTime,
                                ArrivalDate = DateTime.TryParse(seg.ArrivalDay, out var ra) ? ra : null,
                                ArrivalTime = seg.ArrivalTime,
                                BookingClass = seg.BookingClass
                            });
                        }
                    }
                }
                catch (Exception readEx)
                {
                    _logger.LogWarning(readEx, "[PaymentService] ReadShoppingFile (refresh) basarisiz, mevcut segmentler korundu.");
                }
            }

            booking.Status = finalize.Status ?? "Ticketed";
            booking.IsFinalized = true;
            booking.TicketedAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(finalize.BookingCode))
                booking.PNR = finalize.BookingCode;

            // InternalPnr (varsa korunur, yoksa uretilir)
            if (string.IsNullOrEmpty(booking.InternalPnr))
            {
                var internalPnr = await PnrGenerator.GenerateUniqueAsync(_bookingRepository);
                booking.InternalPnr = internalPnr;
                result.InternalPnr = internalPnr;
            }
            else
            {
                result.InternalPnr = booking.InternalPnr;
            }

            // Bilet numaralarini ilgili yolcuya bagla
            foreach (var ticket in finalize.Tickets ?? new())
            {
                var pax = booking.Passengers.FirstOrDefault(p =>
                    string.Equals(p.FirstName, ticket.FirstName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.LastName, ticket.LastName, StringComparison.OrdinalIgnoreCase));
                if (pax != null)
                    pax.TicketNumber = ticket.TicketNumber;
            }

            // BB FinalizeShopping bazi rotalar/statulerde Tickets listesini bos donebiliyor
            // (ozellikle Status=Reservation). Bu durumda ReadShoppingFile ile acik bilet
            // numaralarini cek ve yolculara isle.
            var anyTicketAssigned = booking.Passengers.Any(p => !string.IsNullOrEmpty(p.TicketNumber));
            if (!anyTicketAssigned)
            {
                try
                {
                    var read = await _flightService.ReadShoppingFileAsync(new ReadShoppingFileRequest
                    {
                        SessionId = sessionId ?? "",
                        SessionToken = sessionToken ?? "",
                        ShoppingFileId = shoppingFileId ?? ""
                    });

                    if (!read.HasError)
                    {
                        // Once Tickets listesinden dene
                        foreach (var t in read.Tickets ?? new())
                        {
                            if (string.IsNullOrEmpty(t.TicketNumber)) continue;
                            var pax = booking.Passengers.FirstOrDefault(p =>
                                string.Equals(p.FirstName, t.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(p.LastName, t.LastName, StringComparison.OrdinalIgnoreCase));
                            if (pax != null && string.IsNullOrEmpty(pax.TicketNumber))
                                pax.TicketNumber = t.TicketNumber;
                        }

                        // Yedek: Passengers[].TicketNumber alanindan dene
                        foreach (var rp in read.Passengers ?? new())
                        {
                            if (string.IsNullOrEmpty(rp.TicketNumber)) continue;
                            var pax = booking.Passengers.FirstOrDefault(p =>
                                string.Equals(p.FirstName, rp.FirstName, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(p.LastName, rp.LastName, StringComparison.OrdinalIgnoreCase));
                            if (pax != null && string.IsNullOrEmpty(pax.TicketNumber))
                                pax.TicketNumber = rp.TicketNumber;
                        }

                        // Tickets result'a da ekle (frontend'in PDF butonu ve ozet icin)
                        if ((result.Tickets == null || result.Tickets.Count == 0) && read.Tickets?.Count > 0)
                            result.Tickets = read.Tickets;
                    }
                }
                catch (Exception readEx)
                {
                    _logger.LogWarning(readEx, "[PaymentService] ReadShoppingFile (ticket fallback) basarisiz. BookingId={BookingId}", booking.Id);
                }
            }

            _db.BookingLogs.Add(BuildLog(bookingId, sessionId, sessionToken,
                "FinalizeShopping_Auto", isSuccess: true, error: null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PaymentService] Auto-finalize exception. BookingId={BookingId}", bookingId);
            _db.BookingLogs.Add(BuildLog(bookingId, sessionId, sessionToken,
                "FinalizeShopping_Auto", isSuccess: false, error: ex.Message));
        }
    }

    private static BookingLog BuildLog(Guid? bookingId, string? sessionId, string? sessionToken,
        string operation, bool isSuccess, string? error)
    {
        return new BookingLog
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            SessionId = sessionId,
            SessionToken = sessionToken,
            Operation = operation,
            IsSuccess = isSuccess,
            ErrorMessage = error,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<bool> MarkLatestPendingPaymentSuccessAsync(Guid bookingId, string? bbPaymentId, CancellationToken ct = default)
    {
        if (bookingId == Guid.Empty) return false;

        // Bu booking'in en yeni Pending3D / Pending Payment kaydini bul
        var pending = await _db.Payments
            .Where(p => p.BookingId == bookingId &&
                        (p.Status == "Pending3D" || p.Status == "Pending"))
            .OrderByDescending(p => p.TransactionDate)
            .FirstOrDefaultAsync(ct);

        if (pending == null) return false;

        pending.Status = "Success";
        pending.ErrorMessage = null;
        pending.ErrorCode = null;
        if (!string.IsNullOrEmpty(bbPaymentId))
            pending.BiletBankPaymentId = bbPaymentId;

        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("[PaymentService] Pending payment Success'e cekildi. BookingId={BookingId}, PaymentId={PaymentId}",
                bookingId, pending.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PaymentService] MarkLatestPendingPaymentSuccessAsync save failed. BookingId={BookingId}", bookingId);
            return false;
        }
    }

    /// <summary>
    /// Hata mesajini kategori koduna donusturur (filtre/raporlama icin).
    /// </summary>
    private static string? CategorizeErrorCode(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return null;

        var msg = errorMessage.ToLowerInvariant();

        if (msg.Contains("limit") || msg.Contains("yetersiz bakiye") || msg.Contains("insufficient"))
            return "CardLimit";

        if (msg.Contains("3d") || msg.Contains("şifre") || msg.Contains("sifre")
            || msg.Contains("sms") || msg.Contains("doğrulama") || msg.Contains("dogrulama"))
            return "ThreeDFailed";

        if (msg.Contains("reject") || msg.Contains("decline") || msg.Contains("reddedildi"))
            return "BankRejected";

        if (msg.Contains("timeout") || msg.Contains("zaman aşımı") || msg.Contains("zaman asimi")
            || msg.Contains("connection"))
            return "Timeout";

        var hasCardWord = msg.Contains("card") || msg.Contains("kart");
        var hasInvalidWord = msg.Contains("invalid") || msg.Contains("geçersiz") || msg.Contains("gecersiz");
        if (hasCardWord && hasInvalidWord)
            return "InvalidCard";

        return "Other";
    }

    /// <summary>
    /// "411111111111111" -> "411111******1111" formatina cevirir.
    /// 10 haneden kisa ise sadece son 4 hanesi gosterilir.
    /// </summary>
    private static string? MaskCardNumber(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return null;

        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
            return digits.Length >= 4 ? new string('*', digits.Length - 4) + digits[^4..] : new string('*', digits.Length);

        return digits[..6] + new string('*', digits.Length - 10) + digits[^4..];
    }

    private static string? ExtractLastFour(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return null;
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : null;
    }

    /// <summary>
    /// Raw SOAP XML icindeki hassas alanlari (CardNumber, CV2, CardHolder)
    /// loglamadan once maskele. CardHolder kismen tutulur (ilk 1 + soyad).
    /// </summary>
    private static string? MaskRawXml(string? xml)
    {
        if (string.IsNullOrEmpty(xml)) return xml;

        // <trev1:CardNumber>...</trev1:CardNumber>
        xml = System.Text.RegularExpressions.Regex.Replace(
            xml,
            @"(<\w*:?CardNumber>)([^<]+)(</\w*:?CardNumber>)",
            m => m.Groups[1].Value + MaskCardNumber(m.Groups[2].Value) + m.Groups[3].Value,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // <trev1:CV2>123</trev1:CV2>
        xml = System.Text.RegularExpressions.Regex.Replace(
            xml,
            @"(<\w*:?CV2>)([^<]+)(</\w*:?CV2>)",
            m => m.Groups[1].Value + "***" + m.Groups[3].Value,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // <trev1:Cvv>123</trev1:Cvv>
        xml = System.Text.RegularExpressions.Regex.Replace(
            xml,
            @"(<\w*:?Cvv>)([^<]+)(</\w*:?Cvv>)",
            m => m.Groups[1].Value + "***" + m.Groups[3].Value,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return xml;
    }
}
