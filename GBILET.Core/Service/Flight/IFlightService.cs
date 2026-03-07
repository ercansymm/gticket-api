using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GBILET.Core.Service.Flight;

public interface IFlightService
{
    Task<string> SearchFlight(string from, string to);
}