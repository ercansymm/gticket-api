using GBILET.Core.Models.Ticket;
using GBILET.Core.Service.Ticket;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GBILET.Infrastructure.Services;

public class TicketPdfService : ITicketPdfService
{
    public byte[] GeneratePdf(TicketPdfDataDto data)
    {
        var document = Document.Create(container =>
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
                    text.Span("Ata").Bold().FontSize(24).FontColor("#DC2626");
                    text.Span("Bilet").Bold().FontSize(24).FontColor("#1a1a1a");
                });

                row.RelativeItem().AlignRight().AlignMiddle().Column(info =>
                {
                    info.Item().Text("AtaBilet").Bold().FontSize(10);
                    info.Item().Text(data.ContactPhone).FontSize(8).FontColor("#6B7280");
                    info.Item().Text(data.ContactEmail).FontSize(8).FontColor("#6B7280");
                });
            });

            // Red separator line
            column.Item().PaddingVertical(8).LineHorizontal(2).LineColor("#DC2626");

            // E-TICKET title
            column.Item().AlignCenter().Text("E-B\u0130LET / E-TICKET").Bold().FontSize(16);

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
        container.Background("#F5F5F5").Padding(15).Row(row =>
        {
            // Left column — Passenger details
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("YOLCU / PASSENGER").Bold().FontSize(9).FontColor("#6B7280");
                col.Item().PaddingTop(2).Text(data.PassengerName).Bold().FontSize(12);

                col.Item().PaddingTop(10).Text("PNR NUMARASI / PNR NUMBER").Bold().FontSize(9).FontColor("#6B7280");
                col.Item().PaddingTop(2).Text(data.Pnr).Bold().FontSize(12);

                col.Item().PaddingTop(10).Text("B\u0130LET NO / TICKET NUMBER").Bold().FontSize(9).FontColor("#6B7280");
                col.Item().PaddingTop(2).Text(data.TicketNumber).FontSize(11);

                col.Item().PaddingTop(10).Text("B\u0130LET OLU\u015eTURMA TAR\u0130H\u0130 / TICKET ISSUE DATE").Bold().FontSize(9).FontColor("#6B7280");
                col.Item().PaddingTop(2).Text(FormatDateTurkish(data.IssueDate)).FontSize(11);

                // TC Kimlik No (always shown if available)
                if (!string.IsNullOrWhiteSpace(data.TcNo))
                {
                    col.Item().PaddingTop(10).Text("TC K\u0130ML\u0130K NO / ID NUMBER").Bold().FontSize(9).FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text(data.TcNo).FontSize(11);
                }

                // Passport fields (international flights)
                if (data.IsInternational)
                {
                    col.Item().PaddingTop(10).Text("PASAPORT NO / PASSPORT NO").Bold().FontSize(9).FontColor("#6B7280");
                    col.Item().PaddingTop(2).Text(!string.IsNullOrWhiteSpace(data.PassportNo) ? data.PassportNo : "—").FontSize(11);

                    if (!string.IsNullOrWhiteSpace(data.PassportCountry))
                    {
                        col.Item().PaddingTop(6).Text("PASAPORT \u00dcLKES\u0130 / PASSPORT COUNTRY").Bold().FontSize(9).FontColor("#6B7280");
                        col.Item().PaddingTop(2).Text(data.PassportCountry).FontSize(11);
                    }
                }
            });

            row.ConstantItem(20);

            // Right column — Price info
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("\u00dcCRET B\u0130LG\u0130LER\u0130 / PRICE INFO").Bold().FontSize(9).FontColor("#6B7280");

                // Per-flight fare breakdown
                foreach (var item in data.FareItems)
                {
                    col.Item().PaddingTop(8).Text(item.Route).FontSize(8).FontColor("#374151");
                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text("Bilet \u00dccreti / Ticket Fare").FontSize(10);
                        r.AutoItem().AlignRight().Text($"{item.Amount:N2} {item.Currency}").FontSize(10);
                    });
                }

                // Separator
                col.Item().PaddingVertical(8).LineHorizontal(1).LineColor("#D1D5DB");

                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("TOPLAM TUTAR / TOTAL FARE").Bold().FontSize(11);
                    r.AutoItem().AlignRight().Text($"{data.TotalFare:N2} {data.Currency}").Bold().FontSize(13).FontColor("#DC2626");
                });
            });
        });
    }

    private static void ComposeFlightInfo(IContainer container, TicketPdfDataDto data)
    {
        container.Column(column =>
        {
            // Section header
            column.Item().Text("U\u00c7U\u015e B\u0130LG\u0130LER\u0130 / FLIGHT INFO").Bold().FontSize(12);
            column.Item().PaddingTop(6);

            // Table header
            column.Item().Background("#1a1a1a").Padding(8).Row(row =>
            {
                row.RelativeItem(3).Text("Havayolu / U\u00e7u\u015f Kodu").FontSize(9).Bold().FontColor("#FFFFFF");
                row.RelativeItem(3).Text("Kalk\u0131\u015f Bilgisi").FontSize(9).Bold().FontColor("#FFFFFF");
                row.RelativeItem(3).Text("Var\u0131\u015f Bilgisi").FontSize(9).Bold().FontColor("#FFFFFF");
                row.RelativeItem(1).Text("Bagaj").FontSize(9).Bold().FontColor("#FFFFFF");
            });

            // Flight rows
            for (var i = 0; i < data.Flights.Count; i++)
            {
                var flight = data.Flights[i];
                var bgColor = i % 2 == 0 ? "#FFFFFF" : "#FAFAFA";

                column.Item().BorderLeft(3).BorderColor("#DC2626").Background(bgColor).Padding(8).Row(row =>
                {
                    // Airline + flight code + class
                    row.RelativeItem(3).Column(col =>
                    {
                        col.Item().Text($"{flight.AirlineName} {flight.FlightCode} - {flight.BookingClass}").FontSize(10).Bold();
                        if (!string.IsNullOrWhiteSpace(flight.FareBasisName))
                        {
                            col.Item().PaddingTop(2).Text(flight.FareBasisName).FontSize(8).FontColor("#DC2626");
                        }
                    });

                    // Departure info
                    row.RelativeItem(3).Column(col =>
                    {
                        col.Item().Text($"{flight.OriginCity}").FontSize(10).Bold();
                        col.Item().Text($"{flight.OriginAirport} ({flight.OriginCode})").FontSize(8).FontColor("#6B7280");
                        col.Item().PaddingTop(2).Text($"{flight.DepartureDate}").FontSize(9);
                        col.Item().Text($"{flight.DepartureTime}").FontSize(10).Bold();
                    });

                    // Arrival info
                    row.RelativeItem(3).Column(col =>
                    {
                        col.Item().Text($"{flight.DestinationCity}").FontSize(10).Bold();
                        col.Item().Text($"{flight.DestinationAirport} ({flight.DestinationCode})").FontSize(8).FontColor("#6B7280");
                        col.Item().PaddingTop(2).Text($"{flight.ArrivalDate}").FontSize(9);
                        col.Item().Text($"{flight.ArrivalTime}").FontSize(10).Bold();
                    });

                    // Baggage
                    row.RelativeItem(1).AlignMiddle().Text(flight.BaggageAllowance).FontSize(9);
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            // Separator
            column.Item().LineHorizontal(1).LineColor("#D1D5DB");
            column.Item().PaddingTop(6);

            // Disclaimer header
            column.Item().Text("Genel Kurallar ve Bilgilendirmeler").Bold().FontSize(8).FontColor("#6B7280");
            column.Item().PaddingTop(4);

            // Turkish disclaimer
            column.Item().Text(TicketConstants.DisclaimerTR).FontSize(7).FontColor("#9CA3AF").LineHeight(1.3f);
            column.Item().PaddingTop(4);

            // English disclaimer
            column.Item().Text(TicketConstants.DisclaimerEN).FontSize(7).FontColor("#9CA3AF").LineHeight(1.3f);
            column.Item().PaddingTop(6);

            // Bottom warning
            column.Item().Text("Bilgi ama\u00e7l\u0131d\u0131r, fatura yerine ge\u00e7mez.")
                .FontSize(8).FontColor("#DC2626").Italic();
        });
    }

    private static string FormatDateTurkish(DateTime date)
    {
        var months = new[]
        {
            "", "Ocak", "\u015eubat", "Mart", "Nisan", "May\u0131s", "Haziran",
            "Temmuz", "A\u011fustos", "Eyl\u00fcl", "Ekim", "Kas\u0131m", "Aral\u0131k"
        };
        return $"{date.Day} {months[date.Month]} {date.Year}";
    }
}
