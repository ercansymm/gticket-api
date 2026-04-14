using GBILET.Core.Models.Ticket;
using GBILET.Core.Service.Ticket;
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

    // Airline brand colors for circle logo
    private static readonly Dictionary<string, string> AirlineColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "TK", "#E31E24" },  // Turkish Airlines
        { "PC", "#FFD200" },  // Pegasus
        { "XQ", "#FF6600" },  // SunExpress
        { "AJ", "#003DA5" },  // AnadoluJet
        { "VF", "#00529B" },  // AJet
        { "6Y", "#003DA5" },  // SmartWings
        { "KK", "#005F9E" },  // AtlasGlobal
    };

    public byte[] GeneratePdf(List<TicketPdfDataDto> passengers)
    {
        var document = Document.Create(container =>
        {
            foreach (var data in passengers)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(30);
                    page.MarginBottom(30);
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

    private static void ComposeHeader(IContainer container, TicketPdfDataDto data)
    {
        container.Column(column =>
        {
            // Logo row + company info
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
                    info.Item().Text("Zlatna Rota Turizm Seyahat Acentasi").Bold().FontSize(9);
                    info.Item().PaddingTop(2).Text(text =>
                    {
                        text.Span("TURSAB: 18474").FontSize(8).FontColor(GrayColor);
                        text.Span("  |  ").FontSize(8).FontColor("#D1D5DB");
                        text.Span("destek@atabilet.com").FontSize(8).FontColor(GrayColor);
                    });
                    info.Item().PaddingTop(2).Text("Acil Durum Bilet Hatti: 0532 015 26 38").FontSize(8).FontColor(GrayColor);
                });
            });

            // Red separator line
            column.Item().PaddingVertical(8).LineHorizontal(2).LineColor(RedColor);

            // E-TICKET title
            column.Item().AlignCenter().Text("E-BILET / E-TICKET").Bold().FontSize(16);

            column.Item().PaddingBottom(10);
        });
    }

    private static void ComposeContent(IContainer container, TicketPdfDataDto data)
    {
        container.Column(column =>
        {
            // Passenger & Booking Info Section
            column.Item().Element(c => ComposePassengerInfo(c, data));

            column.Item().PaddingVertical(10);

            // Flight Info Section
            column.Item().Element(c => ComposeFlightInfo(c, data));
        });
    }

    private static void ComposePassengerInfo(IContainer container, TicketPdfDataDto data)
    {
        container.Border(1).BorderColor("#E5E7EB").Background(LightGrayColor).Padding(15).Row(row =>
        {
            // Left column — Passenger details
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("YOLCU / PASSENGER").Bold().FontSize(9).FontColor(GrayColor);
                col.Item().PaddingTop(2).Text(data.PassengerName).Bold().FontSize(12);

                col.Item().PaddingTop(10).Text("PNR NUMARASI / PNR NUMBER").Bold().FontSize(9).FontColor(GrayColor);
                col.Item().PaddingTop(2).Text(data.Pnr).Bold().FontSize(12);

                col.Item().PaddingTop(10).Text("BILET NO / TICKET NUMBER").Bold().FontSize(9).FontColor(GrayColor);
                col.Item().PaddingTop(2).Text(data.TicketNumber).FontSize(11);

                col.Item().PaddingTop(10).Text("BILET OLUSTURMA TARIHI / TICKET ISSUE DATE").Bold().FontSize(9).FontColor(GrayColor);
                col.Item().PaddingTop(2).Text(FormatDateTurkish(data.IssueDate)).FontSize(11);

                // TC Kimlik No
                if (!string.IsNullOrWhiteSpace(data.TcNo))
                {
                    col.Item().PaddingTop(10).Text("TC KIMLIK NO / ID NUMBER").Bold().FontSize(9).FontColor(GrayColor);
                    col.Item().PaddingTop(2).Text(data.TcNo).FontSize(11);
                }

                // Passport fields (international flights)
                if (data.IsInternational)
                {
                    col.Item().PaddingTop(10).Text("PASAPORT NO / PASSPORT NO").Bold().FontSize(9).FontColor(GrayColor);
                    col.Item().PaddingTop(2).Text(!string.IsNullOrWhiteSpace(data.PassportNo) ? data.PassportNo : "—").FontSize(11);

                    if (!string.IsNullOrWhiteSpace(data.PassportCountry))
                    {
                        col.Item().PaddingTop(6).Text("PASAPORT ULKESI / PASSPORT COUNTRY").Bold().FontSize(9).FontColor(GrayColor);
                        col.Item().PaddingTop(2).Text(data.PassportCountry).FontSize(11);
                    }
                }
            });

            row.ConstantItem(20);

            // Right column — Price info
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("UCRET BILGILERI / PRICE INFO").Bold().FontSize(9).FontColor(GrayColor);

                // Per-flight fare breakdown
                foreach (var item in data.FareItems)
                {
                    col.Item().PaddingTop(8).Text(item.Route).FontSize(8).FontColor("#374151");
                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text("Bilet Ucreti / Ticket Fare").FontSize(10);
                        r.AutoItem().AlignRight().Text($"{item.Amount:N2} {item.Currency}").FontSize(10);
                    });
                }

                col.Item().PaddingTop(8);

                // Base fare
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("Esas Ucret / Base Fare").FontSize(10);
                    r.AutoItem().AlignRight().Text($"{data.BaseFare:N2} {data.Currency}").FontSize(10);
                });

                // Taxes
                col.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("Vergiler ve Diger Ucretler / Taxes & Fees").FontSize(10);
                    r.AutoItem().AlignRight().Text($"{data.Taxes:N2} {data.Currency}").FontSize(10);
                });

                // Separator
                col.Item().PaddingVertical(8).LineHorizontal(1).LineColor("#D1D5DB");

                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("TOPLAM TUTAR / TOTAL FARE").Bold().FontSize(11);
                    r.AutoItem().AlignRight().Text($"{data.TotalFare:N2} {data.Currency}").Bold().FontSize(13).FontColor(RedColor);
                });
            });
        });
    }

    private static void ComposeFlightInfo(IContainer container, TicketPdfDataDto data)
    {
        container.Column(column =>
        {
            // Section header
            column.Item().Text("UCUS BILGILERI / FLIGHT INFO").Bold().FontSize(12);
            column.Item().PaddingTop(6);

            // Flight cards
            for (var i = 0; i < data.Flights.Count; i++)
            {
                var flight = data.Flights[i];

                if (i > 0)
                    column.Item().PaddingTop(8);

                // Flight card with border
                column.Item().Border(1).BorderColor("#E5E7EB").Column(card =>
                {
                    // Card header with airline info
                    card.Item().Background(DarkColor).Padding(10).Row(headerRow =>
                    {
                        // Airline badge (colored rounded box with code)
                        var airlineColor = AirlineColors.GetValueOrDefault(flight.AirlineCode, "#4B5563");
                        headerRow.ConstantItem(32).Height(32).AlignCenter().AlignMiddle()
                            .Background(airlineColor).Padding(2)
                            .AlignCenter().AlignMiddle()
                            .Text(flight.AirlineCode).Bold().FontSize(11).FontColor("#FFFFFF");

                        headerRow.ConstantItem(10); // spacing

                        headerRow.RelativeItem().AlignMiddle().Column(hCol =>
                        {
                            hCol.Item().Text($"{flight.AirlineName}").Bold().FontSize(11).FontColor("#FFFFFF");
                            hCol.Item().Text($"{flight.FlightCode} - {flight.BookingClass}").FontSize(9).FontColor("#D1D5DB");
                        });

                        if (!string.IsNullOrWhiteSpace(flight.FareBasisName))
                        {
                            headerRow.AutoItem().AlignMiddle().AlignRight()
                                .Text(flight.FareBasisName).FontSize(9).FontColor(RedColor)
                                .BackgroundColor("#2d2d2d");
                        }
                    });

                    // Card body — departure / arrow / arrival
                    card.Item().Background("#FFFFFF").Padding(12).Row(bodyRow =>
                    {
                        // Departure
                        bodyRow.RelativeItem().Column(dep =>
                        {
                            dep.Item().Text("KALKIS / DEPARTURE").FontSize(7).FontColor(GrayColor);
                            dep.Item().PaddingTop(4).Text(flight.DepartureTime).Bold().FontSize(18).FontColor(DarkColor);
                            dep.Item().PaddingTop(2).Text(flight.OriginCity).Bold().FontSize(10);
                            dep.Item().Text($"{flight.OriginAirport} ({flight.OriginCode})").FontSize(8).FontColor(GrayColor);
                            dep.Item().PaddingTop(2).Text(flight.DepartureDate).FontSize(9).FontColor(GrayColor);
                        });

                        // Arrow indicator
                        bodyRow.ConstantItem(60).AlignMiddle().AlignCenter().Column(arrow =>
                        {
                            arrow.Item().AlignCenter().Text(">>>").Bold().FontSize(14).FontColor(RedColor);
                        });

                        // Arrival
                        bodyRow.RelativeItem().AlignRight().Column(arr =>
                        {
                            arr.Item().AlignRight().Text("VARIS / ARRIVAL").FontSize(7).FontColor(GrayColor);
                            arr.Item().PaddingTop(4).AlignRight().Text(flight.ArrivalTime).Bold().FontSize(18).FontColor(DarkColor);
                            arr.Item().PaddingTop(2).AlignRight().Text(flight.DestinationCity).Bold().FontSize(10);
                            arr.Item().AlignRight().Text($"{flight.DestinationAirport} ({flight.DestinationCode})").FontSize(8).FontColor(GrayColor);
                            arr.Item().PaddingTop(2).AlignRight().Text(flight.ArrivalDate).FontSize(9).FontColor(GrayColor);
                        });
                    });

                    // Card footer — baggage
                    card.Item().Background(LightGrayColor).PaddingHorizontal(12).PaddingVertical(6).Row(footRow =>
                    {
                        footRow.RelativeItem().Text(text =>
                        {
                            text.Span("Bagaj / Baggage: ").FontSize(9).FontColor(GrayColor);
                            text.Span(flight.BaggageAllowance).Bold().FontSize(9);
                        });
                    });
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor("#D1D5DB");
            column.Item().PaddingTop(6);

            column.Item().Text("Genel Kurallar ve Bilgilendirmeler").Bold().FontSize(8).FontColor(GrayColor);
            column.Item().PaddingTop(4);

            column.Item().Text(TicketConstants.DisclaimerTR).Bold().FontSize(7).FontColor("#9CA3AF").LineHeight(1.3f);
            column.Item().PaddingTop(4);

            column.Item().Text(TicketConstants.DisclaimerEN).Bold().FontSize(7).FontColor("#9CA3AF").LineHeight(1.3f);
            column.Item().PaddingTop(6);

            column.Item().Text("Bilgi amaclidir, fatura yerine gecmez.")
                .FontSize(8).FontColor(RedColor).Italic();
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
