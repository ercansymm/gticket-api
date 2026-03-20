using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities;

public class BookingClass
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int FareTypeId { get; set; }
    public string NameTr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsRefundable { get; set; } = false;
    public bool IsChangeable { get; set; } = false;
    public int SortOrder { get; set; } = 0;

    // Navigation
    public FareType FareType { get; set; } = null!;
}
