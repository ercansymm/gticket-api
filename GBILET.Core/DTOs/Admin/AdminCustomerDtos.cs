namespace GBILET.Core.DTOs.Admin;

public class CustomerListItemDto
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public int BookingCount { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CustomerListResponseDto
{
    public List<CustomerListItemDto> Items { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
}
