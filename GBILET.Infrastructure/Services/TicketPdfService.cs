using GBILET.Core.Models.Ticket;
using GBILET.Core.Service.Ticket;
using Microsoft.AspNetCore.Hosting;
using SkiaSharp;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GBILET.Infrastructure.Services;

public class TicketPdfService : ITicketPdfService
{
    private const string RedColor = "#DC2626";
    private const string DarkColor = "#1a1a1a";
    private const string GrayColor = "#6B7280";
    private const string LightGrayColor = "#F5F5F5";

    private static readonly Dictionary<string, string> AirlineColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "TK", "#E31E24" }, { "PC", "#FFD200" }, { "XQ", "#FF6600" },
        { "AJ", "#003DA5" }, { "VF", "#00529B" }, { "6Y", "#003DA5" },
        { "KK", "#005F9E" },
    };

    // In-memory cache for downloaded airline logos (per service lifetime)
    private readonly Dictionary<string, byte[]?> _logoCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _airlineLogoBasePath;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public TicketPdfService(IWebHostEnvironment env)
    {
        _airlineLogoBasePath = Path.Combine(env.WebRootPath ?? "", "images", "airlines");
    }

    public byte[] GeneratePdf(List<TicketPdfDataDto> passengers)
    {
        // Pre-download all airline logos before generating PDF
        var airlineCodes = passengers
            .SelectMany(p => p.Flights)
            .Select(f => f.AirlineCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var code in airlineCodes)
            GetAirlineLogo(code);

        var document = Document.Create(container =>
        {
            foreach (var data in passengers)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(25);
                    page.MarginBottom(20);
                    page.MarginLeft(30);
                    page.MarginRight(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(header => ComposeHeader(header, data));
                    page.Content().Element(content => ComposeContent(content, data));
                    page.Footer().Element(ComposeFooter);
                });
            }
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// Get airline logo: first check local file, then download from Kiwi CDN, cache result
    /// </summary>
    private byte[]? GetAirlineLogo(string airlineCode)
    {
        if (string.IsNullOrWhiteSpace(airlineCode)) return null;

        var upperCode = airlineCode.ToUpperInvariant();

        // Check cache first
        if (_logoCache.TryGetValue(upperCode, out var cached))
            return cached;

        byte[]? logoBytes = null;

        // 1. Try local file: wwwroot/images/airlines/{CODE}.png
        var localPath = Path.Combine(_airlineLogoBasePath, $"{upperCode}.png");
        if (File.Exists(localPath))
        {
            logoBytes = File.ReadAllBytes(localPath);
        }
        else
        {
            // 2. Download from Aviasales CDN
            try
            {
                var url = $"https://pics.avs.io/64/64/{upperCode}.png";
                var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    logoBytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

                    // Save locally for future use (best effort)
                    try
                    {
                        Directory.CreateDirectory(_airlineLogoBasePath);
                        File.WriteAllBytes(localPath, logoBytes);
                    }
                    catch { /* ignore save errors */ }
                }
            }
            catch { /* network error — fall back to badge */ }
        }

        _logoCache[upperCode] = logoBytes;
        return logoBytes;
    }

    // ═══════════════════════════════════════════════════════════
    //  HEADER
    // ═══════════════════════════════════════════════════════════

    private static void ComposeHeader(IContainer container, TicketPdfDataDto data)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().AlignMiddle().Text(text =>
                {
                    text.Span("Ata").Bold().FontSize(24).FontColor(RedColor);
                    text.Span("Bilet").Bold().FontSize(24).FontColor(DarkColor);
                    text.Span(".com").FontSize(14).FontColor(GrayColor);
                });

                row.RelativeItem().AlignRight().AlignMiddle().Column(info =>
                {
                    info.Item().AlignRight().Text("Zlatna Rota Turizm Seyahat Acentasi").Bold().FontSize(9);
                    info.Item().PaddingTop(2).AlignRight().Text(text =>
                    {
                        text.Span("TURSAB: 18474").FontSize(8).FontColor(GrayColor);
                        text.Span("  |  ").FontSize(8).FontColor("#D1D5DB");
                        text.Span("destek@atabilet.com").FontSize(8).FontColor(GrayColor);
                    });
                    info.Item().PaddingTop(2).AlignRight()
                        .Text("Acil Durum Bilet Hatti: 0532 015 26 38").FontSize(8).FontColor(GrayColor);
                });
            });

            column.Item().PaddingVertical(6).LineHorizontal(2).LineColor(RedColor);
            column.Item().AlignCenter().Text("E-BILET / E-TICKET").Bold().FontSize(14);
            column.Item().PaddingBottom(8);
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  CONTENT
    // ═══════════════════════════════════════════════════════════

    private void ComposeContent(IContainer container, TicketPdfDataDto data)
    {
        container.Column(column =>
        {
            column.Item().Element(c => ComposePassengerInfo(c, data));
            column.Item().PaddingVertical(8);
            column.Item().Element(c => ComposeFlightInfo(c, data));
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  PASSENGER + PRICE INFO
    // ═══════════════════════════════════════════════════════════

    private static void ComposePassengerInfo(IContainer container, TicketPdfDataDto data)
    {
        container.Border(1).BorderColor("#E5E7EB").Background(LightGrayColor).Padding(12).Row(row =>
        {
            // Left — passenger
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("YOLCU / PASSENGER").Bold().FontSize(8).FontColor(GrayColor);
                col.Item().PaddingTop(2).Text(data.PassengerName).Bold().FontSize(11);

                col.Item().PaddingTop(8).Text("PNR NUMARASI / PNR NUMBER").Bold().FontSize(8).FontColor(GrayColor);
                col.Item().PaddingTop(2).Text(data.Pnr).Bold().FontSize(11);

                col.Item().PaddingTop(8).Text("BILET NO / TICKET NUMBER").Bold().FontSize(8).FontColor(GrayColor);
                col.Item().PaddingTop(2).Text(data.TicketNumber).FontSize(10);

                col.Item().PaddingTop(8).Text("BILET OLUSTURMA TARIHI / TICKET ISSUE DATE").Bold().FontSize(8).FontColor(GrayColor);
                col.Item().PaddingTop(2).Text(FormatDateTurkish(data.IssueDate)).FontSize(10);

                if (!string.IsNullOrWhiteSpace(data.TcNo))
                {
                    col.Item().PaddingTop(8).Text("TC KIMLIK NO / ID NUMBER").Bold().FontSize(8).FontColor(GrayColor);
                    col.Item().PaddingTop(2).Text(data.TcNo).FontSize(10);
                }

                if (data.IsInternational)
                {
                    col.Item().PaddingTop(8).Text("PASAPORT NO / PASSPORT NO").Bold().FontSize(8).FontColor(GrayColor);
                    col.Item().PaddingTop(2).Text(!string.IsNullOrWhiteSpace(data.PassportNo) ? data.PassportNo : "-").FontSize(10);

                    if (!string.IsNullOrWhiteSpace(data.PassportCountry))
                    {
                        col.Item().PaddingTop(6).Text("PASAPORT ULKESI / PASSPORT COUNTRY").Bold().FontSize(8).FontColor(GrayColor);
                        col.Item().PaddingTop(2).Text(data.PassportCountry).FontSize(10);
                    }
                }
            });

            row.ConstantItem(15);

            // Right — price
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("UCRET BILGILERI / PRICE INFO").Bold().FontSize(8).FontColor(GrayColor);

                foreach (var item in data.FareItems)
                {
                    col.Item().PaddingTop(6).Text(item.Route).FontSize(7).FontColor("#374151");
                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text("Bilet Ucreti / Ticket Fare").FontSize(9);
                        r.AutoItem().AlignRight().Text($"{item.Amount:N2} {item.Currency}").FontSize(9);
                    });
                }

                col.Item().PaddingTop(6);

                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("Esas Ucret / Base Fare").FontSize(9);
                    r.AutoItem().AlignRight().Text($"{data.BaseFare:N2} {data.Currency}").FontSize(9);
                });

                col.Item().PaddingTop(3).Row(r =>
                {
                    r.RelativeItem().Text("Vergiler ve Diger Ucretler / Taxes & Fees").FontSize(9);
                    r.AutoItem().AlignRight().Text($"{data.Taxes:N2} {data.Currency}").FontSize(9);
                });

                col.Item().PaddingVertical(6).LineHorizontal(1).LineColor("#D1D5DB");

                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("TOPLAM TUTAR / TOTAL FARE").Bold().FontSize(10);
                    r.AutoItem().AlignRight().Text($"{data.TotalFare:N2} {data.Currency}").Bold().FontSize(12).FontColor(RedColor);
                });
            });
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  FLIGHT INFO — redesigned cards
    // ═══════════════════════════════════════════════════════════

    private void ComposeFlightInfo(IContainer container, TicketPdfDataDto data)
    {
        container.Column(column =>
        {
            column.Item().Text("UCUS BILGILERI / FLIGHT INFO").Bold().FontSize(11);
            column.Item().PaddingTop(5);

            for (var i = 0; i < data.Flights.Count; i++)
            {
                var flight = data.Flights[i];

                if (i > 0)
                    column.Item().PaddingTop(6);

                var flightLabel = i == 0 ? "GIDIS UCUSU / OUTBOUND" : "DONUS UCUSU / RETURN";

                column.Item().ShowEntire().Border(1).BorderColor("#E5E7EB").Column(card =>
                {
                    // ── 1. Dark bar: GIDIS / DONUS label only ──
                    card.Item().Background(DarkColor).PaddingVertical(6)
                        .AlignCenter()
                        .Text(flightLabel).Bold().FontSize(9).FontColor("#FFFFFF");

                    // ── 2. Airline row: logo + name + flight code (white bg) ──
                    card.Item().Background("#FFFFFF").BorderBottom(1).BorderColor("#E5E7EB")
                        .PaddingHorizontal(12).PaddingVertical(8).Row(airlineRow =>
                    {
                        var logoBytes = GetAirlineLogo(flight.AirlineCode);

                        if (logoBytes != null)
                        {
                            airlineRow.ConstantItem(36).Height(36)
                                .Image(logoBytes).FitArea();
                        }
                        else
                        {
                            var clr = AirlineColors.GetValueOrDefault(flight.AirlineCode, "#4B5563");
                            airlineRow.ConstantItem(36).Height(36).AlignCenter().AlignMiddle()
                                .Background(clr).Padding(2)
                                .AlignCenter().AlignMiddle()
                                .Text(flight.AirlineCode).Bold().FontSize(11).FontColor("#FFFFFF");
                        }

                        airlineRow.ConstantItem(8);

                        airlineRow.RelativeItem().AlignMiddle().Column(hCol =>
                        {
                            hCol.Item().Text(flight.AirlineName).Bold().FontSize(10).FontColor(DarkColor);
                            hCol.Item().Text($"{flight.FlightCode} - {flight.BookingClass}").FontSize(8).FontColor(GrayColor);
                        });

                        if (!string.IsNullOrWhiteSpace(flight.FareBasisName))
                        {
                            airlineRow.AutoItem().AlignMiddle().AlignRight()
                                .Background("#F3F4F6").Padding(4)
                                .Text(flight.FareBasisName).FontSize(8).FontColor(DarkColor);
                        }
                    });

                    // ── 3. Flight times: departure — arc connector — arrival ──
                    card.Item().Background("#FFFFFF").PaddingHorizontal(12).PaddingVertical(10).Row(bodyRow =>
                    {
                        // Departure
                        bodyRow.RelativeItem().Column(dep =>
                        {
                            dep.Item().Text("KALKIS / DEPARTURE").FontSize(7).FontColor(GrayColor);
                            dep.Item().PaddingTop(3).Text(flight.DepartureTime).Bold().FontSize(16).FontColor(DarkColor);
                            dep.Item().PaddingTop(2).Text(flight.OriginCity).Bold().FontSize(9);
                            dep.Item().Text($"{flight.OriginAirport} ({flight.OriginCode})").FontSize(7).FontColor(GrayColor);
                            dep.Item().PaddingTop(2).Text(flight.DepartureDate).FontSize(8).FontColor(GrayColor);
                        });



                        // Arrival
                        bodyRow.RelativeItem().AlignRight().Column(arr =>
                        {
                            arr.Item().AlignRight().Text("VARIS / ARRIVAL").FontSize(7).FontColor(GrayColor);
                            arr.Item().PaddingTop(3).AlignRight().Text(flight.ArrivalTime).Bold().FontSize(16).FontColor(DarkColor);
                            arr.Item().PaddingTop(2).AlignRight().Text(flight.DestinationCity).Bold().FontSize(9);
                            arr.Item().AlignRight().Text($"{flight.DestinationAirport} ({flight.DestinationCode})").FontSize(7).FontColor(GrayColor);
                            arr.Item().PaddingTop(2).AlignRight().Text(flight.ArrivalDate).FontSize(8).FontColor(GrayColor);
                        });
                    });

                    // ── 4. Footer: baggage ──
                    card.Item().Background(LightGrayColor).PaddingHorizontal(12).PaddingVertical(5).Row(footRow =>
                    {
                        footRow.RelativeItem().Text(text =>
                        {
                            text.Span("Bagaj / Baggage: ").FontSize(8).FontColor(GrayColor);
                            var bag = flight.BaggageAllowance?.Trim();
                            var baggageText = string.IsNullOrWhiteSpace(bag) || bag == "-" || bag == "\u2014" || bag == "--"
                                ? "Bagaj Yok"
                                : bag;
                            text.Span(baggageText).Bold().FontSize(8);
                        });
                    });
                });
            }
        });
    }



    // ═══════════════════════════════════════════════════════════
    //  FOOTER
    // ═══════════════════════════════════════════════════════════

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor("#D1D5DB");
            column.Item().PaddingTop(4);

            column.Item().Text("Genel Kurallar ve Bilgilendirmeler").Bold().FontSize(7).FontColor(GrayColor);
            column.Item().PaddingTop(3);

            column.Item().Text(TicketConstants.DisclaimerTR).Bold().FontSize(6).FontColor("#4B5563").LineHeight(1.2f);
            column.Item().PaddingTop(3);

            column.Item().Text(TicketConstants.DisclaimerEN).Bold().FontSize(6).FontColor("#4B5563").LineHeight(1.2f);
            column.Item().PaddingTop(4);

            column.Item().Text("Bilgi amaclidir, fatura yerine gecmez.")
                .FontSize(7).FontColor(RedColor).Italic();
        });
    }

    private static string FormatDateTurkish(DateTime date)
    {
        var months = new[]
        {
            "", "Ocak", "Subat", "Mart", "Nisan", "Mayis", "Haziran",
            "Temmuz", "Agustos", "Eylul", "Ekim", "Kasim", "Aralik"
        };
        return $"{date.Day} {months[date.Month]} {date.Year}";
    }
}