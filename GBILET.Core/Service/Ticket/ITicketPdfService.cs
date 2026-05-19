using GBILET.Core.Models.Ticket;

namespace GBILET.Core.Service.Ticket;

public interface ITicketPdfService
{
    byte[] GeneratePdf(List<TicketPdfDataDto> passengers);
}
