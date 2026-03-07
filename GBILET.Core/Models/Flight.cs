using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Models.Flight;

public class LoginResponse
{
    public string SessionId { get; set; }
    public string SessionToken { get; set; }
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; }
}